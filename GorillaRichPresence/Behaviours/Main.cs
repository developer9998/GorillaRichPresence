using Discord;
using GorillaGameModes;
using GorillaNetworking;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Tools;
using GorillaRichPresence.Utils;
using GorillaTagScripts.ModIO;
using ModIO;
using System.Linq;
using System.Threading.Tasks;
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
        }

        public void Start()
        {
            // Add network events
            NetworkSystem.Instance.OnMultiplayerStarted += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerJoined += UpdateMultiplayerWithPlayer;
            NetworkSystem.Instance.OnPlayerLeft += UpdateMultiplayerWithPlayer;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += UpdateSingleplayer;

            // Add mod.io map events
            ModIOManager.OnModIOLoggedIn.AddListener(new UnityAction(OnModIOLoggedIn));
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
            object[] secretContent = secrets.Split("\n");

            Logging.Info($"Secret: {string.Join(", ", secretContent)}");

            if(secretContent.ElementAtOrDefault(0) is not string roomCode || string.IsNullOrEmpty(roomCode) || string.IsNullOrWhiteSpace(roomCode) || secretContent.ElementAtOrDefault(1) is not string zone || string.IsNullOrEmpty(zone) || string.IsNullOrWhiteSpace(zone))
            {
                Logging.Warning("Secrets are malformed");
                return;
            }

            if (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.RoomName == roomCode)
            {
                Logging.Warning("Already with player");
                return;
            }

            if (roomCode.StartsWith(GorillaComputer.instance.VStumpRoomPrepend))
            {
                Logging.Warning("Entering VStump rooms is unsupported for the time being");
                return;
            }

            if (zone == "none")
            {
                Logging.Warning("Unidentified zone");
                return;
            }

            if (zone != "private")
            {
                bool validZone = false;

                foreach (GTZone gtzone in ZoneManagement.instance.activeZones)
                {
                    if (gtzone == GTZone.cave && zone.ToLower() == "mines")
                    {
                        validZone = true;
                        break;
                    }

                    if (zone.ToLower().Contains(gtzone.GetName().ToLower()))
                    {
                        validZone = true;
                        break;
                    }
                }

                if (!validZone)
                {
                    Logging.Warning("Invalid zone");
                    return;
                }
            }

            Logging.Info($"Joining {roomCode}");

            GorillaComputer.instance.roomToJoin = roomCode;
            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomCode, JoinType.Solo);
        }

        // Profile

        public void ChangeName(string nickName)
        {
            DiscordWrapper.SetActivity(Activity =>
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
            if (ModIOManager.IsLoggedIn())
            {
                ModId currentRoomMap = CustomMapManager.GetRoomMapId();
                if (currentRoomMap != ModId.Null)
                {
                    ModIOManager.GetModProfile(currentRoomMap, (ModIORequestResultAnd<ModProfile> result) =>
                    {
                        if (result.result.success && ModIOManager.IsLoggedIn()) // second login check, just to be safe!
                        {
                            DiscordWrapper.SetActivity((Activity Activity) =>
                            {
                                // Update map (custom map)
                                Activity.Assets.LargeImage = result.data.logoImage320x180.url;
                                Activity.Assets.LargeText = $"{result.data.creator.username}: {result.data.name}";

                                return Activity;
                            });

                            return;
                        }
                    });
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

        private void UpdateMultiplayerWithPlayer(NetPlayer netPlayer) => UpdateMultiplayer(false);

        private async void UpdateMultiplayer(bool isInitialJoin)
        {
            if (ZoneManagement.IsInZone(GTZone.customMaps))
            {
                CheckCustomMap();
            }

            for (int i = 0; i < 2; i++)
            {
                DiscordWrapper.SetActivity((Activity Activity) =>
                {
                    // Define game mode 

                    string gameModeString = NetworkSystem.Instance.GameModeString;
                    string gameTypeName = GameMode.FindGameModeInString(gameModeString);
                    string networkZone = GorillaComputer.instance.primaryTriggersByZone.Keys.FirstOrDefault(zone => gameModeString.StartsWith(zone)) ?? (gameModeString.StartsWith(PhotonNetworkController.Instance.privateTrigger.networkZone) ? PhotonNetworkController.Instance.privateTrigger.networkZone : null);

                    string currentQueue = gameModeString;

                    if (!string.IsNullOrEmpty(networkZone))
                        currentQueue = currentQueue.RemoveStart(networkZone);

                    if (!string.IsNullOrEmpty(gameTypeName))
                        currentQueue = currentQueue.RemoveEnd(gameTypeName);

                    bool isModdedRoom = false;

                    if (currentQueue.EndsWith("modded_"))
                    {
                        isModdedRoom = true;
                        currentQueue.RemoveEnd("modded_");
                    }

                    ModId currentRoomMap = CustomMapManager.GetRoomMapId();
                    if (currentRoomMap != ModId.Null)
                        currentQueue = currentQueue.Split(currentRoomMap.id.ToString()).FirstOrDefault() ?? currentQueue;

                    string gameModeName = GameMode.gameModeKeyByName.TryGetValue(gameTypeName, out int key) && GameMode.gameModeTable.TryGetValue(key, out GorillaGameManager manager) ? manager.GameModeName() : gameTypeName;

                    // Update description
                    Activity.State = NetworkSystem.Instance.SessionIsPrivate ? $"In {(Configuration.DisplayPrivateCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Private Room")}" : $"In {(Configuration.DisplayPublicCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Public Room")}";
                    Activity.Details = $"Playing{(isModdedRoom ? " Modded " : " ")}{gameModeName.ToTitleCase()} under {currentQueue.ToTitleCase()}";

                    // Update party
                    Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                    Activity.Party.Size.MaxSize = NetworkSystem.Instance.config.MaxPlayerCount;
                    Activity.Party.Id = string.Concat(NetworkSystem.Instance.RoomName, NetworkSystem.Instance.CurrentRegion.Replace("/*", ""), NetworkSystem.Instance.MasterClient.ActorNumber);
                    Activity.Instance = true;

                    if (isInitialJoin && currentRoomMap == ModId.Null)
                    {
                        // Update map
                        (string image, string text) = ZoneUtils.GetActivityAssets(ActiveZones.Length == 0 ? PhotonNetworkController.Instance.StartZone : ActiveZones[0]);
                        Activity.Assets.LargeImage = image;
                        Activity.Assets.LargeText = text;
                    }

                    return Activity;
                });

                DiscordWrapper.UpdateActivity();
                await Task.Delay(300);
            }
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
    }
}
