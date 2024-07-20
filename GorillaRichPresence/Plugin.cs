using BepInEx;
using Bepinject;
using HarmonyLib;
using Utilla;

namespace GorillaRichPresence
{
    [ModdedGamemode, BepInDependency("org.legoandmars.gorillatag.utilla")]
    [BepInPlugin(RP_Constants.Guid, RP_Constants.Name, RP_Constants.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Zenjector.Install<RP_Installer>().OnProject().WithConfig(Config).WithLog(Logger);
            new Harmony(RP_Constants.Guid).PatchAll(typeof(Plugin).Assembly);
        }

        [ModdedGamemodeJoin]
        public void OnModdedJoin() => RP_Events.Instance.ModdedStatusChanged(true);

        [ModdedGamemodeLeave]
        public void OnModdedLeave() => RP_Events.Instance.ModdedStatusChanged(false);
    }
}
