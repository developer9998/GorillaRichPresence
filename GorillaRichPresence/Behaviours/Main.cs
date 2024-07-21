using Discord;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Tools;
using UnityEngine;

namespace GorillaRichPresence.Behaviours
{
    public class Main : MonoBehaviour
    {
        private bool _isModdedRoom;

        public void Start()
        {
            Logging.Log("object created");
            GlobalEvents.OnMapEntered += OnMapEntered;
            GlobalEvents.OnNameChanged += OnNameChanged;
            GlobalEvents.OnModdedStatusChanged += OnModdedStatusChanged;

            NetworkSystem.Instance.OnMultiplayerStarted += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerJoined += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerLeft += UpdateMultiplayer;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += UpdateSingleplayer;

            Logging.Log("beginning construct");
            DiscordRegistrar.Construct();

            Logging.Log("constructed! setting up initial activity");
            DiscordRegistrar.ModifyActivity((Activity Activity) =>
            {
                Activity.State = "Not in Room";
                Activity.Details = string.Empty;
                Activity.Assets.SmallImage = "gorilla";
                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();
                Activity.Assets.LargeImage = "forest";
                Activity.Assets.LargeText = "forest".ToUpper();
                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        private void OnMapEntered(GTZone zone)
        {
            DiscordRegistrar.ModifyActivity((Activity Activity) =>
            {
                Activity.Assets.LargeImage = zone.ToString().ToLower();
                Activity.Assets.LargeText = zone.FormalZoneName();
                return Activity;
            });

            if (!NetworkSystem.Instance.InRoom || (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.SessionIsPrivate)) DiscordRegistrar.UpdateActivity();
        }

        private void OnNameChanged(string nickname)
        {
            DiscordRegistrar.ModifyActivity((Activity Activity) =>
            {
                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();
                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        private void OnModdedStatusChanged(bool isModdedRoom)
        {
            _isModdedRoom = isModdedRoom;

            if (!NetworkSystem.Instance.InRoom) return;
            UpdateMultiplayer();
        }

        private void UpdateMultiplayer(int player) => UpdateMultiplayer();

        private void UpdateMultiplayer()
        {
            DiscordRegistrar.ModifyActivity((Activity Activity) =>
            {
                string currentQueue = NetworkSystem.Instance.GameModeString.ToUpper().ToBestMatch("DEFAULT", "MINIGAMES", "COMPETITIVE");
                string currentGameMode = NetworkSystem.Instance.GameModeString.ToUpper().ToBestMatch("INFECTION", "CASUAL", "HUNT", "BATTLE", "PAINTBRAWL").Replace("BATTLE", "PAINTBRAWL");

                Activity.State = NetworkSystem.Instance.SessionIsPrivate ? string.Format("In {0}", Configuration.DisplayPrivateCode.Value ? string.Concat("Room ", NetworkSystem.Instance.RoomName) : "Private Room") : string.Format("In {0}", Configuration.DisplayPublicCode.Value ? string.Concat("Room ", NetworkSystem.Instance.RoomName) : "Public Room");
                Activity.Details = string.Concat(currentQueue.ToTitleCase(), ", ", _isModdedRoom ? "Modded " : "", currentGameMode.ToTitleCase());
                Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                Activity.Party.Size.MaxSize = 10;

                return Activity;
            });

            DiscordRegistrar.UpdateActivity();
        }

        private void UpdateSingleplayer()
        {
            DiscordRegistrar.ModifyActivity((Activity Activity) =>
            {
                Activity.State = "Not in Room";
                Activity.Details = string.Empty;

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
    }
}
