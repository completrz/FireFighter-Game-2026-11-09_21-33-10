using UnityEngine;

namespace Firehouse.Networking
{
    /// <summary>
    /// Spawns the persistent NetworkRoot prefab (NetworkManager, transports, Steam, session, menu) before the first
    /// scene loads, so pressing Play in any scene works and there's never more than one NetworkManager.
    /// </summary>
    public static class NetworkRootBoot
    {
        public const string ResourcePath = "NetworkRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (SessionManager.Instance != null) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[Firehouse] Resources/NetworkRoot prefab missing. Run Tools/Firehouse/Build/Network Root.");
                return;
            }
            var root = Object.Instantiate(prefab);
            root.name = "NetworkRoot";
        }
    }
}
