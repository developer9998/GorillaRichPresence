namespace GorillaRichPresence.Extensions
{
    public static class ZoneEx
    {
        public static string ToString(this GTZone zone)
        {
            string zoneName = zone.ToString().ToUpper();

            return zoneName switch
            {
                "SKYJUNGLE" => "CLOUDS",
                "CITYNOBUILDINGS" or "CITYWITHSKYJUNGLE" => "CITY",
                "CUSTOMMAPS" => "VIRTUAL STUMP",
                _ => zoneName
            };
        }
    }
}
