using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firehouse.World
{
    /// <summary>
    /// Deterministic low-poly terrain with a winding dirt road (potholes, washboard, jump humps, a cattle grate)
    /// and a creek. Generated on every client from the same parameters, so nothing is networked and the scene
    /// file stays tiny. [ExecuteAlways] so it also shows in the editor (the mesh is never saved).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ProceduralTerrain : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private float _size = 260f;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private int _seed = 1234;

        [Header("Hills")]
        [SerializeField] private float _hillHeight = 7f;
        [SerializeField] private float _hillScale = 0.008f;
        [SerializeField] private float _detailHeight = 0.35f;

        [Header("Road")]
        [SerializeField] private Vector2[] _roadPoints =
        {
            new(-20, -118), new(0, -92), new(26, -62), new(20, -30), new(-14, -2),
            new(-24, 30), new(2, 56), new(32, 80), new(36, 112)
        };
        [SerializeField] private float _roadHalfWidth = 3.6f;
        [SerializeField] private float _roadShoulder = 3f;
        [SerializeField] private int _potholes = 45;
        [SerializeField] private Vector2 _washboardRange = new(60f, 95f);   // meters along the road
        [SerializeField] private float[] _humpsAt = { 140f, 152f, 270f };  // meters along the road
        [SerializeField] private float _cattleGrateAt = 205f;

        [Header("Creek (crosses the road; the bridge is placed by the scene builder)")]
        [SerializeField] private float _creekZ = 12f;
        [SerializeField] private float _creekDepth = 3.2f;
        [SerializeField] private float _creekHalfWidth = 5f;

        [Header("Colors")]
        [SerializeField] private Color _grassA = new(0.42f, 0.62f, 0.28f);
        [SerializeField] private Color _grassB = new(0.52f, 0.68f, 0.30f);
        [SerializeField] private Color _dirt = new(0.62f, 0.47f, 0.30f);
        [SerializeField] private Color _dirtDark = new(0.45f, 0.33f, 0.21f);
        [SerializeField] private Color _mud = new(0.33f, 0.36f, 0.30f);

        private readonly List<Vector2> _road = new();     // dense polyline
        private readonly List<float> _roadDist = new();   // cumulative distance at each point
        private readonly List<Vector4> _holes = new();    // x, z, radius, depth
        private Mesh _mesh;

        public float RoadLength => _roadDist.Count > 0 ? _roadDist[^1] : 0f;
        public float CreekZ => _creekZ;

        private void OnEnable() => Generate();

        private void OnDisable()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            }
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Generate();
        }

        // ------------------------------------------------------------------ public sampling (used by builders)

        /// <summary>Ground height at a world XZ position (relative to this transform's Y).</summary>
        public float SampleHeight(float x, float z)
        {
            EnsureRoad();
            return transform.position.y + Height(x - transform.position.x, z - transform.position.z, out _, out _);
        }

        /// <summary>Point and tangent (XZ, world space) at <paramref name="distance"/> meters along the road.</summary>
        public void RoadPointAt(float distance, out Vector3 point, out Vector3 tangent)
        {
            EnsureRoad();
            distance = Mathf.Clamp(distance, 0f, RoadLength);
            int i = 1;
            while (i < _roadDist.Count - 1 && _roadDist[i] < distance) i++;
            float t = Mathf.InverseLerp(_roadDist[i - 1], _roadDist[i], distance);
            var p = Vector2.Lerp(_road[i - 1], _road[i], t);
            var d = (_road[i] - _road[i - 1]).normalized;
            var o = transform.position;
            point = new Vector3(p.x + o.x, SampleHeight(p.x + o.x, p.y + o.z), p.y + o.z);
            tangent = new Vector3(d.x, 0f, d.y);
        }

        /// <summary>Distance along the road where it crosses the creek (first crossing), or -1.</summary>
        public float CreekCrossingDistance()
        {
            EnsureRoad();
            for (int i = 1; i < _road.Count; i++)
                if ((_road[i - 1].y - _creekZ) * (_road[i].y - _creekZ) <= 0f)
                {
                    float t = Mathf.InverseLerp(_road[i - 1].y, _road[i].y, _creekZ);
                    return Mathf.Lerp(_roadDist[i - 1], _roadDist[i], t);
                }
            return -1f;
        }

        // ------------------------------------------------------------------ generation

        public void Generate()
        {
            _road.Clear();
            EnsureRoad();

            int n = Mathf.Max(2, Mathf.RoundToInt(_size / _cellSize) + 1);
            float half = _size * 0.5f;
            var verts = new Vector3[n * n];
            var colors = new Color[n * n];
            var rng = new System.Random(_seed + 99);
            for (int zi = 0; zi < n; zi++)
            for (int xi = 0; xi < n; xi++)
            {
                float x = -half + xi * _cellSize, z = -half + zi * _cellSize;
                float h = Height(x, z, out float roadBlend, out float holeDepth);
                verts[zi * n + xi] = new Vector3(x, h, z);

                float creek = CreekFactor(z);
                float jitter = (float)rng.NextDouble();
                var grass = Color.Lerp(_grassA, _grassB, Mathf.PerlinNoise(x * 0.05f + 300, z * 0.05f + 300) * 0.8f + jitter * 0.2f);
                var dirt = Color.Lerp(_dirt, _dirtDark, Mathf.Clamp01(holeDepth * 4f) + jitter * 0.15f);
                var c = Color.Lerp(grass, dirt, roadBlend);
                c = Color.Lerp(c, _mud, creek * (1f - roadBlend * 0.5f));
                colors[zi * n + xi] = c;
            }

            var tris = new int[(n - 1) * (n - 1) * 6];
            int k = 0;
            for (int zi = 0; zi < n - 1; zi++)
            for (int xi = 0; xi < n - 1; xi++)
            {
                int a = zi * n + xi, b = a + 1, c = a + n, d = c + 1;
                // Alternate the diagonal for a less regular low-poly look.
                if (((xi + zi) & 1) == 0) { tris[k++] = a; tris[k++] = c; tris[k++] = b; tris[k++] = b; tris[k++] = c; tris[k++] = d; }
                else { tris[k++] = a; tris[k++] = c; tris[k++] = d; tris[k++] = a; tris[k++] = d; tris[k++] = b; }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ProceduralTerrain", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            }
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.colors = colors;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshCollider>().sharedMesh = null;
            GetComponent<MeshCollider>().sharedMesh = _mesh;
        }

        private void EnsureRoad()
        {
            if (_road.Count > 0) return;
            _roadDist.Clear();
            _holes.Clear();
            // Catmull-Rom through the control points, sampled every ~1 m.
            var pts = _roadPoints;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var p0 = pts[Mathf.Max(0, i - 1)]; var p1 = pts[i]; var p2 = pts[i + 1]; var p3 = pts[Mathf.Min(pts.Length - 1, i + 2)];
                int steps = Mathf.Max(4, Mathf.CeilToInt(Vector2.Distance(p1, p2)));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    _road.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            _road.Add(pts[^1]);
            float acc = 0f;
            _roadDist.Add(0f);
            for (int i = 1; i < _road.Count; i++) { acc += Vector2.Distance(_road[i - 1], _road[i]); _roadDist.Add(acc); }

            // Potholes scattered along the road (skipping the start so the truck can get going).
            var rng = new System.Random(_seed);
            for (int i = 0; i < _potholes; i++)
            {
                float d = Mathf.Lerp(25f, acc - 10f, (float)rng.NextDouble());
                int idx = IndexAt(d);
                var dir = idx < _road.Count - 1 ? (_road[idx + 1] - _road[idx]).normalized : Vector2.up;
                var side = new Vector2(-dir.y, dir.x);
                var c = _road[idx] + side * Mathf.Lerp(-_roadHalfWidth * 0.8f, _roadHalfWidth * 0.8f, (float)rng.NextDouble());
                float r = Mathf.Lerp(0.5f, 1.3f, (float)rng.NextDouble());
                float depth = Mathf.Lerp(0.15f, 0.35f, (float)rng.NextDouble());
                _holes.Add(new Vector4(c.x, c.y, r, depth));
            }
        }

        private int IndexAt(float distance)
        {
            int i = _roadDist.BinarySearch(distance);
            if (i < 0) i = ~i;
            return Mathf.Clamp(i, 0, _road.Count - 1);
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private float Hills(float x, float z)
        {
            float o = _seed * 0.137f;
            float h = (Mathf.PerlinNoise(x * _hillScale + o, z * _hillScale + o) - 0.5f) * 2f
                    + (Mathf.PerlinNoise(x * _hillScale * 2.3f + o + 50, z * _hillScale * 2.3f + o + 50) - 0.5f) * 0.6f;
            return h * _hillHeight;
        }

        private float CreekFactor(float z)
        {
            float d = Mathf.Abs(z - _creekZ);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_creekHalfWidth * 0.4f, _creekHalfWidth, d));
        }

        /// <summary>Local height at local (x, z). Also returns how "road" this spot is (0..1) and pothole depth.</summary>
        private float Height(float x, float z, out float roadBlend, out float holeDepth)
        {
            // Nearest road point (brute force over the dense polyline; fine for generation-time cost).
            float best = float.MaxValue; int bestI = 0; float bestT = 0f;
            var p = new Vector2(x, z);
            for (int i = 1; i < _road.Count; i++)
            {
                var a = _road[i - 1]; var ab = _road[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                float d2 = (a + ab * t - p).sqrMagnitude;
                if (d2 < best) { best = d2; bestI = i; bestT = t; }
            }
            float dist = Mathf.Sqrt(best);
            var near = Vector2.Lerp(_road[bestI - 1], _road[bestI], bestT);
            float along = Mathf.Lerp(_roadDist[bestI - 1], _roadDist[bestI], bestT);

            float terrain = Hills(x, z) + (Mathf.PerlinNoise(x * 0.07f + 11, z * 0.07f + 7) - 0.5f) * 2f * _detailHeight;
            float roadBase = Hills(near.x, near.y) * 0.85f;

            // Road surface features
            float feature = 0f;
            if (along > _washboardRange.x && along < _washboardRange.y)
                feature += 0.05f * Mathf.Sin(along * Mathf.PI * 2f / 0.9f);
            foreach (var hump in _humpsAt)
            {
                float u = (along - hump) / 2.2f;
                if (Mathf.Abs(u) < 1f) feature += 0.55f * (0.5f + 0.5f * Mathf.Cos(u * Mathf.PI));
            }
            float g = along - _cattleGrateAt;
            if (g > -1.5f && g < 1.5f) feature += Mathf.Repeat(along * 4f, 1f) < 0.3f ? 0.07f : 0f;

            holeDepth = 0f;
            foreach (var h in _holes)
            {
                float hd = Vector2.Distance(p, new Vector2(h.x, h.y));
                if (hd < h.z) holeDepth = Mathf.Max(holeDepth, h.w * (0.5f + 0.5f * Mathf.Cos(hd / h.z * Mathf.PI)));
            }

            float roadH = roadBase + feature - holeDepth;
            roadBlend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_roadHalfWidth, _roadHalfWidth + _roadShoulder, dist));
            float h2 = Mathf.Lerp(terrain, roadH, roadBlend);

            // Creek cuts through everything (the bridge spans it).
            h2 -= CreekFactor(z) * _creekDepth;
            return h2;
        }
    }
}
