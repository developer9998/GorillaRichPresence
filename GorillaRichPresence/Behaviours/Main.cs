using Discord;
using GorillaGameModes;
using GorillaNetworking;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Tools;
using GorillaRichPresence.Utils;
using GorillaTagScripts.ModIO;
using ModIO;
using Photon.Pun;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace GorillaRichPresence.Behaviours
{
    public class Main : MonoBehaviour
    {
        public static Main Instance { get; private set; }

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
            Logging.Log("Object created");

            NetworkSystem.Instance.OnMultiplayerStarted += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerJoined += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerLeft += UpdateMultiplayer;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += UpdateSingleplayer;
            GameEvents.OnModIOLoggedIn.AddListener(new UnityAction(OnModIOLoggedIn));
            CustomMapManager.OnRoomMapChanged.AddListener(new UnityAction<ModId>(OnRoomMapChanged));

            Logging.Log("Constructing Discord client");
            DiscordRegistrar.Construct();

            Logging.Log("Constructing initial Activity");
            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                Activity.Details = "Not in Room";
                Activity.State = string.Empty;
                Activity.Assets.SmallImage = "gorilla";
                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();
                (string image, string text) = ZoneUtils.GetActivityAssets(PhotonNetworkController.Instance.StartZone);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;
                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        public void EnterMap(GTZone zone)
        {
            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                (string image, string text) = ZoneUtils.GetActivityAssets(zone);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;
                return Activity;
            });

            if (!NetworkSystem.Instance.InRoom || (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.SessionIsPrivate)) DiscordRegistrar.UpdateActivity();
        }

        public void ChangeName(string nickName)
        {
            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                Activity.Assets.SmallText = nickName;
                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        public void OnModIOLoggedIn()
        {
            CheckCustomMap();
            DiscordRegistrar.UpdateActivity();
        }

        public void OnRoomMapChanged(ModId roomMapModId)
        {
            CheckCustomMap();
            DiscordRegistrar.UpdateActivity();
        }

        private void UpdateMultiplayer(NetPlayer netPlayer) => UpdateMultiplayer();

        private void UpdateMultiplayer()
        {
            if (ZoneManagement.IsInZone(GTZone.customMaps))
            {
                CheckCustomMap();
            }

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                string gameModeString = NetworkSystem.Instance.GameModeString.ToUpper();
                bool isModdedRoom = gameModeString.Contains("MODDED_");

                string[] queueNames = ["DEFAULT", "MINIGAMES", "COMPETITIVE"];
                string currentQueue = queueNames.First(queue => queueNames.Contains(queue)).ToLower().ToTitleCase();

                string currentGameMode = GameMode.ActiveGameMode?.GameModeName()?.ToLower()?.ToTitleCase() ?? "Null";

                Activity.State = NetworkSystem.Instance.SessionIsPrivate ? $"In {(Configuration.DisplayPrivateCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Private Room")}" : $"In {(Configuration.DisplayPublicCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Public Room")}";
                Activity.Details = $"{currentQueue}, {(isModdedRoom ? "Modded " : "")}{currentGameMode}";
                Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                Activity.Party.Size.MaxSize = 10;
                Activity.Party.Id = NetworkSystem.Instance.RoomName + PhotonNetwork.CloudRegion.Replace("/*", "").ToUpper();

                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        private void UpdateSingleplayer()
        {
            if (ZoneManagement.IsInZone(GTZone.customMaps))
            {
                CheckCustomMap();
            }

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                Activity.Details = "Not in Room";
                Activity.State = string.Empty;

                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();

                Activity.Party.Size.CurrentSize = 0;
                Activity.Party.Size.MaxSize = 0;
                Activity.Party.Id = "null";
                Activity.Secrets.Join = string.Empty;

                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
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
                            DiscordRegistrar.ConstructActivity((Activity Activity) =>
                            {
                                Activity.Assets.LargeImage = result.data.logoImage640x360.url;
                                Activity.Assets.LargeText = $"{result.data.name} ({result.data.creator.username})";
                                return Activity;
                            });
                            return;
                        }
                    }, false);
                }
            }

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                (string image, string text) = ZoneUtils.GetActivityAssets(GTZone.customMaps);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;
                return Activity;
            });
        }
    }
}
