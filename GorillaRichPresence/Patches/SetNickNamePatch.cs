using HarmonyLib;

namespace GorillaRichPresence.Patches
{
    [HarmonyPatch]
    public class SetNickNamePatch
    {
        [HarmonyPatch(typeof(NetworkSystemPUN), "SetMyNickName"), HarmonyPrefix]
        public static void SetNamePhotonUnityNetworking(string id) => RP_Events.Instance.NameChanged(id);

        [HarmonyPatch(typeof(NetworkSystemFusion), "SetMyNickName"), HarmonyPrefix]
        public static void SetNameFusion(string name) => RP_Events.Instance.NameChanged(name);
    }
}
