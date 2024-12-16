using GorillaRichPresence.Extensions;

namespace GorillaRichPresence.Utils
{
    public static class ZoneUtils
    {
        public static string ToString(GTZone zone) => zone switch
        {
            GTZone.skyJungle => "Clouds",
            GTZone.cityNoBuildings or GTZone.cityWithSkyJungle => "City",
            GTZone.customMaps => "Virtual Stump",
            GTZone.monkeBlocks => "Monke Blocks",
            _ => zone.ToString().ToLower().ToTitleCase()
        };

        public static (string image, string text) GetActivityAssets(GTZone zone) => (zone.ToString().ToLower(), ToString(zone));
    }
}
