using System.Collections.Generic;
using UnityEngine;

namespace Firehouse.Core
{
    /// <summary>
    /// Builds low-poly vertex-colored meshes out of simple parts (boxes, cylinders, spheres, capsules).
    /// Each part gets its own vertices so it keeps a solid color; the shared shader does the faceting.
    /// Vertex alpha below 1 marks a part as emissive (0 = full glow), see FlatVertexColor.shader.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _verts = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _tris = new();

        public Matrix4x4 Transform = Matrix4x4.identity;

        public int VertexCount => _verts.Count;
        public int TriangleCount => _tris.Count / 3;

        public MeshBuilder SetTRS(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            Transform = Matrix4x4.TRS(pos, rot, scale);
            return this;
        }

        public MeshBuilder ResetTransform()
        {
            Transform = Matrix4x4.identity;
            return this;
        }

        private int AddVertex(Vector3 p, Vector3 n, Color c)
        {
            _verts.Add(Transform.MultiplyPoint3x4(p));
            _normals.Add(Transform.MultiplyVector(n).normalized);
            _colors.Add(c);
            return _verts.Count - 1;
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = AddVertex(a, n, color);
            AddVertex(b, n, color);
            AddVertex(c, n, color);
            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = AddVertex(a, n, color);
            AddVertex(b, n, color);
            AddVertex(c, n, color);
            AddVertex(d, n, color);
            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
        }

        /// <summary>Axis-aligned box centered on <paramref name="center"/> (in builder space).</summary>
        public MeshBuilder AddBox(Vector3 center, Vector3 size, Color color)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            AddQuad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), color);     // +Z
            AddQuad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), color); // -Z
            AddQuad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), color);     // +X
            AddQuad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), color); // -X
            AddQuad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), color);     // +Y
            AddQuad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), color); // -Y
            return this;
        }

        /// <summary>Cylinder along local Y, centered on <paramref name="center"/>. Radii can differ to make cones/frustums.</summary>
        public MeshBuilder AddCylinder(Vector3 center, float radiusBottom, float radiusTop, float height, int sides, Color color, bool caps = true)
        {
            sides = Mathf.Max(3, sides);
            float y0 = -height * 0.5f, y1 = height * 0.5f;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                var b0 = center + d0 * radiusBottom + Vector3.up * y0;
                var b1 = center + d1 * radiusBottom + Vector3.up * y0;
                var t0 = center + d0 * radiusTop + Vector3.up * y1;
                var t1 = center + d1 * radiusTop + Vector3.up * y1;
                AddQuad(b1, b0, t0, t1, color);
                if (caps)
                {
                    if (radiusTop > 0f) AddTriangle(center + Vector3.up * y1, t1, t0, color);
                    if (radiusBottom > 0f) AddTriangle(center + Vector3.up * y0, b0, b1, color);
                }
            }
            return this;
        }

        /// <summary>Low-poly UV sphere (an ellipsoid if <paramref name="radii"/> differ).</summary>
        public MeshBuilder AddSphere(Vector3 center, Vector3 radii, int segments, int rings, Color color)
        {
            segments = Mathf.Max(3, segments);
            rings = Mathf.Max(2, rings);
            Vector3 P(int ring, int seg)
            {
                float v = Mathf.PI * ring / rings;
                float u = Mathf.PI * 2f * seg / segments;
                var d = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                return center + Vector3.Scale(d, radii);
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                var a = P(r, s); var b = P(r, s + 1); var c = P(r + 1, s + 1); var d = P(r + 1, s);
                if (r == 0) AddTriangle(a, c, d, color);
                else if (r == rings - 1) AddTriangle(a, b, d, color);
                else AddQuad(a, b, c, d, color);
            }
            return this;
        }

        /// <summary>
        /// Partial sphere band between two polar angles (degrees, 0 = top). Useful for helmets and bellies.
        /// </summary>
        public MeshBuilder AddSphereBand(Vector3 center, Vector3 radii, float fromDeg, float toDeg, int segments, int rings, Color color)
        {
            float v0 = fromDeg * Mathf.Deg2Rad, v1 = toDeg * Mathf.Deg2Rad;
            Vector3 P(int ring, int seg)
            {
                float v = Mathf.Lerp(v0, v1, (float)ring / rings);
                float u = Mathf.PI * 2f * seg / segments;
                var d = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                return center + Vector3.Scale(d, radii);
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                var a = P(r, s); var b = P(r, s + 1); var c = P(r + 1, s + 1); var d = P(r + 1, s);
                if (r == 0 && fromDeg <= 0.01f) AddTriangle(a, c, d, color);
                else AddQuad(a, b, c, d, color);
            }
            return this;
        }

        /// <summary>Capsule along local Y. Total height includes the end caps.</summary>
        public MeshBuilder AddCapsule(Vector3 center, float radius, float height, int segments, Color color)
        {
            float body = Mathf.Max(0f, height - 2f * radius);
            int half = Mathf.Max(2, segments / 2);
            AddSphereBand(center + Vector3.up * body * 0.5f, Vector3.one * radius, 0, 90, segments, half / 2 + 1, color);
            AddCylinder(center, radius, radius, body, segments, color, caps: false);
            AddSphereBand(center - Vector3.up * body * 0.5f, Vector3.one * radius, 90, 180, segments, half / 2 + 1, color);
            return this;
        }

        public Mesh Build(string name = "Generated")
        {
            var mesh = new Mesh { name = name };
            BuildInto(mesh);
            return mesh;
        }

        /// <summary>Writes the geometry into an existing mesh (for meshes rebuilt at runtime, e.g. fire voxels).</summary>
        public void BuildInto(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = _verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Empties the builder so it can be reused.</summary>
        public void Clear()
        {
            _verts.Clear();
            _normals.Clear();
            _colors.Clear();
            _tris.Clear();
            Transform = Matrix4x4.identity;
        }

        /// <summary>Returns a copy of <paramref name="source"/> with every vertex set to <paramref name="color"/>.</summary>
        public static Mesh PaintedCopy(Mesh source, Color color)
        {
            var m = Object.Instantiate(source);
            m.name = $"{source.name}_{ColorUtility.ToHtmlStringRGBA(color)}";
            var cols = new Color[m.vertexCount];
            for (int i = 0; i < cols.Length; i++) cols[i] = color;
            m.colors = cols;
            return m;
        }
    }
}
