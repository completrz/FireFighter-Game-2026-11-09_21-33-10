using System.Collections.Generic;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Long floppy cosmetic arms: two jointed rigidbody chains hanging from the shoulders.
    /// Simulated locally on every client (never networked). Hands are pulled toward targets:
    /// the held object, forward while reaching, or the ladder while climbing. Otherwise they just flop.
    /// </summary>
    public sealed class SmokieArms : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] _left;   // shoulder → hand
        [SerializeField] private Rigidbody[] _right;
        [SerializeField] private float _handPull = 60f;
        [SerializeField] private float _handDamping = 6f;
        [SerializeField] private float _maxPull = 25f;

        private SmokieController _smokie;
        private PlayerGrabber _grabber;
        private readonly List<GameObject> _detached = new();

        private void Awake()
        {
            _smokie = GetComponent<SmokieController>();
            _grabber = GetComponent<PlayerGrabber>();
        }

        private void Start()
        {
            // Detach segments so they simulate in world space (joints keep them attached to the body).
            Detach(_left);
            Detach(_right);
        }

        private void Detach(Rigidbody[] chain)
        {
            foreach (var rb in chain)
            {
                if (rb == null) continue;
                rb.transform.SetParent(null, true);
                _detached.Add(rb.gameObject);
            }
        }

        private void OnDestroy()
        {
            foreach (var go in _detached)
                if (go != null) Destroy(go);
        }

        private void FixedUpdate()
        {
            if (_smokie == null || _left.Length == 0 || _right.Length == 0) return;
            var right = transform.right;

            if (_smokie.State == SmokieState.KnockedDown) return; // full flop

            var hose = _smokie.IsSpawned ? Water.TruckWater.HeldBy(_smokie.OwnerClientId) : null;
            var held = _grabber != null ? _grabber.Held : null;
            if (hose != null && hose.TryGetNozzle(out var nozzle, out var aim))
            {
                // Both hands on the nozzle, one a bit further back along the line.
                Pull(_left[^1], nozzle - aim * 0.25f - right * 0.08f);
                Pull(_right[^1], nozzle + right * 0.06f);
            }
            else if (held != null)
            {
                var c = held.Body.worldCenterOfMass;
                float spread = Mathf.Clamp(held.GetComponentInChildren<Collider>().bounds.extents.magnitude * 0.6f, 0.15f, 0.5f);
                Pull(_left[^1], c - right * spread);
                Pull(_right[^1], c + right * spread);
            }
            else if (_smokie.State == SmokieState.Climbing && _smokie.CurrentLadder != null)
            {
                // Alternate hands up the ladder as we climb.
                float phase = Mathf.Sin(transform.position.y * 4f) * 0.25f;
                var front = transform.position + Vector3.up * 1.2f + transform.forward * 0.45f;
                Pull(_left[^1], front - right * 0.25f + Vector3.up * phase);
                Pull(_right[^1], front + right * 0.25f - Vector3.up * phase);
            }
            else if (_grabber != null && _grabber.IsReaching)
            {
                var front = _smokie.LookOrigin + _smokie.LookDirection * 0.9f;
                Pull(_left[^1], front - right * 0.2f);
                Pull(_right[^1], front + right * 0.2f);
            }
        }

        private void Pull(Rigidbody hand, Vector3 target)
        {
            var f = (target - hand.position) * _handPull - hand.linearVelocity * _handDamping;
            hand.AddForce(Vector3.ClampMagnitude(f, _maxPull), ForceMode.Acceleration);
        }
    }
}
