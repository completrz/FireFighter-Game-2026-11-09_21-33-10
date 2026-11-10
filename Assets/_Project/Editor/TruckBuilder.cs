using System.Collections.Generic;
using Firehouse.Core;
using Firehouse.Truck;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Builds the starter mini-pumper (Prefabs/P_Truck_Starter.prefab): low-poly body with a hollow crew cab,
    /// four raycast wheels, 8 ride spots (driver, 3 cab, 2 back step, 2 on the hose bed), lightbar, horn/siren/engine.
    /// Local space: +Z forward, ground ≈ y 0 at rest.
    /// </summary>
    public static class TruckBuilder
    {
        public const string PrefabPath = ProjectAssets.PrefabsFolder + "/P_Truck_Starter.prefab";
        public const string StatsPath = ProjectAssets.DataFolder + "/TruckStats_Starter.asset";

        static readonly Color Red = new(0.80f, 0.12f, 0.10f);
        static readonly Color RedDark = new(0.55f, 0.08f, 0.07f);
        static readonly Color Chrome = new(0.82f, 0.84f, 0.86f);
        static readonly Color Dark = new(0.13f, 0.13f, 0.14f);
        static readonly Color Tire = new(0.10f, 0.10f, 0.11f);
        static readonly Color Hub = new(0.85f, 0.85f, 0.82f);
        static readonly Color SeatColor = new(0.20f, 0.20f, 0.22f);
        static readonly Color Stripe = new(0.95f, 0.95f, 0.85f, 0.7f);
        static readonly Color Yellow = new(0.95f, 0.78f, 0.15f);
        static readonly Color Headlight = new(1f, 0.95f, 0.75f, 0f);
        static readonly Color HoseYellow = new(0.95f, 0.80f, 0.25f);
        static readonly Color Lens = new(0.25f, 0.25f, 0.28f); // lightbar lens base (glow added at runtime)

        const float MountY = 0.8f;
        const float WheelX = 1.0f, FrontZ = 1.95f, RearZ = -1.75f;

        [MenuItem("Tools/Firehouse/Build/Truck (Starter)")]
        public static GameObject Build()
        {
            ProjectAssets.EnsureFolder(ProjectAssets.PrefabsFolder);
            var mat = ProjectAssets.SharedMaterial;
            var stats = LoadOrCreateStats();

            var root = new GameObject("P_Truck_Starter") { layer = Layers.Vehicle };
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = stats.Mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;

            var no = root.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            root.AddComponent<NetworkRigidbody>();

            // ---- body mesh (static parts)
            var body = new MeshBuilder();
            BuildBody(body);
            Part(root.transform, "Body", ProjectAssets.SaveMesh(body.Build(), "Truck_Starter_Body"), mat);

            // ---- lightbar halves (separate renderers so they can flash)
            var lbL = Part(root.transform, "LightbarLeft",
                ProjectAssets.SaveMesh(new MeshBuilder().AddBox(new Vector3(-0.3f, 2.62f, 2.15f), new Vector3(0.56f, 0.14f, 0.3f), Lens).Build(), "Truck_LightbarL"), mat);
            var lbR = Part(root.transform, "LightbarRight",
                ProjectAssets.SaveMesh(new MeshBuilder().AddBox(new Vector3(0.3f, 2.62f, 2.15f), new Vector3(0.56f, 0.14f, 0.3f), Lens).Build(), "Truck_LightbarR"), mat);

            // ---- colliders
            Box(root, "Col_Body", new Vector3(0, 1.3f, 0.12f), new Vector3(2.1f, 1.2f, 5.66f));        // y 0.7..1.9, z -2.7..2.95
            Box(root, "Col_Cab", new Vector3(0, 2.22f, 1.97f), new Vector3(2.1f, 0.66f, 1.96f));      // y 1.9..2.55
            Box(root, "Col_Chassis", new Vector3(0, 0.58f, 0.05f), new Vector3(1.6f, 0.25f, 5.9f));
            Box(root, "Col_BackStep", new Vector3(0, 0.56f, -2.93f), new Vector3(2.0f, 0.12f, 0.46f));
            Box(root, "Col_Bumper", new Vector3(0, 0.62f, 3.05f), new Vector3(2.2f, 0.25f, 0.2f));

            // ---- wheels
            var wheelMesh = ProjectAssets.SaveMesh(BuildWheel(stats.WheelRadius), "Truck_Wheel");
            var wheels = new List<TruckController.Wheel>();
            foreach (var (x, z, front, name) in new[] { (-WheelX, FrontZ, true, "FL"), (WheelX, FrontZ, true, "FR"), (-WheelX, RearZ, false, "RL"), (WheelX, RearZ, false, "RR") })
            {
                var mount = new GameObject($"WheelMount_{name}").transform;
                mount.SetParent(root.transform, false);
                mount.localPosition = new Vector3(x, MountY, z);
                var vis = new GameObject($"Wheel_{name}").transform;
                vis.SetParent(root.transform, false);
                vis.localPosition = new Vector3(x, MountY - stats.RestLength, z);
                var mesh = Part(vis, "Mesh", wheelMesh, mat);
                mesh.transform.localRotation = Quaternion.identity;
                wheels.Add(new TruckController.Wheel { Mount = mount, Visual = vis, Front = front });
            }

            // ---- run-over volume
            var hit = new GameObject("HitVolume").transform;
            hit.SetParent(root.transform, false);
            hit.localPosition = new Vector3(0, 1.2f, 0.2f);
            hit.localScale = new Vector3(2.5f, 1.8f, 6.6f);

            // ---- seats
            var seatsRoot = new GameObject("Seats").transform;
            seatsRoot.SetParent(root.transform, false);
            var seatList = new List<TruckSeat>
            {
                Seat(seatsRoot, "Driver", new Vector3(-0.5f, 0.75f, 2.2f), 0, SeatKind.Driver, "Drive", new Vector3(-1.7f, 0, 0)),
                Seat(seatsRoot, "CabFront", new Vector3(0.5f, 0.75f, 2.2f), 0, SeatKind.Cab, "Ride (front)", new Vector3(1.7f, 0, 0)),
                Seat(seatsRoot, "CabRearL", new Vector3(-0.5f, 0.75f, 1.45f), 0, SeatKind.Cab, "Ride (cab)", new Vector3(-1.7f, 0, 0)),
                Seat(seatsRoot, "CabRearR", new Vector3(0.5f, 0.75f, 1.45f), 0, SeatKind.Cab, "Ride (cab)", new Vector3(1.7f, 0, 0)),
                Seat(seatsRoot, "BackStepL", new Vector3(-0.55f, 0.62f, -3.0f), 0, SeatKind.BackStep, "Ride the back step", new Vector3(0, 0, -1.3f)),
                Seat(seatsRoot, "BackStepR", new Vector3(0.55f, 0.62f, -3.0f), 0, SeatKind.BackStep, "Ride the back step", new Vector3(0, 0, -1.3f)),
                Seat(seatsRoot, "HoseBedA", new Vector3(-0.35f, 2.0f, -0.6f), 0, SeatKind.BackStep, "Ride on the hose bed", new Vector3(-1.9f, -1.9f, 0)),
                Seat(seatsRoot, "HoseBedB", new Vector3(0.35f, 2.0f, -1.8f), 0, SeatKind.BackStep, "Ride on the hose bed", new Vector3(1.9f, -1.9f, 0)),
            };

            // ---- audio + lights
            var engine = Audio(root, "EngineAudio", new Vector3(0, 1f, 2.6f), 3f, 45f);
            var horn = Audio(root, "HornAudio", new Vector3(0, 1f, 3f), 4f, 90f);
            var siren = Audio(root, "SirenAudio", new Vector3(0, 2.6f, 2.15f), 6f, 140f);
            var flashL = PointLight(root, "FlashLeft", new Vector3(-0.3f, 2.9f, 2.15f), new Color(1f, 0.15f, 0.1f));
            var flashR = PointLight(root, "FlashRight", new Vector3(0.3f, 2.9f, 2.15f), new Color(0.2f, 0.4f, 1f));
            var head = new[] { SpotLight(root, "HeadlightL", new Vector3(-0.75f, 1.0f, 3.0f)), SpotLight(root, "HeadlightR", new Vector3(0.75f, 1.0f, 3.0f)) };

            // ---- behaviours
            var controller = root.AddComponent<TruckController>();
            NetworkPrefabsBuilder.SetField(controller, "_stats", stats);
            SetWheels(controller, wheels);
            NetworkPrefabsBuilder.SetField(controller, "_hitVolume", hit);

            var seats = root.AddComponent<TruckSeats>();
            NetworkPrefabsBuilder.SetField(seats, "_seats", seatList.ToArray());

            var fx = root.AddComponent<TruckEffects>();
            NetworkPrefabsBuilder.SetField(fx, "_truck", controller);
            NetworkPrefabsBuilder.SetField(fx, "_engine", engine);
            NetworkPrefabsBuilder.SetField(fx, "_horn", horn);
            NetworkPrefabsBuilder.SetField(fx, "_siren", siren);
            NetworkPrefabsBuilder.SetField(fx, "_lightbarLeft", lbL.GetComponent<Renderer>());
            NetworkPrefabsBuilder.SetField(fx, "_lightbarRight", lbR.GetComponent<Renderer>());
            NetworkPrefabsBuilder.SetField(fx, "_flashLeft", flashL);
            NetworkPrefabsBuilder.SetField(fx, "_flashRight", flashR);
            NetworkPrefabsBuilder.SetField(fx, "_headlights", head);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            NetworkPrefabsBuilder.RegisterNetworkPrefab(prefab);
            Debug.Log($"[Firehouse] Built {PrefabPath}");
            return prefab;
        }

        // ------------------------------------------------------------------ geometry

        static void BuildBody(MeshBuilder mb)
        {
            // Chassis + bumpers
            mb.AddBox(new Vector3(0, 0.58f, 0.05f), new Vector3(1.6f, 0.25f, 5.9f), Dark);
            mb.AddBox(new Vector3(0, 0.62f, 3.05f), new Vector3(2.2f, 0.25f, 0.2f), Chrome);
            mb.AddBox(new Vector3(0, 0.56f, -2.93f), new Vector3(2.0f, 0.12f, 0.46f), Chrome); // back step (diamond plate)

            // Apparatus body behind the cab (z -2.7..1.0, y 0.7..1.9)
            mb.AddBox(new Vector3(0, 1.3f, -0.85f), new Vector3(2.1f, 1.2f, 3.7f), Red);
            // Roll-up compartment doors on both sides
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                float z = -2.25f + i * 1.15f;
                mb.AddBox(new Vector3(side * 1.06f, 1.25f, z), new Vector3(0.03f, 0.85f, 1.0f), Chrome);
                for (int r = 0; r < 4; r++)
                    mb.AddBox(new Vector3(side * 1.08f, 0.95f + r * 0.2f, z), new Vector3(0.01f, 0.02f, 0.96f), Dark);
            }
            // Reflective stripe along the sides and back
            mb.AddBox(new Vector3(-1.065f, 1.78f, 0.1f), new Vector3(0.01f, 0.08f, 5.6f), Stripe);
            mb.AddBox(new Vector3(1.065f, 1.78f, 0.1f), new Vector3(0.01f, 0.08f, 5.6f), Stripe);
            mb.AddBox(new Vector3(0, 1.6f, -2.705f), new Vector3(2.0f, 0.08f, 0.01f), Stripe);
            // Hose bed on top + hose folds
            mb.AddBox(new Vector3(0, 1.95f, -0.85f), new Vector3(1.9f, 0.1f, 3.5f), Dark);
            for (int i = 0; i < 6; i++)
                mb.AddBox(new Vector3(0.55f, 2.03f, -2.4f + i * 0.25f), new Vector3(0.6f, 0.08f, 0.18f), HoseYellow);
            // Hose reel at the back (step 5 pulls the hose from here)
            mb.SetTRS(new Vector3(0, 2.22f, -2.45f), Quaternion.Euler(0, 0, 90), Vector3.one);
            mb.AddCylinder(Vector3.zero, 0.36f, 0.36f, 1.3f, 12, Red);
            mb.AddCylinder(Vector3.zero, 0.3f, 0.3f, 1.1f, 12, HoseYellow, caps: false);
            mb.ResetTransform();
            // Ground ladder on the left of the hose bed
            for (int rail = 0; rail < 2; rail++)
                mb.AddBox(new Vector3(-0.95f + rail * 0.35f, 2.06f, -0.85f), new Vector3(0.05f, 0.05f, 3.4f), Chrome);
            for (int r = 0; r < 10; r++)
                mb.AddBox(new Vector3(-0.775f, 2.06f, -2.4f + r * 0.34f), new Vector3(0.35f, 0.03f, 0.03f), Chrome);
            // Rear grab rails for the back step riders
            foreach (float x in new[] { -1.0f, 1.0f })
            {
                mb.AddCylinder(new Vector3(x, 1.35f, -2.75f), 0.035f, 0.035f, 1.1f, 6, Yellow);
            }
            // Fenders over the wheels
            foreach (float x in new[] { -WheelX, WheelX })
            foreach (float z in new[] { FrontZ, RearZ })
                mb.AddBox(new Vector3(x * 1.06f, 1.02f, z), new Vector3(0.4f, 0.06f, 1.15f), Dark);

            BuildCab(mb);
        }

        static void BuildCab(MeshBuilder mb)
        {
            // Hollow crew cab: x ±1.05, z 1.0..2.95, y 0.7..2.55, with open windows so riders are visible.
            const float x0 = -1.05f, x1 = 1.05f, z0 = 1.0f, z1 = 2.95f, yF = 0.7f, yR = 2.55f;
            void B(float ax, float ay, float az, float bx, float by, float bz, Color c) =>
                mb.AddBox(new Vector3((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2), new Vector3(bx - ax, by - ay, bz - az), c);

            B(x0, yF, z0, x1, yF + 0.05f, z1, Dark);                 // floor
            B(x0, yR - 0.1f, z0, x1, yR, z1, Red);                   // roof
            B(x0, yF, z0, x1, yR, z0 + 0.08f, RedDark);              // back wall
            // Front: dash wall, windshield frame with a center pillar
            B(x0, yF, z1 - 0.1f, x1, 1.5f, z1, Red);
            B(x0, 2.35f, z1 - 0.1f, x1, yR - 0.1f, z1, Red);
            B(-0.05f, 1.5f, z1 - 0.1f, 0.05f, 2.35f, z1, Dark);
            B(x0, 1.5f, z1 - 0.1f, x0 + 0.08f, 2.35f, z1, Dark);
            B(x1 - 0.08f, 1.5f, z1 - 0.1f, x1, 2.35f, z1, Dark);
            // Sides: lower panel, window posts, top strip
            foreach (var (xa, xb) in new[] { (x0, x0 + 0.05f), (x1 - 0.05f, x1) })
            {
                B(xa, yF, z0, xb, 1.45f, z1, Red);
                B(xa, 2.3f, z0, xb, yR - 0.1f, z1, Red);
                B(xa, 1.45f, z0, xb, 2.3f, z0 + 0.15f, Red);
                B(xa, 1.45f, 1.85f, xb, 2.3f, 1.95f, Red);
                B(xa, 1.45f, z1 - 0.2f, xb, 2.3f, z1, Red);
            }
            // Reflective stripe on the doors
            B(x0 - 0.01f, 1.2f, z0, x0, 1.28f, z1, Stripe);
            B(x1, 1.2f, z0, x1 + 0.01f, 1.28f, z1, Stripe);
            // Interior: dashboard, steering wheel, seats
            B(x0 + 0.05f, 1.1f, 2.6f, x1 - 0.05f, 1.35f, z1 - 0.1f, Dark);
            mb.SetTRS(new Vector3(-0.5f, 1.45f, 2.5f), Quaternion.Euler(-60, 0, 0), Vector3.one);
            mb.AddCylinder(Vector3.zero, 0.2f, 0.2f, 0.04f, 10, Dark);
            mb.ResetTransform();
            foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { 2.2f, 1.45f })
            {
                B(x - 0.28f, yF, z - 0.28f, x + 0.28f, 1.05f, z + 0.28f, SeatColor);
                B(x - 0.28f, 1.05f, z - 0.32f, x + 0.28f, 1.75f, z - 0.24f, SeatColor);
            }
            // Front: grille + headlights + bumper lights
            B(-0.6f, 0.8f, z1, 0.6f, 1.35f, z1 + 0.02f, Chrome);
            B(-0.95f, 0.9f, z1, -0.55f, 1.1f, z1 + 0.04f, Headlight);
            B(0.55f, 0.9f, z1, 0.95f, 1.1f, z1 + 0.04f, Headlight);
            // Lightbar base
            B(-0.62f, yR, 2.0f, 0.62f, yR + 0.04f, 2.3f, Dark);
        }

        static Mesh BuildWheel(float radius)
        {
            var mb = new MeshBuilder();
            mb.SetTRS(Vector3.zero, Quaternion.Euler(0, 0, 90), Vector3.one);
            mb.AddCylinder(Vector3.zero, radius, radius, 0.34f, 12, Tire);
            mb.AddCylinder(Vector3.zero, radius * 0.5f, radius * 0.5f, 0.36f, 8, Hub);
            mb.ResetTransform();
            return mb.Build();
        }

        // ------------------------------------------------------------------ helpers

        static TruckSeat Seat(Transform parent, string name, Vector3 pos, float yaw, SeatKind kind, string label, Vector3 exit)
        {
            var go = new GameObject($"Seat_{name}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var seat = go.AddComponent<TruckSeat>();
            NetworkPrefabsBuilder.SetField(seat, "_kind", (int)kind);
            NetworkPrefabsBuilder.SetField(seat, "_label", label);
            var so = new SerializedObject(seat);
            so.FindProperty("_exitOffset").vector3Value = exit;
            so.ApplyModifiedPropertiesWithoutUndo();
            return seat;
        }

        static void SetWheels(TruckController controller, List<TruckController.Wheel> wheels)
        {
            var so = new SerializedObject(controller);
            var arr = so.FindProperty("_wheels");
            arr.arraySize = wheels.Count;
            for (int i = 0; i < wheels.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Mount").objectReferenceValue = wheels[i].Mount;
                e.FindPropertyRelative("Visual").objectReferenceValue = wheels[i].Visual;
                e.FindPropertyRelative("Front").boolValue = wheels[i].Front;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name) { layer = Layers.Vehicle };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name) { layer = Layers.Vehicle };
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        static AudioSource Audio(GameObject root, string name, Vector3 pos, float min, float max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = min;
            s.maxDistance = max;
            return s;
        }

        static Light PointLight(GameObject root, string name, Vector3 pos, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = 14f;
            l.intensity = 4f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        static Light SpotLight(GameObject root, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(8, 0, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.95f, 0.8f);
            l.range = 35f;
            l.spotAngle = 60f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        public static TruckStats LoadOrCreateStats()
        {
            var s = AssetDatabase.LoadAssetAtPath<TruckStats>(StatsPath);
            if (s != null) return s;
            ProjectAssets.EnsureFolder(ProjectAssets.DataFolder);
            s = ScriptableObject.CreateInstance<TruckStats>();
            AssetDatabase.CreateAsset(s, StatsPath);
            return s;
        }
    }
}
