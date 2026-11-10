using System.Collections.Generic;
using Firehouse.Core;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Level blockout helper: boxes are batched into one vertex-colored render mesh per group,
    /// and each box gets its own BoxCollider child (so colliders stay simple and exact).
    /// Coordinates are world-space min/max corners in meters.
    /// </summary>
    public sealed class Blockout
    {
        public struct Opening
        {
            public float From, To;     // along the wall
            public float Bottom, Top;  // height
            public Opening(float from, float to, float bottom, float top) { From = from; To = to; Bottom = bottom; Top = top; }
        }

        private readonly Transform _root;
        private readonly Transform _colliders;
        private readonly Dictionary<string, MeshBuilder> _groups = new();
        private readonly Material _material;

        public Blockout(Transform root, Material material)
        {
            _root = root;
            _material = material;
            _colliders = new GameObject("Colliders").transform;
            _colliders.SetParent(root, false);
        }

        private MeshBuilder Group(string group)
        {
            if (!_groups.TryGetValue(group, out var mb)) _groups[group] = mb = new MeshBuilder();
            return mb;
        }

        public void Box(string group, string name, Vector3 min, Vector3 max, Color color, bool collider = true)
        {
            var center = (min + max) * 0.5f;
            var size = max - min;
            if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f) return;
            Group(group).AddBox(center, size, color);
            if (!collider) return;
            var go = new GameObject(name);
            go.transform.SetParent(_colliders, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Wall running along X at depth z (thickness along Z), with rectangular openings.</summary>
        public void WallX(string group, string name, float z, float x0, float x1, float y0, float y1, float thickness, Color color, params Opening[] openings)
        {
            foreach (var (a, b, c, d) in SplitWall(x0, x1, y0, y1, openings))
                Box(group, name, new Vector3(a, c, z - thickness * 0.5f), new Vector3(b, d, z + thickness * 0.5f), color);
        }

        /// <summary>Wall running along Z at x (thickness along X), with rectangular openings.</summary>
        public void WallZ(string group, string name, float x, float z0, float z1, float y0, float y1, float thickness, Color color, params Opening[] openings)
        {
            foreach (var (a, b, c, d) in SplitWall(z0, z1, y0, y1, openings))
                Box(group, name, new Vector3(x - thickness * 0.5f, c, a), new Vector3(x + thickness * 0.5f, d, b), color);
        }

        /// <summary>Splits a wall rectangle into solid pieces around the openings: (from, to, bottom, top).</summary>
        private static IEnumerable<(float, float, float, float)> SplitWall(float s0, float s1, float y0, float y1, Opening[] openings)
        {
            var sorted = new List<Opening>(openings);
            sorted.Sort((p, q) => p.From.CompareTo(q.From));
            float cursor = s0;
            foreach (var o in sorted)
            {
                if (o.From > cursor) yield return (cursor, o.From, y0, y1);              // full-height column
                if (o.Bottom > y0) yield return (o.From, o.To, y0, o.Bottom);            // sill
                if (o.Top < y1) yield return (o.From, o.To, o.Top, y1);                  // lintel
                cursor = Mathf.Max(cursor, o.To);
            }
            if (cursor < s1) yield return (cursor, s1, y0, y1);
        }

        /// <summary>Straight staircase rising along +X or +Z. Each step is a box from the floor up (solid, no gaps).</summary>
        public void Stairs(string group, string name, Vector3 start, Vector3 dir, float width, float run, float rise, int steps, Color color)
        {
            dir = dir.normalized;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            float stepRun = run / steps, stepRise = rise / steps;
            for (int i = 0; i < steps; i++)
            {
                var a = start + dir * (i * stepRun) - side * (width * 0.5f);
                var b = start + dir * ((i + 1) * stepRun) + side * (width * 0.5f) + Vector3.up * ((i + 1) * stepRise);
                Box(group, $"{name}_{i}", Vector3.Min(a, b), Vector3.Max(a, b), color);
            }
        }

        public void AddMeshPart(string group, System.Action<MeshBuilder> build) => build(Group(group));

        /// <summary>Saves the batched group meshes and creates renderers for them.</summary>
        public void Finish(string meshPrefix)
        {
            foreach (var kv in _groups)
            {
                var mesh = ProjectAssets.SaveMesh(kv.Value.Build(), $"{meshPrefix}_{kv.Key}");
                var go = new GameObject(kv.Key);
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
                go.isStatic = true;
            }
            _colliders.gameObject.isStatic = true;
            foreach (Transform t in _colliders) t.gameObject.isStatic = true;
        }
    }
}
