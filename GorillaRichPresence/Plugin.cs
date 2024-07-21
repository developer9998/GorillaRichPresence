using BepInEx;
using GorillaRichPresence.Behaviours;
using GorillaRichPresence.Tools;
using HarmonyLib;
using System;
using UnityEngine;
using Utilla;

namespace GorillaRichPresence
{
    [ModdedGamemode, BepInDependency("org.legoandmars.gorillatag.utilla")]
    [BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            Logging.Logger = Logger;
            Configuration.Construct(Config);
        }

        public void Start()
        {
            GorillaTagger.OnPlayerSpawned(Initialize);
            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, Constants.Guid);
        }

        public void Initialize()
        {
            try
            {
                new GameObject("GorillaRichPresence", typeof(Main));
            }
            catch (Exception ex)
            {
                Logging.Error($"Error when initializing GorillaRichPresence: {ex}");
            }
        }

        [ModdedGamemodeJoin]
        public void OnModdedJoin() => GlobalEvents.Instance.ModdedStatusChanged(true);

        [ModdedGamemodeLeave]
        public void OnModdedLeave() => GlobalEvents.Instance.ModdedStatusChanged(false);
    }
}
