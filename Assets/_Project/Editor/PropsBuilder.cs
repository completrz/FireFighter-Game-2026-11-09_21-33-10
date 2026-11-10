using Firehouse.Core;
using Firehouse.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>Builds networked carryable props (Prefabs/Props/*.prefab).</summary>
    public static class PropsBuilder
    {
        public const string Folder = ProjectAssets.PrefabsFolder + "/Props";

        static readonly Color Wood = new(0.68f, 0.50f, 0.30f);
        static readonly Color WoodDark = new(0.50f, 0.35f, 0.20f);
        static readonly Color Orange = new(0.95f, 0.45f, 0.10f);
        static readonly Color Black = new(0.10f, 0.10f, 0.10f);
        static readonly Color Reflect = new(0.95f, 0.95f, 0.95f, 0.6f);
        static readonly Color Dummy = new(0.85f, 0.65f, 0.35f);

        public static string CratePath => $"{Folder}/P_Crate.prefab";
        public static string BigCratePath => $"{Folder}/P_BigCrate.prefab";
        public static string BallPath => $"{Folder}/P_Ball.prefab";
        public static string ConePath => $"{Folder}/P_Cone.prefab";
        public static string DummyPath => $"{Folder}/P_RescueDummy.prefab";
        public static string MailboxPath => $"{Folder}/P_Mailbox.prefab";

        [MenuItem("Tools/Firehouse/Build/Props")]
        public static void BuildAll()
        {
            ProjectAssets.EnsureFolder(Folder);

            // Small crate: easy one-hand carry
            var crate = new MeshBuilder().AddBox(Vector3.zero, Vector3.one * 0.6f, Wood)
                .AddBox(new Vector3(0, 0, 0.301f), new Vector3(0.62f, 0.1f, 0.01f), WoodDark)
                .AddBox(new Vector3(0, 0, -0.301f), new Vector3(0.62f, 0.1f, 0.01f), WoodDark).Build();
            Make("P_Crate", "Crate", crate, 8f, go => go.AddComponent<BoxCollider>().size = Vector3.one * 0.6f);

            // Big crate: one Smokie can only drag it; two can carry it
            var big = new MeshBuilder().AddBox(Vector3.zero, new Vector3(1.1f, 0.9f, 1.1f), WoodDark)
                .AddBox(new Vector3(0, 0, 0.551f), new Vector3(1.12f, 0.12f, 0.01f), Wood)
                .AddBox(new Vector3(0, 0, -0.551f), new Vector3(1.12f, 0.12f, 0.01f), Wood).Build();
            Make("P_BigCrate", "Big crate", big, 140f, go => go.AddComponent<BoxCollider>().size = new Vector3(1.1f, 0.9f, 1.1f), maxHolders: 3);

            // Basketball for the bay hoop
            var ball = new MeshBuilder().AddSphere(Vector3.zero, Vector3.one * 0.12f, 12, 8, Orange).Build();
            var bouncy = new PhysicsMaterial("PM_Ball") { bounciness = 0.75f, bounceCombine = PhysicsMaterialCombine.Maximum, dynamicFriction = 0.6f, staticFriction = 0.6f };
            Make("P_Ball", "Ball", ball, 0.6f, go =>
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.12f;
                sc.sharedMaterial = SaveAsset(bouncy, "PM_Ball.physicMaterial");
            });

            // Traffic cone
            var cone = new MeshBuilder()
                .AddBox(new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.04f, 0.36f), Black)
                .AddCylinder(new Vector3(0, 0.29f, 0), 0.15f, 0.03f, 0.5f, 10, Orange)
                .AddCylinder(new Vector3(0, 0.30f, 0), 0.105f, 0.088f, 0.07f, 10, Reflect, caps: false).Build();
            Make("P_Cone", "Cone", cone, 2f, go =>
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.34f, 0.55f, 0.34f);
                bc.center = new Vector3(0, 0.275f, 0);
            });

            // Rescue dummy: heavy, carry it with a friend (the training-yard toy)
            var dummy = new MeshBuilder()
                .AddCapsule(new Vector3(0, 0, 0), 0.22f, 1.1f, 10, Dummy)
                .AddSphere(new Vector3(0, 0.68f, 0), Vector3.one * 0.17f, 10, 6, Dummy).Build();
            Make("P_RescueDummy", "Rescue dummy", dummy, 70f, go =>
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.22f;
                cc.height = 1.5f;
                cc.center = new Vector3(0, 0.15f, 0);
            }, maxHolders: 2);

            // Rural mailbox on a post: free-standing so the truck can send it flying
            var mailbox = new MeshBuilder()
                .AddBox(new Vector3(0, 0.55f, 0), new Vector3(0.1f, 1.1f, 0.1f), WoodDark)
                .AddBox(new Vector3(0, 1.2f, 0), new Vector3(0.28f, 0.22f, 0.5f), new Color(0.25f, 0.35f, 0.6f))
                .AddBox(new Vector3(0.16f, 1.3f, -0.1f), new Vector3(0.02f, 0.18f, 0.06f), new Color(0.85f, 0.15f, 0.1f)) // flag
                .AddBox(new Vector3(0, 0.04f, 0), new Vector3(0.4f, 0.08f, 0.4f), WoodDark).Build();
            Make("P_Mailbox", "Mailbox", mailbox, 12f, go =>
            {
                var post = go.AddComponent<BoxCollider>();
                post.size = new Vector3(0.4f, 1.1f, 0.4f);
                post.center = new Vector3(0, 0.55f, 0);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(0.3f, 0.35f, 0.52f);
                box.center = new Vector3(0, 1.25f, 0);
            });

            AssetDatabase.SaveAssets();
        }

        static void Make(string prefabName, string displayName, Mesh mesh, float mass, System.Action<GameObject> addCollider, int maxHolders = 2)
        {
            var go = new GameObject(prefabName) { layer = Layers.Grabbable };
            go.AddComponent<MeshFilter>().sharedMesh = ProjectAssets.SaveMesh(mesh, $"Prop_{prefabName}");
            go.AddComponent<MeshRenderer>().sharedMaterial = ProjectAssets.SharedMaterial;
            addCollider(go);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.angularDamping = 0.3f;

            var no = go.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            go.AddComponent<NetworkRigidbody>();
            var g = go.AddComponent<Grabbable>();
            NetworkPrefabsBuilder.SetField(g, "_displayName", displayName);
            NetworkPrefabsBuilder.SetField(g, "_maxHolders", maxHolders);

            var path = $"{Folder}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            NetworkPrefabsBuilder.RegisterNetworkPrefab(prefab);
        }

        static T SaveAsset<T>(T asset, string fileName) where T : Object
        {
            var path = $"{ProjectAssets.DataFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
