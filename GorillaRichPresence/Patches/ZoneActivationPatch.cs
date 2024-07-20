using HarmonyLib;
using System.Linq;

namespace GorillaRichPresence.Patches
{
    [HarmonyPatch(typeof(ZoneManagement), "SetZones"), HarmonyWrapSafe]
    public class ZoneActivationPatch
    {
        public static void Prefix(GTZone[] newActiveZones)
        {
            GTZone primaryZone = newActiveZones.First();
            RP_Events.Instance.MapEntered(primaryZone);
        }
    }
}
