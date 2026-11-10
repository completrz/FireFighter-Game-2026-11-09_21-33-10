using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>Global fire simulation tuning. Asset: Data/Fire/FireSettings.asset.</summary>
    [CreateAssetMenu(menuName = "Firehouse/Fire Settings", fileName = "FireSettings")]
    public sealed class FireSettings : TunableData
    {
        [Header("Simulation (host)")]
        [Tunable(2, 30)] public float SimRate = 10f;        // steps per second
        [Tunable(1, 15)] public float SyncRate = 4f;        // delta messages per second
        [Tunable(0, 1)] public float Spread = 0.3f;         // share of heat output pushed into each neighbor
        [Tunable(0, 3)] public float UpwardBias = 1.6f;     // fire climbs
        [Tunable(0, 1)] public float DownwardBias = 0.35f;
        [Tunable(0, 0.5f)] public float AmbientCooling = 0.06f; // fraction of heat lost per second
        [Tunable(100, 3000)] public float MaxHeat = 1000f;
        [Tunable(0.1f, 0.9f)] public float ExtinguishFraction = 0.55f; // goes out below IgnitionTemp × this
        [Tunable(0, 1)] public float Randomness = 0.45f;

        [Header("Wetness")]
        [Tunable(1, 20)] public float MaxWet = 6f;
        [Tunable(0, 1)] public float DryRate = 0.08f;       // wetness lost per second
        [Tunable(10, 500)] public float HeatPerWet = 90f;   // heat needed to boil off one unit of wetness

        [Header("Wind")]
        public Vector3 WindDirection = new(1f, 0f, 0.3f);
        [Tunable(0, 2)] public float WindStrength = 0.6f;

        [Header("Visuals (local)")]
        [Tunable(0, 40)] public float FlameParticlesPerCell = 9f;
        [Tunable(0, 10)] public float SmokeParticlesPerCell = 0.6f;
    }
}
