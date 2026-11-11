using System.Collections.Generic;
using System.IO;
using Firehouse.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Imports the Blender firehouse (ArtSource/Firehouse.blend → Assets/Art/Firehouse/Firehouse.fbx) and builds
    /// Scenes/Firehouse_Test.unity, an offline walk-around scene for checking the model.
    ///   Tools/Firehouse/Art/Setup Firehouse Import  — import settings, extract + set up URP Lit materials
    ///   Tools/Firehouse/Build/Firehouse Test Scene  — runs the setup above, then builds the scene
    /// The postprocessor below keeps the FBX / texture import settings correct on every re-export.
    /// </summary>
    public sealed class FirehouseArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Art/Firehouse";
        public const string FbxPath = Root + "/Firehouse.fbx";
        public const string TexturesFolder = Root + "/Textures";
        public const string MaterialsFolder = Root + "/Materials";
        public const string ScenePath = "Assets/Scenes/Firehouse_Test.unity";

        // Objects that move or are gameplay markers: never lighting-static.
        private static readonly string[] s_nonStaticPrefixes = { "Door_", "GarageDoor_", "PlayerSpawn_", "TruckSpawn_" };

        // Values read from the Blender materials (Principled roughness; base colors are linear, from the RGB nodes).
        private struct MatSpec { public float Roughness; public Color? LinearColor; public float Metallic; }
        private static readonly Dictionary<string, MatSpec> s_specs = new()
        {
            { "asphalt",      new MatSpec { Roughness = 0.92f } },
            { "brick",        new MatSpec { Roughness = 0.85f } },
            { "concrete",     new MatSpec { Roughness = 0.90f } },
            { "grass",        new MatSpec { Roughness = 0.95f } },
            { "metal_red",    new MatSpec { Roughness = 0.45f } },
            { "plaster",      new MatSpec { Roughness = 0.90f } },
            { "roof",         new MatSpec { Roughness = 0.95f } },
            { "trim",         new MatSpec { Roughness = 0.50f } },
            { "wood",         new MatSpec { Roughness = 0.70f } },
            { "paint_white",  new MatSpec { Roughness = 0.55f, LinearColor = new Color(0.88f, 0.88f, 0.85f) } },
            { "paint_yellow", new MatSpec { Roughness = 0.50f, LinearColor = new Color(0.92f, 0.62f, 0.05f) } },
            { "glass",        new MatSpec { Roughness = 0.06f, LinearColor = new Color(0.28f, 0.36f, 0.42f), Metallic = 0.35f } },
        };

        // ---------------------------------------------------------------- import settings (automatic)

        private void OnPreprocessModel()
        {
            if (assetPath != FbxPath) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = true;
            mi.importBlendShapes = false;
            mi.addCollider = true; // Generate Colliders
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import; // flat "Face" smoothing from Blender
            mi.importTangents = ModelImporterTangents.CalculateMikk; // n-gon meshes have no exported tangents
            mi.generateSecondaryUV = true; // lightmap UVs, since the scene is lighting-static
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab; // required for extraction
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder + "/")) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = Path.GetFileNameWithoutExtension(assetPath).EndsWith("_normal");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 4;
        }

        // ---------------------------------------------------------------- materials

        [MenuItem("Tools/Firehouse/Art/Setup Firehouse Import")]
        public static bool SetupImport()
        {
            if (!File.Exists(FbxPath))
            {
                Debug.LogError($"[Firehouse] {FbxPath} not found. Export it from ArtSource/Firehouse.blend first.");
                return false;
            }
            AssetDatabase.ImportAsset(TexturesFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            ProjectAssets.EnsureFolder(MaterialsFolder);

            // Extract embedded materials (skips ones already extracted; ExtractAsset also adds the remap).
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is not Material m) continue;
                string dest = $"{MaterialsFolder}/{m.name}.mat";
                if (File.Exists(dest)) continue;
                string err = AssetDatabase.ExtractAsset(m, dest);
                if (!string.IsNullOrEmpty(err)) Debug.LogError($"[Firehouse] Extract {m.name}: {err}");
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
            importer.SaveAndReimport();

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null) continue;
                SetupMaterial(mat, lit);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Firehouse] Firehouse import set up: {count} URP Lit materials in {MaterialsFolder}.");
            return true;
        }

        private static void SetupMaterial(Material mat, Shader lit)
        {
            string key = mat.name.StartsWith("M_") ? mat.name.Substring(2).ToLowerInvariant() : mat.name.ToLowerInvariant();
            if (!s_specs.TryGetValue(key, out var spec))
            {
                Debug.LogWarning($"[Firehouse] No spec for material {mat.name}; using defaults.");
                spec = new MatSpec { Roughness = 0.5f };
            }

            if (lit != null && mat.shader != lit) mat.shader = lit;

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{key}_albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{key}_normal.png");

            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            Color baseColor = spec.LinearColor.HasValue ? spec.LinearColor.Value.gamma : Color.white;
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);

            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");

            mat.SetFloat("_WorkflowMode", 1f); // metallic
            mat.SetFloat("_Metallic", spec.Metallic);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetFloat("_Smoothness", 1f - spec.Roughness);
            mat.SetFloat("_Surface", 0f); // opaque
            mat.SetFloat("_Cull", (float)CullMode.Back);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        // ---------------------------------------------------------------- test scene

        [MenuItem("Tools/Firehouse/Build/Firehouse Test Scene")]
        public static void BuildTestScene()
        {
            if (!SetupImport()) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null) { Debug.LogError("[Firehouse] Could not load the firehouse model."); return; }

            ProjectAssets.ConfirmSaveOpenScenes();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Camera>() != null) Object.DestroyImmediate(go);

            // Firehouse
            var house = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            house.name = "Firehouse";
            house.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            int staticCount = 0, dynamicCount = 0;
            const StaticEditorFlags lightingStatic = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                                     StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic |
                                                     StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
            {
                if (t == house.transform) continue;
                bool isDynamic = IsNonStatic(t);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, isDynamic ? 0 : lightingStatic);
                if (isDynamic) dynamicCount++; else staticCount++;
            }

            // Light: warm, soft shadows
            var light = Object.FindAnyObjectByType<Light>();
            if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
            light.name = "Sun (warm)";
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.89f, 0.74f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.27f, 0.24f);

            // Player (~1.8 m) at PlayerSpawn_01
            var spawn = FindDeep(house.transform, "PlayerSpawn_01");
            if (spawn == null) Debug.LogWarning("[Firehouse] PlayerSpawn_01 not found in the FBX; placing the player at the origin.");
            var player = new GameObject("FPS_Player");
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            var fps = player.AddComponent<SimpleFirstPersonController>();
            var so = new SerializedObject(fps);
            so.FindProperty("_camera").objectReferenceValue = camGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (spawn != null)
            {
                float yaw = spawn.eulerAngles.y;
                player.transform.SetPositionAndRotation(spawn.position + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            }

            ProjectAssets.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var bounds = new Bounds(house.transform.position, Vector3.zero);
            foreach (var r in house.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Debug.Log($"[Firehouse] Built {ScenePath}: {staticCount} static objects, {dynamicCount} doors/markers left dynamic. " +
                      $"Firehouse bounds {bounds.size.x:F1} x {bounds.size.y:F1} x {bounds.size.z:F1} m. Player at {player.transform.position}.");
        }

        private static bool IsNonStatic(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                foreach (var prefix in s_nonStaticPrefixes)
                    if (p.name.StartsWith(prefix)) return true;
            return false;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
