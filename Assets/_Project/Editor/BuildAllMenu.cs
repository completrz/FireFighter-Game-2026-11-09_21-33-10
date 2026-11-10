using UnityEditor;

namespace Firehouse.EditorTools
{
    public static class BuildAllMenu
    {
        /// <summary>Regenerates every generated asset and scene, in dependency order.</summary>
        [MenuItem("Tools/Firehouse/Build/Rebuild Everything", priority = 0)]
        public static void RebuildEverything()
        {
            LayerSetup.Apply();
            ArtTestSceneBuilder.CreateSharedMaterial();
            SmokieBuilder.Build();
            PropsBuilder.BuildAll();
            NetworkPrefabsBuilder.BuildNetworkRoot();
            FirehouseBlockoutBuilder.Build();
            AssetDatabase.SaveAssets();
        }
    }
}
