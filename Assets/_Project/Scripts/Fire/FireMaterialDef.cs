using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>A burnable material. Units are arbitrary "heat" and "fuel-seconds".</summary>
    [CreateAssetMenu(menuName = "Firehouse/Fire Material", fileName = "FireMat_")]
    public sealed class FireMaterialDef : TunableData
    {
        public FireClass Class = FireClass.Ordinary;
        public Color Color = new(0.6f, 0.45f, 0.3f);
        [Tunable(20, 1000)] public float IgnitionTemp = 300f;
        [Tunable(1, 300)] public float Fuel = 60f;          // seconds of burning at BurnRate 1
        [Tunable(0.1f, 5)] public float BurnRate = 1f;
        [Tunable(10, 600)] public float HeatOutput = 120f;  // heat per second while burning
        [Tooltip("Burnt-out cells disappear (walls burn through). Off = they stay as charred ground.")]
        public bool RemoveWhenBurntOut = true;
    }
}
