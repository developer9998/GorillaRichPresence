using GorillaRichPresence.Tools;
using System.Diagnostics;
using UnityEngine;

namespace GorillaRichPresence.Extensions
{
    internal static class BehaviourEx
    {
        public static void LogCurrentMethod(this MonoBehaviour behaviour)
        {
            var methodInfo = new StackTrace().GetFrame(1).GetMethod();
            Logging.Info($"{behaviour.GetType().Name} {methodInfo.Name}");
        }
    }
}
