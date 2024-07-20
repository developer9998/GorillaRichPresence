using System;

namespace GorillaRichPresence
{
    public class RP_Events
    {
        public static RP_Events Instance => events;

        private static readonly RP_Events events = new();

        public static event Action<GTZone> OnMapEntered;
        public static event Action<string> OnNameChanged;
        public static event Action<bool> OnModdedStatusChanged;

        public virtual void MapEntered(GTZone zone) => OnMapEntered?.Invoke(zone);
        public virtual void NameChanged(string nickname) => OnNameChanged?.Invoke(nickname);
        public virtual void ModdedStatusChanged(bool isModdedRoom) => OnModdedStatusChanged?.Invoke(isModdedRoom);
    }
}
