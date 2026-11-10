using Firehouse.Core;
using Firehouse.Networking;
using Firehouse.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Builds Scenes/TestRoad.unity: procedural hills with a bumpy dirt road (potholes, washboard, jump humps,
    /// a cattle grate), a narrow bridge over a creek, mailboxes and cones to smash, fences, and the starter truck
    /// at the start of the road with the crew spawning next to it.
    /// </summary>
    public static class TestRoadBuilder
    {
        public const string ScenePath = ProjectAssets.ScenesFolder + "/TestRoad.unity";

        static readonly Color Plank = new(0.55f, 0.40f, 0.25f);
        static readonly Color PlankDark = new(0.40f, 0.28f, 0.17f);
        static readonly Color FenceWood = new(0.60f, 0.47f, 0.32f);

        [MenuItem("Tools/Firehouse/Build/Test Road Scene")]
        public static void Build()
        {
            if (!ProjectAssets.ConfirmSaveOpenScenes()) return;
            PropsBuilder.BuildAll();
            var truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckBuilder.PrefabPath) ?? TruckBuilder.Build();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = ProjectAssets.SharedMaterial;

            // Terrain
            var terrainGo = new GameObject("Terrain", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            terrainGo.GetComponent<MeshRenderer>().sharedMaterial = mat;
            terrainGo.isStatic = true;
            var terrain = terrainGo.AddComponent<ProceduralTerrain>();
            terrain.Generate();

            var root = new GameObject("TestRoad").transform;
            var blockout = new Blockout(root, mat);

            // Start: truck on the road, facing along it; spawn points beside it.
            terrain.RoadPointAt(6f, out var startPos, out var startDir);
            var truck = (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab);
            truck.transform.SetPositionAndRotation(startPos + Vector3.up * 0.4f, Quaternion.LookRotation(startDir));

            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(root, false);
            var side = Vector3.Cross(Vector3.up, startDir).normalized;
            for (int i = 0; i < 8; i++)
            {
                var p = startPos - startDir * (6f + (i / 4) * 1.6f) + side * ((i % 4) - 1.5f) * 1.5f;
                p.y = terrain.SampleHeight(p.x, p.z) + 0.05f;
                var go = new GameObject($"Spawn {i + 1}");
                go.transform.SetParent(spawns, false);
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(startDir));
                go.AddComponent<SpawnPoint>();
            }

            // Bridge over the creek: narrow (3.2 m) on purpose.
            float crossing = terrain.CreekCrossingDistance();
            if (crossing > 0f)
            {
                terrain.RoadPointAt(crossing, out var bp, out var bdir);
                // Deck height: the road level just before and after the creek.
                terrain.RoadPointAt(crossing - 9f, out var before, out _);
                terrain.RoadPointAt(crossing + 9f, out var after, out _);
                float deckY = Mathf.Max(before.y, after.y) + 0.05f;
                BuildBridge(blockout, bp, bdir, deckY, 3.2f, 18f);
            }

            // Mailboxes and fences along the road, cones at the start.
            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            var rng = new System.Random(5);
            for (float d = 30f; d < terrain.RoadLength - 10f; d += 22f + (float)rng.NextDouble() * 18f)
            {
                if (crossing > 0 && Mathf.Abs(d - crossing) < 14f) continue;
                terrain.RoadPointAt(d, out var p, out var dir);
                var n = Vector3.Cross(Vector3.up, dir).normalized * (rng.NextDouble() < 0.5 ? -1f : 1f);
                var mp = p + n * 4.6f;
                mp.y = terrain.SampleHeight(mp.x, mp.z);
                Place(PropsBuilder.MailboxPath, props, mp, Quaternion.LookRotation(-n));
            }
            for (int i = 0; i < 5; i++)
            {
                terrain.RoadPointAt(20f + i * 3f, out var p, out var dir);
                var cp = p + Vector3.Cross(Vector3.up, dir).normalized * ((i % 2 == 0) ? 1.5f : -1.5f);
                cp.y = terrain.SampleHeight(cp.x, cp.z) + 0.02f;
                Place(PropsBuilder.ConePath, props, cp, Quaternion.identity);
            }
            BuildFence(blockout, terrain, 100f, 135f, 5.5f);
            BuildFence(blockout, terrain, 220f, 260f, -5.5f);

            blockout.Finish("TestRoad");
            BuildLighting();
            BuildMenuCamera(startPos, startDir);
            new GameObject("PlayerSpawner").AddComponent<PlayerSpawner>();

            ProjectAssets.EnsureFolder(ProjectAssets.ScenesFolder);
            FirehouseBlockoutBuilder.AddSceneToBuild(ScenePath);
            ProjectAssets.SaveSceneWithNetworkIds(scene, ScenePath);
            Debug.Log($"[Firehouse] Built {ScenePath} (road {terrain.RoadLength:0} m, bridge at {crossing:0} m)");
        }

        static void Place(string path, Transform parent, Vector3 pos, Quaternion rot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
        }

        static void BuildBridge(Blockout b, Vector3 center, Vector3 dir, float deckY, float width, float length)
        {
            // Build in local bridge space via a rotated child so colliders line up with the road.
            var rot = Quaternion.LookRotation(dir);
            var go = new GameObject("Bridge");
            go.transform.SetPositionAndRotation(new Vector3(center.x, deckY, center.z), rot);
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, -0.15f, 0), new Vector3(width, 0.3f, length), Plank);
            for (float z = -length / 2 + 0.5f; z < length / 2; z += 1f)
                mb.AddBox(new Vector3(0, 0.005f, z), new Vector3(width, 0.01f, 0.08f), PlankDark);
            foreach (float x in new[] { -width / 2, width / 2 })
            {
                mb.AddBox(new Vector3(x, 0.45f, 0), new Vector3(0.12f, 0.1f, length), PlankDark);
                for (float z = -length / 2; z <= length / 2; z += 3f)
                    mb.AddBox(new Vector3(x, 0.2f, z), new Vector3(0.14f, 0.5f, 0.14f), PlankDark);
                // Piers down into the creek
                for (float z = -length / 2 + 3f; z < length / 2; z += 6f)
                    mb.AddBox(new Vector3(x * 0.8f, -2.2f, z), new Vector3(0.3f, 4f, 0.3f), PlankDark);
            }
            go.AddComponent<MeshFilter>().sharedMesh = ProjectAssets.SaveMesh(mb.Build(), "TestRoad_Bridge");
            go.AddComponent<MeshRenderer>().sharedMaterial = ProjectAssets.SharedMaterial;
            go.isStatic = true;

            void Col(Vector3 c, Vector3 s)
            {
                var cgo = new GameObject("BridgeCollider");
                cgo.transform.SetParent(go.transform, false);
                cgo.transform.localPosition = c;
                cgo.AddComponent<BoxCollider>().size = s;
            }
            Col(new Vector3(0, -0.15f, 0), new Vector3(width, 0.3f, length));
            // Low rails: they stop a slow truck but a fast one can jump them.
            Col(new Vector3(-width / 2, 0.25f, 0), new Vector3(0.14f, 0.5f, length));
            Col(new Vector3(width / 2, 0.25f, 0), new Vector3(0.14f, 0.5f, length));
            // Short ramps at both ends so the wheels roll on.
            foreach (float end in new[] { -1f, 1f })
            {
                var ramp = new GameObject("BridgeRamp");
                ramp.transform.SetParent(go.transform, false);
                ramp.transform.localPosition = new Vector3(0, -0.3f, end * (length / 2 + 1.2f));
                ramp.transform.localRotation = Quaternion.Euler(end * 10f, 0, 0);
                ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.3f, 2.6f);
            }
        }

        static void BuildFence(Blockout b, ProceduralTerrain terrain, float from, float to, float offset)
        {
            for (float d = from; d < to; d += 2.5f)
            {
                terrain.RoadPointAt(d, out var p, out var dir);
                var n = Vector3.Cross(Vector3.up, dir).normalized;
                var post = p + n * offset;
                post.y = terrain.SampleHeight(post.x, post.z);
                b.Box("Fences", "FencePost", post + new Vector3(-0.07f, 0, -0.07f), post + new Vector3(0.07f, 1.1f, 0.07f), FenceWood);
                // Rails as no-collider visuals (posts block; rails are just for looks).
                b.AddMeshPart("Fences", mb =>
                {
                    mb.SetTRS(post + Vector3.up * 0.8f + dir * 1.25f, Quaternion.LookRotation(dir), Vector3.one);
                    mb.AddBox(Vector3.zero, new Vector3(0.05f, 0.1f, 2.5f), FenceWood);
                    mb.SetTRS(post + Vector3.up * 0.45f + dir * 1.25f, Quaternion.LookRotation(dir), Vector3.one);
                    mb.AddBox(Vector3.zero, new Vector3(0.05f, 0.1f, 2.5f), FenceWood);
                    mb.ResetTransform();
                });
            }
        }

        static void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(42, -60, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
            RenderSettings.fogStartDistance = 70;
            RenderSettings.fogEndDistance = 180;
        }

        static void BuildMenuCamera(Vector3 start, Vector3 dir)
        {
            var go = new GameObject("MenuCamera") { tag = "MainCamera" };
            go.AddComponent<Camera>().fieldOfView = 55;
            go.AddComponent<AudioListener>();
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            go.transform.position = start - dir * 14f + side * 8f + Vector3.up * 6f;
            go.transform.LookAt(start + dir * 10f);
        }
    }
}
