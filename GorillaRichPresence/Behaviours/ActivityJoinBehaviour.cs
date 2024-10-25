using ExitGames.Client.Photon;
using GorillaExtensions;
using GorillaLocomotion;
using GorillaNetworking;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Models;
using GorillaRichPresence.Patches;
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
            this.LogCurrentMethod();

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

            this.LogCurrentMethod();

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
            this.LogCurrentMethod();

            if (doRoomNameCheck && NetworkSystem.Instance.RoomName != Secrets.RoomName)
            {
                Logging.Warning($"Currently in wrong room ({NetworkSystem.Instance.RoomName}, expecting {Secrets.RoomName})");
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
                Logging.Warning($"Player of activity host not found");
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

            object[] content =
            [
                "GRP.RAD".GetStaticHash(),
            ];

            PhotonNetwork.RaiseEvent((int)ActivityJoinEventCode.RequestActivityData, content, raiseEventOptions, SendOptions.SendReliable);
            Logging.Info($"Player of activity host found ({targetPlayer.NickName}) data request sent");
        }

        public async void HandleRoomData()
        {
            this.LogCurrentMethod();

            var zoneData = Data.Zones.Select(GetZoneData).ToArray();

            Logging.Info("Setting activity host zones");

            // enable movement restrictions
            restrictPlayer = true;
            Player.Instance.disableMovement = true;

            if (Data.Zones.Contains(GTZone.customMaps) && !GetZoneData(GTZone.customMaps).active)
            {
                Logging.Info("Proceeding with custom map zone");

                GameObject primaryObject = ZoneManagement.instance.GetPrimaryGameObject(GTZone.arcade);
                var loginTeleporter = primaryObject.GetComponentInChildren<ModIOLoginTeleporter>(true);
                if (loginTeleporter)
                {
                    Logging.Info("LoginTeleporter found, logging into the custom map system and teleporting to the virtual stump");
                    loginTeleporter.LoginAndTeleport();
                }
                else
                {
                    Logging.Warning("LoginTeleporter not found");
                }
            }
            else if (!Data.Zones.Contains(GTZone.customMaps))
            {
                Logging.Info($"Proceeding with regular zones ({string.Join(", ", Data.Zones)}");

                ZoneManagement.SetActiveZones(Data.Zones);
                var zoneDataWithScene = zoneData.Where(zd => !string.IsNullOrEmpty(zd.sceneName));
                if (zoneDataWithScene.Any())
                {
                    Logging.Info("Using scenes to load a zone");

                    TaskCompletionSource<bool> zoneSceneLoadCompletionSource = new();
                    Dictionary<string, bool> loadedScenes = zoneDataWithScene.ToDictionary(zd => zd.sceneName, zd => false);

                    int zoneCheckSceneCount = SceneManager.sceneCount;
                    for (int i = 0; i < zoneCheckSceneCount; i++)
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

                            if (loadedScenes.All(dict => dict.Value == true))
                            {
                                Logging.Info("All zone scenes are now loaded");
                                zoneSceneLoadCompletionSource.SetResult(true);
                            }
                            else
                            {
                                Logging.Info("Waiting for scenes to load");
                                Logging.Info(string.Join(Environment.NewLine, loadedScenes));
                            }
                        }
                    }

                    if (loadedScenes.All(dict => dict.Value == true))
                    {
                        Logging.Info("All zone scenes were loaded based on initial dictionary");
                        zoneSceneLoadCompletionSource.SetResult(true);
                    }
                    else
                    {
                        Logging.Info("Waiting for scenes to load");
                        Logging.Info(string.Join(Environment.NewLine, loadedScenes));

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

            Logging.Info("Setting activity host shader settings as active instance");

            Data.ShaderSettings.BecomeActiveInstance(false);

            // activate low effort zone
            Logging.Info("Triggering activity host low effort zone");

            if (string.IsNullOrEmpty(Data.LowEffortZone))
            {
                Data.LowEffortZone = "TreeRoomSide";
            }

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

            // teleport to player
            Logging.Info("Teleporting to player of activity user");

            // disable movement restrictions
            restrictPlayer = false;
            Player.Instance.disableMovement = false;

            Main.Instance.StartCoroutine(TeleportPlayer(GorillaGameManager.StaticFindRigForPlayer(Data.Player), 1f, 0.8f));

            Logging.Info("Concluded! Have fun with your group");

            Destroy(this);
        }

        public void OnEvent(EventData data)
        {
            if (data.Code != (int)ActivityJoinEventCode.SendActivityData) return;

            Logging.Info("Player of activity host has been reached with data sent from their end");

            object[] eventData = (object[])data.CustomData;

            if (eventData[0] is not int || ((int)eventData[0]) != "GRP.SAD".GetStaticHash()) return;

            Data.Zones = ((string)eventData[1]).Split('.').Select(str => (GTZone)Enum.Parse(typeof(GTZone), str)).ToArray();
            Data.LowEffortZone = ((string)eventData[2]).Trim();
            string shaderSettingName = (string)eventData[3];
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

            Player.Instance.GetComponent<SizeManager>().enabled = true;

            yield break;
        }

        public ZoneShaderSettings GetShaderSettings(GorillaTriggerBoxShaderSettings box) => (ZoneShaderSettings)zoneShaderSettingsField.GetValue(box);

        public ZoneData GetZoneData(GTZone zone) => (ZoneData)getZoneDataField.Invoke(ZoneManagement.instance, [zone]);
    }
}