using Firehouse.Lobby;
using Firehouse.Networking;
using Firehouse.Networking.Transport;
using Firehouse.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>Builds the persistent NetworkRoot prefab and owns the NetworkPrefabs list.</summary>
    public static class NetworkPrefabsBuilder
    {
        public const string ResourcesFolder = ProjectAssets.Root + "/Resources";
        public const string NetworkRootPath = ResourcesFolder + "/NetworkRoot.prefab";
        public const string PlayerPrefabPath = SmokieBuilder.PlayerPrefabPath;
        public const string NetworkPrefabsListPath = ProjectAssets.DataFolder + "/NetworkPrefabs.asset";

        [MenuItem("Tools/Firehouse/Build/Network Root")]
        public static void BuildNetworkRoot()
        {
            ProjectAssets.EnsureFolder(ResourcesFolder);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) ?? SmokieBuilder.Build();

            var root = new GameObject("NetworkRoot");
            var nm = root.AddComponent<NetworkManager>();
            var utp = root.AddComponent<UnityTransport>();
            var steamTransport = root.AddComponent<FacepunchTransport>();
            var steam = root.AddComponent<SteamService>();
            var lobby = root.AddComponent<SteamLobbyService>();
            var session = root.AddComponent<SessionManager>();
            var menu = root.AddComponent<MainMenu>();

            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                PlayerPrefab = null, // spawned per scene by PlayerSpawner
                EnableSceneManagement = true,
                ConnectionApproval = false,
                TickRate = 30,
            };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(LoadOrCreatePrefabsList());

            SetField(session, "_network", nm);
            SetField(session, "_localTransport", utp);
            SetField(session, "_steamTransport", steamTransport);
            SetField(session, "_lobby", lobby);
            SetField(session, "_playerPrefab", player.GetComponent<NetworkObject>());
            SetField(menu, "_session", session);
            _ = steam;

            PrefabUtility.SaveAsPrefabAsset(root, NetworkRootPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Firehouse] Built {NetworkRootPath}");
        }

        private static NetworkPrefabsList LoadOrCreatePrefabsList()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (list != null) return list;
            ProjectAssets.EnsureFolder(ProjectAssets.DataFolder);
            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);
            return list;
        }

        /// <summary>Adds a prefab to our NetworkPrefabs list (idempotent).</summary>
        public static void RegisterNetworkPrefab(GameObject prefab)
        {
            var list = LoadOrCreatePrefabsList();
            if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Sets a private [SerializeField] through SerializedObject so it's saved in the prefab.</summary>
        public static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Firehouse] Field '{field}' not found on {target.GetType().Name}");
                return;
            }
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case Object[] arr:
                    prop.arraySize = arr.Length;
                    for (int i = 0; i < arr.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                    break;
                case float f: prop.floatValue = f; break;
                case int i2: prop.intValue = i2; break;
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
                default: Debug.LogError($"[Firehouse] Unsupported value type for {field}"); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
