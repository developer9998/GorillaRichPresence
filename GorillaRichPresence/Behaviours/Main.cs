using Discord;
using GorillaGameModes;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Tools;
using GorillaTagScripts.ModIO;
using ModIO;
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace GorillaRichPresence.Behaviours
{
    public class Main : MonoBehaviour
    {
        public static Main Instance { get; private set; }

        private GTZone Zone;

        private bool UseRoomMap;
        private ModProfile RoomMap;

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
                Activity.Assets.LargeImage = "forest";
                Activity.Assets.LargeText = "forest".ToUpper();
                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        public void EnterMap(GTZone zone)
        {
            Zone = zone;

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                Activity.Assets.LargeImage = zone.ToString().ToLower();
                Activity.Assets.LargeText = ZoneEx.ToString(zone);
                return Activity;
            });

            if (!NetworkSystem.Instance.InRoom || (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.SessionIsPrivate) || Zone == GTZone.customMaps) DiscordRegistrar.UpdateActivity();
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
            UpdateRoomMap();

            DiscordRegistrar.UpdateActivity();
        }

        public void OnRoomMapChanged(ModId roomMapModId)
        {
            UpdateRoomMap();

            DiscordRegistrar.UpdateActivity();
        }

        private void UpdateMultiplayer(NetPlayer netPlayer) => UpdateMultiplayer();

        private void UpdateMultiplayer()
        {
            if (Zone == GTZone.customMaps)
            {
                UpdateRoomMap();
            }

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

                string gameModeString = NetworkSystem.Instance.GameModeString.ToUpper();
                bool isModdedRoom = gameModeString.Contains("MODDED_");

                string[] queueNames = ["DEFAULT", "MINIGAMES", "COMPETITIVE"];
                string currentQueue = textInfo.ToTitleCase(queueNames.First(queue => queueNames.Contains(queue)).ToLower());

                var gameModeNames = Enum.GetNames(typeof(GameModeType));
                string currentGameMode = textInfo.ToTitleCase(gameModeNames.Select(gameMode => gameMode.ToUpper()).First(gameMode => gameModeString.Contains(gameMode)).ToLower());

                Activity.State = NetworkSystem.Instance.SessionIsPrivate ? string.Format("In {0}", Configuration.DisplayPrivateCode.Value ? string.Concat("Room ", NetworkSystem.Instance.RoomName) : "Private Room") : string.Format("In {0}", Configuration.DisplayPublicCode.Value ? string.Concat("Room ", NetworkSystem.Instance.RoomName) : "Public Room");
                Activity.Details = string.Concat(currentQueue, ", ", isModdedRoom ? "Modded " : "", currentGameMode);
                Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                Activity.Party.Size.MaxSize = 10;

                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        private void UpdateSingleplayer()
        {
            if (Zone == GTZone.customMaps)
            {
                UpdateRoomMap();
            }

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                Activity.Details = "Not in Room";
                Activity.State = string.Empty;

                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();

                Activity.Party.Size.CurrentSize = 0;
                Activity.Party.Size.MaxSize = 0;
                Activity.Party.Id = 128.ToString();
                Activity.Secrets.Match = string.Empty;
                Activity.Secrets.Spectate = string.Empty;
                Activity.Secrets.Join = string.Empty;

                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        private void UpdateRoomMap()
        {
            UseRoomMap = false;

            if (ModIODataStore.IsLoggedIn())
            {
                ModId currentRoomMap = CustomMapManager.GetRoomMapId();
                if (currentRoomMap != ModId.Null)
                {
                    ModIODataStore.GetModProfile(currentRoomMap, delegate (ModIORequestResultAnd<ModProfile> result)
                    {
                        if (ModIODataStore.IsLoggedIn() && result.result.success)
                        {
                            UseRoomMap = true;
                            RoomMap = result.data;
                        }
                    }, false);
                }
            }

            if (UseRoomMap)
            {
                DiscordRegistrar.ConstructActivity((Activity Activity) =>
                {
                    Activity.Assets.LargeImage = RoomMap.logoImage640x360.url;
                    Activity.Assets.LargeText = $"{RoomMap.name} ({RoomMap.creator.username})";
                    return Activity;
                });
                return;
            }

            DiscordRegistrar.ConstructActivity((Activity Activity) =>
            {
                Activity.Assets.LargeImage = GTZone.customMaps.ToString().ToLower();
                Activity.Assets.LargeText = ZoneEx.ToString(GTZone.customMaps);
                return Activity;
            });
        }
    }
}
