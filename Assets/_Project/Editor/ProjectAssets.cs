using System.IO;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>Paths and helpers for project-wide generated assets.</summary>
    public static class ProjectAssets
    {
        public const string Root = "Assets/_Project";
        public const string SharedMaterialPath = Root + "/Art/Materials/M_VertexColor.mat";
        public const string ShaderName = "Firehouse/FlatVertexColor";
        public const string MeshesFolder = Root + "/Art/Meshes";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string ScenesFolder = Root + "/Scenes";
        public const string DataFolder = Root + "/Data";

        /// <summary>Loads (or creates) the one shared vertex-color material.</summary>
        public static Material SharedMaterial
        {
            get
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
                if (mat != null) return mat;
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"[Firehouse] Shader '{ShaderName}' not found.");
                    return null;
                }
                EnsureFolder(Path.GetDirectoryName(SharedMaterialPath));
                mat = new Material(shader) { name = "M_VertexColor", enableInstancing = true };
                AssetDatabase.CreateAsset(mat, SharedMaterialPath);
                AssetDatabase.SaveAssets();
                return mat;
            }
        }

        /// <summary>
        /// Creates the folder on disk (never through the MCP asset tool) and imports it.
        /// </summary>
        public static void EnsureFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            Directory.CreateDirectory(assetPath);
            AssetDatabase.ImportAsset(assetPath);
        }

        /// <summary>Saves a generated mesh as an asset, replacing the previous one in place so references stay valid.</summary>
        public static Mesh SaveMesh(Mesh mesh, string fileName)
        {
            EnsureFolder(MeshesFolder);
            string path = $"{MeshesFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = fileName;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            mesh.name = fileName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
