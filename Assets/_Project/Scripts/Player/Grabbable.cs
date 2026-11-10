using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    /// <summary>
    /// A physics object players can pick up and carry, R.E.P.O. style: it's pulled toward each holder's hands by a
    /// strength-limited spring, so heavy things sag and drag and two people carry better than one.
    ///
    /// Networking: the host keeps the holder list. Ownership moves to the first holder so their client simulates
    /// the object (no input lag), and it uses an owner-authority NetworkTransform + NetworkRigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Grabbable : NetworkBehaviour
    {
        public static readonly List<Grabbable> All = new();

        [SerializeField, Range(1, 4)] private int _maxHolders = 2;
        [SerializeField, Tooltip("Shown in the crosshair hint.")] private string _displayName = "";

        private readonly NetworkList<ulong> _holders = new();
        private Rigidbody _rb;
        private Collider[] _colliders;

        public Rigidbody Body => _rb;
        public Collider[] Colliders => _colliders;
        public float Mass => _rb.mass;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name.Replace("(Clone)", "").Replace("P_", "") : _displayName;
        public int HolderCount => _holders.Count;
        public bool IsFull => _holders.Count >= _maxHolders;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public override void OnNetworkSpawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        public bool IsHeldBy(ulong clientId)
        {
            for (int i = 0; i < _holders.Count; i++)
                if (_holders[i] == clientId) return true;
            return false;
        }

        /// <summary>Returns the object held by <paramref name="clientId"/>, or null.</summary>
        public static Grabbable HeldBy(ulong clientId)
        {
            foreach (var g in All)
                if (g.IsSpawned && g.IsHeldBy(clientId)) return g;
            return null;
        }

        // ---------------------------------------------------------------- requests (called by PlayerGrabber)

        public void RequestGrab() => GrabRpc();

        private float _lastClaimRequest = -10f;

        /// <summary>
        /// Ask to simulate this object without holding it (the truck claims loose props it's about to hit,
        /// so they get knocked aside on the driver's machine instead of acting like immovable walls).
        /// </summary>
        public void RequestClaim()
        {
            if (Time.time - _lastClaimRequest < 1f) return;
            _lastClaimRequest = Time.time;
            ClaimRpc();
        }

        [Rpc(SendTo.Server)]
        private void ClaimRpc(RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (_holders.Count == 0 && OwnerClientId != sender) NetworkObject.ChangeOwnership(sender);
        }
        public void RequestRelease() => ReleaseRpc();

        [Rpc(SendTo.Server)]
        private void GrabRpc(RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (IsHeldBy(sender) || IsFull) return;
            if (HeldBy(sender) != null) return; // one thing at a time
            _holders.Add(sender);
            if (_holders.Count == 1 && OwnerClientId != sender) NetworkObject.ChangeOwnership(sender);
        }

        [Rpc(SendTo.Server)]
        private void ReleaseRpc(RpcParams rpc = default) => RemoveHolder(rpc.Receive.SenderClientId);

        private void OnClientDisconnected(ulong clientId) => RemoveHolder(clientId);

        private void RemoveHolder(ulong clientId)
        {
            if (!_holders.Contains(clientId)) return;
            _holders.Remove(clientId);
            // Hand simulation to the remaining holder so their carrying stays responsive.
            if (_holders.Count > 0 && OwnerClientId != _holders[0]) NetworkObject.ChangeOwnership(_holders[0]);
        }

        /// <summary>Owner-side: launch the object (used for throwing). Velocity is scaled down for heavy objects.</summary>
        public void Launch(Vector3 velocity, Vector3 inheritVelocity)
        {
            if (!IsOwner) return;
            float massScale = Mathf.Clamp(20f / Mathf.Max(0.1f, _rb.mass), 0.12f, 1f);
            _rb.linearVelocity = inheritVelocity + velocity * massScale;
        }

        // ---------------------------------------------------------------- carry physics (owner only)

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || _holders.Count == 0) return;

            int active = 0;
            for (int i = 0; i < _holders.Count; i++)
            {
                var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(_holders[i]);
                if (player == null) continue;
                var smokie = player.GetComponent<SmokieController>();
                if (smokie == null || smokie.State == SmokieState.KnockedDown) continue;
                active++;
                ApplyCarryForce(smokie);
            }
            if (active > 0) _rb.angularVelocity *= 0.9f; // calm spinning while carried
        }

        private void ApplyCarryForce(SmokieController holder)
        {
            var s = holder.Stats;
            int n = Mathf.Max(1, _holders.Count);
            var toTarget = holder.HoldPoint - _rb.worldCenterOfMass;
            var relVel = _rb.linearVelocity - holder.Velocity;
            // Spring + damper, plus this holder's share of holding the weight up, limited by arm strength.
            var force = (toTarget * s.CarryStiffness - relVel * s.CarryDamping) * _rb.mass
                        - Physics.gravity * _rb.mass / n;
            force = Vector3.ClampMagnitude(force, s.CarryStrength);
            _rb.AddForce(force, ForceMode.Force);
        }
    }
}
