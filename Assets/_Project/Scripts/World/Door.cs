using Firehouse.Player;
using UnityEngine;

namespace Firehouse.World
{
    /// <summary>
    /// A door that opens and closes with Interact (E). The transform's position is the pivot (Blender origin):
    /// <see cref="DoorKind.Hinged"/> swings 90° about the vertical axis, away from whoever opens it;
    /// <see cref="DoorKind.TiltUp"/> (garage doors, pivot on the top edge) tilts up to horizontal toward <see cref="_openToward"/>.
    /// Local-only for now: in a networked session each client toggles its own copy (sync comes with the lobby pass).
    /// </summary>
    public sealed class Door : Interactable
    {
        public enum DoorKind { Hinged, TiltUp }

        [SerializeField] private DoorKind _kind = DoorKind.Hinged;
        [SerializeField] private float _openAngle = 95f;
        [SerializeField] private float _openSeconds = 0.6f;
        [Tooltip("TiltUp: world direction the bottom edge swings toward (into the building). Zero = away from the user.")]
        [SerializeField] private Vector3 _openToward;

        private Quaternion _closedRotation;
        private Quaternion _openRotation;
        private Vector3 _localCenter;
        private float _t;      // 0 closed .. 1 open
        private bool _open;

        public bool IsOpen => _open;
        public override Vector3 InteractPoint => transform.TransformPoint(_localCenter);

        private void Awake()
        {
            _closedRotation = transform.rotation;
            var r = GetComponent<Renderer>();
            _localCenter = r != null ? transform.InverseTransformPoint(r.bounds.center) : Vector3.zero;
        }

        public override string GetPrompt(SmokieController who) => _open ? "Close door" : "Open door";
        public override bool CanInteract(SmokieController who) => true;
        public override void Interact(SmokieController who) => Toggle(who.transform.position);

        /// <summary>Opens (away from <paramref name="userPosition"/>) or closes the door.</summary>
        public void Toggle(Vector3 userPosition)
        {
            if (!_open && _t <= 0.001f) _openRotation = ComputeOpenRotation(userPosition);
            _open = !_open;
        }

        private Quaternion ComputeOpenRotation(Vector3 user)
        {
            Vector3 center = InteractPoint; // called while closed
            Vector3 arm = center - transform.position;
            Vector3 axis = Vector3.up;
            if (_kind == DoorKind.TiltUp)
            {
                var r = GetComponent<Renderer>();
                var size = r != null ? r.bounds.size : Vector3.one;
                axis = size.x >= size.z ? Vector3.right : Vector3.forward; // along the door's width
            }
            Vector3 want = _openToward.sqrMagnitude > 0f ? _openToward : center - user;
            want.y = 0f;
            var plus = Quaternion.AngleAxis(_openAngle, axis);
            var minus = Quaternion.AngleAxis(-_openAngle, axis);
            return (Vector3.Dot(plus * arm, want) >= Vector3.Dot(minus * arm, want) ? plus : minus) * _closedRotation;
        }

        private void Update()
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.05f, _openSeconds));
            transform.rotation = Quaternion.Slerp(_closedRotation, _openRotation, Mathf.SmoothStep(0f, 1f, _t));
        }

        /// <summary>Editor/builder setup.</summary>
        public void Configure(DoorKind kind, float openAngle, float openSeconds, Vector3 openToward)
        {
            _kind = kind;
            _openAngle = openAngle;
            _openSeconds = openSeconds;
            _openToward = openToward;
        }
    }
}
