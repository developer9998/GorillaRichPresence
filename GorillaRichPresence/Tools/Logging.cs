using BepInEx.Logging;
using Bepinject;
using Zenject;

namespace GorillaRichPresence.Tools
{
    internal class Logging : IInitializable
    {
        internal static ManualLogSource _manualLogSource;

        [Inject]
        public void Construct(BepInLog bepInLog)
        {
            _manualLogSource = bepInLog.Logger;
        }

        public void Initialize()
        {

        }

        public static void Info(object data) => _manualLogSource.LogInfo(data);

        public static void Warning(object data) => _manualLogSource.LogWarning(data);

        public static void Error(object data) => _manualLogSource.LogError(data);
    }
}
