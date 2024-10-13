using GorillaRichPresence.Behaviours;
using HarmonyLib;

namespace GorillaRichPresence.Patches
{
    [HarmonyPatch]
    public class SetNickNamePatch
    {
        [HarmonyPatch(typeof(NetworkSystemPUN), "SetMyNickName"), HarmonyPostfix]
        public static void SetNamePhotonUnityNetworking(string id) => Main.Instance.ChangeName(id);

        [HarmonyPatch(typeof(NetworkSystemFusion), "SetMyNickName"), HarmonyPostfix]
        public static void SetNameFusion(string name) => Main.Instance.ChangeName(name);
    }
}
