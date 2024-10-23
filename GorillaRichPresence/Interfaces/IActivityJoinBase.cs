using GorillaTag.Rendering;

namespace GorillaRichPresence.Interfaces
{
    internal interface IActivityJoinBase
    {
        public string Secrets { get; set; }

        string ActivityRoomName { get; protected set; }

        string ActivityUserId { get; protected set; }

        GTZone[] ActivityZones { get; protected set; }

        string ActivityLowEffortZone { get; protected set; }

        ZoneShaderSettings ActivityShaderSettings { get; protected set; }

        void Conclude();
    }
}
