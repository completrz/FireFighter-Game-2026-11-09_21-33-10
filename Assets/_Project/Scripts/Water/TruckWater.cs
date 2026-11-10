using System.Collections.Generic;
using Firehouse.Core;
using Firehouse.Fire;
using Firehouse.Player;
using Firehouse.Truck;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Water
{
    /// <summary>
    /// A truck's water system: tank, pump pressure and one hose line.
    ///
    /// Authority: the host owns tank, pressure, who holds the hose and whether they're spraying (players ask via RPCs).
    /// The holder's own client handles their input, the hose length limit and nozzle kickback (they own their body).
    /// The host traces the water arc from the holder's synced aim to cool fire cells and shove sprayed teammates.
    /// Every client draws the water particles, the rope and the nozzle locally from that synced state.
    /// </summary>
    public sealed class TruckWater : NetworkBehaviour
    {
        public const ulong NoHolder = ulong.MaxValue;
        public static readonly List<TruckWater> All = new();

        [SerializeField] private HoseStats _stats;
        [SerializeField] private Transform _reelPoint;     // where the hose leaves the truck
        [SerializeField] private Material _material;

        private readonly NetworkVariable<float> _tank = new();
        private readonly NetworkVariable<byte> _pressureStep = new(2);
        private readonly NetworkVariable<ulong> _holder = new(NoHolder);
        private readonly NetworkVariable<bool> _spraying = new();

        public HoseStats Stats => _stats;
        public float Tank01 => _stats.TankCapacity > 0 ? _tank.Value / _stats.TankCapacity : 0f;
        public float Pressure => HoseStats.PressureSteps[Mathf.Clamp(_pressureStep.Value, 0, HoseStats.PressureSteps.Length - 1)];
        public ulong Holder => _holder.Value;
        public bool HasHolder => _holder.Value != NoHolder;
        /// <summary>Water is actually coming out (someone's spraying, pump on, tank not empty).</summary>
        public bool Flowing => _spraying.Value && HasHolder && Pressure > 0f && _tank.Value > 0f;
        public Vector3 ReelPosition => _reelPoint.position;

        private TruckController _truck;
        private HoseRope _rope;
        private ParticleSystem _particles;
        private Transform _nozzle;
        private float _hitTimer, _sprayHeldTime;
        private bool _localSprayWanted;
        private readonly Dictionary<ulong, float> _exposure = new();
        private readonly Dictionary<ulong, float> _lastExposed = new();

        // ---------------------------------------------------------------- lookup helpers

        /// <summary>The truck whose hose this client/player is holding, or null.</summary>
        public static TruckWater HeldBy(ulong clientId)
        {
            foreach (var w in All)
                if (w.IsSpawned && w._holder.Value == clientId) return w;
            return null;
        }

        private SmokieController HolderSmokie()
        {
            if (!HasHolder || NetworkManager == null) return null;
            var no = NetworkManager.SpawnManager.GetPlayerNetworkObject(_holder.Value);
            return no != null ? no.GetComponent<SmokieController>() : null;
        }

        /// <summary>Nozzle position and direction for the current holder (works on every client).</summary>
        public bool TryGetNozzle(out Vector3 position, out Vector3 direction)
        {
            var s = HolderSmokie();
            if (s == null) { position = direction = Vector3.zero; return false; }
            direction = s.LookDirection;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            position = s.LookOrigin + direction * 0.75f + right * 0.12f - Vector3.up * 0.28f;
            return true;
        }

        // ---------------------------------------------------------------- lifecycle

        private void Awake()
        {
            _truck = GetComponent<TruckController>();
            CreateVisuals();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _tank.Value = _stats.TankCapacity;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            _holder.OnValueChanged += (_, _) => _rope.ResetRope(ReelPosition, ReelPosition);
            TuningRegistry.AddSection("Hose", DrawDebug);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            TuningRegistry.RemoveSection("Hose");
        }

        private void OnClientDisconnected(ulong id)
        {
            if (_holder.Value == id) { _holder.Value = NoHolder; _spraying.Value = false; }
        }

        // ---------------------------------------------------------------- requests

        public void RequestTakeHose() => TakeHoseRpc();
        public void RequestDropHose() => DropHoseRpc();
        public void RequestCyclePressure() => CyclePressureRpc();

        [Rpc(SendTo.Server)]
        private void TakeHoseRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (HasHolder || HeldBy(who) != null) return;
            _holder.Value = who;
            _spraying.Value = false;
        }

        [Rpc(SendTo.Server)]
        private void DropHoseRpc(RpcParams rpc = default)
        {
            if (_holder.Value != rpc.Receive.SenderClientId) return;
            _holder.Value = NoHolder;
            _spraying.Value = false;
        }

        [Rpc(SendTo.Server)]
        private void SetSprayingRpc(bool on, RpcParams rpc = default)
        {
            if (_holder.Value == rpc.Receive.SenderClientId) _spraying.Value = on;
        }

        [Rpc(SendTo.Server)]
        private void CyclePressureRpc()
        {
            _pressureStep.Value = (byte)((_pressureStep.Value + 1) % HoseStats.PressureSteps.Length);
        }

        /// <summary>Host: refill the tank (debug now; hydrants later).</summary>
        public void Refill()
        {
            if (IsServer) _tank.Value = _stats.TankCapacity;
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) TickServer();
            var local = SmokieController.Local;
            if (local != null && _holder.Value == local.OwnerClientId) TickHolder(local);
            UpdateVisuals();
        }

        private void TickServer()
        {
            // Holder got into a seat → hose drops.
            if (HasHolder && TruckSeats.FindSeatOf(_holder.Value, out _) >= 0)
            {
                _holder.Value = NoHolder;
                _spraying.Value = false;
            }
            if (!Flowing) return;

            _tank.Value = Mathf.Max(0f, _tank.Value - _stats.FlowAtMax * Pressure * Time.deltaTime);
            _hitTimer += Time.deltaTime;
            if (_hitTimer < 0.1f) return;
            float dt = _hitTimer;
            _hitTimer = 0f;
            if (TryGetNozzle(out var pos, out var dir)) TraceSpray(pos, dir, dt);
        }

        /// <summary>Host: march the water arc, cool fire where it lands, shove anyone standing in it.</summary>
        private void TraceSpray(Vector3 pos, Vector3 dir, float dt)
        {
            float speed = _stats.SpeedFor(Pressure);
            var vel = dir * speed;
            const int mask = (1 << Layers.Default) | (1 << Layers.Vehicle) | (1 << Layers.Grabbable) | (1 << Layers.Player);
            const float step = 0.05f;
            var holder = HolderSmokie();
            for (float t = 0f; t < 2.5f; t += step)
            {
                var next = pos + vel * step + 0.5f * Physics.gravity * step * step;
                vel += Physics.gravity * step;
                if (Physics.Linecast(pos, next, out var hit, mask, QueryTriggerInteraction.Ignore))
                {
                    var smokie = hit.collider.GetComponentInParent<SmokieController>();
                    if (smokie != null && smokie == holder) { pos = next; continue; } // the stream starts inside our own reach
                    float amount = _stats.FlowAtMax * Pressure * dt * _stats.AgentPerUnit;
                    FireSystem.ApplyAgentAt(hit.point, _stats.HitRadius, _stats.Agent, amount);
                    if (smokie != null) PushPlayer(smokie, vel.normalized, dt);
                    else if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                        hit.rigidbody.AddForceAtPosition(vel.normalized * Pressure * 60f * dt, hit.point, ForceMode.Impulse);
                    return;
                }
                pos = next;
            }
        }

        private void PushPlayer(SmokieController target, Vector3 dir, float dt)
        {
            ulong id = target.OwnerClientId;
            if (!_lastExposed.TryGetValue(id, out float last) || Time.time - last > 0.4f) _exposure[id] = 0f;
            _lastExposed[id] = Time.time;
            _exposure[id] = _exposure.GetValueOrDefault(id) + dt;
            bool knockdown = Pressure >= 0.5f && _exposure[id] >= _stats.SprayKnockdownTime;
            var push = new Vector3(dir.x, Mathf.Max(0.15f, dir.y * 0.3f), dir.z).normalized * (_stats.PlayerPushAtMax * Pressure * dt);
            target.Knockback(push, knockdown);
            if (knockdown) _exposure[id] = -1f; // give them a moment before the next knockdown
        }

        /// <summary>The holder's own client: spray input, hose length limit, kickback.</summary>
        private void TickHolder(SmokieController me)
        {
            bool canAct = !UIFocus.IsBlocked && me.State != SmokieState.KnockedDown && me.Input != null;
            bool want = canAct && me.Input.Grab.IsPressed();
            if (want != _localSprayWanted)
            {
                _localSprayWanted = want;
                SetSprayingRpc(want);
            }

            // Hose length: you can't walk further than the line; a departing truck drags you along.
            var body = me.Body;
            var reel = ReelPosition;
            var offset = body.position - reel;
            offset.y = 0f;
            float dist = offset.magnitude;
            if (dist > _stats.MaxLength + _stats.YankDropExtra)
            {
                me.Knockback(-offset.normalized * 6f + Vector3.up * 3f, forceKnockdown: true);
                RequestDropHose();
                return;
            }
            if (dist > _stats.MaxLength && !body.isKinematic)
            {
                var n = offset / dist;
                var target = reel + n * _stats.MaxLength;
                body.position = new Vector3(target.x, body.position.y, target.z);
                float outward = Vector3.Dot(body.linearVelocity, n);
                if (outward > 0f) body.linearVelocity -= n * outward;
                // Being dragged: inherit the truck's motion toward us.
                var tv = _truck.GetPointVelocity(reel);
                if (Vector3.Dot(tv, -n) > 0.5f) body.linearVelocity += Vector3.ProjectOnPlane(tv, Vector3.up) * 0.5f;
            }

            // Kickback: high pressure shoves you backwards, and at max it can throw you over.
            if (Flowing && !body.isKinematic)
            {
                var back = -Vector3.ProjectOnPlane(me.LookDirection, Vector3.up).normalized;
                body.AddForce(back * _stats.KickbackAtMax * Pressure * Pressure, ForceMode.Acceleration);
                _sprayHeldTime += Time.deltaTime;
                if (Pressure >= _stats.KnockoverPressure && _sprayHeldTime > _stats.KnockoverAfter && Random.value < Time.deltaTime * 0.6f)
                {
                    me.Knockback(back * 6f + Vector3.up * 3f, forceKnockdown: true); // this also drops the hose (see below)
                    _sprayHeldTime = 0f;
                }
            }
            else _sprayHeldTime = 0f;

            if (me.State == SmokieState.KnockedDown) RequestDropHose();
        }

        // ---------------------------------------------------------------- visuals (every client)

        private void CreateVisuals()
        {
            var ropeGo = new GameObject("HoseRope");
            ropeGo.transform.SetParent(transform, false);
            _rope = ropeGo.AddComponent<HoseRope>();
            _rope.Init(_material, 24);

            var nozzleMesh = new MeshBuilder();
            nozzleMesh.SetTRS(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one);
            nozzleMesh.AddCylinder(new Vector3(0, -0.05f, 0), 0.06f, 0.045f, 0.3f, 8, new Color(0.15f, 0.15f, 0.16f));
            nozzleMesh.AddCylinder(new Vector3(0, 0.15f, 0), 0.045f, 0.03f, 0.12f, 8, new Color(0.85f, 0.65f, 0.25f));
            var nozzleGo = new GameObject("Nozzle", typeof(MeshFilter), typeof(MeshRenderer));
            nozzleGo.transform.SetParent(transform, false);
            nozzleGo.GetComponent<MeshFilter>().sharedMesh = nozzleMesh.Build("HoseNozzle");
            nozzleGo.GetComponent<MeshRenderer>().sharedMaterial = _material;
            _nozzle = nozzleGo.transform;

            var psGo = new GameObject("WaterSpray");
            psGo.transform.SetParent(transform, false);
            _particles = psGo.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 1f;
            main.maxParticles = 1500;
            main.startLifetime = 2.2f;
            main.playOnAwake = false;
            var em = _particles.emission; em.enabled = false;
            var shape = _particles.shape; shape.enabled = false;
            var col = _particles.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Low;
            col.lifetimeLoss = 1f; // die on impact
            col.collidesWith = (1 << Layers.Default) | (1 << Layers.Vehicle) | (1 << Layers.Player) | (1 << Layers.Grabbable);
            var sol = _particles.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var r = psGo.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = new MeshBuilder().AddBox(Vector3.zero, Vector3.one, Color.white).Build("WaterDrop");
            r.enableGPUInstancing = false;
            var waterMat = new Material(_material) { name = "M_WaterSpray" };
            waterMat.SetFloat("_UseVertexColor", 0f);
            waterMat.SetColor("_BaseColor", new Color(0.55f, 0.78f, 1f));
            waterMat.SetColor("_GlowColor", new Color(0.12f, 0.2f, 0.3f));
            r.sharedMaterial = waterMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _particles.Play();
        }

        private void UpdateVisuals()
        {
            bool held = TryGetNozzle(out var nozzlePos, out var dir);
            _nozzle.gameObject.SetActive(held);
            _rope.gameObject.SetActive(held);
            if (!held) return;

            _nozzle.SetPositionAndRotation(nozzlePos, Quaternion.LookRotation(dir));
            _rope.Simulate(ReelPosition, nozzlePos - dir * 0.2f, _stats.MaxLength);

            // Water: everyone sees the holder's local intent immediately on their own screen; others see the synced state.
            var local = SmokieController.Local;
            bool iHold = local != null && local.OwnerClientId == _holder.Value;
            bool flowing = iHold ? _localSprayWanted && Pressure > 0f && _tank.Value > 0f : Flowing;
            if (!flowing) return;
            float speed = _stats.SpeedFor(Pressure);
            float expected = _stats.ParticlesPerSecond * (0.4f + 0.6f * Pressure) * Time.deltaTime;
            int n = Mathf.FloorToInt(expected) + (Random.value < expected % 1f ? 1 : 0);
            var inherit = HolderSmokie() != null ? HolderSmokie().Velocity : Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var jitter = Quaternion.Euler(Random.Range(-_stats.Spread, _stats.Spread), Random.Range(-_stats.Spread, _stats.Spread), 0f);
                var v = jitter * dir * speed * Random.Range(0.92f, 1.05f) + inherit;
                _particles.Emit(new ParticleSystem.EmitParams
                {
                    position = nozzlePos + dir * 0.15f + Random.insideUnitSphere * 0.03f,
                    velocity = v,
                    startSize = Random.Range(0.06f, 0.13f) * (0.7f + Pressure * 0.6f),
                    startLifetime = 2.2f,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        // ---------------------------------------------------------------- debug

        private void DrawDebug()
        {
            string holder = HasHolder ? $"client {_holder.Value}" : "nobody";
            GUILayout.Label($"{name}: tank {Tank01 * 100f:0}% · pressure {Pressure * 100f:0}% · hose held by {holder}{(Flowing ? " · SPRAYING" : "")}");
            if (IsServer && GUILayout.Button("Refill tank (host)")) Refill();
        }
    }
}
