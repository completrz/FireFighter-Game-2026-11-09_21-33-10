using Firehouse.Core;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// Local-player grabbing: hold Grab (LMB) on a <see cref="Grabbable"/> to carry it, release to drop,
    /// Throw (RMB) to throw. Also draws the crosshair.
    /// </summary>
    [RequireComponent(typeof(SmokieController))]
    public sealed class PlayerGrabber : NetworkBehaviour
    {
        private SmokieController _smokie;
        private Grabbable _aimed;
        private Grabbable _ignoredWith; // object whose collisions with our body are currently ignored
        private bool _requested;

        /// <summary>The object this player is carrying (works on every client).</summary>
        public Grabbable Held => IsSpawned ? Grabbable.HeldBy(OwnerClientId) : null;

        /// <summary>True on the owner while the grab button is held but nothing is in hand (arms reach forward).</summary>
        public bool IsReaching { get; private set; }

        private void Awake() => _smokie = GetComponent<SmokieController>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner) _smokie.KnockedDown += ReleaseHeld;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) _smokie.KnockedDown -= ReleaseHeld;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _smokie.Input == null) return;

            var held = Held;
            UpdateCollisionIgnore(held);
            _aimed = held == null ? FindAimed() : null;

            bool holdingHose = Water.TruckWater.HeldBy(OwnerClientId) != null; // LMB sprays instead
            bool blocked = UIFocus.IsBlocked || holdingHose || _smokie.State == SmokieState.KnockedDown || _smokie.State == SmokieState.Seated;
            if (holdingHose) { _aimed = null; if (held != null) ReleaseHeld(); }
            if (_smokie.State == SmokieState.Seated && held != null) ReleaseHeld();
            bool grabDown = !blocked && _smokie.Input.Grab.IsPressed();
            IsReaching = grabDown && held == null;

            if (held != null)
            {
                bool tooFar = Vector3.Distance(held.Body.worldCenterOfMass, _smokie.HoldPoint) > _smokie.Stats.DropDistance;
                if (!grabDown || tooFar)
                {
                    ReleaseHeld();
                }
                else if (!blocked && _smokie.Input.Throw.WasPressedThisFrame())
                {
                    held.Launch(_smokie.LookDirection * _smokie.Stats.ThrowSpeed, _smokie.Velocity);
                    ReleaseHeld();
                }
                _requested = false;
                return;
            }

            if (!grabDown) _requested = false;
            if (grabDown && !_requested && _aimed != null && !_aimed.IsFull)
            {
                _aimed.RequestGrab();
                _requested = true; // one request per press
            }
        }

        private void ReleaseHeld()
        {
            var held = Held;
            if (held != null) held.RequestRelease();
        }

        private Grabbable FindAimed()
        {
            var ray = new Ray(_smokie.LookOrigin, _smokie.LookDirection);
            if (!Physics.Raycast(ray, out var hit, _smokie.Stats.GrabRange, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                return null;
            return hit.collider.GetComponentInParent<Grabbable>();
        }

        private void UpdateCollisionIgnore(Grabbable held)
        {
            if (held == _ignoredWith) return;
            SetIgnore(_ignoredWith, false);
            SetIgnore(held, true);
            _ignoredWith = held;
        }

        private void SetIgnore(Grabbable g, bool ignore)
        {
            if (g == null) return;
            foreach (var c in g.Colliders)
                if (c != null) Physics.IgnoreCollision(_smokie.Capsule, c, ignore);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || UIFocus.IsBlocked) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var held = Held;
            bool highlight = _aimed != null || held != null;
            GUI.color = highlight ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.7f);
            float s = highlight ? 8f : 5f;
            GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string hint = held != null ? (held.HolderCount > 1 ? "Carrying together · release to drop" : "Release to drop · RMB throw")
                        : _aimed != null ? (_aimed.IsFull ? $"{_aimed.DisplayName} (hands full)" : $"Hold LMB: grab {_aimed.DisplayName}")
                        : null;
            if (hint == null) return;
            var size = GUI.skin.label.CalcSize(new GUIContent(hint));
            GUI.Label(new Rect(cx - size.x * 0.5f, cy + 16, size.x, size.y), hint);
        }
    }
}
