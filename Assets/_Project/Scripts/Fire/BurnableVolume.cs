using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>
    /// Authoring box that says "the fire grid cells inside me are made of this material".
    /// No collider or renderer needed: <see cref="FireGrid"/> voxelizes these (math only, identical on every client)
    /// and draws/collides the result itself. Later volumes override earlier ones (use for doors/windows = empty).
    /// </summary>
    public sealed class BurnableVolume : MonoBehaviour
    {
        public FireMaterialDef Material;            // null = carve out (empty)
        public Vector3 Size = Vector3.one;

        public bool Contains(Vector3 worldPoint)
        {
            var p = transform.InverseTransformPoint(worldPoint);
            var h = Size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Material != null ? new Color(Material.Color.r, Material.Color.g, Material.Color.b, 0.6f) : new Color(1, 1, 1, 0.3f);
            Gizmos.DrawWireCube(Vector3.zero, Size);
        }
    }
}
