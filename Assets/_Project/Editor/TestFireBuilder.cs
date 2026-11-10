using Firehouse.Fire;
using Firehouse.Networking;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Builds Scenes/TestFire.unity: a 30×30 m grass field (with a dirt path as a natural firebreak),
    /// a wooden shed full of hay at the far edge, three trees, the starter truck and spawn points.
    /// All burnables are FireGrids voxelized from BurnableVolumes.
    /// </summary>
    public static class TestFireBuilder
    {
        public const string ScenePath = ProjectAssets.ScenesFolder + "/TestFire.unity";

        static readonly Color Ground = new(0.40f, 0.56f, 0.27f);
        static readonly Color Dirt = new(0.55f, 0.42f, 0.28f);

        [MenuItem("Tools/Firehouse/Build/Test Fire Scene")]
        public static void Build()
        {
            if (!ProjectAssets.ConfirmSaveOpenScenes()) return;
            FireAssetsBuilder.BuildAll();
            var truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckBuilder.PrefabPath) ?? TruckBuilder.Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("TestFire").transform;
            var b = new Blockout(root, ProjectAssets.SharedMaterial);
            b.Box("Ground", "Ground", new Vector3(-100, -0.5f, -100), new Vector3(100, 0f, 100), Ground);
            b.Box("Ground", "TruckPad", new Vector3(-6, 0f, -16), new Vector3(6, 0.02f, -4), Dirt);
            b.Box("Ground", "FieldPath", new Vector3(-16, 0f, 13.5f), new Vector3(16, 0.015f, 15.5f), Dirt, collider: false);
            b.Finish("TestFire");

            var grass = FireAssetsBuilder.Material("Grass");
            var wood = FireAssetsBuilder.Material("Wood");
            var planks = FireAssetsBuilder.Material("Planks");
            var leaves = FireAssetsBuilder.Material("Leaves");
            var hay = FireAssetsBuilder.Material("Hay");

            // Grass field: 30 × 30 cells of 1 m, a dirt path across the middle (firebreak).
            var field = MakeGrid("GrassField", new Vector3(-15, 0, 0), new Vector3Int(30, 1, 30), new Vector3(1f, 0.35f, 1f), solid: false, seed: 11);
            Volume(field, "Grass", new Vector3(0, 0.175f, 15f), new Vector3(30, 0.4f, 30), grass);
            Volume(field, "Path (carved)", new Vector3(0, 0.175f, 14.5f), new Vector3(30, 0.4f, 2f), null);
            Volume(field, "Under shed (carved)", new Vector3(7.25f, 0.175f, 30.5f), new Vector3(5.5f, 0.4f, 3f), null);

            // Shed: 4.5 × 3.5 × 4 m of planks, door on the east side, stepped roof, hay bales inside.
            var shedOrigin = new Vector3(5f, 0f, 28.5f);
            var shed = MakeGrid("Shed", shedOrigin, new Vector3Int(9, 7, 8), new Vector3(0.5f, 0.5f, 0.5f), solid: true, seed: 22);
            var c = shedOrigin + new Vector3(2.25f, 0, 2f);
            Volume(shed, "WallSouth", c + new Vector3(0, 1.25f, -1.75f), new Vector3(4.5f, 2.5f, 0.5f), planks);
            Volume(shed, "WallNorth", c + new Vector3(0, 1.25f, 1.75f), new Vector3(4.5f, 2.5f, 0.5f), planks);
            Volume(shed, "WallWest", c + new Vector3(-2f, 1.25f, 0), new Vector3(0.5f, 2.5f, 4f), planks);
            Volume(shed, "WallEast", c + new Vector3(2f, 1.25f, 0), new Vector3(0.5f, 2.5f, 4f), planks);
            Volume(shed, "Door (carved)", c + new Vector3(2f, 1f, 0), new Vector3(0.6f, 2f, 1.4f), null);
            Volume(shed, "Window (carved)", c + new Vector3(0, 1.5f, -1.75f), new Vector3(1.4f, 0.6f, 0.6f), null);
            Volume(shed, "RoofLow", c + new Vector3(0, 2.75f, 0), new Vector3(4.5f, 0.5f, 4f), wood);
            Volume(shed, "RoofHigh", c + new Vector3(0, 3.25f, 0), new Vector3(4.5f, 0.5f, 2f), wood);
            Volume(shed, "Hay A", c + new Vector3(-1f, 0.5f, 0.75f), new Vector3(1.4f, 1f, 1f), hay);
            Volume(shed, "Hay B", c + new Vector3(-1f, 0.25f, -0.5f), new Vector3(1f, 0.5f, 1f), hay);

            // Trees around the field.
            Tree("TreeA", new Vector3(-11f, 0, 6f), wood, leaves, 31);
            Tree("TreeB", new Vector3(-6f, 0, 23f), wood, leaves, 32);
            Tree("TreeC", new Vector3(10f, 0, 9f), wood, leaves, 33);

            // Truck and crew at the south end.
            var truck = (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab);
            truck.transform.SetPositionAndRotation(new Vector3(0, 0.3f, -10f), Quaternion.identity);
            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(root, false);
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject($"Spawn {i + 1}");
                go.transform.SetParent(spawns, false);
                go.transform.SetPositionAndRotation(new Vector3(-5.25f + (i % 4) * 3.5f, 0.05f, -17f - (i / 4) * 1.5f), Quaternion.identity);
                go.AddComponent<SpawnPoint>();
            }
            new GameObject("PlayerSpawner").AddComponent<PlayerSpawner>();

            BuildLighting();
            var cam = new GameObject("MenuCamera") { tag = "MainCamera" };
            cam.AddComponent<Camera>().fieldOfView = 55;
            cam.AddComponent<AudioListener>();
            cam.transform.position = new Vector3(-22f, 11f, -14f);
            cam.transform.LookAt(new Vector3(0, 0, 16f));

            ProjectAssets.EnsureFolder(ProjectAssets.ScenesFolder);
            FirehouseBlockoutBuilder.AddSceneToBuild(ScenePath);
            ProjectAssets.SaveSceneWithNetworkIds(scene, ScenePath);
            Debug.Log($"[Firehouse] Built {ScenePath}");
        }

        static GameObject MakeGrid(string name, Vector3 origin, Vector3Int size, Vector3 cellSize, bool solid, int seed)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = origin;
            go.AddComponent<NetworkObject>();
            var grid = go.AddComponent<FireGrid>();
            NetworkPrefabsBuilder.SetField(grid, "_settings", FireAssetsBuilder.Settings());
            NetworkPrefabsBuilder.SetField(grid, "_debugAgent", FireAssetsBuilder.Agent("Water"));
            NetworkPrefabsBuilder.SetField(grid, "_solid", solid);
            NetworkPrefabsBuilder.SetField(grid, "_seed", seed);
            var so = new SerializedObject(grid);
            so.FindProperty("_size").vector3IntValue = size;
            so.FindProperty("_cellSize").vector3Value = cellSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            var view = go.AddComponent<FireGridView>();
            NetworkPrefabsBuilder.SetField(view, "_material", ProjectAssets.SharedMaterial);
            return go;
        }

        static void Volume(GameObject grid, string name, Vector3 worldCenter, Vector3 size, FireMaterialDef mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid.transform, false);
            go.transform.position = worldCenter;
            var v = go.AddComponent<BurnableVolume>();
            v.Material = mat;
            v.Size = size;
        }

        static void Tree(string name, Vector3 basePos, FireMaterialDef wood, FireMaterialDef leaves, int seed)
        {
            // 5 × 11 × 5 cells of 0.5 m; trunk in the middle, chunky canopy on top.
            var origin = basePos - new Vector3(1.25f, 0, 1.25f);
            var grid = MakeGrid(name, origin, new Vector3Int(5, 11, 5), new Vector3(0.5f, 0.5f, 0.5f), solid: true, seed: seed);
            Volume(grid, "Trunk", basePos + new Vector3(0, 1.75f, 0), new Vector3(0.5f, 3.5f, 0.5f), wood);
            Volume(grid, "Canopy", basePos + new Vector3(0, 3.75f, 0), new Vector3(2.5f, 2f, 2.5f), leaves);
            Volume(grid, "CanopyTop", basePos + new Vector3(0, 5f, 0), new Vector3(1.5f, 1f, 1.5f), leaves);
        }

        static void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.85f, 0.65f); // late afternoon
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(28, -40, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.66f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.25f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.76f, 0.82f);
            RenderSettings.fogStartDistance = 60;
            RenderSettings.fogEndDistance = 170;
        }
    }
}
