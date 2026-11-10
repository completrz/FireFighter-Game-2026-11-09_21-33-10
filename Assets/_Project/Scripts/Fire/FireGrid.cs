using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>
    /// A burnable thing (a shed, a grass field, a tree) as a grid of cells. transform.position is the grid's
    /// minimum corner; cells extend along +X/+Y/+Z (rotation allowed).
    ///
    /// Every client voxelizes the child <see cref="BurnableVolume"/>s into materials (deterministic math, no sync).
    /// The host runs <see cref="FireGridSim"/> and sends changed cell states a few times per second;
    /// <see cref="FireGridView"/> draws/collides the voxels and spawns local flames and smoke from those states.
    /// </summary>
    public sealed class FireGrid : NetworkBehaviour
    {
        [SerializeField] private FireSettings _settings;
        [SerializeField, Tooltip("Used by the F1 'Douse at crosshair' button.")] private AgentDef _debugAgent;
        [SerializeField] private Vector3Int _size = new(8, 6, 8);
        [SerializeField] private Vector3 _cellSize = new(0.5f, 0.5f, 0.5f);
        [SerializeField, Tooltip("Build a collider from the voxels (walls). Off for grass.")] private bool _solid = true;
        [SerializeField] private int _seed = 1;

        private byte[] _material;
        private FireMaterialDef[] _materials;
        private byte[] _states;
        private FireGridSim _sim;
        private float _simAccumulator, _syncTimer;
        private readonly List<int> _dirty = new();
        private readonly List<int> _changed = new();
        private int _burningCount;

        /// <summary>Raised on every client when cell states change (indices, or null for "everything").</summary>
        public event Action<List<int>> StatesChanged;

        public Vector3Int Size => _size;
        public Vector3 CellSize => _cellSize;
        public bool Solid => _solid;
        public FireSettings Settings => _settings;
        public int Count => _size.x * _size.y * _size.z;
        public byte[] States => _states;
        public int BurningCellCount => _burningCount;
        public bool IsSurface => _size.y == 1;

        public FireMaterialDef MaterialAt(int i) => _material[i] == 0 ? null : _materials[_material[i] - 1];

        // ---------------------------------------------------------------- setup

        private void Awake()
        {
            Voxelize();
            _states = new byte[Count];
        }

        private void Voxelize()
        {
            var volumes = GetComponentsInChildren<BurnableVolume>(true);
            _material = new byte[Count];
            var table = new List<FireMaterialDef>();
            for (int i = 0; i < Count; i++)
            {
                var c = CellCenter(i);
                FireMaterialDef m = null;
                bool hit = false;
                foreach (var v in volumes)
                    if (v.Contains(c)) { m = v.Material; hit = true; }
                if (!hit || m == null) continue;
                int idx = table.IndexOf(m);
                if (idx < 0) { table.Add(m); idx = table.Count - 1; }
                _material[i] = (byte)(idx + 1);
            }
            _materials = table.ToArray();
        }

        public override void OnNetworkSpawn()
        {
            FireSystem.Grids.Add(this);
            FireSystem.EnsureDebugSection(_debugAgent);
            if (IsServer)
            {
                ResetSim();
            }
            else
            {
                RequestSnapshotRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            FireSystem.Grids.Remove(this);
            FireSystem.RemoveDebugSectionIfEmpty();
        }

        /// <summary>Host: start fresh (all fuel back, nothing burning) and send everyone the full state.</summary>
        public void ResetSim()
        {
            if (!IsServer) return;
            _sim = new FireGridSim(_size.x, _size.y, _size.z, (byte[])_material.Clone(), _materials, _settings, _seed);
            _sim.CollectDirty(_dirty);
            _dirty.Clear();
            Array.Copy(_sim.State, _states, Count);
            _burningCount = 0;
            StatesChanged?.Invoke(null);
            if (IsSpawned) SnapshotRpc(FireSyncCodec.EncodeSnapshot(_states), RpcTarget.NotServer);
        }

        // ---------------------------------------------------------------- host simulation

        private void Update()
        {
            if (!IsSpawned || !IsServer || _sim == null) return;
            float step = 1f / Mathf.Max(1f, _settings.SimRate);
            _simAccumulator += Time.deltaTime;
            int guard = 0;
            while (_simAccumulator >= step && guard++ < 4)
            {
                _simAccumulator -= step;
                _sim.Step(step);
                SpreadToOtherGrids(step);
            }
            if (guard >= 4) _simAccumulator = 0f; // don't spiral if the host hitches

            _syncTimer += Time.deltaTime;
            if (_syncTimer >= 1f / Mathf.Max(0.5f, _settings.SyncRate))
            {
                _syncTimer = 0f;
                FlushDelta();
            }
        }

        private void FlushDelta()
        {
            _dirty.Clear();
            _sim.CollectDirty(_dirty);
            _burningCount = _sim.BurningCount;
            if (_dirty.Count == 0) return;
            _dirty.Sort();
            foreach (int i in _dirty) _states[i] = _sim.State[i];
            StatesChanged?.Invoke(_dirty);
            DeltaRpc(FireSyncCodec.EncodeDelta(_dirty, _sim.State));
        }

        private void SpreadToOtherGrids(float dt)
        {
            if (FireSystem.Grids.Count < 2 || _sim.BurningCount == 0) return;
            for (int i = 0; i < Count; i++)
            {
                if (!_sim.Burning[i]) continue;
                var m = _sim.Mat(i);
                float heat = m.HeatOutput * _settings.Spread * dt;
                // Grids can have different cell sizes, so heat other grids' cells within roughly one cell of us,
                // centred a little above this cell (fire climbs).
                float reach = Mathf.Max(_cellSize.x, Mathf.Max(_cellSize.y, _cellSize.z)) * 1.2f;
                var c = CellCenter(i) + transform.up * (_cellSize.y * 0.5f);
                FireSystem.AddHeatAt(c, heat, reach, this);
            }
        }

        // ---------------------------------------------------------------- host API (via FireSystem)

        /// <summary>Adds heat to cells within <paramref name="radius"/> (falling off with distance).</summary>
        public void AddHeatNear(Vector3 worldPos, float heat, float radius)
        {
            if (_sim == null) return;
            ForCellsInRadius(worldPos, radius, (i, w) => _sim.AddHeat(i, heat * w));
        }

        public void ApplyAgentAt(Vector3 worldPos, float radius, AgentDef agent, float amount)
        {
            if (_sim == null || agent == null) return;
            ForCellsInRadius(worldPos, radius, (i, w) => _sim.ApplyAgent(i, agent, amount * w));
        }

        public void IgniteAt(Vector3 worldPos, float radius)
        {
            if (_sim == null) return;
            ForCellsInRadius(worldPos, radius, (i, w) => _sim.Ignite(i));
        }

        private void ForCellsInRadius(Vector3 worldPos, float radius, Action<int, float> action)
        {
            var local = transform.InverseTransformPoint(worldPos);
            int x0 = Mathf.FloorToInt((local.x - radius) / _cellSize.x), x1 = Mathf.FloorToInt((local.x + radius) / _cellSize.x);
            int y0 = Mathf.FloorToInt((local.y - radius) / _cellSize.y), y1 = Mathf.FloorToInt((local.y + radius) / _cellSize.y);
            int z0 = Mathf.FloorToInt((local.z - radius) / _cellSize.z), z1 = Mathf.FloorToInt((local.z + radius) / _cellSize.z);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(_size.z - 1, z1); z++)
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(_size.y - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_size.x - 1, x1); x++)
            {
                int i = x + _size.x * (y + _size.y * z);
                if (_material[i] == 0) continue;
                float d = Vector3.Distance(CellCenter(i), worldPos);
                if (d > radius) continue;
                action(i, 1f - d / (radius + 0.001f) * 0.7f);
            }
        }

        // ---------------------------------------------------------------- queries (every client)

        public Vector3 CellCenter(int i)
        {
            int x = i % _size.x, y = i / _size.x % _size.y, z = i / (_size.x * _size.y);
            return transform.TransformPoint(new Vector3((x + 0.5f) * _cellSize.x, (y + 0.5f) * _cellSize.y, (z + 0.5f) * _cellSize.z));
        }

        /// <summary>Cheap bounds test (in grid space) before doing per-cell work.</summary>
        public bool MightContain(Vector3 worldPos, float radius)
        {
            var l = transform.InverseTransformPoint(worldPos);
            var size = Vector3.Scale(_size, _cellSize);
            return l.x > -radius && l.y > -radius && l.z > -radius && l.x < size.x + radius && l.y < size.y + radius && l.z < size.z + radius;
        }

        public int CellAtWorld(Vector3 worldPos)
        {
            var l = transform.InverseTransformPoint(worldPos);
            int x = Mathf.FloorToInt(l.x / _cellSize.x), y = Mathf.FloorToInt(l.y / _cellSize.y), z = Mathf.FloorToInt(l.z / _cellSize.z);
            if (x < 0 || y < 0 || z < 0 || x >= _size.x || y >= _size.y || z >= _size.z) return -1;
            return x + _size.x * (y + _size.y * z);
        }

        public bool IsBurningNear(Vector3 worldPos, float radius)
        {
            bool found = false;
            ForCellsInRadius(worldPos, radius, (i, w) => found |= FireCellState.Burning(_states[i]));
            return found;
        }

        // ---------------------------------------------------------------- networking

        [Rpc(SendTo.NotServer)]
        private void DeltaRpc(byte[] data)
        {
            _changed.Clear();
            if (!FireSyncCodec.ApplyDelta(data, _states, _changed)) { Debug.LogWarning($"[Fire] Bad delta on {name}"); return; }
            RecountBurning();
            StatesChanged?.Invoke(_changed);
        }

        [Rpc(SendTo.Server)]
        private void RequestSnapshotRpc(RpcParams rpc = default)
        {
            if (_sim == null) return;
            SnapshotRpc(FireSyncCodec.EncodeSnapshot(_sim.State), RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SnapshotRpc(byte[] data, RpcParams rpc)
        {
            if (!FireSyncCodec.ApplySnapshot(data, _states)) { Debug.LogWarning($"[Fire] Bad snapshot on {name}"); return; }
            RecountBurning();
            StatesChanged?.Invoke(null);
        }

        private void RecountBurning()
        {
            int n = 0;
            foreach (var s in _states) if (FireCellState.Burning(s)) n++;
            _burningCount = n;
        }

        /// <summary>Any client: ask the host to ignite or douse around a point (F1 debug buttons).</summary>
        public void RequestDebug(Vector3 point, bool ignite) => DebugRpc(point, ignite);

        [Rpc(SendTo.Server)]
        private void DebugRpc(Vector3 point, bool ignite)
        {
            if (ignite) FireSystem.IgniteAt(point, 1.3f);
            else FireSystem.ApplyAgentAt(point, 3f, FireSystem.DebugAgent, 4f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            var size = Vector3.Scale(_size, _cellSize);
            Gizmos.DrawWireCube(size * 0.5f, size);
        }
    }
}
