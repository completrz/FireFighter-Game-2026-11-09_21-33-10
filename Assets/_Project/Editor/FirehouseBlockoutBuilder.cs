using System.Collections.Generic;
using System.Linq;
using Firehouse.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using O = Firehouse.EditorTools.Blockout.Opening;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Builds the Firehouse lobby blockout scene (Assets/_Project/Scenes/Firehouse.unity).
    /// Layout (meters, +Z = into the building, front doors face -Z):
    ///   Apparatus bay   x -10..4,  z 0..20, 6.5 m tall, two big doors in front, a loft at the back (y 3.5) with the fire pole.
    ///   Wing            x 4..14,   z 0..20: kitchen/day room downstairs, bunk room + captain's office upstairs (y 3.5).
    ///   Back lot        z 20..40 behind the building, fenced, with a training tower placeholder.
    /// </summary>
    public static class FirehouseBlockoutBuilder
    {
        public const string ScenePath = ProjectAssets.ScenesFolder + "/Firehouse.unity";

        // Palette
        static readonly Color Grass = new(0.42f, 0.62f, 0.30f);
        static readonly Color Concrete = new(0.58f, 0.58f, 0.56f);
        static readonly Color Asphalt = new(0.28f, 0.28f, 0.30f);
        static readonly Color Brick = new(0.66f, 0.26f, 0.20f);
        static readonly Color Trim = new(0.92f, 0.90f, 0.84f);
        static readonly Color InteriorWall = new(0.86f, 0.82f, 0.72f);
        static readonly Color Wood = new(0.62f, 0.45f, 0.30f);
        static readonly Color Roof = new(0.25f, 0.25f, 0.28f);
        static readonly Color Red = new(0.80f, 0.15f, 0.12f);
        static readonly Color Yellow = new(0.95f, 0.78f, 0.15f);
        static readonly Color Metal = new(0.70f, 0.72f, 0.75f);
        static readonly Color Dark = new(0.16f, 0.16f, 0.18f);
        static readonly Color Couch = new(0.30f, 0.38f, 0.62f);
        static readonly Color Fence = new(0.55f, 0.42f, 0.28f);

        const float T = 0.3f;        // wall thickness
        const float Upper = 3.5f;    // upstairs floor height
        const float BayTop = 6.5f;
        const float WingTop = 7.0f;

        [MenuItem("Tools/Firehouse/Build/Firehouse Blockout Scene")]
        public static void Build()
        {
            if (!ProjectAssets.ConfirmSaveOpenScenes()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = ProjectAssets.SharedMaterial;

            var root = new GameObject("Firehouse").transform;
            var b = new Blockout(root, mat);

            BuildGround(b);
            BuildBay(b);
            BuildWing(b);
            BuildBackLot(b);
            b.Finish("Firehouse");

            BuildLighting(root);
            BuildSpawns(root);
            BuildMenuCamera();
            new GameObject("PlayerSpawner").AddComponent<PlayerSpawner>();

            ProjectAssets.EnsureFolder(ProjectAssets.ScenesFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath, first: true);
            Debug.Log($"[Firehouse] Built {ScenePath}");
        }

        static void BuildGround(Blockout b)
        {
            b.Box("Ground", "Ground", new Vector3(-200, -0.5f, -200), new Vector3(200, 0f, 200), Grass);
            b.Box("Ground", "Apron", new Vector3(-10, 0f, -14), new Vector3(4, 0.02f, 0), Concrete);
            b.Box("Ground", "Road", new Vector3(-200, 0f, -24), new Vector3(200, 0.02f, -14), Asphalt);
            b.Box("Ground", "Path", new Vector3(7.5f, 0f, -14), new Vector3(10.5f, 0.02f, 0), Concrete);
            // Road center line dashes (no colliders)
            for (float x = -198; x < 198; x += 6)
                b.Box("Ground", "Dash", new Vector3(x, 0.02f, -19.15f), new Vector3(x + 3, 0.03f, -18.85f), Yellow, collider: false);
        }

        static void BuildBay(Blockout b)
        {
            const string g = "Building";
            // Floor
            b.Box(g, "BayFloor", new Vector3(-10, 0, 0), new Vector3(4, 0.05f, 20), Concrete);
            // Front wall with two big apparatus doors (open) and trim around them
            b.WallX(g, "BayFront", 0, -10, 4, 0, BayTop, T, Brick, new O(-9, -4.5f, 0, 4.8f), new O(-3.5f, 1, 0, 4.8f));
            b.Box(g, "DoorTrimA", new Vector3(-9.1f, 4.8f, -0.25f), new Vector3(-4.4f, 5.1f, -0.15f), Trim, collider: false);
            b.Box(g, "DoorTrimB", new Vector3(-3.6f, 4.8f, -0.25f), new Vector3(1.1f, 5.1f, -0.15f), Trim, collider: false);
            // Side and back walls
            b.WallZ(g, "BayLeft", -10, 0, 20, 0, BayTop, T, Brick);
            b.WallX(g, "BayBack", 20, -10, 4, 0, BayTop, T, Brick, new O(-6, -4, 0, 2.4f)); // back door to the lot
            // Shared wall with the wing: doorway at ground level, open above the loft so upstairs connects
            b.WallZ(g, "BayWingWall", 4, 0, 20, 0, BayTop, T, InteriorWall,
                new O(5, 6.5f, 0, 2.4f),          // ground doorway to the day room
                new O(14, 20, Upper, BayTop));    // loft opens into the upstairs hall
            // Loft at the back of the bay, with a hole for the fire pole
            const float poleX = -1f, poleZ = 15.5f, hole = 0.7f;
            b.Box(g, "Loft", new Vector3(-10, Upper - 0.2f, 14), new Vector3(poleX - hole, Upper, 20), Wood);
            b.Box(g, "Loft", new Vector3(poleX + hole, Upper - 0.2f, 14), new Vector3(4, Upper, 20), Wood);
            b.Box(g, "Loft", new Vector3(poleX - hole, Upper - 0.2f, 14), new Vector3(poleX + hole, Upper, poleZ - hole), Wood);
            b.Box(g, "Loft", new Vector3(poleX - hole, Upper - 0.2f, poleZ + hole), new Vector3(poleX + hole, Upper, 20), Wood);
            // Loft railing (gap at the pole)
            b.Box(g, "LoftRail", new Vector3(-10, Upper, 13.95f), new Vector3(poleX - 0.8f, Upper + 1.0f, 14.1f), Red);
            b.Box(g, "LoftRail", new Vector3(poleX + 0.8f, Upper, 13.95f), new Vector3(4, Upper + 1.0f, 14.1f), Red);
            // Fire pole (brass-ish), visual + thin collider
            b.AddMeshPart(g, mb => mb.AddCylinder(new Vector3(poleX, BayTop * 0.5f, poleZ), 0.06f, 0.06f, BayTop, 8, Yellow));
            // Roof
            b.Box("Roof", "BayRoof", new Vector3(-10.2f, BayTop, -0.2f), new Vector3(4.2f, BayTop + 0.3f, 20.2f), Roof);
            // Parking stripes for the truck
            b.Box(g, "Stripe", new Vector3(-6.95f, 0.05f, 1), new Vector3(-6.75f, 0.06f, 13), Yellow, collider: false);
            b.Box(g, "Stripe", new Vector3(-1.45f, 0.05f, 1), new Vector3(-1.25f, 0.06f, 13), Yellow, collider: false);
            // Basketball hoop on the left wall
            b.Box(g, "Backboard", new Vector3(-9.85f, 2.9f, 6.2f), new Vector3(-9.75f, 3.9f, 7.8f), Trim);
            b.AddMeshPart(g, mb => mb.AddCylinder(new Vector3(-9.45f, 3.05f, 7f), 0.25f, 0.25f, 0.04f, 10, Red, caps: false));
            // Gear racks along the left wall
            for (int i = 0; i < 4; i++)
            {
                float z = 9 + i * 1.1f;
                b.Box(g, "Locker", new Vector3(-9.85f, 0, z), new Vector3(-9.2f, 2.0f, z + 1.0f), Red);
            }
            // Alarm bell by the front
            b.AddMeshPart(g, mb => mb.AddSphereBand(new Vector3(3.6f, 3.2f, 1.2f), Vector3.one * 0.3f, 0, 100, 10, 3, Yellow));
        }

        static void BuildWing(Blockout b)
        {
            const string g = "Building";
            // Floors
            b.Box(g, "WingFloor", new Vector3(4, 0, 0), new Vector3(14, 0.05f, 20), Wood);
            // Upstairs floor with a stairwell hole at x 6..12.5, z 18..20
            b.Box(g, "UpperFloor", new Vector3(4, Upper - 0.2f, 0), new Vector3(14, Upper, 18), Wood);
            b.Box(g, "UpperFloor", new Vector3(12.5f, Upper - 0.2f, 18), new Vector3(14, Upper, 20), Wood);
            b.Box(g, "UpperFloor", new Vector3(4, Upper - 0.2f, 18), new Vector3(6, Upper, 20), Wood);
            b.Box(g, "StairRail", new Vector3(6, Upper, 17.85f), new Vector3(12.5f, Upper + 1f, 18f), Red);
            // Stairs along the back wall rising toward +X
            b.Stairs(g, "Stairs", new Vector3(6, 0, 19), Vector3.right, 1.9f, 6.5f, Upper, 12, Wood);

            // Outer walls (front door + windows)
            b.WallX(g, "WingFront", 0, 4, 14, 0, WingTop, T, Brick,
                new O(8, 10, 0, 2.4f),                 // front door
                new O(11, 13, 1, 2.4f),                // day room window
                new O(5.5f, 7.5f, Upper + 1, Upper + 2.4f),
                new O(10.5f, 12.5f, Upper + 1, Upper + 2.4f));
            b.WallZ(g, "WingRight", 14, 0, 20, 0, WingTop, T, Brick,
                new O(4, 6, 1, 2.4f), new O(10, 12, 1, 2.4f),
                new O(4, 6, Upper + 1, Upper + 2.4f), new O(10, 12, Upper + 1, Upper + 2.4f));
            b.WallX(g, "WingBack", 20, 4, 14, 0, WingTop, T, Brick, new O(4.5f, 5.7f, 0, 2.4f));
            // Bay/wing wall above the bay roof line is covered by the wing roof
            b.WallZ(g, "WingUpperLeft", 4, 0, 20, BayTop, WingTop, T, Brick);
            // Upstairs interior: bunk room (back) / captain's office (front)
            b.WallX(g, "OfficeWall", 9, 4, 14, Upper, WingTop, 0.15f, InteriorWall, new O(6, 7.2f, Upper, Upper + 2.3f));
            // Window glass strips (emissive-ish, no collider) – just a light tint so windows read
            // Roof
            b.Box("Roof", "WingRoof", new Vector3(3.8f, WingTop, -0.2f), new Vector3(14.2f, WingTop + 0.3f, 20.2f), Roof);
            // Sign over the bay doors
            b.Box(g, "Sign", new Vector3(-8f, 5.3f, -0.35f), new Vector3(0f, 6.1f, -0.15f), Trim, collider: false);
            b.Box(g, "SignText", new Vector3(-7.6f, 5.5f, -0.4f), new Vector3(-0.4f, 5.9f, -0.34f), new Color(0.85f, 0.15f, 0.1f, 0.6f), collider: false);

            // Kitchen / day room props
            b.Box(g, "Counter", new Vector3(4.3f, 0, 15), new Vector3(5.1f, 0.95f, 17.8f), Metal);
            b.Box(g, "Stove", new Vector3(4.3f, 0, 12.5f), new Vector3(5.1f, 0.95f, 13.7f), Dark);
            b.Box(g, "Fridge", new Vector3(4.3f, 0, 11), new Vector3(5.1f, 1.9f, 12.2f), Trim);
            b.Box(g, "Table", new Vector3(7, 0.7f, 12), new Vector3(9.5f, 0.8f, 14), Wood);
            b.Box(g, "TableLeg", new Vector3(7.1f, 0, 12.1f), new Vector3(9.4f, 0.7f, 13.9f), Wood, collider: false);
            b.Box(g, "Couch", new Vector3(10.5f, 0, 4.5f), new Vector3(13.6f, 0.5f, 5.5f), Couch);
            b.Box(g, "CouchBack", new Vector3(10.5f, 0.5f, 5.2f), new Vector3(13.6f, 1.1f, 5.5f), Couch);
            b.Box(g, "TV", new Vector3(11.4f, 0.6f, 1.0f), new Vector3(12.8f, 1.5f, 1.1f), Dark);
            b.Box(g, "TVStand", new Vector3(11.2f, 0, 0.6f), new Vector3(13f, 0.6f, 1.2f), Wood);
            b.Box(g, "PokerTable", new Vector3(5.5f, 0.75f, 4), new Vector3(7.5f, 0.85f, 6), new Color(0.15f, 0.45f, 0.25f));
            b.Box(g, "PokerBase", new Vector3(6.3f, 0, 4.8f), new Vector3(6.7f, 0.75f, 5.2f), Dark);
            // Upstairs: bunks (back) and captain's desk (front)
            for (int i = 0; i < 4; i++)
            {
                float x = 9.5f + (i % 2) * 2.2f;
                float z = 10 + (i / 2) * 3.2f;
                b.Box(g, "Bunk", new Vector3(x, Upper, z), new Vector3(x + 1.0f, Upper + 0.5f, z + 2.1f), Couch);
            }
            b.Box(g, "Desk", new Vector3(10, Upper, 3), new Vector3(12.5f, Upper + 0.8f, 4.2f), Wood);
            b.Box(g, "MissionBoard", new Vector3(13.8f, Upper + 1, 6.5f), new Vector3(13.85f, Upper + 2.3f, 8.5f), new Color(0.9f, 0.85f, 0.6f));
        }

        static void BuildBackLot(Blockout b)
        {
            const string g = "BackLot";
            b.Box(g, "LotSlab", new Vector3(-12, 0, 20), new Vector3(16, 0.04f, 40), Concrete);
            // Fence
            b.Box(g, "Fence", new Vector3(-12.1f, 0, 20), new Vector3(-11.9f, 1.6f, 40), Fence);
            b.Box(g, "Fence", new Vector3(15.9f, 0, 20), new Vector3(16.1f, 1.6f, 40), Fence);
            b.Box(g, "Fence", new Vector3(-12, 0, 39.9f), new Vector3(16, 1.6f, 40.1f), Fence);
            // Training tower (open frame + platforms)
            const float tx = 8, tz = 32, s = 3, h = 9;
            foreach (var (dx, dz) in new[] { (0f, 0f), (s, 0f), (0f, s), (s, s) })
                b.Box(g, "TowerPost", new Vector3(tx + dx - 0.15f, 0, tz + dz - 0.15f), new Vector3(tx + dx + 0.15f, h, tz + dz + 0.15f), Red);
            for (int lvl = 1; lvl <= 3; lvl++)
                b.Box(g, "TowerDeck", new Vector3(tx - 0.15f, lvl * 3 - 0.15f, tz - 0.15f), new Vector3(tx + s + 0.15f, lvl * 3, tz + s + 0.15f), Wood);
            // Ladder rungs on the tower face (climbing comes in step 2)
            for (float y = 0.3f; y < h; y += 0.35f)
                b.Box(g, "Rung", new Vector3(tx + 1, y, tz - 0.25f), new Vector3(tx + 2, y + 0.05f, tz - 0.2f), Metal, collider: false);
            // Hose target cans on a plank
            b.Box(g, "Plank", new Vector3(-8, 0, 34), new Vector3(-2, 1, 34.6f), Wood);
            for (int i = 0; i < 6; i++)
                b.AddMeshPart(g, mb => mb.AddCylinder(new Vector3(-7.5f + i, 1.15f, 34.3f), 0.12f, 0.12f, 0.3f, 8, Red));
        }

        static void BuildLighting(Transform root)
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48, -35, 0);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
            RenderSettings.fogStartDistance = 60;
            RenderSettings.fogEndDistance = 160;

            var lights = new GameObject("InteriorLights").transform;
            lights.SetParent(root, false);
            void Point(string n, Vector3 p, float range, float intensity)
            {
                var go = new GameObject(n);
                go.transform.SetParent(lights, false);
                go.transform.position = p;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.intensity = intensity;
                l.color = new Color(1f, 0.9f, 0.75f);
                l.shadows = LightShadows.None;
            }
            Point("BayLight A", new Vector3(-6, 5.8f, 6), 12, 2.5f);
            Point("BayLight B", new Vector3(-1, 5.8f, 6), 12, 2.5f);
            Point("LoftLight", new Vector3(-3, 6f, 17), 9, 1.8f);
            Point("DayRoomLight", new Vector3(9, 3.1f, 6), 9, 2f);
            Point("KitchenLight", new Vector3(7, 3.1f, 15), 9, 2f);
            Point("BunkLight", new Vector3(10, 6.6f, 14), 9, 1.6f);
            Point("OfficeLight", new Vector3(10, 6.6f, 4), 8, 1.6f);
        }

        static void BuildSpawns(Transform root)
        {
            var parent = new GameObject("SpawnPoints").transform;
            parent.SetParent(root, false);
            // Two rows of four at the back of the bay, facing the doors.
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject($"Spawn {i + 1}");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(-8 + (i % 4) * 2.4f, 0.05f, 11 - (i / 4) * 1.8f);
                go.transform.rotation = Quaternion.LookRotation(Vector3.back);
                go.AddComponent<SpawnPoint>();
            }
        }

        static void BuildMenuCamera()
        {
            var go = new GameObject("MenuCamera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55;
            go.AddComponent<AudioListener>();
            go.transform.SetPositionAndRotation(new Vector3(-14, 7.5f, -24), Quaternion.Euler(14, 32, 0));
        }

        public static void AddSceneToBuild(string path, bool first = false)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path && s.path != "Assets/Scenes/SampleScene.unity").ToList();
            var entry = new EditorBuildSettingsScene(path, true);
            if (first) scenes.Insert(0, entry);
            else scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
