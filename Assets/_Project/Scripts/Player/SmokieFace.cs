using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Swaps the eye-lens mesh to show a simple expression from the synced controller state:
    /// normal, wide (falling/launched), X's (knocked down). More expressions slot in as extra meshes.
    /// </summary>
    public sealed class SmokieFace : MonoBehaviour
    {
        [SerializeField] private MeshFilter _eyes;
        [SerializeField] private Mesh _normal;
        [SerializeField] private Mesh _wide;
        [SerializeField] private Mesh _knockedOut;

        private SmokieController _smokie;

        private void Awake() => _smokie = GetComponent<SmokieController>();

        private void LateUpdate()
        {
            if (_eyes == null || _smokie == null) return;
            Mesh m = _normal;
            if (_smokie.State == SmokieState.KnockedDown) m = _knockedOut;
            else if (_smokie.Velocity.y < -7f || _smokie.Velocity.magnitude > 12f) m = _wide;
            if (m != null && _eyes.sharedMesh != m) _eyes.sharedMesh = m;
        }
    }
}
