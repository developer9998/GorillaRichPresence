using BepInEx.Configuration;

namespace GorillaRichPresence.Tools
{
    public class Configuration
    {
        public static ConfigFile File;

        public static ConfigEntry<bool> DisplayPublicCode;

        public static ConfigEntry<bool> DisplayPrivateCode;

        public static void Construct(ConfigFile file)
        {
            File = file;

            DisplayPublicCode = File.Bind
            (
                "Appearance",
                "Display Public Code",
                true,
                new ConfigDescription("If the current room code will be displayed when in a public room")
            );

            DisplayPrivateCode = File.Bind
            (
               "Appearance",
               "Display Private Code",
               false,
               new ConfigDescription("If the current room code will be displayed when in a private room")
            );
        }
    }
}
