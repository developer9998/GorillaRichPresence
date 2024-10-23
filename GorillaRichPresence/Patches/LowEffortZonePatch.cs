using GorillaRichPresence.Tools;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace GorillaRichPresence.Patches
{
    [HarmonyPatch(typeof(LowEffortZone), nameof(LowEffortZone.OnBoxTriggered))]
    public class LowEffortZonePatch
    {
        public static string LowEffortZoneName;

        private static GameObject[] toEnable, toDisable;

        public static void Postfix(LowEffortZone __instance)
        {
            if (toEnable != null && toDisable != null && Enumerable.SequenceEqual(toEnable, __instance.objectsToDisable) && Enumerable.SequenceEqual(toDisable, __instance.objectsToEnable))
            {
                LowEffortZoneName = string.Empty;
                toEnable = null;
                toDisable = null;
            }
            else
            {
                LowEffortZoneName = __instance.name;
                toEnable = __instance.objectsToEnable;
                toDisable = __instance.objectsToDisable;
            }

            DiscordWrapper.UpdateActivity();
        }
    }
}
