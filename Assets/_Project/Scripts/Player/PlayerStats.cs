using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>All tunable numbers for the Smokies character. Asset: Data/PlayerStats.asset.</summary>
    [CreateAssetMenu(menuName = "Firehouse/Player Stats", fileName = "PlayerStats")]
    public sealed class PlayerStats : TunableData
    {
        [Header("Movement")]
        [Tunable(1, 10)] public float WalkSpeed = 3.8f;
        [Tunable(1, 14)] public float SprintSpeed = 6.2f;
        [Tunable(5, 120)] public float GroundAccel = 45f;
        [Tunable(0, 40)] public float AirAccel = 7f;
        [Tunable(1, 12)] public float JumpSpeed = 5.2f;
        [Tunable(0.5f, 3)] public float GravityMultiplier = 1.4f;
        [Tunable(0, 0.3f)] public float CoyoteTime = 0.12f;

        [Header("Look")]
        [Tunable(0.01f, 0.5f)] public float MouseSensitivity = 0.09f;
        [Tunable(30, 400)] public float GamepadLookSpeed = 170f;
        [Tunable(40, 110)] public float FieldOfView = 78f;

        [Header("Climbing")]
        [Tunable(0.5f, 6)] public float ClimbSpeed = 2.4f;
        [Tunable(1, 15)] public float PoleSlideSpeed = 7f;

        [Header("Knockdown (friendly fire, falls)")]
        [Tunable(1, 30)] public float KnockdownImpulse = 6f;      // velocity change (m/s) that knocks you over
        [Tunable(0.3f, 5)] public float KnockdownMinTime = 1.4f;
        [Tunable(0.1f, 2)] public float GetUpTime = 0.45f;
        [Tunable(5, 40)] public float FallKnockdownSpeed = 11f;   // landing faster than this knocks you down
        [Tunable(0, 30)] public float KnockdownFriction = 9f;     // m/s² of sliding slowdown while knocked down on the ground

        [Header("Grab and carry")]
        [Tunable(1, 5)] public float GrabRange = 2.6f;
        [Tunable(0.6f, 3)] public float HoldDistance = 1.35f;
        [Tunable(100, 3000)] public float CarryStrength = 900f;   // max force (N) per holder
        [Tunable(5, 80)] public float CarryStiffness = 30f;
        [Tunable(1, 20)] public float CarryDamping = 8f;
        [Tunable(1, 20)] public float ThrowSpeed = 9f;
        [Tunable(0.5f, 6)] public float DropDistance = 2.4f;      // object yanked this far from hands = dropped
    }
}
