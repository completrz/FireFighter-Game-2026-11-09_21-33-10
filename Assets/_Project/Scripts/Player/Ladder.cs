using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// A climbable volume (ladder or fire pole). Needs a trigger BoxCollider on the Trigger layer.
    /// transform.up = climb direction; transform.forward points from the climber toward the ladder.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Ladder : MonoBehaviour
    {
        [Tooltip("Fire pole: grab from any side, slide down fast, climb up slowly.")]
        public bool IsPole;

        private BoxCollider _box;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        public Vector3 Up => transform.up;

        /// <summary>World-space top of the climbable section.</summary>
        public float TopY => _box.bounds.max.y;
        public float BottomY => _box.bounds.min.y;

        /// <summary>Closest point on the ladder's center line (in its local Y axis) to <paramref name="p"/>.</summary>
        public Vector3 ClosestPointOnAxis(Vector3 p)
        {
            var center = _box.bounds.center;
            float t = Vector3.Dot(p - center, Up);
            float half = _box.size.y * transform.lossyScale.y * 0.5f;
            return center + Up * Mathf.Clamp(t, -half, half);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = IsPole ? new Color(1f, 0.85f, 0.2f, 0.5f) : new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.DrawLine(box.center, box.center + Vector3.forward * 0.6f);
        }
    }
}
