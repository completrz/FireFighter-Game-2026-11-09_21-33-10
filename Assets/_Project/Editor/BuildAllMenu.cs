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
            TruckBuilder.Build();
            NetworkPrefabsBuilder.BuildNetworkRoot();
            TestRoadBuilder.Build();
            FirehouseBlockoutBuilder.Build(); // last, so the Firehouse scene is left open
            AssetDatabase.SaveAssets();
        }
    }
}
