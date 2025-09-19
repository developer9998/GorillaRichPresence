using Discord;
using GorillaGameModes;
using GorillaNetworking;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Tools;
using GorillaRichPresence.Utils;
using Modio.Mods;
using GorillaTagScripts.VirtualStumpCustomMaps;
using Photon.Pun;
using System;
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
            // Events
            NetworkSystem.Instance.OnMultiplayerStarted += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerJoined += UpdateMultiplayerWithPlayer;
            NetworkSystem.Instance.OnPlayerLeft += UpdateMultiplayerWithPlayer;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += UpdateSingleplayer;

            ModIOManager.OnModIOLoggedIn.AddListener(new UnityAction(OnModIOLoggedIn));
            CustomMapManager.OnRoomMapChanged.AddListener(new UnityAction<ModId>(OnRoomMapChanged));

            DiscordWrapper.OnActivityJoin += OnActivityJoin;
            ZoneManagement.OnZoneChange += OnZoneChange;

            DiscordWrapper.Initialize();
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
            Logging.Message("OnActivityJoin");
            object[] secretContent = secrets.Split("\n");
            Array.ForEach(secretContent, Logging.Info);

            if (secretContent.ElementAtOrDefault(0) is not string roomCode || string.IsNullOrEmpty(roomCode) || string.IsNullOrWhiteSpace(roomCode) || secretContent.ElementAtOrDefault(1) is not string zone || string.IsNullOrEmpty(zone) || string.IsNullOrWhiteSpace(zone))
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

            if (zone == GTZone.none.GetName())
            {
                Logging.Warning("Unidentified zone");
                return;
            }

            if (zone != PhotonNetworkController.Instance.privateTrigger.networkZone)
            {
                bool validZone = false;

                foreach (GTZone gtzone in ZoneManagement.instance.activeZones)
                {
                    if (gtzone == GTZone.cave && zone.ToLower() == GTZone.mines.GetName())
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
            ActiveZones = [.. zones.Where(zone => zone.active).Select(zone => zone.zone)];

            DiscordWrapper.SetActivity(activity =>
            {
                // Update map
                (string image, string text) = ZoneUtils.GetActivityAssets(ActiveZones.First());
                activity.Assets.LargeImage = image;
                activity.Assets.LargeText = text;

                return activity;
            });

            DiscordWrapper.UpdateActivity();
        }

        public void OnModIOLoggedIn()
        {
            ModId modId = CustomMapManager.GetRoomMapId();
            SetVStumpActivity(modId);
        }

        public void OnRoomMapChanged(ModId roomMapModId)
        {
            SetVStumpActivity(roomMapModId);
        }

        private async void SetVStumpActivity(ModId modId)
        {
            if (modId == ModId.Null || !ModIOManager.IsLoggedIn())
            {
                ResetVStumpActivity();
                return;
            }

            var (error, mod) = await ModIOManager.GetMod(modId);

            if (error)
            {
                ResetVStumpActivity();
                return;
            }

            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                Activity.Assets.LargeImage = mod.Logo.GetUri(Mod.LogoResolution.X640_Y360).Url;
                Activity.Assets.LargeText = $"{mod.Name}: {mod.Creator.Username}";

                return Activity;
            });
        }

        public void ResetVStumpActivity()
        {
            DiscordWrapper.SetActivity((Activity Activity) =>
            {
                (string image, string text) = ZoneUtils.GetActivityAssets(GTZone.customMaps);
                Activity.Assets.LargeImage = image;
                Activity.Assets.LargeText = text;

                return Activity;
            });
            DiscordWrapper.UpdateActivity();
        }

        // Room

        private void UpdateMultiplayer() => UpdateMultiplayer(true);

        private void UpdateMultiplayerWithPlayer(NetPlayer netPlayer) => UpdateMultiplayer(false);

        private async void UpdateMultiplayer(bool isInitialJoin)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!NetworkSystem.Instance.InRoom)
                    break;

                DiscordWrapper.SetActivity(Activity =>
                {
                    // Define game mode 

                    bool pun = NetworkSystem.Instance is NetworkSystemPUN;

                    string gameModeString = NetworkSystem.Instance.GameModeString;
                    string gameTypeName = GameMode.FindGameModeInString(gameModeString);
                    string networkZone = GorillaComputer.instance.primaryTriggersByZone.Keys.FirstOrDefault(zone => gameModeString.StartsWith(zone)) ?? (gameModeString.StartsWith(PhotonNetworkController.Instance.privateTrigger.networkZone) ? PhotonNetworkController.Instance.privateTrigger.networkZone : null);

                    bool isModdedRoom = false;

                    ModId modId = CustomMapManager.GetRoomMapId();

                    if (pun && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("queueName", out object value) && value is string currentQueue)
                    {
                        isModdedRoom = gameModeString.Contains("MODDED_");
                    }
                    else
                    {
                        currentQueue = gameModeString;

                        if (!string.IsNullOrEmpty(networkZone))
                            currentQueue = currentQueue.RemoveStart(networkZone);

                        if (!string.IsNullOrEmpty(gameTypeName))
                            currentQueue = currentQueue.RemoveEnd(gameTypeName);

                        if (modId != ModId.Null)
                            currentQueue = currentQueue.Split(modId._id.ToString()).FirstOrDefault() ?? currentQueue;

                        if (currentQueue.EndsWith("MODDED_"))
                        {
                            isModdedRoom = true;
                            currentQueue = currentQueue.RemoveEnd("MODDED_");
                        }
                    }

                    string gameModeName = GameMode.gameModeKeyByName.TryGetValue(gameTypeName, out int key) && GameMode.gameModeTable.TryGetValue(key, out GorillaGameManager manager) ? manager.GameModeName() : gameTypeName;

                    // Update description
                    Activity.State = NetworkSystem.Instance.SessionIsPrivate ? $"In {(Configuration.DisplayPrivateCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Private Room")}" : $"In {(Configuration.DisplayPublicCode.Value ? $"Room {NetworkSystem.Instance.RoomName}" : "Public Room")}";
                    Activity.Details = $"Playing{(isModdedRoom ? " Modded " : " ")}{gameModeName.ToTitleCase()} in {currentQueue.ToTitleCase()}";

                    // Update party
                    Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                    Activity.Party.Size.MaxSize = RoomSystem.GetRoomSize(gameModeString);
                    Activity.Party.Id = string.Concat(NetworkSystem.Instance.RoomName, NetworkSystem.Instance.CurrentRegion.Replace("/*", ""), NetworkSystem.Instance.MasterClient.ActorNumber);
                    // Activity.Party.Privacy = NetworkSystem.Instance.SessionIsPrivate ? 1 : 0;
                    Activity.Instance = true;

                    bool isCustomMap = modId != ModId.Null && ModIOManager.IsLoggedIn();
                    if (i == 0 && isInitialJoin && isCustomMap) SetVStumpActivity(modId);
                    else if (isInitialJoin && !isCustomMap)
                    {
                        // Update map
                        (string image, string text) = ZoneUtils.GetActivityAssets(ActiveZones.Length == 0 ? PhotonNetworkController.Instance.StartZone : ActiveZones[0]);
                        Activity.Assets.LargeImage = image;
                        Activity.Assets.LargeText = text;
                    }

                    return Activity;
                });

                DiscordWrapper.UpdateActivity();

                await Task.Delay(500);
            }
        }

        private void UpdateSingleplayer()
        {
            if (ZoneManagement.IsInZone(GTZone.customMaps))
            {
                ModId modId = CustomMapManager.GetRoomMapId();
                SetVStumpActivity(modId);
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
