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

        /// <summary>
        /// Straight staircase. Steps are visual only; collision is one smooth ramp so the rigidbody player
        /// (which has no step-up logic) walks up without catching on step edges.
        /// </summary>
        public void Stairs(string group, string name, Vector3 start, Vector3 dir, float width, float run, float rise, int steps, Color color)
        {
            dir = dir.normalized;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            float stepRun = run / steps, stepRise = rise / steps;
            for (int i = 0; i < steps; i++)
            {
                var a = start + dir * (i * stepRun) - side * (width * 0.5f);
                var b = start + dir * ((i + 1) * stepRun) + side * (width * 0.5f) + Vector3.up * ((i + 1) * stepRise);
                Box(group, $"{name}_{i}", Vector3.Min(a, b), Vector3.Max(a, b), color, collider: false);
            }
            // Ramp through the step nosings.
            var slope = dir * run + Vector3.up * rise;
            const float thickness = 0.2f;
            var rot = Quaternion.LookRotation(slope.normalized, Vector3.Cross(slope, side).normalized);
            if (Vector3.Dot(rot * Vector3.up, Vector3.up) < 0) rot = Quaternion.LookRotation(slope.normalized, -Vector3.Cross(slope, side).normalized);
            var center = start + slope * 0.5f - (rot * Vector3.up) * (thickness * 0.5f);
            var go = new GameObject($"{name}_Ramp");
            go.transform.SetParent(_colliders, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = new Vector3(width, thickness, slope.magnitude);
        }

        /// <summary>Adds a ladder/pole climb volume (Ladder component + trigger box on the Trigger layer).</summary>
        public GameObject ClimbVolume(string name, Vector3 center, Vector3 size, Vector3 facing, bool pole)
        {
            var go = new GameObject(name) { layer = Firehouse.Core.Layers.Trigger };
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(facing, Vector3.up));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<Firehouse.Player.Ladder>().IsPole = pole;
            return go;
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
