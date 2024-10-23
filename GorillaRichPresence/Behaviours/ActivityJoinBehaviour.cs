using GorillaExtensions;
using GorillaLocomotion;
using GorillaNetworking;
using GorillaRichPresence.Interfaces;
using GorillaRichPresence.Tools;
using GorillaTag.Rendering;
using GorillaTagScripts.ModIO;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GorillaRichPresence.Behaviours
{
    // [DisallowMultipleComponent]
    public class ActivityJoinBehaviour : MonoBehaviour, IActivityJoinBase
    {
        public string Secrets { get; set; }
        public string ActivityRoomName { get; set; }
        public string ActivityUserId { get; set; }
        public GTZone[] ActivityZones { get; set; }
        public string ActivityLowEffortZone { get; set; }
        public ZoneShaderSettings ActivityShaderSettings { get; set; }

        private bool restrictPlayer;

        private Rigidbody playerRigidbody;

        private readonly FieldInfo zoneShaderSettingsField = AccessTools.Field(typeof(GorillaTriggerBoxShaderSettings), "settings");

        private readonly MethodInfo getZoneDataField = AccessTools.Method(typeof(ZoneManagement), "GetZoneData");

        public void Conclude() => Destroy(this);

        public void Start()
        {
            playerRigidbody = Player.Instance.bodyCollider.attachedRigidbody;

            InitializeData();
            JoinRoom();
        }

        public void FixedUpdate()
        {
            if (restrictPlayer)
            {
                playerRigidbody.AddForce(-Physics.gravity * playerRigidbody.mass * Player.Instance.scale);
            }
        }

        public void InitializeData()
        {
            if (string.IsNullOrEmpty(Secrets)) return;

            object[] secretContents = Secrets.Split("\n");
            ActivityRoomName = ((string)secretContents[0]).Trim();
            ActivityUserId = ((string)secretContents[1]).Trim();
            ActivityZones = ((string)secretContents[2]).Split('.').Select(str => (GTZone)Enum.Parse(typeof(GTZone), str)).ToArray();
            ActivityLowEffortZone = ((string)secretContents[3]).Trim();
            string shaderSettingName = (string)secretContents[4];
            ActivityShaderSettings = shaderSettingName == ZoneShaderSettings.defaultsInstance.name
                ? 
                    ZoneShaderSettings.defaultsInstance
                : Player.Instance.gameObject.scene
                .GetComponentsInHierarchy<GorillaTriggerBoxShaderSettings>()
                .Select(GetShaderSettings)
                .FirstOrDefault(settings => settings != null && settings.name == shaderSettingName) ?? ZoneShaderSettings.defaultsInstance;
        }

        public async void JoinRoom()
        {
            if (string.IsNullOrEmpty(Secrets)) return;

            if (NetworkSystem.Instance.InRoom)
            {
                await NetworkSystem.Instance.ReturnToSinglePlayer();
            }

            NetworkSystem.Instance.OnMultiplayerStarted += OnMultiplayerStarted;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += OnReturnedToSinglePlayer;

            do
            {
                await Task.Delay(500);
            }
            while (NetworkSystem.Instance.netState != NetSystemState.Idle);

            Logging.Info("Ready to join room (3000 ms / 3 sec delay)");
            await Task.Delay(3000);

            Logging.Info($"Joining room '{ActivityRoomName}'");

            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(ActivityRoomName, JoinType.Solo);
        }

        public void OnDestroy()
        {
            NetworkSystem.Instance.OnMultiplayerStarted -= OnMultiplayerStarted;
        }

        public async void OnMultiplayerStarted()
        {
            Logging.Info($"Room joined");

            await Task.Delay(150);

            string currentRoomName = NetworkSystem.Instance.RoomName;

            if (currentRoomName != ActivityRoomName)
            {
                Logging.Warning($"We are not in the right room (currently in {currentRoomName}, tried to join {ActivityRoomName}) leaving room");
                await NetworkSystem.Instance.ReturnToSinglePlayer();
                return;
            }

            NetPlayer activityPlayer = null;

            for (int i = 0; i < 2; i++)
            {
                if (i == 1) NetworkSystem.Instance.UpdatePlayers();

                NetPlayer[] currentPlayers = NetworkSystem.Instance.AllNetPlayers;
                foreach (NetPlayer player in currentPlayers)
                {
                    if (player.UserId == ActivityUserId)
                    {
                        activityPlayer = player;
                        break;
                    }
                }
            }

            if (activityPlayer == null)
            {
                Logging.Warning($"Our activity user is not in the room ({ActivityUserId})");
                Conclude();
                return;
            }

            Logging.Info("Locating player to activity user");

            var zoneData = ActivityZones.Select(GetZoneData).ToArray();

            var tcs = new TaskCompletionSource<bool>();

            Logging.Info("Step 1: Set zones");

            restrictPlayer = true;
            // Player.Instance.InReportMenu = true;
            Player.Instance.disableMovement = true;

            ZoneManagement.SetActiveZones(ActivityZones);
            var zoneDataWithScene = zoneData.Where(zd => !string.IsNullOrEmpty(zd.sceneName));
            if (zoneDataWithScene.Any())
            {
                Logging.Info("Using scenes");

                TaskCompletionSource<bool> tsp = new();
                Dictionary<string, bool> loadedScenes = zoneDataWithScene.ToDictionary(zd => zd.sceneName, zd => false);

                int countLoaded = SceneManager.sceneCount;
                for (int i = 0; i < countLoaded; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (loadedScenes.ContainsKey(scene.name))
                    {
                        loadedScenes[scene.name] = scene.isLoaded;
                    }
                }

                bool useSceneLoadAction = false;
                void OnSceneLoaded(Scene loadedScene, LoadSceneMode mode)
                {
                    if (loadedScenes.ContainsKey(loadedScene.name))
                    {
                        loadedScenes[loadedScene.name] = true;
                        Logging.Info($"Scene loaded {loadedScene.name} under mode {mode}");

                        Logging.Info($"New dictionary: {string.Join(Environment.NewLine, loadedScenes)}");

                        if (loadedScenes.All(dict => dict.Value == true))
                        {
                            Logging.Info("New check is futhfilled");
                            tsp.SetResult(true);
                        }
                    }
                }

                Logging.Info($"Initial dictionary: {string.Join(Environment.NewLine, loadedScenes)}");

                if (loadedScenes.All(dict => dict.Value == true))
                {
                    Logging.Info("Initial check is futhfilled");
                    tsp.SetResult(true);
                }
                else
                {
                    Logging.Info("Waiting for scenes to load");

                    useSceneLoadAction = true;
                    SceneManager.sceneLoaded += OnSceneLoaded;
                }

                if (!tsp.Task.IsCompleted) await tsp.Task;
                if (useSceneLoadAction) SceneManager.sceneLoaded -= OnSceneLoaded;
            }
            else
            {
                Logging.Info("Not using scenes (yay)");
            }

            Logging.Info("Step 2: Setiing shader settings as active instance");

            ActivityShaderSettings.BecomeActiveInstance(false);

            // activate low effort zone
            Logging.Info("Step 3: Find low effort zone (if assigned)");

            if (!string.IsNullOrEmpty(ActivityLowEffortZone))
            {
                Logging.Info("Searching for low effort zone (assigned)");

                int countLoaded = SceneManager.sceneCount;
                for (int i = countLoaded - 1; i >= 0; i--)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    var lezArray = scene.GetComponentsInHierarchy<LowEffortZone>(false);
                    LowEffortZone lowEffortZone = lezArray.FirstOrDefault(lez => lez.name == ActivityLowEffortZone);

                    if (lowEffortZone)
                    {
                        Logging.Info("Low effort zone applied");

                        lowEffortZone.OnBoxTriggered();

                        break;
                    }
                }
            }

            // teleport to player
            Logging.Info("Step 4: Teleport to activity user");

            restrictPlayer = false;
            // Player.Instance.InReportMenu = false;
            Player.Instance.disableMovement = false;

            Main.Instance.StartCoroutine(TeleportPlayer(GorillaGameManager.StaticFindRigForPlayer(activityPlayer), 1f, 1f));

            Logging.Info("All done! :3");

            Conclude();
        }

        public IEnumerator TeleportPlayer(VRRig rig, float duration, float moveDuration)
        {
            float elapsed = 0f;
            float distanceOrigin = 1.8f;

            Transform head = rig.headMesh.transform;
            Transform body = head.parent;

            Player.Instance.Turn(body.rotation.eulerAngles.y - Player.Instance.headCollider.transform.rotation.eulerAngles.y);

            while (elapsed < duration)
            {
                Vector3 position = (head.position + (Vector3.up * Mathf.Lerp(0.3f, 0f, elapsed / moveDuration)) + (-body.forward * AnimationCurves.EaseInExpo.Evaluate(Mathf.Lerp(distanceOrigin, 0.25f, elapsed / moveDuration)))) * rig.scaleFactor;

                position -= Player.Instance.bodyCollider.transform.position;
                position += Player.Instance.transform.position;

                Player.Instance.TeleportTo(position, Player.Instance.transform.rotation);
                Player.Instance.bodyCollider.attachedRigidbody.velocity = Vector3.zero;

                yield return null;
                elapsed += Time.deltaTime;
            }

            yield break;
        }

        public void OnReturnedToSinglePlayer()
        {
            Conclude();
        }

        public ZoneShaderSettings GetShaderSettings(GorillaTriggerBoxShaderSettings box) => (ZoneShaderSettings)zoneShaderSettingsField.GetValue(box);

        public ZoneData GetZoneData(GTZone zone) => (ZoneData)getZoneDataField.Invoke(ZoneManagement.instance, [zone]);
    }
}