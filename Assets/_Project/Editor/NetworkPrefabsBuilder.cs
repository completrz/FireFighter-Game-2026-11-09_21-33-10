using Firehouse.Core;
using Firehouse.Lobby;
using Firehouse.Networking;
using Firehouse.Networking.Transport;
using Firehouse.Player;
using Firehouse.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>Builds the persistent NetworkRoot prefab and the (placeholder) player prefab.</summary>
    public static class NetworkPrefabsBuilder
    {
        public const string ResourcesFolder = ProjectAssets.Root + "/Resources";
        public const string NetworkRootPath = ResourcesFolder + "/NetworkRoot.prefab";
        public const string PlayerPrefabPath = ProjectAssets.PrefabsFolder + "/P_Player.prefab";
        public const string NetworkPrefabsListPath = ProjectAssets.DataFolder + "/NetworkPrefabs.asset";

        [MenuItem("Tools/Firehouse/Build/Player Prefab (placeholder)")]
        public static GameObject BuildPlayerPrefab()
        {
            ProjectAssets.EnsureFolder(ProjectAssets.PrefabsFolder);
            var mat = ProjectAssets.SharedMaterial;

            var root = new GameObject("P_Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 0.8f, 0);
            cc.stepOffset = 0.35f;

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;

            // Body: white so the coat tint shows; head/visor separate so the owner can hide it.
            var bodyMesh = new MeshBuilder()
                .AddCapsule(new Vector3(0, 0.75f, 0), 0.42f, 1.5f, 12, Color.white)
                .Build("Player_Placeholder_Body");
            var body = Child(root, "Body", ProjectAssets.SaveMesh(bodyMesh, "Player_Placeholder_Body"), mat);

            var headMesh = new MeshBuilder()
                .AddSphereBand(new Vector3(0, 1.45f, 0), new Vector3(0.5f, 0.42f, 0.5f), 0, 95, 12, 4, new Color(0.95f, 0.8f, 0.2f)) // helmet
                .AddBox(new Vector3(0, 1.32f, 0.36f), new Vector3(0.5f, 0.22f, 0.12f), new Color(0.12f, 0.12f, 0.14f))            // visor
                .Build("Player_Placeholder_Head");
            var head = Child(root, "Head", ProjectAssets.SaveMesh(headMesh, "Player_Placeholder_Head"), mat);

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0, 1.4f, 0.05f);
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.enabled = false;
            var listener = camGo.AddComponent<AudioListener>();
            listener.enabled = false;

            var identity = root.AddComponent<PlayerIdentity>();
            SetField(identity, "_coatRenderers", new Renderer[] { body.GetComponent<Renderer>() });

            var lpc = root.AddComponent<LocalPlayerCamera>();
            SetField(lpc, "_camera", cam);
            SetField(lpc, "_listener", listener);
            SetField(lpc, "_hideForOwner", new Renderer[] { head.GetComponent<Renderer>() });

            var motor = root.AddComponent<SimplePlayerMotor>();
            SetField(motor, "_cameraPivot", pivot.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            RegisterNetworkPrefab(prefab);
            Debug.Log($"[Firehouse] Built {PlayerPrefabPath}");
            return prefab;
        }

        [MenuItem("Tools/Firehouse/Build/Network Root")]
        public static void BuildNetworkRoot()
        {
            ProjectAssets.EnsureFolder(ResourcesFolder);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) ?? BuildPlayerPrefab();

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

        private static GameObject Child(GameObject parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
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
