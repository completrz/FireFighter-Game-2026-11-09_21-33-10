using System.Collections.Generic;
using Firehouse.Core;
using Firehouse.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Firehouse.EditorTools
{
    /// <summary>
    /// Builds the Smokies character meshes and the networked player prefab (Prefabs/P_Player.prefab).
    /// Root is at the feet, +Z forward. Coat and helmet renderers are white and tinted per player (coat color);
    /// gear (boots, stripes, tank, mask) keeps its own vertex colors.
    /// </summary>
    public static class SmokieBuilder
    {
        public const string PlayerPrefabPath = ProjectAssets.PrefabsFolder + "/P_Player.prefab";
        public const string StatsPath = ProjectAssets.DataFolder + "/PlayerStats.asset";

        // Proportions
        const float BodyCenterY = 0.68f;
        static readonly Vector3 BodyRadii = new(0.42f, 0.58f, 0.38f);
        static readonly Vector3 HeadPivot = new(0f, 0.98f, 0f);
        static readonly Vector3 CameraPos = new(0f, 1.12f, 0.36f);
        const float ShoulderY = 0.92f, ShoulderX = 0.40f;
        const float UpperArm = 0.30f, Forearm = 0.30f, ArmRadius = 0.07f, HandRadius = 0.09f;

        // Palette
        static readonly Color White = Color.white;
        static readonly Color Reflective = new(0.85f, 0.95f, 0.35f, 0.55f); // alpha < 1 = glows a bit
        static readonly Color Boot = new(0.12f, 0.12f, 0.13f);
        static readonly Color Glove = new(0.30f, 0.24f, 0.17f);
        static readonly Color Tank = new(0.72f, 0.74f, 0.78f);
        static readonly Color TankCap = new(0.80f, 0.15f, 0.12f);
        static readonly Color Strap = new(0.15f, 0.15f, 0.16f);
        static readonly Color MaskDark = new(0.20f, 0.22f, 0.25f);
        static readonly Color MaskRim = new(0.08f, 0.08f, 0.09f);
        static readonly Color Hood = new(0.16f, 0.16f, 0.17f);
        static readonly Color Lens = new(0.80f, 0.92f, 1.00f, 0.75f);
        static readonly Color Pupil = new(0.05f, 0.05f, 0.07f);
        static readonly Color Leather = new(0.35f, 0.20f, 0.10f);
        static readonly Color Grille = new(0.30f, 0.32f, 0.35f);

        [MenuItem("Tools/Firehouse/Build/Player Prefab (Smokie)")]
        public static GameObject Build()
        {
            ProjectAssets.EnsureFolder(ProjectAssets.PrefabsFolder);
            var mat = ProjectAssets.SharedMaterial;
            var stats = LoadOrCreateStats();
            var playerMat = LoadOrCreatePlayerPhysicsMaterial();

            var root = new GameObject("P_Player") { layer = Layers.Player };

            // --- physics + networking
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.5f;
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.38f;
            col.height = 1.6f;
            col.center = new Vector3(0, 0.8f, 0);
            col.sharedMaterial = playerMat;

            var no = root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            root.AddComponent<NetworkRigidbody>();

            // --- visuals
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var body = Part(visual, "Body", Save(BuildBody(), "Smokie_Body"), mat);
            Part(visual, "Gear", Save(BuildGear(), "Smokie_Gear"), mat);

            var head = new GameObject("Head").transform;
            head.SetParent(visual, false);
            head.localPosition = HeadPivot;
            var helmet = Part(head, "Helmet", Save(BuildHelmet(), "Smokie_Helmet"), mat);
            var headGear = Part(head, "HeadGear", Save(BuildHeadGear(), "Smokie_HeadGear"), mat);
            var eyesNormal = Save(BuildEyes(EyeStyle.Normal), "Smokie_Eyes_Normal");
            var eyesWide = Save(BuildEyes(EyeStyle.Wide), "Smokie_Eyes_Wide");
            var eyesX = Save(BuildEyes(EyeStyle.KnockedOut), "Smokie_Eyes_X");
            var eyes = Part(head, "Eyes", eyesNormal, mat);
            var grille = Part(head, "Grille", Save(BuildGrille(), "Smokie_Grille"), mat);

            // --- camera
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = CameraPos;
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.04f;
            cam.fieldOfView = stats.FieldOfView;
            cam.enabled = false;
            var listener = camGo.AddComponent<AudioListener>();
            listener.enabled = false;

            // --- arms
            var armMesh = Save(new MeshBuilder().AddCapsule(Vector3.zero, ArmRadius, UpperArm + ArmRadius, 8, White).Build(), "Smokie_ArmSegment");
            var handMesh = Save(new MeshBuilder().AddSphere(Vector3.zero, Vector3.one * HandRadius, 8, 5, Glove).Build(), "Smokie_Hand");
            var left = BuildArm(root, rb, -1, armMesh, handMesh, mat, out var leftCoat);
            var right = BuildArm(root, rb, +1, armMesh, handMesh, mat, out var rightCoat);

            // --- behaviours
            var coatRenderers = new List<Renderer> { body.GetComponent<Renderer>(), helmet.GetComponent<Renderer>() };
            coatRenderers.AddRange(leftCoat);
            coatRenderers.AddRange(rightCoat);

            var identity = root.AddComponent<PlayerIdentity>();
            NetworkPrefabsBuilder.SetField(identity, "_coatRenderers", coatRenderers.ToArray());

            var lpc = root.AddComponent<LocalPlayerCamera>();
            NetworkPrefabsBuilder.SetField(lpc, "_camera", cam);
            NetworkPrefabsBuilder.SetField(lpc, "_listener", listener);
            NetworkPrefabsBuilder.SetField(lpc, "_hideForOwner", new Renderer[]
            {
                helmet.GetComponent<Renderer>(), headGear.GetComponent<Renderer>(),
                eyes.GetComponent<Renderer>(), grille.GetComponent<Renderer>()
            });

            var controller = root.AddComponent<SmokieController>();
            NetworkPrefabsBuilder.SetField(controller, "_stats", stats);
            NetworkPrefabsBuilder.SetField(controller, "_cameraPivot", pivot);
            NetworkPrefabsBuilder.SetField(controller, "_camera", cam);
            NetworkPrefabsBuilder.SetField(controller, "_headPivot", head);

            root.AddComponent<PlayerGrabber>();

            var face = root.AddComponent<SmokieFace>();
            NetworkPrefabsBuilder.SetField(face, "_eyes", eyes.GetComponent<MeshFilter>());
            NetworkPrefabsBuilder.SetField(face, "_normal", eyesNormal);
            NetworkPrefabsBuilder.SetField(face, "_wide", eyesWide);
            NetworkPrefabsBuilder.SetField(face, "_knockedOut", eyesX);

            var arms = root.AddComponent<SmokieArms>();
            NetworkPrefabsBuilder.SetField(arms, "_left", left);
            NetworkPrefabsBuilder.SetField(arms, "_right", right);

            // --- proximity voice: 3D source at the mask grille
            var voiceGo = new GameObject("VoiceSource");
            voiceGo.transform.SetParent(head, false);
            voiceGo.transform.localPosition = new Vector3(0, -0.05f, 0.4f);
            var src = voiceGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            voiceGo.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            var playback = voiceGo.AddComponent<Firehouse.Audio.VoicePlayback>();
            var voice = root.AddComponent<SmokieVoice>();
            NetworkPrefabsBuilder.SetField(voice, "_settings", LoadOrCreateVoiceSettings());
            NetworkPrefabsBuilder.SetField(voice, "_playback", playback);
            NetworkPrefabsBuilder.SetField(voice, "_grille", grille.GetComponent<Renderer>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            NetworkPrefabsBuilder.RegisterNetworkPrefab(prefab);
            Debug.Log($"[Firehouse] Built {PlayerPrefabPath} (body+gear+head ≈ {CountTris(prefab)} tris)");
            return prefab;
        }

        // ------------------------------------------------------------------ meshes

        static Mesh BuildBody() =>
            new MeshBuilder().AddSphere(new Vector3(0, BodyCenterY, 0), BodyRadii, 14, 9, White).Build();

        static Mesh BuildGear()
        {
            var mb = new MeshBuilder();
            // Reflective stripes around the coat (slightly larger ellipsoid bands)
            var stripeRadii = BodyRadii * 1.025f;
            mb.AddSphereBand(new Vector3(0, BodyCenterY, 0), stripeRadii, 100, 110, 14, 1, Reflective);
            mb.AddSphereBand(new Vector3(0, BodyCenterY, 0), stripeRadii, 128, 136, 14, 1, Reflective);
            // Boots
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                mb.AddCylinder(new Vector3(x, 0.11f, 0f), 0.13f, 0.12f, 0.22f, 8, Boot);
                mb.AddBox(new Vector3(x, 0.06f, 0.12f), new Vector3(0.22f, 0.12f, 0.14f), Boot);
            }
            // Air tank on the back + straps
            mb.AddCylinder(new Vector3(0, 0.80f, -0.44f), 0.12f, 0.12f, 0.56f, 10, Tank);
            mb.AddCylinder(new Vector3(0, 1.12f, -0.44f), 0.06f, 0.05f, 0.08f, 8, TankCap);
            foreach (float x in new[] { -0.2f, 0.2f })
                mb.AddBox(new Vector3(x, 0.85f, 0f), new Vector3(0.06f, 0.5f, 0.82f), Strap);
            // Coat clasps
            for (int i = 0; i < 3; i++)
                mb.AddBox(new Vector3(0, 0.55f + i * 0.16f, BodyRadii.z * 0.97f - i * 0.02f), new Vector3(0.1f, 0.035f, 0.04f), Strap);
            return mb.Build();
        }

        static Mesh BuildHelmet()
        {
            var mb = new MeshBuilder();
            // Oversized dome
            mb.AddSphereBand(new Vector3(0, 0.30f, 0.02f), new Vector3(0.46f, 0.40f, 0.50f), 0, 92, 14, 5, White);
            // Brim: wide ellipse, longer at the back (classic firefighter helmet)
            mb.SetTRS(new Vector3(0, 0.28f, -0.08f), Quaternion.identity, new Vector3(1.0f, 1f, 1.2f));
            mb.AddCylinder(Vector3.zero, 0.58f, 0.56f, 0.05f, 16, White);
            mb.ResetTransform();
            // Ridge on top
            mb.AddBox(new Vector3(0, 0.70f, -0.02f), new Vector3(0.06f, 0.05f, 0.75f), White);
            return mb.Build();
        }

        static Mesh BuildHeadGear()
        {
            var mb = new MeshBuilder();
            // Hood/neck under the helmet
            mb.AddCylinder(new Vector3(0, 0.08f, 0), 0.26f, 0.30f, 0.36f, 12, Hood);
            // Leather front shield (helmet number goes here later), hugging the front of the dome
            mb.SetTRS(new Vector3(0, 0.44f, 0.50f), Quaternion.Euler(-18, 0, 0), Vector3.one);
            mb.AddBox(Vector3.zero, new Vector3(0.22f, 0.18f, 0.03f), Leather);
            // Round breathing mask facing +Z
            mb.SetTRS(new Vector3(0, 0.06f, 0.30f), Quaternion.Euler(90, 0, 0), Vector3.one);
            mb.AddCylinder(Vector3.zero, 0.30f, 0.30f, 0.06f, 16, MaskRim);
            mb.SetTRS(new Vector3(0, 0.06f, 0.35f), Quaternion.Euler(90, 0, 0), Vector3.one);
            mb.AddCylinder(Vector3.zero, 0.27f, 0.25f, 0.08f, 16, MaskDark);
            mb.ResetTransform();
            return mb.Build();
        }

        enum EyeStyle { Normal, Wide, KnockedOut }

        static Mesh BuildEyes(EyeStyle style)
        {
            var mb = new MeshBuilder();
            foreach (float x in new[] { -0.11f, 0.11f })
            {
                var c = new Vector3(x, 0.10f, 0.40f);
                // Lens disc
                mb.SetTRS(c, Quaternion.Euler(90, 0, 0), Vector3.one);
                mb.AddCylinder(Vector3.zero, 0.095f, 0.095f, 0.02f, 14, Lens);
                switch (style)
                {
                    case EyeStyle.Normal:
                        mb.SetTRS(c + new Vector3(0, 0.012f, 0.012f), Quaternion.Euler(90, 0, 0), Vector3.one);
                        mb.AddCylinder(Vector3.zero, 0.042f, 0.042f, 0.01f, 10, Pupil);
                        break;
                    case EyeStyle.Wide:
                        mb.SetTRS(c + new Vector3(0, 0f, 0.012f), Quaternion.Euler(90, 0, 0), Vector3.one);
                        mb.AddCylinder(Vector3.zero, 0.02f, 0.02f, 0.01f, 8, Pupil);
                        break;
                    case EyeStyle.KnockedOut:
                        foreach (float a in new[] { 45f, -45f })
                        {
                            mb.SetTRS(c + new Vector3(0, 0, 0.012f), Quaternion.Euler(0, 0, a), Vector3.one);
                            mb.AddBox(Vector3.zero, new Vector3(0.13f, 0.025f, 0.01f), Pupil);
                        }
                        break;
                }
            }
            mb.ResetTransform();
            return mb.Build();
        }

        static Mesh BuildGrille()
        {
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, -0.08f, 0.40f), new Vector3(0.15f, 0.07f, 0.05f), Grille);
            for (int i = 0; i < 3; i++)
                mb.AddBox(new Vector3(0, -0.10f + i * 0.02f, 0.425f), new Vector3(0.12f, 0.006f, 0.01f), MaskRim);
            return mb.Build();
        }

        // ------------------------------------------------------------------ arms

        static Rigidbody[] BuildArm(GameObject root, Rigidbody body, int side, Mesh segMesh, Mesh handMesh, Material mat, out Renderer[] coatRenderers)
        {
            var armRoot = new GameObject(side < 0 ? "LeftArm" : "RightArm").transform;
            armRoot.SetParent(root.transform, false);
            var shoulder = new Vector3(ShoulderX * side, ShoulderY, 0.02f);

            var chain = new List<Rigidbody>();
            var coat = new List<Renderer>();
            Rigidbody prev = body;
            float y = shoulder.y;
            (string name, float len, bool hand)[] segs =
            {
                ("Upper", UpperArm, false), ("Fore", Forearm, false), ("Hand", HandRadius * 2f, true)
            };
            foreach (var (name, len, isHand) in segs)
            {
                var go = new GameObject($"{armRoot.name}_{name}") { layer = Layers.PlayerArms };
                go.transform.SetParent(armRoot, false);
                go.transform.localPosition = new Vector3(shoulder.x, y - len * 0.5f, shoulder.z);
                go.AddComponent<MeshFilter>().sharedMesh = isHand ? handMesh : segMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                if (!isHand) coat.Add(r);

                if (isHand)
                {
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = HandRadius * 0.9f;
                }
                else
                {
                    var cc = go.AddComponent<CapsuleCollider>();
                    cc.radius = ArmRadius * 0.85f;
                    cc.height = len;
                }

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = isHand ? 0.3f : 0.4f;
                rb.linearDamping = 0.3f;
                rb.angularDamping = 1.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var joint = go.AddComponent<CharacterJoint>();
                joint.connectedBody = prev;
                joint.anchor = new Vector3(0, len * 0.5f, 0);
                joint.axis = Vector3.right;
                joint.swingAxis = Vector3.forward;
                joint.lowTwistLimit = new SoftJointLimit { limit = -60 };
                joint.highTwistLimit = new SoftJointLimit { limit = 60 };
                joint.swing1Limit = new SoftJointLimit { limit = 130 };
                joint.swing2Limit = new SoftJointLimit { limit = 130 };
                joint.enableProjection = true;
                joint.projectionDistance = 0.05f;

                chain.Add(rb);
                prev = rb;
                y -= len;
            }
            coatRenderers = coat.ToArray();
            return chain.ToArray();
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name) { layer = Layers.Player };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Mesh Save(Mesh m, string name) => ProjectAssets.SaveMesh(m, name);

        static int CountTris(GameObject prefab)
        {
            int t = 0;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) t += mf.sharedMesh.triangles.Length / 3;
            return t;
        }

        public static PlayerStats LoadOrCreateStats()
        {
            var stats = AssetDatabase.LoadAssetAtPath<PlayerStats>(StatsPath);
            if (stats != null) return stats;
            ProjectAssets.EnsureFolder(ProjectAssets.DataFolder);
            stats = ScriptableObject.CreateInstance<PlayerStats>();
            AssetDatabase.CreateAsset(stats, StatsPath);
            return stats;
        }

        public static Firehouse.Audio.VoiceSettings LoadOrCreateVoiceSettings()
        {
            const string path = ProjectAssets.DataFolder + "/VoiceSettings.asset";
            var s = AssetDatabase.LoadAssetAtPath<Firehouse.Audio.VoiceSettings>(path);
            if (s != null) return s;
            ProjectAssets.EnsureFolder(ProjectAssets.DataFolder);
            s = ScriptableObject.CreateInstance<Firehouse.Audio.VoiceSettings>();
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        static PhysicsMaterial LoadOrCreatePlayerPhysicsMaterial()
        {
            const string path = ProjectAssets.DataFolder + "/PM_PlayerZeroFriction.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) return pm;
            ProjectAssets.EnsureFolder(ProjectAssets.DataFolder);
            pm = new PhysicsMaterial("PM_PlayerZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }
    }
}
