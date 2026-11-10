using UnityEngine;

namespace Firehouse.Core
{
    /// <summary>
    /// Physics layer indices. Created by Tools/Firehouse/Setup/Physics Layers (keep both in sync).
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Player = 6;      // player body capsules
        public const int PlayerArms = 7;  // cosmetic floppy arm segments (only collide with world)
        public const int Grabbable = 8;   // carryable physics props
        public const int Vehicle = 9;     // trucks (step 4)
        public const int Water = 10;      // hose spray hit volumes (step 5); named "WaterSpray" (Unity's built-in layer 4 is "Water")
        public const int Trigger = 11;    // ladders, zones

        public static readonly string[] Names =
        {
            null, null, null, null, null, null,
            "Player", "PlayerArms", "Grabbable", "Vehicle", "WaterSpray", "Trigger"
        };

        /// <summary>What the player's ground check and grab ray can hit.</summary>
        public static int WorldMask => (1 << Default) | (1 << Grabbable) | (1 << Vehicle) | (1 << Player);

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (var l in layers) m |= 1 << l;
            return m;
        }

        /// <summary>Applies the collision matrix. Called by the editor setup and again at runtime as a safety net.</summary>
        public static void ApplyCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(PlayerArms, i, i != Default && i != Vehicle);
            Physics.IgnoreLayerCollision(Water, Water, true);
            // Note: don't ignore collisions for the Trigger layer, because that would also stop trigger messages.
            // Queries skip triggers with QueryTriggerInteraction.Ignore instead.
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeApply() => ApplyCollisionMatrix();
    }
}
