using System.IO;
using Firehouse.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Stages a "what the finished game could look like" key-art shot (a Level 2 suburban house fire at dusk)
    /// in a scratch scene, using the real truck and Smokie prefabs plus static flame/smoke/water props,
    /// and renders it to docs/concept-art.png. Not gameplay: everything here is posed by hand.
    /// </summary>
    public static class ConceptArtBuilder
    {
        const string OutputPath = "docs/concept-art.png";

        static readonly Color Siding = new(0.55f, 0.70f, 0.80f);
        static readonly Color Trim = new(0.95f, 0.94f, 0.90f);
        static readonly Color RoofC = new(0.30f, 0.24f, 0.24f);
        static readonly Color Char = new(0.07f, 0.06f, 0.06f);
        static readonly Color WindowFire = new(1f, 0.55f, 0.15f, 0.05f);
        static readonly Color WindowDark = new(0.10f, 0.12f, 0.18f);
        static readonly Color WindowWarm = new(1f, 0.85f, 0.55f, 0.35f);
        static readonly Color Lawn = new(0.30f, 0.46f, 0.22f);
        static readonly Color Asphalt = new(0.18f, 0.18f, 0.21f);
        static readonly Color Sidewalk = new(0.55f, 0.55f, 0.56f);
        static readonly Color FlameA = new(1.0f, 0.60f, 0.12f, 0.45f);  // partial glow keeps them orange under bloom
        static readonly Color FlameB = new(0.95f, 0.25f, 0.04f, 0.35f);
        static readonly Color Smoke = new(0.20f, 0.20f, 0.26f);
        static readonly Color Water = new(0.65f, 0.85f, 1f, 0.55f);
        static readonly Color HoseC = new(0.95f, 0.78f, 0.22f);

        [MenuItem("Tools/Firehouse/Render Concept Art")]
        public static void Build()
        {
            ProjectAssets.ConfirmSaveOpenScenes();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = ProjectAssets.SharedMaterial;
            var rng = new System.Random(42);

            var world = new MeshBuilder();
            var fx = new MeshBuilder(); // flames, smoke, water (separate so it never casts shadows)

            // --- street, sidewalk, lawn
            world.AddBox(new Vector3(0, -0.25f, 20), new Vector3(140, 0.5f, 120), Lawn);
            world.AddBox(new Vector3(0, 0.01f, -4), new Vector3(140, 0.04f, 9), Asphalt);
            world.AddBox(new Vector3(0, 0.04f, 1.6f), new Vector3(140, 0.08f, 2.2f), Sidewalk);
            for (float x = -60; x < 60; x += 5) world.AddBox(new Vector3(x, 0.035f, -4), new Vector3(2.4f, 0.01f, 0.15f), new Color(0.9f, 0.8f, 0.3f));
            world.AddBox(new Vector3(2.5f, 0.03f, 6f), new Vector3(1.4f, 0.05f, 8f), Sidewalk); // front path

            // --- the house (front faces -Z), 10 × 8 m, two stories
            BuildHouse(world, fx, rng);

            // --- neighbours' houses as dark silhouettes with warm windows
            foreach (float x in new[] { -24f, -38f, 22f, 36f })
            {
                world.AddBox(new Vector3(x, 3f, 16f), new Vector3(9f, 6f, 8f), new Color(0.35f, 0.38f, 0.45f));
                Gable(world, new Vector3(x, 6f, 16f), 9.6f, 8.6f, 2.6f, RoofC);
                world.AddBox(new Vector3(x - 2f, 4.2f, 11.95f), new Vector3(1.2f, 1.1f, 0.1f), WindowWarm);
                world.AddBox(new Vector3(x + 2f, 1.6f, 11.95f), new Vector3(1.2f, 1.1f, 0.1f), WindowWarm);
            }
            // picket fence, mailbox, a tree, street lamp
            for (float x = -8f; x < 13f; x += 0.5f)
                if (x < 1.6f || x > 3.4f) world.AddBox(new Vector3(x, 0.45f, 3.3f), new Vector3(0.12f, 0.9f, 0.06f), Trim);
            world.AddBox(new Vector3(-3f, 0.7f, 3.3f), new Vector3(10.2f, 0.08f, 0.05f), Trim);
            world.AddBox(new Vector3(8f, 0.7f, 3.3f), new Vector3(10.4f, 0.08f, 0.05f), Trim);
            world.AddBox(new Vector3(4.2f, 0.55f, 2.9f), new Vector3(0.1f, 1.1f, 0.1f), new Color(0.4f, 0.3f, 0.2f));
            world.AddBox(new Vector3(4.2f, 1.2f, 2.9f), new Vector3(0.3f, 0.25f, 0.5f), new Color(0.25f, 0.35f, 0.6f));
            world.AddCylinder(new Vector3(-9f, 1.5f, 8f), 0.25f, 0.25f, 3f, 6, new Color(0.35f, 0.25f, 0.16f));
            world.AddSphere(new Vector3(-9f, 4.2f, 8f), new Vector3(2.4f, 2.2f, 2.4f), 7, 4, new Color(0.22f, 0.38f, 0.18f));
            world.AddCylinder(new Vector3(-14f, 2.5f, 1.2f), 0.08f, 0.08f, 5f, 6, new Color(0.2f, 0.2f, 0.22f));
            world.AddBox(new Vector3(-13.6f, 4.95f, 1.2f), new Vector3(0.9f, 0.12f, 0.35f), new Color(1f, 0.9f, 0.6f, 0.1f));

            // --- the crew's ladder to the upstairs window
            BuildLadder(world, new Vector3(-1.5f, 0f, 6.9f), new Vector3(-1.5f, 5.6f, 7.85f));

            Spawn("World", world.Build("Concept_World"), mat, shadows: true);

            // --- truck at the curb, lights flashing
            var truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckBuilder.PrefabPath);
            var truck = (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab);
            truck.transform.SetPositionAndRotation(new Vector3(-12.8f, 0.02f, -2.4f), Quaternion.Euler(0, 104, 0));
            Glow(truck.transform.Find("LightbarLeft"), new Color(5f, 0.35f, 0.2f));
            Glow(truck.transform.Find("LightbarRight"), new Color(0.3f, 0.6f, 5f));
            foreach (var n in new[] { "FlashLeft", "FlashRight", "HeadlightL", "HeadlightR" })
            {
                var l = truck.transform.Find(n)?.GetComponent<Light>();
                if (l != null) { l.enabled = true; l.intensity *= 1.6f; }
            }

            // --- the crew
            var reel = truck.transform.TransformPoint(new Vector3(0, 1.95f, -2.85f));
            // 1. Hose holder spraying the downstairs window
            var holder = Smokie("Hoser", new Vector3(-2.2f, 0f, 2.0f), 30f, Color.red * 0.85f);
            PoseArms(holder, Quaternion.Euler(-15, 30, 0) * Vector3.forward, Quaternion.Euler(-10, 15, 0) * Vector3.forward);
            var nozzle = holder.transform.TransformPoint(new Vector3(0.05f, 1.0f, 0.95f));
            var aim = Quaternion.Euler(-28f, 30f, 0f) * Vector3.forward;
            WaterArc(fx, nozzle, aim * 10.5f, rng);
            HoseLine(fx, reel, nozzle - aim * 0.3f);
            // 2. Teammate flattened by the spray (friendly fire), X-eyes
            var flattened = Smokie("Flattened", new Vector3(2.6f, 0.38f, 4.1f), 0f, new Color(0.2f, 0.42f, 0.8f));
            flattened.transform.rotation = Quaternion.Euler(-80f, 210f, 15f);
            SwapEyes(flattened, "Smokie_Eyes_X");
            // 3. Climbing the ladder to the upstairs window
            var climber = Smokie("Climber", new Vector3(-1.5f, 2.6f, 7.0f), 0f, new Color(0.22f, 0.62f, 0.3f));
            PoseArms(climber, Quaternion.Euler(-70, -10, 0) * Vector3.forward, Quaternion.Euler(-80, 10, 0) * Vector3.forward);
            // 4. Carrying a rescued civilian out the front door
            var carrier = Smokie("Carrier", new Vector3(4.6f, 0f, 5.4f), 200f, new Color(0.95f, 0.5f, 0.15f));
            PoseArms(carrier, Quaternion.Euler(-5, -12, 0) * Vector3.forward, Quaternion.Euler(-5, 12, 0) * Vector3.forward);
            Civilian(fx, carrier.transform.TransformPoint(new Vector3(0, 0.95f, 0.75f)), carrier.transform.rotation);
            SwapEyes(carrier, "Smokie_Eyes_Wide");
            // 5. Running up from the truck with an extinguisher
            var runner = Smokie("Runner", new Vector3(-5.6f, 0f, -0.2f), 45f, new Color(0.55f, 0.3f, 0.75f));
            PoseArms(runner, Quaternion.Euler(20, -20, 0) * Vector3.down, Quaternion.Euler(-40, 0, 0) * Vector3.forward);
            fx.AddCylinder(runner.transform.TransformPoint(new Vector3(0.45f, 0.55f, 0.5f)), 0.12f, 0.12f, 0.45f, 8, new Color(0.85f, 0.1f, 0.1f));
            // 6. Driver in the cab
            var driverPos = truck.transform.TransformPoint(new Vector3(-0.5f, 0.75f, 2.2f));
            Smokie("Driver", driverPos, truck.transform.eulerAngles.y, new Color(0.15f, 0.65f, 0.68f));

            // --- the cat on the roof
            var cat = new Vector3(4.2f, 8.1f, 9.6f);
            fx.AddBox(cat, new Vector3(0.45f, 0.25f, 0.25f), new Color(0.95f, 0.55f, 0.15f));
            fx.AddBox(cat + new Vector3(0.28f, 0.15f, 0), new Vector3(0.22f, 0.2f, 0.2f), new Color(0.95f, 0.55f, 0.15f));
            fx.AddBox(cat + new Vector3(-0.32f, 0.2f, 0), new Vector3(0.06f, 0.35f, 0.06f), new Color(0.95f, 0.55f, 0.15f));

            Spawn("Effects", fx.Build("Concept_FX"), mat, shadows: false);
            Lighting();
            var cam = CameraAndPost();
            Render(cam, OutputPath, 1920, 1080);
        }

        // ------------------------------------------------------------------ house + fire

        static void BuildHouse(MeshBuilder w, MeshBuilder fx, System.Random rng)
        {
            const float x0 = -3f, x1 = 7f, z0 = 8f, z1 = 16f, h = 6f;
            var c = new Vector3((x0 + x1) / 2, h / 2, (z0 + z1) / 2);
            w.AddBox(c, new Vector3(x1 - x0, h, z1 - z0), Siding);
            // trim bands + porch roof
            w.AddBox(new Vector3(c.x, 3.05f, z0 - 0.02f), new Vector3(x1 - x0 + 0.1f, 0.15f, 0.06f), Trim);
            w.AddBox(new Vector3(2.5f, 2.6f, z0 - 1.0f), new Vector3(4f, 0.15f, 2f), Trim);
            foreach (float px in new[] { 0.7f, 4.3f }) w.AddBox(new Vector3(px, 1.3f, z0 - 1.9f), new Vector3(0.15f, 2.6f, 0.15f), Trim);
            // gable roof with a burnt-through hole
            Gable(w, new Vector3(c.x, h, c.z), x1 - x0 + 0.8f, z1 - z0 + 0.8f, 3f, RoofC);
            w.AddBox(new Vector3(0.2f, 7.4f, 10.4f), new Vector3(1.8f, 0.2f, 1.6f), Char);
            // door (open, glowing from inside)
            w.AddBox(new Vector3(2.5f, 1.1f, z0 - 0.03f), new Vector3(1.1f, 2.2f, 0.06f), WindowFire);
            // windows: downstairs left on fire, upstairs left + middle on fire, others dark
            Window(w, new Vector3(-1.3f, 1.6f, z0), true);
            Window(w, new Vector3(5.5f, 1.6f, z0), false);
            Window(w, new Vector3(-1.5f, 4.6f, z0), true);
            Window(w, new Vector3(2.5f, 4.6f, z0), true);
            Window(w, new Vector3(5.5f, 4.6f, z0), false);
            // scorch marks above the burning windows
            foreach (var p in new[] { new Vector3(-1.5f, 5.7f, z0), new Vector3(2.5f, 5.7f, z0), new Vector3(-1.3f, 2.8f, z0) })
                w.AddBox(p + new Vector3(0, 0, -0.04f), new Vector3(1.8f, 0.9f, 0.04f), Char);

            // flames pouring out of windows and the roof hole
            Flames(fx, new Vector3(-1.5f, 5.1f, z0 - 0.4f), new Vector3(1.4f, 1.6f, 0.9f), 55, rng);
            Flames(fx, new Vector3(2.5f, 5.1f, z0 - 0.4f), new Vector3(1.4f, 1.4f, 0.9f), 45, rng);
            Flames(fx, new Vector3(-1.3f, 2.0f, z0 - 0.3f), new Vector3(1.2f, 1.0f, 0.6f), 30, rng);
            Flames(fx, new Vector3(0.2f, 8.4f, 10.4f), new Vector3(1.8f, 2.4f, 1.6f), 80, rng);
            Flames(fx, new Vector3(2.5f, 1.2f, z0 - 0.2f), new Vector3(0.8f, 0.9f, 0.3f), 14, rng);
            // smoke column drifting right with the wind
            for (int i = 0; i < 140; i++)
            {
                float t = (float)rng.NextDouble();
                var p = new Vector3(0.2f + t * t * 9f + R(rng) * (1f + t * 4f), 9.5f + t * 16f + R(rng) * 2f, 10.4f + R(rng) * (1f + t * 3f));
                float s = 0.6f + t * 2.6f + (float)rng.NextDouble() * 0.6f;
                fx.SetTRS(p, Random3(rng), Vector3.one * s);
                fx.AddBox(Vector3.zero, Vector3.one, Color.Lerp(Smoke, Smoke * 1.6f, t));
            }
            fx.ResetTransform();
        }

        static void Window(MeshBuilder w, Vector3 p, bool fire)
        {
            w.AddBox(p + new Vector3(0, 0, -0.05f), new Vector3(1.5f, 1.3f, 0.06f), Trim);
            w.AddBox(p + new Vector3(0, 0, -0.08f), new Vector3(1.2f, 1.0f, 0.04f), fire ? WindowFire : WindowDark);
        }

        static void Flames(MeshBuilder fx, Vector3 center, Vector3 size, int count, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                float up = (float)rng.NextDouble();
                var p = center + new Vector3(R(rng) * size.x * 0.5f, up * size.y, R(rng) * size.z * 0.5f);
                float s = Mathf.Lerp(0.55f, 0.15f, up) * (0.7f + (float)rng.NextDouble() * 0.6f);
                fx.SetTRS(p, Random3(rng), new Vector3(s, s * 1.5f, s));
                fx.AddSphere(Vector3.zero, Vector3.one * 0.5f, 4, 2, Color.Lerp(FlameB, FlameA, up + R(rng) * 0.3f));
            }
            fx.ResetTransform();
        }

        static void WaterArc(MeshBuilder fx, Vector3 start, Vector3 velocity, System.Random rng)
        {
            var p = start;
            var v = velocity;
            for (int i = 0; i < 90; i++)
            {
                p += v * 0.012f;
                v += Physics.gravity * 0.012f;
                if (p.z > 7.9f) break;
                for (int k = 0; k < 2; k++)
                {
                    float s = 0.08f + i * 0.0015f;
                    fx.SetTRS(p + Random.insideUnitSphere * (0.04f + i * 0.004f), Random3(rng), Vector3.one * s);
                    fx.AddBox(Vector3.zero, Vector3.one, Water);
                }
            }
            for (int i = 0; i < 25; i++) // splash on the wall
            {
                fx.SetTRS(p + new Vector3(R(rng) * 0.7f, R(rng) * 0.7f, -0.2f - (float)rng.NextDouble() * 0.4f), Random3(rng), Vector3.one * 0.12f);
                fx.AddBox(Vector3.zero, Vector3.one, Water);
            }
            fx.ResetTransform();
        }

        static void HoseLine(MeshBuilder fx, Vector3 from, Vector3 to)
        {
            // Sagging line lying on the lawn between truck and nozzle.
            Vector3 prev = from;
            const int n = 30;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                var p = Vector3.Lerp(from, to, t);
                float ground = 0.06f;
                p.y = Mathf.Max(ground, Mathf.Lerp(from.y, to.y, t) - Mathf.Sin(t * Mathf.PI) * 2.2f);
                p.x += Mathf.Sin(t * 9f) * 0.35f * Mathf.Sin(t * Mathf.PI);
                var mid = (prev + p) / 2;
                var d = p - prev;
                fx.SetTRS(mid, Quaternion.FromToRotation(Vector3.up, d.normalized), Vector3.one);
                fx.AddCylinder(Vector3.zero, 0.05f, 0.05f, d.magnitude + 0.02f, 6, HoseC);
                prev = p;
            }
            fx.ResetTransform();
        }

        static void BuildLadder(MeshBuilder w, Vector3 bottom, Vector3 top)
        {
            var dir = (top - bottom);
            var rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                w.SetTRS((bottom + top) / 2 + Vector3.right * x, rot, Vector3.one);
                w.AddBox(Vector3.zero, new Vector3(0.07f, dir.magnitude, 0.07f), new Color(0.85f, 0.87f, 0.9f));
            }
            for (float t = 0.05f; t < 1f; t += 0.07f)
            {
                w.SetTRS(Vector3.Lerp(bottom, top, t), rot, Vector3.one);
                w.AddBox(Vector3.zero, new Vector3(0.6f, 0.04f, 0.04f), new Color(0.85f, 0.87f, 0.9f));
            }
            w.ResetTransform();
        }

        static void Civilian(MeshBuilder fx, Vector3 at, Quaternion carrierRot)
        {
            // Pyjama-clad civilian carried sideways in both arms.
            var rot = carrierRot * Quaternion.Euler(0, 0, 90);
            fx.SetTRS(at, rot, Vector3.one);
            fx.AddCapsule(Vector3.zero, 0.2f, 1.2f, 8, new Color(0.95f, 0.6f, 0.75f));
            fx.AddSphere(new Vector3(0, 0.78f, 0), Vector3.one * 0.17f, 8, 5, new Color(0.95f, 0.78f, 0.62f));
            fx.AddBox(new Vector3(0, 0.95f, 0), new Vector3(0.28f, 0.08f, 0.28f), new Color(0.35f, 0.22f, 0.12f));
            fx.ResetTransform();
        }

        static void Gable(MeshBuilder w, Vector3 baseCenter, float width, float depth, float height, Color c)
        {
            float hx = width / 2, hz = depth / 2;
            var a = baseCenter + new Vector3(-hx, 0, -hz); var b = baseCenter + new Vector3(hx, 0, -hz);
            var cc = baseCenter + new Vector3(hx, 0, hz); var d = baseCenter + new Vector3(-hx, 0, hz);
            var r0 = baseCenter + new Vector3(-hx, height, 0); var r1 = baseCenter + new Vector3(hx, height, 0);
            w.AddQuad(a, r0, r1, b, c);   // front slope
            w.AddQuad(cc, r1, r0, d, c);  // back slope
            w.AddTriangle(d, r0, a, Trim); // gable ends
            w.AddTriangle(b, r1, cc, Trim);
        }

        // ------------------------------------------------------------------ smokies

        static GameObject Smokie(string name, Vector3 pos, float yaw, Color coat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmokieBuilder.PlayerPrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", coat);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.name == "Body" || r.name == "Helmet" || r.name.Contains("_Upper") || r.name.Contains("_Fore"))
                    r.SetPropertyBlock(mpb);
            var cam = go.GetComponentInChildren<Camera>(true);
            if (cam != null) cam.enabled = false;
            return go;
        }

        /// <summary>Points the (normally physics-driven) arm chains in fixed directions (local space).</summary>
        static void PoseArms(GameObject smokie, Vector3 leftDir, Vector3 rightDir)
        {
            PoseArm(smokie, "LeftArm", new Vector3(-0.40f, 0.92f, 0.02f), leftDir.normalized);
            PoseArm(smokie, "RightArm", new Vector3(0.40f, 0.92f, 0.02f), rightDir.normalized);
        }

        static void PoseArm(GameObject smokie, string arm, Vector3 shoulder, Vector3 dir)
        {
            var root = smokie.transform.Find(arm);
            if (root == null) return;
            float[] lengths = { 0.30f, 0.30f, 0.18f };
            float along = 0f;
            int i = 0;
            foreach (Transform seg in root)
            {
                float len = lengths[Mathf.Min(i, 2)];
                seg.localPosition = shoulder + dir * (along + len * 0.5f);
                seg.localRotation = Quaternion.FromToRotation(Vector3.down, dir);
                along += len;
                i++;
            }
        }

        static void SwapEyes(GameObject smokie, string meshName)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{ProjectAssets.MeshesFolder}/{meshName}.asset");
            var eyes = smokie.transform.Find("Visual/Head/Eyes")?.GetComponent<MeshFilter>();
            if (mesh != null && eyes != null) eyes.sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ helpers

        static float R(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;
        static Quaternion Random3(System.Random rng) => Quaternion.Euler(R(rng) * 180f, R(rng) * 180f, R(rng) * 180f);

        static void Spawn(string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static void Glow(Transform t, Color c)
        {
            if (t == null) return;
            var r = t.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_GlowColor", c);
            r.SetPropertyBlock(mpb);
        }

        static void Lighting()
        {
            var sun = new GameObject("Dusk Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.62f, 0.45f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(14, 70, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.36f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.30f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.26f, 0.24f, 0.40f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 110f;

            // Fire light spilling out of the house
            void Point(Vector3 p, Color c, float range, float intensity)
            {
                var l = new GameObject("FireLight").AddComponent<Light>();
                l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
                l.transform.position = p;
            }
            Point(new Vector3(-0.5f, 4.5f, 6.5f), new Color(1f, 0.5f, 0.15f), 16f, 9f);
            Point(new Vector3(0.2f, 9.5f, 10f), new Color(1f, 0.55f, 0.2f), 20f, 7f);
            Point(new Vector3(-1.3f, 1.8f, 7f), new Color(1f, 0.45f, 0.1f), 9f, 5f);
            Point(new Vector3(-13.6f, 4.6f, 1.2f), new Color(1f, 0.85f, 0.55f), 10f, 2.5f); // street lamp
        }

        static Camera CameraAndPost()
        {
            var go = new GameObject("ConceptCamera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.22f, 0.20f, 0.36f);
            cam.fieldOfView = 46f;
            cam.farClipPlane = 300f;
            go.transform.position = new Vector3(-10.5f, 3.0f, -9.5f);
            go.transform.LookAt(new Vector3(1.2f, 3.4f, 6.2f));
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(1.0f);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.postExposure.Override(0.2f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.sharedProfile = profile;
            return cam;
        }

        static void Render(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // second pass lets post-processing history settle
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[Firehouse] Concept art rendered to {Path.GetFullPath(path)}");
        }
    }
}
