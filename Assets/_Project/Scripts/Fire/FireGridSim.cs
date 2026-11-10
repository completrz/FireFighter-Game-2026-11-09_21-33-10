using System;
using System.Collections.Generic;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>
    /// Host-only grid fire simulation (plain C#, unit-tested). Each cell has a material, heat, fuel and wetness.
    /// Burning cells consume fuel and push heat into their 6 neighbors (more upward and downwind);
    /// cells ignite above their material's ignition temperature and go out when cooled well below it.
    /// Water removes heat and leaves cells wet; heat must boil the wetness off before it can warm the cell.
    ///
    /// The sim also derives a 1-byte <see cref="FireCellState"/> per cell and tracks which ones changed,
    /// which is all clients ever receive.
    /// </summary>
    public sealed class FireGridSim
    {
        public readonly int SizeX, SizeY, SizeZ, Count;
        public readonly byte[] Material;          // 0 = empty, else 1-based index into Materials
        public readonly FireMaterialDef[] Materials;
        public readonly float[] Heat, Fuel, Wet;
        public readonly bool[] Burning;
        public readonly byte[] State;
        public FireSettings Settings;

        private readonly float[] _incoming;
        private readonly bool[] _dirtyFlag;
        private readonly List<int> _dirty = new();
        private readonly System.Random _rng;
        private int _burningCount;

        public int BurningCount => _burningCount;

        public FireGridSim(int sx, int sy, int sz, byte[] material, FireMaterialDef[] materials, FireSettings settings, int seed = 1)
        {
            SizeX = sx; SizeY = sy; SizeZ = sz; Count = sx * sy * sz;
            if (material.Length != Count) throw new ArgumentException("material array size mismatch");
            Material = material;
            Materials = materials;
            Settings = settings;
            Heat = new float[Count];
            Fuel = new float[Count];
            Wet = new float[Count];
            Burning = new bool[Count];
            State = new byte[Count];
            _incoming = new float[Count];
            _dirtyFlag = new bool[Count];
            _rng = new System.Random(seed);
            for (int i = 0; i < Count; i++)
            {
                var m = Mat(i);
                Fuel[i] = m != null ? m.Fuel : 0f;
                State[i] = ComputeState(i);
            }
        }

        public FireMaterialDef Mat(int i) => Material[i] == 0 ? null : Materials[Material[i] - 1];
        public int Index(int x, int y, int z) => x + SizeX * (y + SizeY * z);
        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SizeX && y < SizeY && z < SizeZ;

        public void Coords(int i, out int x, out int y, out int z)
        {
            x = i % SizeX;
            y = i / SizeX % SizeY;
            z = i / (SizeX * SizeY);
        }

        // ---------------------------------------------------------------- external inputs

        /// <summary>Adds heat (from neighbors in other grids, explosions, a lighter...). Wetness absorbs it first.</summary>
        public void AddHeat(int i, float heat)
        {
            if (Material[i] == 0 || heat <= 0f) return;
            _incoming[i] += heat;
        }

        /// <summary>Sets a cell on fire immediately (debug, arson, sparks).</summary>
        public void Ignite(int i)
        {
            var m = Mat(i);
            if (m == null || Fuel[i] <= 0f) return;
            Wet[i] = 0f;
            Heat[i] = Mathf.Max(Heat[i], m.IgnitionTemp * 1.3f);
            if (!Burning[i]) { Burning[i] = true; _burningCount++; }
            Refresh(i);
        }

        /// <summary>Applies an extinguishing agent to a cell. Returns the class-adjusted effectiveness used (0..1+).</summary>
        public float ApplyAgent(int i, AgentDef agent, float amount)
        {
            var m = Mat(i);
            if (m == null || amount <= 0f) return 0f;
            float eff = agent.EffectivenessFor(m.Class);
            Heat[i] = Mathf.Max(0f, Heat[i] - amount * agent.Cooling * eff);
            Wet[i] = Mathf.Min(Settings.MaxWet, Wet[i] + amount * agent.Wetting * Mathf.Max(eff, 0.25f));
            if (Burning[i] && Heat[i] < m.IgnitionTemp * Settings.ExtinguishFraction) { Burning[i] = false; _burningCount--; }
            Refresh(i);
            return eff;
        }

        // ---------------------------------------------------------------- simulation

        public void Step(float dt)
        {
            var s = Settings;
            var wind = s.WindDirection.sqrMagnitude > 0f ? s.WindDirection.normalized * s.WindStrength : Vector3.zero;

            // 1) Burning cells consume fuel, heat up, and push heat to neighbors.
            for (int i = 0; i < Count; i++)
            {
                if (!Burning[i]) continue;
                var m = Mat(i);
                Fuel[i] -= m.BurnRate * dt;
                if (Fuel[i] <= 0f)
                {
                    Fuel[i] = 0f;
                    Burning[i] = false;
                    _burningCount--;
                    Heat[i] *= 0.4f;
                    Refresh(i);
                    continue;
                }
                Heat[i] = Mathf.Min(s.MaxHeat, Heat[i] + m.HeatOutput * dt);
                float emit = m.HeatOutput * s.Spread * dt;
                Coords(i, out int x, out int y, out int z);
                Push(x + 1, y, z, emit * (1f + Mathf.Max(0f, wind.x)));
                Push(x - 1, y, z, emit * (1f + Mathf.Max(0f, -wind.x)));
                Push(x, y + 1, z, emit * s.UpwardBias);
                Push(x, y - 1, z, emit * s.DownwardBias);
                Push(x, y, z + 1, emit * (1f + Mathf.Max(0f, wind.z)));
                Push(x, y, z - 1, emit * (1f + Mathf.Max(0f, -wind.z)));
            }

            // 2) Every cell: absorb incoming heat (wetness first), cool, dry, ignite or go out.
            for (int i = 0; i < Count; i++)
            {
                if (Material[i] == 0) continue;
                var m = Mat(i);
                float inc = _incoming[i];
                _incoming[i] = 0f;
                if (inc > 0f)
                {
                    if (Wet[i] > 0f)
                    {
                        float boil = Mathf.Min(Wet[i], inc / s.HeatPerWet);
                        Wet[i] -= boil;
                        inc -= boil * s.HeatPerWet;
                    }
                    Heat[i] = Mathf.Min(s.MaxHeat, Heat[i] + Mathf.Max(0f, inc));
                }
                Heat[i] -= Heat[i] * s.AmbientCooling * dt;
                if (Wet[i] > 0f) Wet[i] = Mathf.Max(0f, Wet[i] - s.DryRate * dt);

                if (!Burning[i] && Fuel[i] > 0f && Heat[i] >= m.IgnitionTemp && Wet[i] <= 0.01f)
                {
                    Burning[i] = true;
                    _burningCount++;
                }
                else if (Burning[i] && Heat[i] < m.IgnitionTemp * s.ExtinguishFraction)
                {
                    Burning[i] = false;
                    _burningCount--;
                }
                Refresh(i);
            }
        }

        private void Push(int x, int y, int z, float heat)
        {
            if (!InBounds(x, y, z)) return;
            int n = Index(x, y, z);
            if (Material[n] == 0 || Fuel[n] <= 0f) return;
            float r = Settings.Randomness;
            _incoming[n] += heat * (1f + r * ((float)_rng.NextDouble() * 2f - 1f));
        }

        // ---------------------------------------------------------------- visual state + change tracking

        private void Refresh(int i)
        {
            byte s = ComputeState(i);
            if (s == State[i]) return;
            State[i] = s;
            if (!_dirtyFlag[i]) { _dirtyFlag[i] = true; _dirty.Add(i); }
        }

        private byte ComputeState(int i)
        {
            var m = Mat(i);
            if (m == null) return 0;
            int intensity = 0;
            if (Burning[i])
            {
                float over = (Heat[i] - m.IgnitionTemp) / Mathf.Max(1f, Settings.MaxHeat - m.IgnitionTemp);
                intensity = Mathf.Clamp(1 + Mathf.FloorToInt(over * 3f), 1, 3);
            }
            bool gone = Fuel[i] <= 0f && m.RemoveWhenBurntOut;
            bool charred = Fuel[i] < m.Fuel * 0.6f;
            bool wet = Wet[i] > 0.5f;
            bool hot = !Burning[i] && Heat[i] > m.IgnitionTemp * 0.45f;
            return FireCellState.Pack(intensity, charred, gone, wet, hot);
        }

        /// <summary>Moves the indices of cells whose visible state changed into <paramref name="output"/>.</summary>
        public void CollectDirty(List<int> output)
        {
            foreach (int i in _dirty) _dirtyFlag[i] = false;
            output.AddRange(_dirty);
            _dirty.Clear();
        }
    }

    /// <summary>The 1-byte per-cell state clients receive.</summary>
    public static class FireCellState
    {
        // bits 0-1 intensity (0 = not burning), 2 charred, 3 gone, 4 wet, 5 hot (smouldering / steaming)
        public static byte Pack(int intensity, bool charred, bool gone, bool wet, bool hot) =>
            (byte)((intensity & 3) | (charred ? 4 : 0) | (gone ? 8 : 0) | (wet ? 16 : 0) | (hot ? 32 : 0));

        public static int Intensity(byte s) => s & 3;
        public static bool Burning(byte s) => (s & 3) != 0;
        public static bool Charred(byte s) => (s & 4) != 0;
        public static bool Gone(byte s) => (s & 8) != 0;
        public static bool Wet(byte s) => (s & 16) != 0;
        public static bool Hot(byte s) => (s & 32) != 0;
    }
}
