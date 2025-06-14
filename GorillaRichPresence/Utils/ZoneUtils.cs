using GorillaRichPresence.Extensions;

namespace GorillaRichPresence.Utils
{
    public static class ZoneUtils
    {
        public static string ToString(GTZone zone) => zone switch
        {
            GTZone.cityNoBuildings or GTZone.cityWithSkyJungle => "City",
            GTZone.skyJungle => "Clouds",
            GTZone.customMaps => "Virtual Stump",
            GTZone.monkeBlocks => "Monke Blocks",
            GTZone.hoverboard => "Hoverpark",
            GTZone.arena => "Magmarena",
            GTZone.mall => "Atrium",
            GTZone.ghostReactor or GTZone.ghostReactorTunnel => "Ghost Reactor",
            GTZone.monkeBlocksShared => "Share my Blocks",
            _ => zone.GetName().ToTitleCase()
        };

        public static (string image, string text) GetActivityAssets(GTZone zone) => (zone.GetName().ToLower(), ToString(zone));
    }
}
