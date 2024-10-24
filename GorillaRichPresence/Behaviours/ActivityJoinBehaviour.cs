using ExitGames.Client.Photon;
using GorillaExtensions;
using GorillaLocomotion;
using GorillaNetworking;
using GorillaRichPresence.Models;
using GorillaRichPresence.Tools;
using GorillaTag.Rendering;
using HarmonyLib;
using Photon.Pun;
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
    public class ActivityJoinBehaviour : MonoBehaviour
    {
        public ActivityJoinSecrets Secrets;

        public ActivityJoinData Data;

        private bool restrictPlayer;

        private Rigidbody playerRigidbody;

        private readonly FieldInfo zoneShaderSettingsField = AccessTools.Field(typeof(GorillaTriggerBoxShaderSettings), "settings");

        private readonly MethodInfo getZoneDataField = AccessTools.Method(typeof(ZoneManagement), "GetZoneData");

        public void Awake()
        {
            PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        }

        public void Start()
        {
            Logging.Info("Start");

            playerRigidbody = Player.Instance.bodyCollider.attachedRigidbody;

            JoinRoom();
        }

        public void FixedUpdate()
        {
            if (restrictPlayer)
            {
                playerRigidbody.AddForce(-Physics.gravity * playerRigidbody.mass * Player.Instance.scale);
            }
        }

        public void OnDestroy()
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        }

        public async void JoinRoom()
        {
            if (Secrets == null || string.IsNullOrEmpty(Secrets.Secrets)) return;

            // If we are already in the room with the activity user, perform our original set of steps used to get to them
            if (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.RoomName == Secrets.RoomName)
            {
                HandleRoomEntry(false);
                return;
            }

            if (!NetworkSystem.Instance.InRoom)
            {
                bool wasIdle = true;

                do
                {
                    wasIdle = false;
                    await Task.Delay(500);
                }
                while (NetworkSystem.Instance.netState != NetSystemState.Idle);

                if (!wasIdle)
                {
                    // Likely signals to the player initially booting up their game
                    Logging.Info("Set to join room (waiting for a little bit to wait for mods to load)");
                    await Task.Delay(2000);
                }
                else
                {
                    Logging.Info("Set to join room");
                }
            }

            Logging.Info($"Joining room '{Secrets.RoomName}'");
            await PhotonNetworkController.Instance.AttemptToJoinSpecificRoomAsync(Secrets.RoomName, NetworkSystem.Instance.InRoom ? JoinType.FollowingParty : JoinType.Solo, async (NetJoinResult result) =>
            {
                if (result == 0)
                {
                    // Give the game a bit to load stuff
                    await Task.Delay(200);

                    Logging.Info("Applying activity join behaviour");
                    HandleRoomEntry();
                    return;
                }

                if (result == NetJoinResult.Failed_Other)
                {
                    // Retry the join room process
                    JoinRoom();
                    return;
                }

                Logging.Warning($"NetJoinResult is {result}");
                Destroy(this);
            });
        }

        public async void HandleRoomEntry(bool doRoomNameCheck = true)
        {
            if (doRoomNameCheck && NetworkSystem.Instance.RoomName != Secrets.RoomName)
            {
                Logging.Warning("incorrect room");
                await NetworkSystem.Instance.ReturnToSinglePlayer();
                Destroy(this);
                return;
            }

            NetPlayer targetPlayer = null;

            for (int i = 0; i < 2; i++)
            {
                if (i == 1) NetworkSystem.Instance.UpdatePlayers();

                NetPlayer[] currentPlayers = NetworkSystem.Instance.AllNetPlayers;
                foreach (NetPlayer player in currentPlayers)
                {
                    if (player.UserId == Secrets.PlayerId)
                    {
                        targetPlayer = player;
                        break;
                    }
                }
            }

            if (targetPlayer == null)
            {
                Logging.Warning("player not found");
                Destroy(this);
                return;
            }

            Data = new ActivityJoinData()
            {
                Player = targetPlayer
            };

            Photon.Realtime.RaiseEventOptions raiseEventOptions = new()
            {
                TargetActors = [targetPlayer.ActorNumber]
            };

            PhotonNetwork.RaiseEvent((int)ActivityJoinEventCode.RequestActivityData, new object[] { }, raiseEventOptions, SendOptions.SendReliable);
            Logging.Info($"Sending to {targetPlayer.NickName}");
        }

        public async void HandleRoomData()
        {
            var zoneData = Data.Zones.Select(GetZoneData).ToArray();

            Logging.Info("Step 1: Set zones");

            // enable movement restrictions
            restrictPlayer = true;
            Player.Instance.disableMovement = true;

            if (Data.Zones.Contains(GTZone.customMaps) )
            {
                Logging.Info("Proceeding with custom map zone");

                GameObject primaryObject = ZoneManagement.instance.GetPrimaryGameObject(GTZone.arcade);
                var loginTeleporter = primaryObject.GetComponentInChildren<ModIOLoginTeleporter>(true);
                if (loginTeleporter)
                {
                    loginTeleporter.LoginAndTeleport();
                }
                else
                {
                    Logging.Warning("LoginTeleporter not found");
                }
            }
            else if (!Data.Zones.Contains(GTZone.customMaps))
            {
                ZoneManagement.SetActiveZones(Data.Zones);
                var zoneDataWithScene = zoneData.Where(zd => !string.IsNullOrEmpty(zd.sceneName));
                if (zoneDataWithScene.Any())
                {
                    Logging.Info("Using scenes");

                    TaskCompletionSource<bool> zoneSceneLoadCompletionSource = new();
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
                                zoneSceneLoadCompletionSource.SetResult(true);
                            }
                        }
                    }

                    Logging.Info($"Initial dictionary: {string.Join(Environment.NewLine, loadedScenes)}");

                    if (loadedScenes.All(dict => dict.Value == true))
                    {
                        Logging.Info("Initial check is futhfilled");
                        zoneSceneLoadCompletionSource.SetResult(true);
                    }
                    else
                    {
                        Logging.Info("Waiting for scenes to load");

                        useSceneLoadAction = true;
                        SceneManager.sceneLoaded += OnSceneLoaded;
                    }

                    if (!zoneSceneLoadCompletionSource.Task.IsCompleted) await zoneSceneLoadCompletionSource.Task;
                    if (useSceneLoadAction) SceneManager.sceneLoaded -= OnSceneLoaded;
                }
                else
                {
                    Logging.Info("Not using scenes (yay)");
                }
            }
            
            Logging.Info("Step 2: Setiing shader settings as active instance");

            Data.ShaderSettings.BecomeActiveInstance(false);

            // activate low effort zone
            Logging.Info("Step 3: Find low effort zone (if assigned)");

            if (!string.IsNullOrEmpty(Data.LowEffortZone))
            {
                Logging.Info("Searching for low effort zone (assigned)");
                bool hasFoundZone = false;
                int countLoaded = SceneManager.sceneCount;
                for (int i = countLoaded - 1; i >= 0; i--)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    var lezArray = scene.GetComponentsInHierarchy<LowEffortZone>(false);
                    LowEffortZone lowEffortZone = lezArray.FirstOrDefault(lez => lez.name == Data.LowEffortZone);
                    if (lowEffortZone)
                    {
                        Logging.Info("Low effort zone applied");
                        lowEffortZone.OnBoxTriggered();
                        hasFoundZone = true;
                        break;
                    }
                }
                if (!hasFoundZone)
                {
                    Logging.Warning($"Low effort zone ({Data.LowEffortZone}) was not found");
                }
            }

            // teleport to player
            Logging.Info("Step 4: Teleport to activity user");

            // disable movement restrictions
            restrictPlayer = false;
            Player.Instance.disableMovement = false;

            Main.Instance.StartCoroutine(TeleportPlayer(GorillaGameManager.StaticFindRigForPlayer(Data.Player), 1f, 0.8f));

            Logging.Info("Completed steps");

            Destroy(this);
        }

        public void OnEvent(EventData data)
        {
            if (data.Code != (int)ActivityJoinEventCode.SendActivityData) return;

            Logging.Info($"We heard back from {Data.Player.NickName}!!");

            object[] eventData = (object[])data.CustomData;

            Data.Zones = ((string)eventData[0]).Split('.').Select(str => (GTZone)Enum.Parse(typeof(GTZone), str)).ToArray();
            Data.LowEffortZone = ((string)eventData[1]).Trim();
            string shaderSettingName = (string)eventData[2];
            Data.ShaderSettings = shaderSettingName == ZoneShaderSettings.defaultsInstance.name ? ZoneShaderSettings.defaultsInstance : Player.Instance.gameObject.scene.GetComponentsInHierarchy<GorillaTriggerBoxShaderSettings>().Select(GetShaderSettings).FirstOrDefault(settings => settings != null && settings.name == shaderSettingName) ?? ZoneShaderSettings.defaultsInstance;

            HandleRoomData();
        }

        public IEnumerator TeleportPlayer(VRRig rig, float duration, float moveDuration)
        {
            Transform head = rig.headMesh.transform;

            // Rotation
            Quaternion quaternion = Quaternion.LookRotation(head.transform.position - Player.Instance.headCollider.transform.position);
            Player.Instance.Turn(quaternion.eulerAngles.y - Player.Instance.headCollider.transform.rotation.eulerAngles.y);

            // Position
            Vector3 offset = Player.Instance.transform.position - head.position, position;
            float distanceOrigin = 3f;
            if (Physics.Raycast(head.position, offset, out RaycastHit hitInfo, distanceOrigin, Player.Instance.locomotionEnabledLayers))
            {
                distanceOrigin = Mathf.Clamp(Vector3.Distance(head.position, hitInfo.point) - (Player.Instance.bodyCollider.radius + 0.1f), 0f, distanceOrigin);
            }

            // Scale
            Player.Instance.GetComponent<SizeManager>().enabled = false;
            Player.Instance.scale = rig.scaleFactor;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                position = Vector3.ClampMagnitude(offset, Mathf.Lerp(distanceOrigin, 0.8f, AnimationCurves.EaseOutExpo.Evaluate(elapsed / moveDuration))) + head.position + (Vector3.up * Mathf.Lerp(0.6f, 0.2f, elapsed / moveDuration));
                Vector3 adjustedPosition = position - Player.Instance.bodyCollider.transform.position + Player.Instance.transform.position;

                Player.Instance.TeleportTo(adjustedPosition, Player.Instance.transform.rotation);
                Player.Instance.bodyCollider.attachedRigidbody.velocity = Vector3.zero;

                yield return null;
                elapsed += Time.deltaTime;
            }

            yield break;
        }

        public ZoneShaderSettings GetShaderSettings(GorillaTriggerBoxShaderSettings box) => (ZoneShaderSettings)zoneShaderSettingsField.GetValue(box);

        public ZoneData GetZoneData(GTZone zone) => (ZoneData)getZoneDataField.Invoke(ZoneManagement.instance, [zone]);
    }
}