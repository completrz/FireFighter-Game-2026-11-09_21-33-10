using Firehouse.Fire;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Creates the fire data assets in Data/Fire (only if missing — after that they're the designer's to tune):
    /// materials (wood, dry planks, grass, leaves, hay), agents (water, foam, CO2, dry chemical, sand, wet chemical)
    /// and FireSettings.
    /// </summary>
    public static class FireAssetsBuilder
    {
        public const string Folder = ProjectAssets.DataFolder + "/Fire";

        [MenuItem("Tools/Firehouse/Setup/Fire Data Assets")]
        public static void BuildAll()
        {
            ProjectAssets.EnsureFolder(Folder);
            Settings();
            Material("Wood", new Color(0.45f, 0.30f, 0.18f), 300, 90, 1f, 140, remove: true);
            Material("Planks", new Color(0.66f, 0.48f, 0.30f), 260, 60, 1f, 150, remove: true);
            Material("Grass", new Color(0.50f, 0.66f, 0.26f), 160, 6, 1f, 300, remove: false);
            Material("Leaves", new Color(0.30f, 0.52f, 0.22f), 220, 12, 1f, 230, remove: true);
            Material("Hay", new Color(0.88f, 0.76f, 0.36f), 140, 20, 1f, 320, remove: true);

            Agent("Water", 160, 1f, new[] { 1f, 0.2f, 0.4f, 0f, 0f, 0f });
            Agent("Foam", 130, 0.6f, new[] { 0.8f, 1.2f, 0.2f, 0.6f, 0.1f, 0f });
            Agent("CO2", 120, 0f, new[] { 0.4f, 0.8f, 1.2f, 0.5f, 0.1f, 0f });
            Agent("DryChemical", 140, 0f, new[] { 0.7f, 1.1f, 1.1f, 0.6f, 0.6f, 0.3f });
            Agent("Sand", 90, 0.2f, new[] { 0.5f, 0.6f, 0.3f, 0.8f, 1.2f, 0.8f });
            Agent("WetChemical", 140, 0.8f, new[] { 0.8f, 0.4f, 0.1f, 1.3f, 0.2f, 0f });
            AssetDatabase.SaveAssets();
        }

        public static FireSettings Settings()
        {
            string path = $"{Folder}/FireSettings.asset";
            var s = AssetDatabase.LoadAssetAtPath<FireSettings>(path);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<FireSettings>();
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        public static FireMaterialDef Material(string name, Color color, float ignition, float fuel, float burnRate, float heat, bool remove)
        {
            string path = $"{Folder}/FireMat_{name}.asset";
            var m = AssetDatabase.LoadAssetAtPath<FireMaterialDef>(path);
            if (m != null) return m;
            m = ScriptableObject.CreateInstance<FireMaterialDef>();
            m.Color = color;
            m.IgnitionTemp = ignition;
            m.Fuel = fuel;
            m.BurnRate = burnRate;
            m.HeatOutput = heat;
            m.RemoveWhenBurntOut = remove;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static FireMaterialDef Material(string name) =>
            AssetDatabase.LoadAssetAtPath<FireMaterialDef>($"{Folder}/FireMat_{name}.asset");

        public static AgentDef Agent(string name, float cooling = 0, float wetting = 0, float[] effectiveness = null)
        {
            string path = $"{Folder}/Agent_{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<AgentDef>(path);
            if (a != null || effectiveness == null) return a;
            a = ScriptableObject.CreateInstance<AgentDef>();
            a.Cooling = cooling;
            a.Wetting = wetting;
            a.Effectiveness = effectiveness;
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
