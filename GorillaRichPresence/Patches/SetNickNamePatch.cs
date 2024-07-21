using HarmonyLib;

namespace GorillaRichPresence.Patches
{
    [HarmonyPatch]
    public class SetNickNamePatch
    {
        [HarmonyPatch(typeof(NetworkSystemPUN), "SetMyNickName"), HarmonyPrefix]
        public static void SetNamePhotonUnityNetworking(string id) => GlobalEvents.Instance.NameChanged(id);

        [HarmonyPatch(typeof(NetworkSystemFusion), "SetMyNickName"), HarmonyPrefix]
        public static void SetNameFusion(string name) => GlobalEvents.Instance.NameChanged(name);
    }
}
