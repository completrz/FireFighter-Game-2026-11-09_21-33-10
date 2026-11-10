using Firehouse.World;
using NUnit.Framework;
using UnityEngine;

namespace Firehouse.Tests
{
    /// <summary>Every client generates the test-road terrain locally, so it must be deterministic.</summary>
    public class ProceduralTerrainTests
    {
        private static ProceduralTerrain Make(out GameObject go)
        {
            go = new GameObject("TerrainTest", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            return go.AddComponent<ProceduralTerrain>();
        }

        [Test]
        public void SameParameters_GiveIdenticalHeights()
        {
            var a = Make(out var goA);
            var b = Make(out var goB);
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    float x = -120f + i * 4.9f, z = 100f - i * 4.1f;
                    Assert.AreEqual(a.SampleHeight(x, z), b.SampleHeight(x, z), 1e-5f);
                }
                Assert.AreEqual(a.GetComponent<MeshFilter>().sharedMesh.vertexCount, b.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void Road_IsLongAndCrossesTheCreek()
        {
            var t = Make(out var go);
            try
            {
                Assert.Greater(t.RoadLength, 200f);
                Assert.Greater(t.CreekCrossingDistance(), 0f);
                t.RoadPointAt(10f, out var p, out var dir);
                Assert.AreEqual(1f, dir.magnitude, 0.01f);
                Assert.AreEqual(t.SampleHeight(p.x, p.z), p.y, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
