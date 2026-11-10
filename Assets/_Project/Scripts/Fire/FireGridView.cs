using System.Collections.Generic;
using Firehouse.Audio;
using Firehouse.Core;
using UnityEngine;

namespace Firehouse.Fire
{
    /// <summary>
    /// Local-only presentation of a <see cref="FireGrid"/> from its synced cell states: a chunky voxel mesh
    /// (charring, glowing embers, wet darkening, burnt-through holes), a matching collider for solid grids,
    /// low-poly flame/smoke/steam particles, a flickering fire light and crackle audio.
    /// </summary>
    [RequireComponent(typeof(FireGrid), typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FireGridView : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [SerializeField, Range(0f, 1f)] private float _colorVariation = 0.12f;

        private static readonly Color Char = new(0.08f, 0.07f, 0.06f);
        private static readonly Color Ember = new(1f, 0.42f, 0.08f);
        private static readonly Color FlameA = new(1f, 0.88f, 0.25f, 0f);
        private static readonly Color FlameB = new(1f, 0.35f, 0.05f, 0f);
        private static readonly Color SmokeColor = new(0.42f, 0.42f, 0.44f, 1f);
        private static readonly Color SteamColor = new(0.86f, 0.88f, 0.9f, 1f);
        private static Mesh s_flameMesh, s_cubeMesh;

        private FireGrid _grid;
        private Mesh _mesh, _colliderMesh;
        private MeshCollider _collider;
        private readonly MeshBuilder _builder = new();
        private bool _meshDirty = true, _colliderDirty = true;
        private float _nextRebuild;
        private readonly List<int> _burning = new();
        private readonly List<int> _steaming = new();
        private ParticleSystem _flames, _smoke, _steam;
        private Light _light;
        private AudioSource _audio;
        private int _goneCount = -1;

        private void Awake()
        {
            _grid = GetComponent<FireGrid>();
            _mesh = new Mesh { name = $"{name}_FireVoxels" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            if (_material != null) mr.sharedMaterial = _material;
            if (_grid.Solid)
            {
                _colliderMesh = new Mesh { name = $"{name}_FireCollider" };
                _collider = gameObject.AddComponent<MeshCollider>();
            }
            CreateEffects();
            _grid.StatesChanged += OnStatesChanged;
        }

        private void OnDestroy()
        {
            if (_grid != null) _grid.StatesChanged -= OnStatesChanged;
            Destroy(_mesh);
            if (_colliderMesh != null) Destroy(_colliderMesh);
        }

        private void Start() => Rebuild();

        private byte[] _prevStates;
        private readonly Dictionary<int, float> _steamUntil = new();

        private void OnStatesChanged(List<int> changed)
        {
            _meshDirty = true;
            var states = _grid.States;
            _prevStates ??= new byte[states.Length];

            // Water knocking a burning cell down (or soaking a hot one) puffs steam for a few seconds.
            for (int i = 0; i < states.Length; i++)
            {
                byte s = states[i], p = _prevStates[i];
                bool doused = FireCellState.Burning(p) && !FireCellState.Burning(s) && FireCellState.Wet(s);
                if (doused || (FireCellState.Hot(s) && FireCellState.Wet(s))) _steamUntil[i] = Time.time + 2.5f;
            }
            System.Array.Copy(states, _prevStates, states.Length);

            // Recompute the burning list (cheap: grids are small).
            _burning.Clear();
            int gone = 0;
            for (int i = 0; i < states.Length; i++)
            {
                if (FireCellState.Burning(states[i])) _burning.Add(i);
                if (FireCellState.Gone(states[i])) gone++;
            }
            if (gone != _goneCount) { _goneCount = gone; _colliderDirty = true; }
        }

        private readonly List<int> _expired = new();

        private void RefreshSteaming()
        {
            _steaming.Clear();
            _expired.Clear();
            foreach (var kv in _steamUntil)
            {
                if (kv.Value > Time.time) _steaming.Add(kv.Key);
                else _expired.Add(kv.Key);
            }
            foreach (var k in _expired) _steamUntil.Remove(k);
        }

        private void Update()
        {
            if (_meshDirty && Time.time >= _nextRebuild) Rebuild();
            RefreshSteaming();
            EmitParticles();
            UpdateLightAndAudio();
        }

        // ---------------------------------------------------------------- voxel mesh

        private bool IsSolid(int x, int y, int z)
        {
            var size = _grid.Size;
            if (x < 0 || y < 0 || z < 0 || x >= size.x || y >= size.y || z >= size.z) return false;
            int i = x + size.x * (y + size.y * z);
            return _grid.MaterialAt(i) != null && !FireCellState.Gone(_grid.States[i]);
        }

        private void Rebuild()
        {
            _meshDirty = false;
            _nextRebuild = Time.time + 0.2f;
            BuildVoxels(_builder, colored: true);
            _builder.BuildInto(_mesh);
            if (_collider != null && _colliderDirty)
            {
                _colliderDirty = false;
                BuildVoxels(_builder, colored: false);
                _builder.BuildInto(_colliderMesh);
                _collider.sharedMesh = null;
                _collider.sharedMesh = _colliderMesh;
            }
        }

        private void BuildVoxels(MeshBuilder mb, bool colored)
        {
            mb.Clear();
            var size = _grid.Size;
            var cs = _grid.CellSize;
            var states = _grid.States;
            bool surface = _grid.IsSurface;
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                if (!IsSolid(x, y, z)) continue;
                int i = x + size.x * (y + size.y * z);
                float hash = Hash(x, y, z);
                var min = new Vector3(x * cs.x, y * cs.y, z * cs.z);
                var max = min + cs;
                if (surface) max.y = min.y + cs.y * (0.55f + 0.9f * hash); // tufty grass
                var color = colored ? CellColor(_grid.MaterialAt(i), states[i], hash) : Color.white;
                AddCulledBox(mb, min, max, color, x, y, z, surface);
            }
        }

        private void AddCulledBox(MeshBuilder mb, Vector3 a, Vector3 b, Color c, int x, int y, int z, bool surface)
        {
            Vector3 P(float px, float py, float pz) => new(px < 0 ? a.x : b.x, py < 0 ? a.y : b.y, pz < 0 ? a.z : b.z);
            // On surface grids heights vary per cell, so always draw the side faces.
            if (surface || !IsSolid(x, y, z + 1)) mb.AddQuad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), c);
            if (surface || !IsSolid(x, y, z - 1)) mb.AddQuad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), c);
            if (surface || !IsSolid(x + 1, y, z)) mb.AddQuad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), c);
            if (surface || !IsSolid(x - 1, y, z)) mb.AddQuad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), c);
            if (!IsSolid(x, y + 1, z)) mb.AddQuad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), c);
            if (!surface && !IsSolid(x, y - 1, z)) mb.AddQuad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), c);
        }

        private Color CellColor(FireMaterialDef m, byte s, float hash)
        {
            var c = m.Color * (1f - _colorVariation + 2f * _colorVariation * hash);
            c.a = 1f;
            if (FireCellState.Charred(s)) c = Color.Lerp(c, Char, 0.85f);
            if (FireCellState.Wet(s)) c = new Color(c.r * 0.6f, c.g * 0.62f, c.b * 0.7f, 1f);
            if (FireCellState.Burning(s))
            {
                c = Color.Lerp(c, Ember, 0.35f + 0.15f * FireCellState.Intensity(s));
                c.a = 0.45f; // vertex alpha < 1 = glow (see FlatVertexColor.shader)
            }
            else if (FireCellState.Hot(s))
            {
                c = Color.Lerp(c, Ember, 0.25f);
                c.a = 0.8f;
            }
            return c;
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ---------------------------------------------------------------- particles, light, audio

        private void CreateEffects()
        {
            s_flameMesh ??= new MeshBuilder().AddSphere(Vector3.zero, new Vector3(0.5f, 0.7f, 0.5f), 4, 2, Color.white).Build("FlameParticle");
            s_cubeMesh ??= new MeshBuilder().AddBox(Vector3.zero, Vector3.one, Color.white).Build("SmokeParticle");

            _flames = CreateSystem("Flames", s_flameMesh, 2500, -0.4f, shrink: true,
                ParticleMaterial(ref s_flameMat, "M_FireFlame", new Color(1f, 0.55f, 0.1f), new Color(2.6f, 1.1f, 0.15f)));
            _smoke = CreateSystem("Smoke", s_cubeMesh, 1500, 0f, shrink: false,
                ParticleMaterial(ref s_smokeMat, "M_FireSmoke", new Color(0.42f, 0.42f, 0.44f), Color.black));
            _steam = CreateSystem("Steam", s_cubeMesh, 600, 0f, shrink: false,
                ParticleMaterial(ref s_steamMat, "M_FireSteam", new Color(0.92f, 0.93f, 0.95f), new Color(0.1f, 0.1f, 0.1f)));

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.55f, 0.2f);
            _light.range = 10f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;

            _audio = lightGo.AddComponent<AudioSource>();
            _audio.clip = ProceduralAudio.FireCrackle;
            _audio.loop = true;
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 3f;
            _audio.maxDistance = 45f;
            _audio.volume = 0f;
            _audio.playOnAwake = false;
        }

        private static Material s_flameMat, s_smokeMat, s_steamMat;

        /// <summary>Copy of the shared material that ignores vertex colors and uses a flat tint (+ glow).</summary>
        private Material ParticleMaterial(ref Material cache, string n, Color baseColor, Color glow)
        {
            if (cache != null) return cache;
            cache = new Material(_material) { name = n };
            cache.SetFloat("_UseVertexColor", 0f);
            cache.SetColor("_BaseColor", baseColor);
            cache.SetColor("_GlowColor", glow);
            return cache;
        }

        private ParticleSystem CreateSystem(string n, Mesh mesh, int max, float gravity, bool shrink, Material material)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = shrink
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f))
                : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.6f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            // Per-particle vertex colors didn't arrive intact with mesh particles + this shader, so each effect
            // gets its own material color instead (see ParticleMaterial) and instancing stays off.
            r.enableGPUInstancing = false;
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private void EmitParticles()
        {
            var settings = _grid.Settings;
            if (settings == null) return;
            float dt = Time.deltaTime;
            var cs = _grid.CellSize;
            float cellScale = (cs.x + cs.z) * 0.5f;
            // Keep huge fires readable without melting the GPU (and without smoke burying the flames).
            float budget = Mathf.Min(1f, 260f / Mathf.Max(1, _burning.Count));
            float smokeBudget = Mathf.Min(1f, 45f / Mathf.Max(1f, _burning.Count * settings.SmokeParticlesPerCell * 2f));
            var wind = settings.WindDirection.normalized * settings.WindStrength;
            var states = _grid.States;

            foreach (int i in _burning)
            {
                int intensity = FireCellState.Intensity(states[i]);
                var center = _grid.CellCenter(i);
                float flames = settings.FlameParticlesPerCell * (0.4f + 0.3f * intensity) * dt * budget;
                Emit(_flames, flames, center, cs, cellScale * (0.45f + 0.15f * intensity), Vector3.up * (1.2f + 0.4f * intensity) + wind * 0.5f,
                    0.45f, 0.85f, FlameA, FlameB);
                float smoke = settings.SmokeParticlesPerCell * intensity * dt * smokeBudget;
                Emit(_smoke, smoke, center + Vector3.up * (cs.y + 1.2f), cs, cellScale * 0.55f, Vector3.up * 2.2f + wind * 1.4f,
                    2.5f, 4f, SmokeColor, SmokeColor * 1.4f);
            }
            foreach (int i in _steaming)
            {
                Emit(_steam, 2.5f * dt, _grid.CellCenter(i), cs, cellScale * 0.4f, Vector3.up * 1.8f + wind, 1.2f, 2f, SteamColor, SteamColor);
            }
        }

        private static void Emit(ParticleSystem ps, float expected, Vector3 center, Vector3 cellSize, float size, Vector3 velocity,
            float lifeMin, float lifeMax, Color a, Color b)
        {
            int n = Mathf.FloorToInt(expected);
            if (Random.value < expected - n) n++;
            for (int k = 0; k < n; k++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = center + new Vector3((Random.value - 0.5f) * cellSize.x, (Random.value - 0.2f) * cellSize.y, (Random.value - 0.5f) * cellSize.z),
                    velocity = velocity + Random.insideUnitSphere * 0.4f,
                    startSize = size * Random.Range(0.7f, 1.3f),
                    startLifetime = Random.Range(lifeMin, lifeMax),
                    startColor = Color.Lerp(a, b, Random.value),
                    applyShapeToPosition = false,
                };
                ps.Emit(ep, 1);
            }
        }

        private void UpdateLightAndAudio()
        {
            int n = _burning.Count;
            if (n == 0)
            {
                if (_light.enabled) _light.enabled = false;
                if (_audio.isPlaying) _audio.volume = Mathf.MoveTowards(_audio.volume, 0f, Time.deltaTime);
                if (_audio.isPlaying && _audio.volume <= 0f) _audio.Stop();
                return;
            }
            var centroid = Vector3.zero;
            // Sample up to 32 cells for the centroid.
            int step = Mathf.Max(1, n / 32), count = 0;
            for (int k = 0; k < n; k += step) { centroid += _grid.CellCenter(_burning[k]); count++; }
            centroid /= count;
            _light.transform.position = centroid + Vector3.up * 1f;
            _light.enabled = true;
            float flicker = 0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 7f, 0.3f);
            _light.intensity = Mathf.Min(7f, 1.5f + n * 0.06f) * flicker;
            _light.range = Mathf.Min(25f, 6f + Mathf.Sqrt(n) * 1.5f);
            if (!_audio.isPlaying) _audio.Play();
            _audio.volume = Mathf.MoveTowards(_audio.volume, Mathf.Min(1f, 0.25f + n * 0.02f), Time.deltaTime);
        }
    }
}
