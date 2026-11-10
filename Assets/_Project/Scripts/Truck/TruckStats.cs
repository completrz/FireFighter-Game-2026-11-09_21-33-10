using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Truck
{
    /// <summary>Starter mini-pumper handling. Asset: Data/TruckStats_Starter.asset. Later trucks get their own assets.</summary>
    [CreateAssetMenu(menuName = "Firehouse/Truck Stats", fileName = "TruckStats")]
    public sealed class TruckStats : TunableData
    {
        [Header("Body")]
        [Tunable(500, 8000)] public float Mass = 2600f;
        [Tunable(-0.5f, 1.5f)] public float CenterOfMassHeight = 0.45f;

        [Header("Suspension (per wheel)")]
        [Tunable(0.1f, 1f)] public float RestLength = 0.45f;
        [Tunable(5000, 120000)] public float Spring = 42000f;
        [Tunable(500, 15000)] public float Damper = 4200f;
        [Tunable(0.3f, 0.8f)] public float WheelRadius = 0.48f;

        [Header("Engine and brakes")]
        [Tunable(2000, 40000)] public float EngineForce = 15000f;
        [Tunable(5, 45)] public float MaxSpeed = 21f;          // m/s (~75 km/h)
        [Tunable(2, 15)] public float MaxReverseSpeed = 6f;
        [Tunable(2000, 40000)] public float BrakeForce = 22000f;
        [Tunable(0, 2)] public float RollingResistance = 0.35f;

        [Header("Steering and grip")]
        [Tunable(5, 45)] public float MaxSteerAngle = 32f;
        [Tunable(0.1f, 1f)] public float HighSpeedSteerFactor = 0.4f;
        [Tunable(1, 20)] public float SteerSpeed = 6f;
        [Tunable(0.1f, 3f)] public float TireFriction = 1.25f;    // max sideways force = load × this
        [Tunable(0, 1)] public float HandbrakeRearGrip = 0.3f;
        [Tunable(0, 3)] public float AirAngularDamping = 1.2f;

        [Header("Comedy")]
        [Tunable(5, 80)] public float BackStepEjectAccel = 22f;   // m/s² of jolt (beyond gravity) that can throw riders off
        [Tunable(1, 20)] public float CrashSpeedChange = 6f;      // sudden speed loss (m/s) in a collision that throws everyone off the step
        [Tunable(1, 15)] public float RunOverMinSpeed = 2.5f;     // truck speed that starts pushing players
        [Tunable(2, 20)] public float RunOverKnockdownSpeed = 5f; // ...and that knocks them over
    }
}
