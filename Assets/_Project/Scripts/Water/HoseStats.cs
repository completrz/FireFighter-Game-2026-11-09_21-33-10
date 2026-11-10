using Firehouse.Core;
using Firehouse.Fire;
using UnityEngine;

namespace Firehouse.Water
{
    /// <summary>Hose line, pump and tank tuning. Asset: Data/HoseStats_Starter.asset.</summary>
    [CreateAssetMenu(menuName = "Firehouse/Hose Stats", fileName = "HoseStats")]
    public sealed class HoseStats : TunableData
    {
        public AgentDef Agent;                                // water (foam later)

        [Header("Line")]
        [Tunable(5, 60)] public float MaxLength = 22f;        // meters of hose on the reel
        [Tunable(0, 10)] public float YankDropExtra = 4f;     // dragged this far past max → knocked over, hose dropped

        [Header("Pump and tank")]
        [Tunable(20, 2000)] public float TankCapacity = 300f; // water units
        [Tunable(0.5f, 30)] public float FlowAtMax = 8f;      // units per second at 100 % pressure
        [Tunable(1, 20)] public float RangeAtMin = 6f;        // meters at 25 %
        [Tunable(5, 40)] public float RangeAtMax = 18f;       // meters at 100 %
        [Tunable(0, 15)] public float Spread = 4f;            // degrees of nozzle scatter

        [Header("Effect on fire")]
        [Tunable(0.5f, 4)] public float HitRadius = 1.4f;
        [Tunable(0, 5)] public float AgentPerUnit = 1f;       // agent units delivered per water unit

        [Header("Friendly fire and kickback")]
        [Tunable(0, 60)] public float PlayerPushAtMax = 22f;  // m/s² on a sprayed teammate at 100 %
        [Tunable(0, 3)] public float SprayKnockdownTime = 0.6f; // seconds sprayed at ≥50 % before they go over
        [Tunable(0, 40)] public float KickbackAtMax = 9f;     // m/s² pushing the holder backwards at 100 %
        [Tunable(0, 1)] public float KnockoverPressure = 0.9f; // at/above this pressure the hose can throw you
        [Tunable(0.2f, 6)] public float KnockoverAfter = 1.8f; // seconds of continuous spraying before that can happen

        [Header("Visuals")]
        [Tunable(10, 400)] public float ParticlesPerSecond = 140f;

        public static readonly float[] PressureSteps = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>Launch speed for the given pressure so a 45° shot lands roughly at the target range.</summary>
        public float SpeedFor(float pressure) =>
            pressure <= 0f ? 0f : Mathf.Sqrt(Mathf.Lerp(RangeAtMin, RangeAtMax, (pressure - 0.25f) / 0.75f) * -Physics.gravity.y);
    }
}
