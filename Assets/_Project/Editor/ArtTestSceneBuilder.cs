using Firehouse.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>Builds a small scene that shows the shared flat vertex-color material on a few test shapes.</summary>
    public static class ArtTestSceneBuilder
    {
        [MenuItem("Tools/Firehouse/Setup/Create Shared Material")]
        public static void CreateSharedMaterial()
        {
            var mat = ProjectAssets.SharedMaterial;
            if (mat != null) Selection.activeObject = mat;
        }

        [MenuItem("Tools/Firehouse/Build/Art Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var mat = ProjectAssets.SharedMaterial;

            var ground = new MeshBuilder().AddBox(Vector3.zero, new Vector3(20, 0.2f, 20), new Color(0.45f, 0.62f, 0.3f));
            Spawn("Ground", ground.Build("ArtTest_Ground"), new Vector3(0, -0.1f, 0), mat);

            var shapes = new MeshBuilder();
            shapes.AddBox(new Vector3(-3, 0.5f, 0), Vector3.one, new Color(0.85f, 0.2f, 0.15f));
            shapes.AddCylinder(new Vector3(-1, 0.6f, 0), 0.5f, 0.5f, 1.2f, 8, new Color(0.95f, 0.75f, 0.1f));
            shapes.AddSphere(new Vector3(1, 0.6f, 0), Vector3.one * 0.6f, 10, 6, new Color(0.2f, 0.45f, 0.85f));
            shapes.AddCapsule(new Vector3(3, 0.8f, 0), 0.4f, 1.6f, 10, new Color(0.9f, 0.9f, 0.85f));
            // Emissive test: alpha 0 = full glow.
            shapes.AddBox(new Vector3(0, 0.25f, 2), new Vector3(2, 0.5f, 0.2f), new Color(1f, 0.85f, 0.2f, 0f));
            Spawn("Shapes", shapes.Build("ArtTest_Shapes"), Vector3.zero, mat);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(new Vector3(0, 3.5f, -7), Quaternion.Euler(22, 0, 0));
                cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            }

            scene.name = "ArtTest";
            Debug.Log("[Firehouse] Art test scene built (unsaved). Meshes are scene-local.");
        }

        private static GameObject Spawn(string name, Mesh mesh, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
