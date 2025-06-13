using System;
using GorillaTag.Rendering;

namespace GorillaRichPresence.Models
{
    [Obsolete]
    public class ActivityJoinData
    {
        public NetPlayer Player;

        public GTZone[] Zones;

        public string LowEffortZone;

        public ZoneShaderSettings ShaderSettings;
    }
}
