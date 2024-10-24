using GorillaTag.Rendering;

namespace GorillaRichPresence.Models
{
    public class ActivityJoinData
    {
        public NetPlayer Player;

        public GTZone[] Zones;

        public string LowEffortZone;

        public ZoneShaderSettings ShaderSettings;
    }
}
