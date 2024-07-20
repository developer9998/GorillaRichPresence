using System;
using BepInEx.Configuration;
using Bepinject;

namespace GorillaRichPresence.Tools
{
    // Token: 0x0200003F RID: 63
    public class Configuration
    {
        // Token: 0x06000138 RID: 312 RVA: 0x00006948 File Offset: 0x00004B48
        public Configuration(BepInConfig config)
        {
            this._configFile = config.Config;
            this.displayPublicCode = this._configFile.Bind<bool>("Appearance", "Display Public Code", true, "Determines whether the current room code for a public room should be displayed");
            this.displayPrivateCode = this._configFile.Bind<bool>("Appearance", "Display Private Code", false, "Determines whether the current room code for a public room should be displayed");
        }

        // Token: 0x04000129 RID: 297
        private ConfigFile _configFile;

        // Token: 0x0400012A RID: 298
        public ConfigEntry<bool> displayPublicCode;

        // Token: 0x0400012B RID: 299
        public ConfigEntry<bool> displayPrivateCode;
    }
}
