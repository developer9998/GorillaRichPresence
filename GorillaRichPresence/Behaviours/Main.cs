using Discord;
using ExitGames.Client.Photon;
using GorillaGameModes;
using GorillaNetworking;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Models;
using GorillaRichPresence.Patches;
using GorillaRichPresence.Tools;
using GorillaRichPresence.Utils;
using GorillaTag.Rendering;
using GorillaTagScripts.ModIO;
using ModIO;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace GorillaRichPresence.Behaviours
{
    public class Main : MonoBehaviour
    {
        public static Main Instance { get; private set; }

        private GTZone[] ActiveZones = [];

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }

            this.LogCurrentMethod();
        }

        public void Start()
        {
            // Add network events
            NetworkSystem.Instance.OnMultiplayerStarted += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerJoined += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerLeft += UpdateMultiplayer;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += UpdateSingleplayer;
            PhotonNetwork.NetworkingClient.EventReceived += OnEvent;

            // Add mod.io map events
            GameEvents.OnModIOLoggedIn.AddListener(new UnityAction(OnModIOLoggedIn));
            CustomMapManager.OnRoomMapChanged.AddListener(new UnityAction<ModId>(OnRoomMapChanged));

            // Add misc events
            DiscordWrapper.OnActivityJoin += OnActivityJoin;
            ZoneManagement.OnZoneChange += OnZoneChange;

            Logging.Log("Constructing Discord client");
            DiscordWrapper.Construct();

            Logging.Log("Constructing initial Activity");
            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                // Update description
                Activity.Details = "Not in Room";
                Activity.State = string.Empty;

                // Update profile
                Activity.Assets.SmallImage = "gorilla";
                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();

                // Update map
                (string image, string text) = ZoneUtils.GetActivityAssets(PhotonNetworkController.Instance.StartZone);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;
                return Activity;
            });

            DiscordWrapper.UpdateActivity();
        }

        // Activities

        private void OnActivityJoin(string secrets)
        {
            if (GetComponent<ActivityJoinBehaviour>())
            {
                Destroy(GetComponent<ActivityJoinBehaviour>());
            }

            gameObject.AddComponent<ActivityJoinBehaviour>().Secrets = new ActivityJoinSecrets(secrets);
        }

        // Profile

        public void ChangeName(string nickName)
        {
            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                Activity.Assets.SmallText = nickName;
                return Activity;
            });

            DiscordWrapper.UpdateActivity();
        }

        // Maps

        public void OnZoneChange(ZoneData[] zones)
        {
            ActiveZones = zones.Where(zone => zone.active).Select(zone => zone.zone).ToArray();

            if (!NetworkSystem.Instance.InRoom || (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.SessionIsPrivate))
            {
                DiscordWrapper.SetActivity((Activity Activity) =>
                {
                    // Update map
                    (string image, string text) = ZoneUtils.GetActivityAssets(ActiveZones.First());
                    Activity.Assets.LargeImage = image;
                    Activity.Assets.LargeText = text;

                    return Activity;
                });
            }

            DiscordWrapper.UpdateActivity();
        }

        public void OnModIOLoggedIn()
        {
            CheckCustomMap();
            DiscordWrapper.UpdateActivity();
        }

        public void OnRoomMapChanged(ModId roomMapModId)
        {
            CheckCustomMap();
            DiscordWrapper.UpdateActivity();
        }

        private void CheckCustomMap()
        {
            if (ModIODataStore.IsLoggedIn())
            {
                ModId currentRoomMap = CustomMapManager.GetRoomMapId();
                if (currentRoomMap != ModId.Null)
                {
                    ModIODataStore.GetModProfile(currentRoomMap, delegate (ModIORequestResultAnd<ModProfile> result)
                    {
                        if (ModIODataStore.IsLoggedIn() && result.result.success)
                        {
                            DiscordWrapper.SetActivity((Activity Activity) =>
                            {
                                // Update map (cuustom map)
                                Activity.Assets.LargeImage = result.data.logoImage320x180.url;
                                Activity.Assets.LargeText = $"{result.data.creator.username}: {result.data.name}";

                                return Activity;
                            });

                            return;
                        }
                    }, false);
                }
            }

            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                // Update map (virtual stump)
                (string image, string text) = ZoneUtils.GetActivityAssets(GTZone.customMaps);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;

                return Activity;
            });
        }

        // Room

        private void UpdateMultiplayer() => UpdateMultiplayer(true);

        private void UpdateMultiplayer(NetPlayer netPlayer) => UpdateMultiplayer(false);

        private void UpdateMultiplayer(bool isInitialJoin)
        {
            if (ZoneManagement.IsInZone(GTZone.customMaps))
            {
                CheckCustomMap();
            }

            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                // Define game mode 
                string currentGameMode = GameMode.ActiveGameMode?.GameModeName()?.ToLower()?.ToTitleCase() ?? "Null";

                // Define modded status
                string gameModeString = NetworkSystem.Instance.GameModeString;
                bool isModdedRoom = gameModeString.Contains("MODDED_");

                // Define queue
                string[] queueNames = ["DEFAULT", "MINIGAMES", "COMPETITIVE"];
                string currentQueue = queueNames.First(queue => queueNames.Contains(queue)).ToLower().ToTitleCase();

                // Update description
                Activity.State = NetworkSystem.Instance.SessionIsPrivate ? $"In {(Configuration.DisplayPrivateCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Private Room")}" : $"In {(Configuration.DisplayPublicCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Public Room")}";
                Activity.Details = $"{currentQueue}, {(isModdedRoom ? "Modded " : "")}{currentGameMode}";

                // Update party
                Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                Activity.Party.Size.MaxSize = PhotonNetworkController.Instance.GetRoomSize(gameModeString);
                Activity.Party.Id = NetworkSystem.Instance.RoomName + NetworkSystem.Instance.CurrentRegion.Replace("/*", "").ToUpper();
                Activity.Instance = true;

                if (isInitialJoin)
                {
                    // Update map
                    (string image, string text) = ZoneUtils.GetActivityAssets(ActiveZones.Length == 0 ? PhotonNetworkController.Instance.StartZone : ActiveZones[0]);
                    Activity.Assets.LargeImage = image;
                    Activity.Assets.LargeText = text;
                }

                return Activity;
            });

            DiscordWrapper.UpdateActivity();
        }

        private void UpdateSingleplayer()
        {
            if (ZoneManagement.IsInZone(GTZone.customMaps))
            {
                CheckCustomMap();
            }

            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                // Update map
                (string image, string text) = ZoneUtils.GetActivityAssets(ActiveZones.Length == 0 ? PhotonNetworkController.Instance.StartZone : ActiveZones[0]);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;

                // Update description
                Activity.Details = "Not in Room";
                Activity.State = string.Empty;

                // Update party
                Activity.Party.Size.CurrentSize = 0;
                Activity.Party.Size.MaxSize = 0;
                Activity.Party.Id = string.Empty;
                Activity.Instance = false;

                return Activity;
            });

            DiscordWrapper.UpdateActivity();
        }

        public void OnEvent(EventData data)
        {
            if (data.Code != (int)ActivityJoinEventCode.RequestActivityData) return;

            Logging.Info("Recieved data from a particular actor requesting activity data");

            object[] content =
            [
                string.Join('.', ZoneManagement.instance.activeZones),
                LowEffortZonePatch.LowEffortZoneName,
                ZoneShaderSettings.hasActiveInstance ? ZoneShaderSettings.activeInstance.name : ZoneShaderSettings.defaultsInstance.name,
                string.Join('.', GorillaComputer.instance.allowedMapsToJoin)
            ];

            RaiseEventOptions raiseEventOptions = new()
            {
                TargetActors = [data.Sender]
            };

            PhotonNetwork.RaiseEvent((int)ActivityJoinEventCode.SendActivityData, content, raiseEventOptions, SendOptions.SendReliable);
            Logging.Info("Send data reliably");
        }
    }
}
