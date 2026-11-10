using Firehouse.Core;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>Names the physics layers listed in <see cref="Layers"/> and applies the collision matrix.</summary>
    public static class LayerSetup
    {
        [MenuItem("Tools/Firehouse/Setup/Physics Layers")]
        public static void Apply()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < Layers.Names.Length; i++)
            {
                if (string.IsNullOrEmpty(Layers.Names[i])) continue;
                var prop = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(prop.stringValue) && prop.stringValue != Layers.Names[i])
                    Debug.LogWarning($"[Firehouse] Layer {i} was '{prop.stringValue}', renaming to '{Layers.Names[i]}'.");
                prop.stringValue = Layers.Names[i];
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Layers.ApplyCollisionMatrix(); // in the editor this writes the project's collision matrix
            AssetDatabase.SaveAssets();
            Debug.Log("[Firehouse] Physics layers set up.");
        }
    }
}
