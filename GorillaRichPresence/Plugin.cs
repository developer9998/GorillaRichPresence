using BepInEx;
using GorillaInfoWatch.Models.Attributes;
using GorillaRichPresence.Behaviours;
using GorillaRichPresence.Tools;
using HarmonyLib;
using UnityEngine;

[assembly: InfoWatchCompatible]

namespace GorillaRichPresence
{
    [BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logging.Logger = Logger;
            Configuration.Initialize(Config);

            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, Constants.Guid);
            GorillaTagger.OnPlayerSpawned(() => new GameObject(Constants.Name, typeof(Main)));
        }
    }
}
