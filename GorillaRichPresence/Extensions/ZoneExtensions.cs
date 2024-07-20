namespace GorillaRichPresence.Extensions
{
    public static class ZoneExtensions
    {
        public static string FormalZoneName(this GTZone zone)
        {
            string zoneName = zone.ToString();
            return zoneName switch
            {
                "cityNoBuildings" => "City",
                "skyJungle" => "Clouds",
                "cityWithSkyJungle" => "City",
                _ => zoneName.ToFormal()
            };
        }
    }
}
