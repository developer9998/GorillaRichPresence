using Discord;
using GorillaRichPresence.Extensions;
using GorillaRichPresence.Tools;
using UnityEngine;
using Zenject;

namespace GorillaRichPresence.Behaviours
{
    public class RP_Core : MonoBehaviour, IInitializable
    {
        private DiscordRegistrar _discordRegistar;
        private Configuration _configuration;

        private bool _isModdedRoom;

        [Inject]
        public void Construct(DiscordRegistrar discordRegistrar, Configuration configuration)
        {
            _discordRegistar = discordRegistrar;
            _configuration = configuration;
        }

        public void Initialize()
        {
            RP_Events.OnMapEntered += OnMapEntered;
            RP_Events.OnNameChanged += OnNameChanged;
            RP_Events.OnModdedStatusChanged += OnModdedStatusChanged;

            NetworkSystem.Instance.OnMultiplayerStarted += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerJoined += UpdateMultiplayer;
            NetworkSystem.Instance.OnPlayerLeft += UpdateMultiplayer;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += UpdateSingleplayer;

            _discordRegistar.Initialize();

            _discordRegistar.ModifyActivity((Activity Activity) =>
            {
                Activity.State = "Not in Room";
                Activity.Details = string.Empty;
                Activity.Assets.SmallImage = "gorilla";
                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();
                return Activity;
            });

            _discordRegistar.UpdateActivity();
        }

        private void OnMapEntered(GTZone zone)
        {
            _discordRegistar.ModifyActivity((Activity Activity) =>
            {
                Activity.Assets.LargeImage = zone.ToString().ToLower();
                Activity.Assets.LargeText = zone.FormalZoneName();
                return Activity;
            });

            if (!NetworkSystem.Instance.InRoom || (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.SessionIsPrivate)) _discordRegistar.UpdateActivity();
        }

        private void OnNameChanged(string nickname)
        {
            _discordRegistar.ModifyActivity((Activity Activity) =>
            {
                Activity.Assets.SmallText = NetworkSystem.Instance.GetMyNickName();
                return Activity;
            });

            _discordRegistar.UpdateActivity();
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
            _discordRegistar.ModifyActivity((Activity Activity) =>
            {
                string currentQueue = NetworkSystem.Instance.GameModeString.ToUpper().ToBestMatch("DEFAULT", "MINIGAMES", "COMPETITIVE");
                string currentGameMode = NetworkSystem.Instance.GameModeString.ToUpper().ToBestMatch("INFECTION", "CASUAL", "HUNT", "BATTLE", "PAINTBRAWL").Replace("BATTLE", "PAINTBRAWL");

                Activity.State = NetworkSystem.Instance.SessionIsPrivate ? string.Format("In {0}", _configuration.displayPrivateCode.Value ? string.Concat("Room ", NetworkSystem.Instance.RoomName) : "Private Room") : string.Format("In {0}", _configuration.displayPublicCode.Value ? string.Concat("Room ", NetworkSystem.Instance.RoomName) : "Public Room");
                Activity.Details = string.Concat(currentQueue.ToFormal(), ", ", _isModdedRoom ? "Modded " : "", currentGameMode.ToFormal());
                Activity.Party.Size.CurrentSize = NetworkSystem.Instance.RoomPlayerCount;
                Activity.Party.Size.MaxSize = 10;

                return Activity;
            });

            _discordRegistar.UpdateActivity();
        }

        private void UpdateSingleplayer()
        {
            _discordRegistar.ModifyActivity((Activity Activity) =>
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

            _discordRegistar.UpdateActivity();
        }
    }
}
