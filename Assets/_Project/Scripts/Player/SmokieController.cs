using System;
using System.Collections.Generic;
using Firehouse.Core;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Player
{
    public enum SmokieState : byte { Walking, Climbing, KnockedDown, GettingUp, Seated }

    /// <summary>
    /// First-person physics controller for a Smokie. The owning client simulates its Rigidbody
    /// (other clients see it through an owner-authority NetworkTransform + NetworkRigidbody).
    /// Handles walk/sprint/jump, ladders and poles, and knockdowns from hits and hard landings.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class SmokieController : NetworkBehaviour
    {
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Camera _camera;
        [SerializeField, Tooltip("Pitched with the look angle so others can see where you're looking.")]
        private Transform _headPivot;

        /// <summary>The local player's controller, if spawned.</summary>
        public static SmokieController Local { get; private set; }

        public event Action KnockedDown;

        private readonly NetworkVariable<SmokieState> _state =
            new(SmokieState.Walking, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<sbyte> _pitchNet =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public PlayerStats Stats => _stats;
        public SmokieState State => _state.Value;
        public PlayerInputReader Input { get; private set; }
        public Rigidbody Body => _rb;
        public CapsuleCollider Capsule => _col;
        public bool IsGrounded => _grounded;
        /// <summary>Velocity: real for the owner, estimated from movement for everyone else (their body is kinematic).</summary>
        public Vector3 Velocity => IsOwner ? _rb.linearVelocity : _remoteVelocity;
        public Ladder CurrentLadder => _ladder;

        /// <summary>Pitch in degrees (local value for the owner, synced value for everyone else).</summary>
        public float Pitch => IsOwner ? _pitch : _pitchNet.Value;
        public Vector3 LookOrigin => _cameraPivot.position;
        public Vector3 LookDirection => Quaternion.Euler(Pitch, YawFromBody(), 0f) * Vector3.forward;
        public Vector3 HoldPoint => LookOrigin + LookDirection * _stats.HoldDistance;

        private Rigidbody _rb;
        private CapsuleCollider _col;
        private float _yaw, _pitch;
        private bool _grounded;
        private Rigidbody _groundBody;
        private float _lastGroundedTime = -10f;
        private float _jumpBufferedUntil = -10f;
        private float _prevVerticalSpeed;
        private Ladder _ladder;
        private readonly List<Ladder> _ladderContacts = new();
        private float _downUntil;
        private float _getUpStart;
        private Quaternion _getUpFrom;
        private Vector3 _remoteVelocity;
        private Vector3 _lastRemotePos;

        // Riding a truck (seat occupancy lives on TruckSeats, so every client can place us without extra sync).
        private Truck.TruckSeats _seats;
        private int _seatIndex = -1;
        private Truck.TruckSeats _lastSeats;
        private int _lastSeatIndex = -1;
        private float _seatLook;              // yaw of the head relative to the seat
        private Vector3? _pendingKnockback;   // knockback that arrived while still seated (e.g. thrown off the back step)
        private bool _pendingForceKnockdown;
        private float _pendingKnockbackTime;

        public bool IsSeated => _seatIndex >= 0;
        public Truck.TruckSeat CurrentSeat => _seats != null && _seatIndex >= 0 ? _seats.Seats[_seatIndex] : null;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<CapsuleCollider>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            Local = this;
            Input = new PlayerInputReader();
            _yaw = transform.eulerAngles.y;
            _camera.fieldOfView = _stats.FieldOfView;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            UIFocus.GameplayWantsCursorLock = true;
            TuningRegistry.AddSection("Local player", DrawDebug);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            if (Local == this) Local = null;
            Input?.Dispose();
            Input = null;
            UIFocus.GameplayWantsCursorLock = false;
            TuningRegistry.RemoveSection("Local player");
        }

        private float YawFromBody()
        {
            if (IsOwner) return _yaw;
            var f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
        }

        // ---------------------------------------------------------------- Update (look, input buffering)

        private void Update()
        {
            if (!IsSpawned) return;
            _seatIndex = Truck.TruckSeats.FindSeatOf(OwnerClientId, out _seats);

            if (!IsOwner)
            {
                if (Time.deltaTime > 0f)
                {
                    var est = (transform.position - _lastRemotePos) / Time.deltaTime;
                    _remoteVelocity = Vector3.Lerp(_remoteVelocity, est, 0.3f);
                }
                _lastRemotePos = transform.position;
                if (_headPivot != null) _headPivot.localRotation = Quaternion.Euler(_pitchNet.Value * 0.5f, 0f, 0f);
                return;
            }

            Input ??= new PlayerInputReader(); // lost after a script reload during play
            var st = _state.Value;
            bool canLook = !UIFocus.IsBlocked && (st == SmokieState.Walking || st == SmokieState.Climbing || st == SmokieState.Seated);
            bool canAct = !UIFocus.IsBlocked && (st == SmokieState.Walking || st == SmokieState.Climbing);
            bool usingGamepad = Input.Look.activeControl?.device is UnityEngine.InputSystem.Gamepad;
            if (canLook && (Cursor.lockState == CursorLockMode.Locked || usingGamepad))
            {
                var d = Input.ReadLookDegrees(_stats.MouseSensitivity, _stats.GamepadLookSpeed);
                if (st == SmokieState.Seated) _seatLook = Mathf.Clamp(_seatLook + d.x, -150f, 150f);
                else _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -85f, 85f);
            }
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, st == SmokieState.Seated ? _seatLook : 0f, 0f);
            if (Mathf.Abs(_pitch - _pitchNet.Value) >= 1f) _pitchNet.Value = (sbyte)Mathf.RoundToInt(_pitch);

            if (canAct && Input.Jump.WasPressedThisFrame()) _jumpBufferedUntil = Time.time + 0.15f;
        }

        // ---------------------------------------------------------------- FixedUpdate (physics, owner only)

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner) return;
            Input ??= new PlayerInputReader(); // lost after a script reload during play

            // Seat changes come from the host's occupancy list.
            if (IsSeated && _state.Value != SmokieState.Seated) EnterSeat();
            else if (!IsSeated && _state.Value == SmokieState.Seated) ExitSeat();

            switch (_state.Value)
            {
                case SmokieState.Walking: TickWalking(); break;
                case SmokieState.Climbing: TickClimbing(); break;
                case SmokieState.KnockedDown: TickKnockedDown(); break;
                case SmokieState.GettingUp: TickGettingUp(); break;
                case SmokieState.Seated: SnapToSeat(); break;
            }
            _prevVerticalSpeed = _rb.linearVelocity.y;
        }

        // ---------------------------------------------------------------- Seats

        private void EnterSeat()
        {
            if (_state.Value == SmokieState.Climbing) StopClimbing(Vector3.zero);
            _state.Value = SmokieState.Seated;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _seatLook = 0f;
            _lastSeats = _seats;
            _lastSeatIndex = _seatIndex;
            SnapToSeat();
        }

        private void ExitSeat()
        {
            Vector3 exitPos = transform.position, inherit = Vector3.zero;
            if (_pendingKnockback.HasValue && Time.time - _pendingKnockbackTime > 1f) _pendingKnockback = null; // stale hit while seated
            if (_lastSeats != null && _lastSeatIndex >= 0)
            {
                var seat = _lastSeats.Seats[_lastSeatIndex];
                inherit = _lastSeats.Truck.GetPointVelocity(seat.transform.position);
                // Getting out on purpose puts you beside the truck; being thrown off starts from the seat itself.
                exitPos = _pendingKnockback.HasValue ? seat.transform.position + Vector3.up * 0.3f : seat.ExitPoint + Vector3.up * 0.2f;
                _yaw = seat.transform.eulerAngles.y + _seatLook;
            }
            _seatLook = 0f;
            _rb.isKinematic = false;
            _rb.position = exitPos;
            transform.position = exitPos;
            _rb.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _rb.linearVelocity = inherit;
            _state.Value = SmokieState.Walking;
            _lastSeats = null;
            _lastSeatIndex = -1;

            if (_pendingKnockback.HasValue)
            {
                var v = _pendingKnockback.Value;
                _pendingKnockback = null;
                ApplyKnockback(v - inherit, _pendingForceKnockdown); // launch velocity already includes the truck's motion
            }
        }

        private void SnapToSeat()
        {
            var seat = CurrentSeat;
            if (seat == null) return;
            _rb.position = seat.transform.position;
            _rb.rotation = seat.transform.rotation;
        }

        /// <summary>Everyone places seated players on their seat after interpolation, so riders stay glued to the truck.</summary>
        private void LateUpdate()
        {
            if (!IsSpawned) return;
            bool seated = IsSeated;
            if (_col.enabled == seated) _col.enabled = !seated;
            if (!seated) return;
            var seat = CurrentSeat;
            transform.SetPositionAndRotation(seat.transform.position, seat.transform.rotation);
        }

        private Vector2 MoveInput => UIFocus.IsBlocked ? Vector2.zero : Vector2.ClampMagnitude(Input.Move.ReadValue<Vector2>(), 1f);

        private void TickWalking()
        {
            _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
            bool wasGrounded = _grounded;
            GroundCheck();

            // Hard landing knocks you over (falling off the truck, off ladders...).
            if (_grounded && !wasGrounded && _prevVerticalSpeed < -_stats.FallKnockdownSpeed)
            {
                KnockDown(_stats.KnockdownMinTime);
                return;
            }

            var mv = MoveInput;
            bool sprint = !UIFocus.IsBlocked && Input.Sprint.IsPressed() && mv.y > 0.1f;
            float speed = sprint ? _stats.SprintSpeed : _stats.WalkSpeed;
            var desired = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(mv.x, 0f, mv.y) * speed;

            // Stand still relative to whatever we're standing on (the truck bed later).
            if (_grounded && _groundBody != null)
            {
                var truck = _groundBody.GetComponent<Truck.TruckController>();
                var gv = truck != null ? truck.GetPointVelocity(_rb.position) : _groundBody.GetPointVelocity(_rb.position);
                desired += new Vector3(gv.x, 0f, gv.z);
            }

            var v = _rb.linearVelocity;
            var horiz = new Vector3(v.x, 0f, v.z);
            float accel = _grounded ? _stats.GroundAccel : _stats.AirAccel;
            var change = Vector3.MoveTowards(horiz, desired, accel * Time.fixedDeltaTime) - horiz;
            _rb.AddForce(change, ForceMode.VelocityChange);
            _rb.AddForce(Physics.gravity * (_stats.GravityMultiplier - 1f), ForceMode.Acceleration);

            bool canJump = _grounded || Time.time - _lastGroundedTime < _stats.CoyoteTime;
            if (Time.time < _jumpBufferedUntil && canJump)
            {
                _jumpBufferedUntil = -10f;
                _lastGroundedTime = -10f;
                v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(v.x, _stats.JumpSpeed, v.z);
                _grounded = false;
            }

            TryStartClimb(mv);
        }

        private void GroundCheck()
        {
            float r = _col.radius * 0.9f;
            var origin = _rb.position + Vector3.up * (r + 0.05f);
            _grounded = false;
            _groundBody = null;
            if (Physics.SphereCast(origin, r, Vector3.down, out var hit, 0.2f, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.6f && hit.rigidbody != _rb)
            {
                // Don't count as grounded while clearly moving up (just jumped).
                if (_rb.linearVelocity.y < 2f)
                {
                    _grounded = true;
                    _groundBody = hit.rigidbody;
                    _lastGroundedTime = Time.time;
                }
            }
        }

        // ---------------------------------------------------------------- Ladders / pole

        private void OnTriggerEnter(Collider other)
        {
            var l = other.GetComponent<Ladder>();
            if (l != null && !_ladderContacts.Contains(l)) _ladderContacts.Add(l);
        }

        private void OnTriggerExit(Collider other)
        {
            var l = other.GetComponent<Ladder>();
            if (l != null) _ladderContacts.Remove(l);
        }

        private void TryStartClimb(Vector2 mv)
        {
            foreach (var l in _ladderContacts)
            {
                if (l == null) continue;
                var look = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                bool facing = l.IsPole || Vector3.Dot(look, l.transform.forward) > 0.3f;
                bool wants = l.IsPole ? (mv.y > 0.1f || !_grounded) : mv.y > 0.1f;
                if (!facing || !wants) continue;
                if (!l.IsPole && _rb.position.y > l.TopY - 0.5f) continue; // already at the top
                _ladder = l;
                _state.Value = SmokieState.Climbing;
                _rb.useGravity = false;
                return;
            }
        }

        private void TickClimbing()
        {
            _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
            if (_ladder == null || !_ladderContacts.Contains(_ladder)) { StopClimbing(Vector3.zero); return; }

            var mv = MoveInput;
            float vertical;
            if (_ladder.IsPole)
                vertical = mv.y > 0.1f ? _stats.ClimbSpeed * 0.5f : -_stats.PoleSlideSpeed * (mv.y < -0.1f ? 1.3f : 1f);
            else
                vertical = mv.y * _stats.ClimbSpeed;

            // Hug the ladder's center line.
            var axisPoint = _ladder.ClosestPointOnAxis(_rb.position);
            var toAxis = Vector3.ProjectOnPlane(axisPoint - _rb.position, _ladder.Up);
            float hugDistance = _ladder.IsPole ? 0.35f : 0.35f;
            var hug = toAxis - toAxis.normalized * Mathf.Min(hugDistance, toAxis.magnitude);
            _rb.linearVelocity = _ladder.Up * vertical + hug * 8f;

            // Top of a ladder: climb over onto the platform.
            if (!_ladder.IsPole && _rb.position.y > _ladder.TopY - 0.25f && mv.y > 0.1f)
            {
                StopClimbing(Vector3.up * 3.5f + _ladder.transform.forward * 2.5f);
                return;
            }
            // Bottom: step off.
            if (vertical < 0f && _rb.position.y <= _ladder.BottomY + 0.05f)
            {
                StopClimbing(Vector3.zero);
                return;
            }
            if (Time.time < _jumpBufferedUntil)
            {
                _jumpBufferedUntil = -10f;
                var away = _ladder.IsPole
                    ? Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * 3f
                    : -_ladder.transform.forward * 3f;
                StopClimbing(away + Vector3.up * 3f);
            }
        }

        private void StopClimbing(Vector3 velocity)
        {
            _ladder = null;
            _rb.useGravity = true;
            _rb.linearVelocity = velocity;
            if (_state.Value == SmokieState.Climbing) _state.Value = SmokieState.Walking;
        }

        // ---------------------------------------------------------------- Knockdown

        /// <summary>
        /// Push this player. Callable from any client: if we're not the owner, it's forwarded to the owner.
        /// <paramref name="velocityChange"/> is in m/s; above <see cref="PlayerStats.KnockdownImpulse"/> it knocks them over.
        /// </summary>
        public void Knockback(Vector3 velocityChange, bool forceKnockdown = false)
        {
            if (IsOwner) ApplyKnockback(velocityChange, forceKnockdown);
            else KnockbackRpc(velocityChange, forceKnockdown);
        }

        [Rpc(SendTo.Owner)]
        private void KnockbackRpc(Vector3 velocityChange, bool forceKnockdown) => ApplyKnockback(velocityChange, forceKnockdown);

        private void ApplyKnockback(Vector3 velocityChange, bool forceKnockdown)
        {
            if (_state.Value == SmokieState.Seated)
            {
                // Seats protect you, except when the host is throwing you off (seat freed right after this).
                _pendingKnockback = velocityChange;
                _pendingForceKnockdown = forceKnockdown;
                _pendingKnockbackTime = Time.time;
                return;
            }
            if (_state.Value == SmokieState.Climbing) StopClimbing(_rb.linearVelocity);
            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
            if (forceKnockdown || velocityChange.magnitude >= _stats.KnockdownImpulse)
                KnockDown(_stats.KnockdownMinTime);
        }

        private void KnockDown(float seconds)
        {
            if (_state.Value == SmokieState.Climbing) StopClimbing(_rb.linearVelocity);
            _state.Value = SmokieState.KnockedDown;
            _downUntil = Time.time + seconds;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.AddTorque(UnityEngine.Random.onUnitSphere * 6f, ForceMode.VelocityChange);
            KnockedDown?.Invoke();
        }

        private void TickKnockedDown()
        {
            // The capsule is frictionless (so walking never sticks to walls), so fake friction while tumbling on the ground.
            if (Physics.Raycast(_rb.worldCenterOfMass, Vector3.down, out _, 0.9f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                var v = _rb.linearVelocity;
                var horiz = new Vector3(v.x, 0f, v.z);
                var slowed = Vector3.MoveTowards(horiz, Vector3.zero, _stats.KnockdownFriction * Time.fixedDeltaTime);
                _rb.linearVelocity = new Vector3(slowed.x, v.y, slowed.z);
                _rb.angularVelocity *= 0.97f;
            }
            if (Time.time < _downUntil || _rb.linearVelocity.magnitude > 1.5f) return;
            _state.Value = SmokieState.GettingUp;
            _getUpStart = Time.time;
            _getUpFrom = _rb.rotation;
            _rb.angularVelocity = Vector3.zero;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void TickGettingUp()
        {
            float t = Mathf.Clamp01((Time.time - _getUpStart) / _stats.GetUpTime);
            _rb.MoveRotation(Quaternion.Slerp(_getUpFrom, Quaternion.Euler(0f, _yaw, 0f), t));
            if (t < 1f) return;
            // Pop up a little so the capsule doesn't stay wedged in the floor.
            _rb.AddForce(Vector3.up * 1.5f, ForceMode.VelocityChange);
            _state.Value = SmokieState.Walking;
        }

        // ---------------------------------------------------------------- Debug

        private void DrawDebug()
        {
            GUILayout.Label($"State: {_state.Value}  grounded {_grounded}  speed {new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude:0.0}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Knock me over")) Knockback(-transform.forward * 4f + Vector3.up * 3f, forceKnockdown: true);
            if (GUILayout.Button("Launch me")) Knockback(Vector3.up * 14f + transform.forward * 4f);
            GUILayout.EndHorizontal();
        }
    }
}
