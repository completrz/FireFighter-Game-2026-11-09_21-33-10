using System.Collections.Generic;
using Firehouse.Core;
using Firehouse.Player;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Truck
{
    /// <summary>
    /// Arcade raycast-suspension truck. Only the owner simulates it: the host while parked, the driver while driving
    /// (see <see cref="TruckSeats"/>). Everyone else sees it through an owner-authority NetworkTransform.
    ///
    /// Also, on the simulating client: throws back-step riders off on hard jolts, knocks over players it drives into,
    /// and claims ownership of loose props nearby so they get bashed aside instead of acting like walls.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TruckController : NetworkBehaviour
    {
        [System.Serializable]
        public struct Wheel
        {
            public Transform Mount;   // where the suspension ray starts (y = top of travel)
            public Transform Visual;  // wheel mesh, moved/rotated locally on every client
            public bool Front;        // steers
        }

        [SerializeField] private TruckStats _stats;
        [SerializeField] private Wheel[] _wheels;
        [SerializeField] private Transform _hitVolume; // box (local scale = size) used for run-over checks

        // Driver controls, written by the owner (= driver while driving).
        private readonly NetworkVariable<sbyte> _throttleNet = new(0, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<sbyte> _steerNet = new(0, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> _horn = new(false, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> _siren = new(false, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> _lights = new(false, writePerm: NetworkVariableWritePermission.Owner);

        public TruckStats Stats => _stats;
        public Rigidbody Body => _rb;
        public IReadOnlyList<Wheel> Wheels => _wheels;
        public float Throttle => _throttleNet.Value / 100f;
        public float Steer => _steerNet.Value / 100f;
        public bool Horn => _horn.Value;
        public bool Siren => _siren.Value;
        public bool Lights => _lights.Value;
        public float SteerAngle { get; private set; }
        /// <summary>World velocity (real on the owner, estimated from movement elsewhere).</summary>
        public Vector3 Velocity => IsOwner ? _rb.linearVelocity : _estVelocity;
        public float ForwardSpeed => Vector3.Dot(Velocity, transform.forward);

        private Rigidbody _rb;
        private TruckSeats _seats;
        private Collider[] _colliders;
        private readonly bool[] _grounded = new bool[8];
        private Vector3 _prevVelocity;
        private float _throttle, _steerInput;
        private bool _handbrake;
        private Vector3? _pendingVelocity, _pendingAngular;
        private readonly Dictionary<ulong, float> _recentHits = new();
        private float _nextClaim, _nextIgnoreRefresh;
        private Vector3 _estVelocity, _estAngular, _lastPos;
        private Quaternion _lastRot;
        private readonly Collider[] _overlap = new Collider[32];

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _seats = GetComponent<TruckSeats>();
            _colliders = GetComponentsInChildren<Collider>();
            ApplyStats();
        }

        private void ApplyStats()
        {
            _rb.mass = _stats.Mass;
            _rb.centerOfMass = new Vector3(0f, _stats.CenterOfMassHeight, 0f);
        }

        public override void OnNetworkSpawn()
        {
            _lastPos = transform.position;
            _lastRot = transform.rotation;
        }

        public override void OnGainedOwnership() => ResetControls();
        public override void OnLostOwnership() => _pendingVelocity = null;

        private void ResetControls()
        {
            if (!IsOwner) return;
            _throttleNet.Value = 0;
            _steerNet.Value = 0;
            _horn.Value = false;
            // Siren and lights stay as they were: the next driver inherits them.
        }

        // ---------------------------------------------------------------- ownership handoff (host)

        /// <summary>Host only: give the truck to another client and keep its motion.</summary>
        public void HandOwnership(ulong clientId, Vector3 velocity, Vector3 angularVelocity)
        {
            if (!IsServer) return;
            NetworkObject.ChangeOwnership(clientId);
            SetMotionRpc(velocity, angularVelocity, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SetMotionRpc(Vector3 velocity, Vector3 angularVelocity, RpcParams rpc)
        {
            _pendingVelocity = velocity;
            _pendingAngular = angularVelocity;
        }

        /// <summary>Velocity of a point on the truck (works on every client).</summary>
        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            if (IsOwner) return _rb.GetPointVelocity(worldPoint);
            return _estVelocity + Vector3.Cross(_estAngular, worldPoint - transform.position);
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (!IsSpawned) return;
            if (!IsOwner) EstimateMotion();
            else ReadDriverInput();
        }

        private void EstimateMotion()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _estVelocity = Vector3.Lerp(_estVelocity, (transform.position - _lastPos) / dt, 0.4f);
            var dq = transform.rotation * Quaternion.Inverse(_lastRot);
            dq.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            var ang = float.IsFinite(axis.x) ? axis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;
            _estAngular = Vector3.Lerp(_estAngular, ang, 0.4f);
            _lastPos = transform.position;
            _lastRot = transform.rotation;
        }

        private SmokieController LocalDriver()
        {
            var local = SmokieController.Local;
            if (local == null || _seats == null) return null;
            return _seats.OccupantOf(0) == local.OwnerClientId ? local : null;
        }

        /// <summary>
        /// Test hook (PlayMode tests, debug tools): when set on the owner, overrides driver input with
        /// (throttle, steer) in -1..1. Leave null for normal play.
        /// </summary>
        public Vector2? AutoDrive { get; set; }

        private void ReadDriverInput()
        {
            if (AutoDrive.HasValue)
            {
                _throttle = Mathf.Clamp(AutoDrive.Value.x, -1f, 1f);
                _steerInput = Mathf.Clamp(AutoDrive.Value.y, -1f, 1f);
                _handbrake = false;
                SyncControls();
                return;
            }
            var driver = LocalDriver();
            if (driver == null || driver.Input == null || UIFocus.IsBlocked)
            {
                _throttle = 0f; _steerInput = 0f; _handbrake = driver == null; // parked: hold the brake
                if (_horn.Value) _horn.Value = false;
                SyncControls();
                return;
            }
            var input = driver.Input;
            var mv = input.Move.ReadValue<Vector2>();
            _throttle = mv.y;
            _steerInput = mv.x;
            // Handbrake on Space, plus an automatic parking brake when idle and nearly stopped (no rolling down hills).
            bool idle = Mathf.Abs(_throttle) < 0.05f && _rb.linearVelocity.magnitude < 1f;
            _handbrake = input.Jump.IsPressed() || idle;
            bool horn = input.Horn.IsPressed();
            if (_horn.Value != horn) _horn.Value = horn;
            if (input.Siren.WasPressedThisFrame()) _siren.Value = !_siren.Value;
            if (input.Lights.WasPressedThisFrame()) _lights.Value = !_lights.Value;
            if (input.Flip.WasPressedThisFrame()) TryFlipUpright();
            SyncControls();
        }

        private void SyncControls()
        {
            var t = (sbyte)Mathf.RoundToInt(_throttle * 100f);
            var s = (sbyte)Mathf.RoundToInt(_steerInput * 100f);
            if (_throttleNet.Value != t) _throttleNet.Value = t;
            if (_steerNet.Value != s) _steerNet.Value = s;
        }

        private void TryFlipUpright()
        {
            if (Vector3.Dot(transform.up, Vector3.up) > 0.5f || _rb.linearVelocity.magnitude > 3f) return;
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            _rb.position += Vector3.up * 1.5f;
            _rb.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // ---------------------------------------------------------------- physics (owner only)

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || _rb.isKinematic) return;
            ApplyStats();

            if (_pendingVelocity.HasValue)
            {
                _rb.linearVelocity = _pendingVelocity.Value;
                _rb.angularVelocity = _pendingAngular ?? Vector3.zero;
                _pendingVelocity = null;
            }

            float dt = Time.fixedDeltaTime;
            float speed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            float steerLimit = _stats.MaxSteerAngle * Mathf.Lerp(1f, _stats.HighSpeedSteerFactor, Mathf.Abs(speed) / _stats.MaxSpeed);
            SteerAngle = Mathf.MoveTowards(SteerAngle, _steerInput * steerLimit, _stats.MaxSteerAngle * _stats.SteerSpeed * dt);

            int groundedCount = 0;
            for (int i = 0; i < _wheels.Length; i++)
                if (SimulateWheel(i, speed, dt)) groundedCount++;

            if (groundedCount == 0)
                _rb.angularVelocity *= 1f - Mathf.Clamp01(_stats.AirAngularDamping * dt);

            CheckJolts(dt);
            CheckRunOver();
            if (Time.time >= _nextClaim) { _nextClaim = Time.time + 0.25f; ClaimNearbyProps(); }
            if (Time.time >= _nextIgnoreRefresh) { _nextIgnoreRefresh = Time.time + 0.5f; RefreshPlayerCollisionIgnores(); }
            _prevVelocity = _rb.linearVelocity;
        }

        private bool SimulateWheel(int i, float speed, float dt)
        {
            var w = _wheels[i];
            var up = transform.up;
            var origin = w.Mount.position;
            float maxDist = _stats.RestLength + _stats.WheelRadius;
            const int mask = (1 << Layers.Default) | (1 << Layers.Grabbable);

            if (!Physics.Raycast(origin, -up, out var hit, maxDist, mask, QueryTriggerInteraction.Ignore))
            {
                _grounded[i] = false;
                return false;
            }
            _grounded[i] = true;

            // Suspension
            float compression = maxDist - hit.distance;
            float springVel = Vector3.Dot(_rb.GetPointVelocity(origin), up);
            float suspension = Mathf.Max(0f, compression * _stats.Spring - springVel * _stats.Damper);
            _rb.AddForceAtPosition(up * suspension, origin);

            // Tire frame
            var fwd = w.Front ? Quaternion.AngleAxis(SteerAngle, up) * transform.forward : transform.forward;
            fwd = Vector3.ProjectOnPlane(fwd, hit.normal).normalized;
            var right = Vector3.Cross(hit.normal, fwd);
            var contactVel = _rb.GetPointVelocity(hit.point);
            float massPerWheel = _rb.mass / _wheels.Length;

            // Sideways grip, limited by load (lets it slide when overloaded / handbraking)
            float lateral = Vector3.Dot(contactVel, right);
            float grip = !w.Front && _handbrake ? _stats.HandbrakeRearGrip : 1f;
            float lateralForce = -lateral * massPerWheel / dt * 0.5f * grip;
            float maxLateral = suspension * _stats.TireFriction * grip;
            lateralForce = Mathf.Clamp(lateralForce, -maxLateral, maxLateral);

            // Drive / brake / rolling resistance along the tire
            float along = Vector3.Dot(contactVel, fwd);
            float drive = 0f;
            if (_handbrake)
            {
                drive = -Mathf.Sign(along) * Mathf.Min(_stats.BrakeForce / _wheels.Length, Mathf.Abs(along) * massPerWheel / dt);
            }
            else if (_throttle > 0.05f)
            {
                if (speed < -0.5f) drive = _stats.BrakeForce / _wheels.Length * _throttle;              // braking from reverse
                else if (speed < _stats.MaxSpeed) drive = _stats.EngineForce / _wheels.Length * _throttle;
            }
            else if (_throttle < -0.05f)
            {
                if (speed > 0.5f) drive = -_stats.BrakeForce / _wheels.Length * -_throttle;            // brake
                else if (speed > -_stats.MaxReverseSpeed) drive = -_stats.EngineForce * 0.5f / _wheels.Length * -_throttle; // reverse
            }
            drive -= along * _stats.RollingResistance * massPerWheel;
            float maxDrive = suspension * _stats.TireFriction * 1.2f;
            drive = Mathf.Clamp(drive, -maxDrive, maxDrive);

            _rb.AddForceAtPosition(right * lateralForce + fwd * drive, hit.point);

            // Push back on whatever we're driving over (props).
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(-up * suspension * 0.5f, hit.point);
            return true;
        }

        // ---------------------------------------------------------------- comedy: back step, run-overs, props

        private void CheckJolts(float dt)
        {
            if (_seats == null) return;
            var accel = (_rb.linearVelocity - _prevVelocity) / dt;
            float jolt = Mathf.Abs(accel.y) + new Vector2(accel.x, accel.z).magnitude * 0.5f;
            if (jolt < _stats.BackStepEjectAccel) return;

            float chance = Mathf.Clamp01((jolt - _stats.BackStepEjectAccel) / _stats.BackStepEjectAccel + 0.3f);
            for (int i = 0; i < _seats.Seats.Count; i++)
            {
                if (_seats.Seats[i].Kind != SeatKind.BackStep || _seats.OccupantOf(i) == TruckSeats.Empty) continue;
                if (Random.value > chance * dt * 30f) continue;
                var seat = _seats.Seats[i];
                var launch = _rb.GetPointVelocity(seat.transform.position) * 0.9f
                             - transform.forward * 2f + Vector3.up * 3.5f + transform.right * Random.Range(-2f, 2f);
                _seats.Eject(i, launch);
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            if (!IsSpawned || !IsOwner || _seats == null) return;
            float speedLoss = c.impulse.magnitude / _rb.mass;
            if (speedLoss < _stats.CrashSpeedChange) return;
            // Big crash: everyone on the back step goes flying forward.
            for (int i = 0; i < _seats.Seats.Count; i++)
            {
                if (_seats.Seats[i].Kind != SeatKind.BackStep || _seats.OccupantOf(i) == TruckSeats.Empty) continue;
                _seats.Eject(i, _prevVelocity * 0.8f + Vector3.up * 4f);
            }
        }

        private void CheckRunOver()
        {
            if (_hitVolume == null) return;
            float speed = _rb.linearVelocity.magnitude;
            if (speed < _stats.RunOverMinSpeed) return;
            int n = Physics.OverlapBoxNonAlloc(_hitVolume.position, _hitVolume.lossyScale * 0.5f, _overlap, _hitVolume.rotation,
                1 << Layers.Player, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var smokie = _overlap[i].GetComponentInParent<SmokieController>();
                if (smokie == null || smokie.State == SmokieState.Seated) continue;
                if (_recentHits.TryGetValue(smokie.OwnerClientId, out float t) && Time.time - t < 1f) continue;
                _recentHits[smokie.OwnerClientId] = Time.time;
                var away = Vector3.ProjectOnPlane(smokie.transform.position - transform.position, transform.up).normalized;
                var push = _rb.linearVelocity * 1.1f + away * 2f + Vector3.up * Mathf.Min(6f, speed * 0.4f);
                smokie.Knockback(push, forceKnockdown: speed >= _stats.RunOverKnockdownSpeed);
            }
        }

        /// <summary>
        /// On the simulating client, other players are kinematic copies, which would stop the truck like posts.
        /// So the truck passes through them physically and uses <see cref="CheckRunOver"/> to shove them instead.
        /// </summary>
        private void RefreshPlayerCollisionIgnores()
        {
            foreach (var smokie in FindObjectsByType<SmokieController>(FindObjectsInactive.Exclude))
            {
                bool ignore = !smokie.IsOwner;
                foreach (var col in _colliders)
                    if (col != null && !col.isTrigger) Physics.IgnoreCollision(col, smokie.Capsule, ignore);
            }
        }

        private void ClaimNearbyProps()
        {
            float r = 6f + _rb.linearVelocity.magnitude * 0.6f;
            int n = Physics.OverlapSphereNonAlloc(transform.position, r, _overlap, 1 << Layers.Grabbable, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var g = _overlap[i].GetComponentInParent<Grabbable>();
                if (g != null && !g.IsOwner && g.HolderCount == 0) g.RequestClaim();
            }
        }
    }
}
