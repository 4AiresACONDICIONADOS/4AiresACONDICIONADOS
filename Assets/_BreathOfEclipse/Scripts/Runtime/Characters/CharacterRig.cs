using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    public enum RigBone
    {
        Visual = 0,
        Hips = 1,
        Spine = 2,
        Chest = 3,
        Neck = 4,
        Head = 5,
        UpperArmL = 6,
        LowerArmL = 7,
        HandL = 8,
        UpperArmR = 9,
        LowerArmR = 10,
        HandR = 11,
        ThighL = 12,
        ShinL = 13,
        FootL = 14,
        ThighR = 15,
        ShinR = 16,
        FootR = 17,
        Weapon = 18
    }

    /// <summary>
    /// Builds a procedural mannequin from primitives (placeholder art that reads well in motion) and exposes
    /// bones, weapon points and renderer controls (hit flash, dissolve, accent color, first-person hiding).
    /// Limb bones point along local +Z; torso bones along local +Y.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        private struct RendererEntry
        {
            public Renderer Renderer;
            public bool Accent;
            public bool Eye;
            public Color BaseColor;
        }

        public RigProfile Profile { get; private set; }
        public Transform WeaponBase { get; private set; }
        public Transform WeaponTip { get; private set; }
        public Transform LockOnPoint { get; private set; }
        public Transform EyePoint { get; private set; }
        public float Scale => Profile != null ? Profile.scale : 1f;

        private readonly Transform[] _bones = new Transform[19];
        private readonly List<RendererEntry> _renderers = new List<RendererEntry>();
        private readonly List<Renderer> _firstPersonHidden = new List<Renderer>();
        private readonly List<MeshFilter> _meshFilters = new List<MeshFilter>();
        private MaterialPropertyBlock _mpb;
        private Renderer _edgeGlow;
        private Color _accent;
        private Color _eyeColor;
        private float _hitFlash;
        private float _dissolve;
        private Color _flashColor = Color.white;
        private Color _edgeColor = Color.black;
        private bool _dirty = true;
        private bool _visible = true;

        public Transform Bone(RigBone bone) => _bones[(int)bone];

        /// <summary>Builds the hierarchy under this transform (the character root).</summary>
        public void Build(RigProfile profile, int layer)
        {
            Profile = profile;
            _accent = profile.accent;
            _eyeColor = profile.eyes;
            _mpb = new MaterialPropertyBlock();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.gameObject.layer = layer;
            visual.localScale = Vector3.one * profile.scale;
            _bones[(int)RigBone.Visual] = visual;

            Transform hips = NewBone(RigBone.Hips, visual, new Vector3(0f, profile.hipHeight, 0f));
            Transform spine = NewBone(RigBone.Spine, hips, new Vector3(0f, 0.05f, 0f));
            Transform chest = NewBone(RigBone.Chest, spine, new Vector3(0f, profile.spineLength, 0f));
            Transform neck = NewBone(RigBone.Neck, chest, new Vector3(0f, profile.chestLength, 0f));
            Transform head = NewBone(RigBone.Head, neck, new Vector3(0f, profile.neckLength, 0f));

            Transform upperR = NewBone(RigBone.UpperArmR, chest, new Vector3(profile.shoulderWidth, profile.shoulderHeight, 0f));
            Transform lowerR = NewBone(RigBone.LowerArmR, upperR, new Vector3(0f, 0f, profile.upperArm));
            Transform handR = NewBone(RigBone.HandR, lowerR, new Vector3(0f, 0f, profile.lowerArm));
            Transform upperL = NewBone(RigBone.UpperArmL, chest, new Vector3(-profile.shoulderWidth, profile.shoulderHeight, 0f));
            Transform lowerL = NewBone(RigBone.LowerArmL, upperL, new Vector3(0f, 0f, profile.upperArm));
            Transform handL = NewBone(RigBone.HandL, lowerL, new Vector3(0f, 0f, profile.lowerArm));

            Transform thighR = NewBone(RigBone.ThighR, hips, new Vector3(profile.hipWidth, -0.03f, 0f));
            Transform shinR = NewBone(RigBone.ShinR, thighR, new Vector3(0f, 0f, profile.thigh));
            Transform footR = NewBone(RigBone.FootR, shinR, new Vector3(0f, 0f, profile.shin));
            Transform thighL = NewBone(RigBone.ThighL, hips, new Vector3(-profile.hipWidth, -0.03f, 0f));
            Transform shinL = NewBone(RigBone.ShinL, thighL, new Vector3(0f, 0f, profile.thigh));
            Transform footL = NewBone(RigBone.FootL, shinL, new Vector3(0f, 0f, profile.shin));

            // Limbs hang down in the bind pose.
            foreach (var b in new[] { upperR, upperL, thighR, thighL })
                b.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            var weapon = NewBone(RigBone.Weapon, handR, Vector3.zero);

            BuildBody(profile, hips, spine, chest, head, upperR, lowerR, handR, upperL, lowerL, handL, thighR, shinR, footR, thighL, shinL, footL);
            BuildWeapon(profile, weapon);

            LockOnPoint = new GameObject("LockOnPoint").transform;
            LockOnPoint.SetParent(chest, false);
            LockOnPoint.localPosition = new Vector3(0f, profile.chestLength * 0.4f, 0f);
            EyePoint = new GameObject("EyePoint").transform;
            EyePoint.SetParent(head, false);
            EyePoint.localPosition = new Vector3(0f, profile.headSize * 0.5f, profile.headSize * 0.3f);

            Layers.SetLayerRecursively(visual.gameObject, layer);
            _dirty = true;
            ApplyProperties();
        }

        private Transform NewBone(RigBone bone, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(bone.ToString()).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            _bones[(int)bone] = t;
            return t;
        }

        private Renderer Part(string name, Mesh mesh, Color color, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale,
            bool accent = false, bool eye = false, bool hideFirstPerson = false, float outline = -1f, Color? emission = null, float spec = 0f)
        {
            var mat = eye
                ? MaterialFactory.Toon(color, 0f, true, color, 1f, 0f)
                : MaterialFactory.Toon(color, outline < 0f ? Profile.outline : outline, true, emission, 0.5f, 0.4f, spec);
            var go = ProceduralMeshes.CreatePart(name, mesh, mat, parent, pos, Quaternion.Euler(euler), scale);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = eye ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            _renderers.Add(new RendererEntry { Renderer = r, Accent = accent, Eye = eye, BaseColor = color });
            _meshFilters.Add(go.GetComponent<MeshFilter>());
            if (hideFirstPerson) _firstPersonHidden.Add(r);
            return r;
        }

        private Renderer Part(string name, PrimitiveType type, Color color, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale,
            bool accent = false, bool eye = false, bool hideFirstPerson = false, float outline = -1f, Color? emission = null, float spec = 0f)
        {
            return Part(name, ProceduralMeshes.Primitive(type), color, parent, pos, euler, scale, accent, eye, hideFirstPerson, outline, emission, spec);
        }

        /// <summary>Capsule along a limb bone (+Z).</summary>
        private void Limb(string name, Transform bone, float length, float thickness, Color color, bool accent = false, bool hideFp = false)
        {
            Part(name, PrimitiveType.Capsule, color, bone, new Vector3(0f, 0f, length * 0.5f), new Vector3(90f, 0f, 0f),
                new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness), accent, false, hideFp);
        }

        private void BuildBody(RigProfile p, Transform hips, Transform spine, Transform chest, Transform head,
            Transform upperR, Transform lowerR, Transform handR, Transform upperL, Transform lowerL, Transform handL,
            Transform thighR, Transform shinR, Transform footR, Transform thighL, Transform shinL, Transform footL)
        {
            var cube = PrimitiveType.Cube;
            var sphere = PrimitiveType.Sphere;
            var capsule = PrimitiveType.Capsule;
            bool hero = p.decoration == RigDecoration.Hero;
            bool demon = p.decoration == RigDecoration.Demon;
            bool oni = p.decoration == RigDecoration.Oni;

            // ---------------- torso
            Part("Pelvis", cube, p.primary, hips, new Vector3(0f, -0.02f, 0f), Vector3.zero, new Vector3(p.torsoWidth * 0.95f, 0.18f, p.torsoDepth), false, false, true);
            Part("Abdomen", capsule, oni ? p.skin : p.primary, spine, new Vector3(0f, p.spineLength * 0.5f, 0f), Vector3.zero,
                new Vector3(p.torsoWidth * 0.8f, p.spineLength * 0.55f, p.torsoDepth * 0.95f), false, false, true);
            Part("Chest", capsule, oni ? p.skin : p.primary, chest, new Vector3(0f, p.chestLength * 0.45f, 0f), Vector3.zero,
                new Vector3(p.torsoWidth, p.chestLength * 0.62f, p.torsoDepth), false, false, true);

            if (hero)
            {
                // Haori (open coat) panels in the secondary color with an accent trim that follows the breathing style.
                Part("HaoriBack", cube, p.secondary, chest, new Vector3(0f, -0.08f, -p.torsoDepth * 0.55f), new Vector3(-6f, 0f, 0f), new Vector3(p.torsoWidth * 1.15f, 0.62f, 0.025f), false, false, true, 1.2f);
                Part("HaoriTrimBack", cube, p.accent, chest, new Vector3(0f, -0.4f, -p.torsoDepth * 0.6f), new Vector3(-6f, 0f, 0f), new Vector3(p.torsoWidth * 1.16f, 0.07f, 0.03f), true, false, true, 0.8f);
                Part("HaoriFrontR", cube, p.secondary, chest, new Vector3(p.torsoWidth * 0.36f, -0.06f, p.torsoDepth * 0.52f), new Vector3(8f, 0f, 0f), new Vector3(0.13f, 0.6f, 0.025f), false, false, true, 1.2f);
                Part("HaoriFrontL", cube, p.secondary, chest, new Vector3(-p.torsoWidth * 0.36f, -0.06f, p.torsoDepth * 0.52f), new Vector3(8f, 0f, 0f), new Vector3(0.13f, 0.6f, 0.025f), false, false, true, 1.2f);
                Part("HaoriTrimR", cube, p.accent, chest, new Vector3(p.torsoWidth * 0.36f, -0.36f, p.torsoDepth * 0.56f), new Vector3(8f, 0f, 0f), new Vector3(0.135f, 0.06f, 0.03f), true, false, true, 0.8f);
                Part("HaoriTrimL", cube, p.accent, chest, new Vector3(-p.torsoWidth * 0.36f, -0.36f, p.torsoDepth * 0.56f), new Vector3(8f, 0f, 0f), new Vector3(0.135f, 0.06f, 0.03f), true, false, true, 0.8f);
                Part("Belt", cube, p.accent * 0.5f + Color.black * 0.5f, hips, new Vector3(0f, 0.07f, 0f), Vector3.zero, new Vector3(p.torsoWidth * 1.0f, 0.07f, p.torsoDepth * 1.08f), false, false, true, 0.8f);
                Part("Collar", cube, p.secondary, chest, new Vector3(0f, p.chestLength * 0.86f, 0.02f), new Vector3(20f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.16f), false, false, true);
            }
            else if (demon)
            {
                Part("Loincloth", cube, p.secondary, hips, new Vector3(0f, -0.2f, 0.06f), new Vector3(8f, 0f, 0f), new Vector3(0.22f, 0.34f, 0.03f), false, false, false, 1f);
                for (int i = 0; i < 3; i++)
                {
                    Part("Rib" + i, cube, p.accent, chest, new Vector3(0f, 0.05f + i * 0.07f, p.torsoDepth * 0.5f), Vector3.zero,
                        new Vector3(p.torsoWidth * (0.7f - i * 0.1f), 0.015f, 0.02f), false, false, false, 0f, p.accent * 2.2f);
                }
                Part("SpineRidge", ProceduralMeshes.Cone(6), p.hair, chest, new Vector3(0f, 0.2f, -p.torsoDepth * 0.5f), new Vector3(-110f, 0f, 0f), new Vector3(0.08f, 0.2f, 0.08f));
            }
            else if (oni)
            {
                Part("Loincloth", cube, p.primary, hips, new Vector3(0f, -0.16f, 0.02f), new Vector3(4f, 0f, 0f), new Vector3(p.torsoWidth * 1.05f, 0.34f, p.torsoDepth * 1.1f));
                Part("RopeBelt", PrimitiveType.Cylinder, p.accent, hips, new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(p.torsoWidth * 1.12f, 0.04f, p.torsoDepth * 1.25f), false, false, false, 1f);
                Part("ShoulderPadR", cube, p.primary, chest, new Vector3(p.shoulderWidth + 0.04f, p.shoulderHeight + 0.06f, 0f), new Vector3(0f, 0f, -18f), new Vector3(0.2f, 0.07f, 0.24f));
                Part("ShoulderPadL", cube, p.primary, chest, new Vector3(-p.shoulderWidth - 0.04f, p.shoulderHeight + 0.06f, 0f), new Vector3(0f, 0f, 18f), new Vector3(0.2f, 0.07f, 0.24f));
                Part("Chains", cube, p.accent * 0.6f, chest, new Vector3(0f, p.chestLength * 0.4f, p.torsoDepth * 0.52f), new Vector3(0f, 0f, 35f), new Vector3(0.6f, 0.03f, 0.02f));
            }

            // ---------------- head
            float hs = p.headSize;
            Part("Neck", capsule, p.skin, head, new Vector3(0f, -p.neckLength * 0.4f, 0f), Vector3.zero, new Vector3(hs * 0.42f, p.neckLength, hs * 0.42f), false, false, true);
            if (oni)
            {
                Part("Head", sphere, p.skin, head, new Vector3(0f, hs * 0.5f, 0f), Vector3.zero, new Vector3(hs * 1.05f, hs * 1.05f, hs * 1.05f), false, false, true);
                // Hollow mask: white face plate with dark hollow eye sockets.
                Part("Mask", sphere, p.secondary, head, new Vector3(0f, hs * 0.52f, hs * 0.18f), Vector3.zero, new Vector3(hs * 0.95f, hs * 1.0f, hs * 0.7f), false, false, true, 1.4f);
                Part("MaskCrack", cube, new Color(0.1f, 0.05f, 0.05f), head, new Vector3(hs * 0.12f, hs * 0.75f, hs * 0.5f), new Vector3(0f, 0f, 30f), new Vector3(0.01f, hs * 0.35f, 0.01f), false, false, true, 0f);
                Part("EyeR", sphere, p.eyes, head, new Vector3(hs * 0.18f, hs * 0.58f, hs * 0.5f), Vector3.zero, Vector3.one * hs * 0.14f, false, true, true);
                Part("EyeL", sphere, p.eyes, head, new Vector3(-hs * 0.18f, hs * 0.58f, hs * 0.5f), Vector3.zero, Vector3.one * hs * 0.14f, false, true, true);
                Part("Mouth", cube, p.eyes * 0.6f, head, new Vector3(0f, hs * 0.3f, hs * 0.5f), Vector3.zero, new Vector3(hs * 0.4f, hs * 0.05f, 0.02f), false, true, true);
                Part("HornR", ProceduralMeshes.Cone(10, 0.35f), p.secondary * 0.9f, head, new Vector3(hs * 0.3f, hs * 0.85f, 0.02f), new Vector3(-10f, 0f, -28f), new Vector3(0.1f, 0.42f, 0.1f), false, false, true);
                Part("HornL", ProceduralMeshes.Cone(10, 0.35f), p.secondary * 0.9f, head, new Vector3(-hs * 0.3f, hs * 0.85f, 0.02f), new Vector3(-10f, 0f, 28f), new Vector3(0.1f, 0.42f, 0.1f), false, false, true);
                for (int i = 0; i < 9; i++)
                {
                    float a = (i / 8f - 0.5f) * 220f;
                    var dir = Quaternion.Euler(0f, a, 0f) * Vector3.back;
                    Part("Mane" + i, ProceduralMeshes.Cone(6), p.hair, head, new Vector3(dir.x * hs * 0.4f, hs * 0.7f, dir.z * hs * 0.4f),
                        (Quaternion.LookRotation(dir + Vector3.down * 0.8f) * Quaternion.Euler(90f, 0f, 0f)).eulerAngles, new Vector3(0.12f, 0.45f, 0.12f), false, false, true);
                }
            }
            else if (demon)
            {
                Part("Head", sphere, p.skin, head, new Vector3(0f, hs * 0.48f, hs * 0.08f), Vector3.zero, new Vector3(hs * 0.85f, hs * 0.95f, hs * 1.15f), false, false, true);
                Part("EyeR", sphere, p.eyes, head, new Vector3(hs * 0.16f, hs * 0.55f, hs * 0.55f), Vector3.zero, new Vector3(hs * 0.16f, hs * 0.07f, hs * 0.08f), false, true, true);
                Part("EyeL", sphere, p.eyes, head, new Vector3(-hs * 0.16f, hs * 0.55f, hs * 0.55f), Vector3.zero, new Vector3(hs * 0.16f, hs * 0.07f, hs * 0.08f), false, true, true);
                Part("Maw", cube, p.eyes * 0.5f, head, new Vector3(0f, hs * 0.26f, hs * 0.6f), Vector3.zero, new Vector3(hs * 0.35f, hs * 0.04f, 0.02f), false, true, true);
                Part("HornR", ProceduralMeshes.Cone(8, 0.6f), p.hair, head, new Vector3(hs * 0.22f, hs * 0.85f, 0f), new Vector3(-35f, 0f, -20f), new Vector3(0.06f, 0.32f, 0.06f), false, false, true);
                Part("HornL", ProceduralMeshes.Cone(8, 0.6f), p.hair, head, new Vector3(-hs * 0.22f, hs * 0.85f, 0f), new Vector3(-35f, 0f, 20f), new Vector3(0.06f, 0.32f, 0.06f), false, false, true);
            }
            else
            {
                Part("Head", sphere, p.skin, head, new Vector3(0f, hs * 0.5f, 0f), Vector3.zero, Vector3.one * hs, false, false, true);
                Part("EyeR", cube, p.eyes, head, new Vector3(hs * 0.17f, hs * 0.52f, hs * 0.47f), Vector3.zero, new Vector3(hs * 0.1f, hs * 0.14f, 0.02f), false, true, true);
                Part("EyeL", cube, p.eyes, head, new Vector3(-hs * 0.17f, hs * 0.52f, hs * 0.47f), Vector3.zero, new Vector3(hs * 0.1f, hs * 0.14f, 0.02f), false, true, true);
                // Spiky anime hair.
                Part("HairCap", sphere, p.hair, head, new Vector3(0f, hs * 0.66f, -hs * 0.06f), Vector3.zero, new Vector3(hs * 1.06f, hs * 0.8f, hs * 1.08f), false, false, true);
                var spikes = new[]
                {
                    new Vector4(0f, 0.95f, 0.1f, -20f), new Vector4(0.25f, 0.85f, 0.05f, -35f), new Vector4(-0.25f, 0.85f, 0.05f, -35f),
                    new Vector4(0.1f, 0.8f, -0.35f, -70f), new Vector4(-0.12f, 0.78f, -0.35f, -70f), new Vector4(0f, 0.6f, -0.45f, -100f),
                    new Vector4(0.35f, 0.65f, -0.2f, -60f), new Vector4(-0.35f, 0.65f, -0.2f, -60f), new Vector4(0.18f, 0.9f, 0.3f, 15f)
                };
                for (int i = 0; i < spikes.Length; i++)
                {
                    var s = spikes[i];
                    float side = s.x * 60f;
                    Part("HairSpike" + i, ProceduralMeshes.Cone(6, 0.25f), p.hair, head, new Vector3(s.x * hs, s.y * hs, s.z * hs),
                        new Vector3(s.w, 0f, -side), new Vector3(hs * 0.32f, hs * 0.75f, hs * 0.32f), false, false, true, 1.2f);
                }
                // Headband with tails in the style accent color.
                Part("Headband", PrimitiveType.Cylinder, p.accent, head, new Vector3(0f, hs * 0.66f, 0f), Vector3.zero, new Vector3(hs * 1.08f, 0.022f, hs * 1.08f), true, false, true, 0.8f);
                Part("BandTailR", cube, p.accent, head, new Vector3(0.04f, hs * 0.5f, -hs * 0.6f), new Vector3(-60f, 15f, 0f), new Vector3(0.035f, 0.26f, 0.01f), true, false, true, 0.6f);
                Part("BandTailL", cube, p.accent, head, new Vector3(-0.04f, hs * 0.48f, -hs * 0.6f), new Vector3(-50f, -20f, 0f), new Vector3(0.035f, 0.24f, 0.01f), true, false, true, 0.6f);
            }

            // ---------------- arms
            Color sleeve = hero ? p.secondary : (oni ? p.skin : p.skin);
            Limb("UpperArmR", upperR, p.upperArm, p.armThickness * (hero ? 1.35f : 1f), sleeve);
            Limb("UpperArmL", upperL, p.upperArm, p.armThickness * (hero ? 1.35f : 1f), sleeve);
            Limb("LowerArmR", lowerR, p.lowerArm, p.armThickness * 0.85f, hero ? p.primary : p.skin);
            Limb("LowerArmL", lowerL, p.lowerArm, p.armThickness * 0.85f, hero ? p.primary : p.skin);
            Part("HandR", cube, p.skin, handR, new Vector3(0f, 0f, 0.02f), Vector3.zero, new Vector3(p.armThickness * 0.8f, p.armThickness * 0.9f, p.armThickness * 0.9f));
            Part("HandL", cube, p.skin, handL, new Vector3(0f, 0f, 0.02f), Vector3.zero, new Vector3(p.armThickness * 0.8f, p.armThickness * 0.9f, p.armThickness * 0.9f));
            if (demon)
            {
                foreach (var hand in new[] { handR, handL })
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float x = (i - 1) * 0.03f;
                        Part("Claw" + i, ProceduralMeshes.Cone(6, -0.4f), p.hair, hand, new Vector3(x, 0.02f, 0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.025f, 0.2f, 0.025f), false, false, false, 1f, p.accent * 0.6f);
                    }
                }
            }

            // ---------------- legs
            Color legColor = hero ? p.primary : (oni ? p.skin : p.skin);
            Limb("ThighR", thighR, p.thigh, p.legThickness * (hero ? 1.35f : 1f), legColor);
            Limb("ThighL", thighL, p.thigh, p.legThickness * (hero ? 1.35f : 1f), legColor);
            Limb("ShinR", shinR, p.shin, p.legThickness * 0.85f, hero ? p.primary * 0.8f : legColor);
            Limb("ShinL", shinL, p.shin, p.legThickness * 0.85f, hero ? p.primary * 0.8f : legColor);
            Color footColor = hero ? new Color(0.12f, 0.1f, 0.1f) : p.hair;
            Part("FootR", cube, footColor, footR, new Vector3(0f, -0.02f, 0.06f), Vector3.zero, new Vector3(0.09f, 0.06f, 0.22f));
            Part("FootL", cube, footColor, footL, new Vector3(0f, -0.02f, 0.06f), Vector3.zero, new Vector3(0.09f, 0.06f, 0.22f));
            if (hero)
            {
                // Wrapped shins (kyahan) in white.
                Part("WrapR", PrimitiveType.Cylinder, p.secondary, shinR, new Vector3(0f, 0f, p.shin * 0.7f), new Vector3(90f, 0f, 0f), new Vector3(p.legThickness * 0.95f, p.shin * 0.22f, p.legThickness * 0.95f), false, false, false, 0.8f);
                Part("WrapL", PrimitiveType.Cylinder, p.secondary, shinL, new Vector3(0f, 0f, p.shin * 0.7f), new Vector3(90f, 0f, 0f), new Vector3(p.legThickness * 0.95f, p.shin * 0.22f, p.legThickness * 0.95f), false, false, false, 0.8f);
            }
        }

        private void BuildWeapon(RigProfile p, Transform weapon)
        {
            WeaponBase = new GameObject("WeaponBase").transform;
            WeaponBase.SetParent(weapon, false);
            WeaponTip = new GameObject("WeaponTip").transform;
            WeaponTip.SetParent(weapon, false);

            switch (p.weapon)
            {
                case RigWeapon.Katana:
                {
                    const float bladeLength = 1.0f;
                    Part("Handle", PrimitiveType.Cylinder, new Color(0.12f, 0.08f, 0.14f), weapon, new Vector3(0f, 0f, -0.04f), new Vector3(90f, 0f, 0f), new Vector3(0.034f, 0.14f, 0.034f), false, false, false, 1f);
                    Part("Pommel", PrimitiveType.Cylinder, new Color(0.72f, 0.55f, 0.2f), weapon, new Vector3(0f, 0f, -0.18f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.012f, 0.04f), false, false, false, 1f);
                    Part("Guard", PrimitiveType.Cylinder, new Color(0.72f, 0.55f, 0.2f), weapon, new Vector3(0f, 0f, 0.105f), new Vector3(90f, 0f, 0f), new Vector3(0.1f, 0.008f, 0.085f), false, false, false, 1f);
                    Part("Blade", ProceduralMeshes.KatanaBlade(bladeLength, 0.034f, 0.009f, 0.035f), new Color(0.78f, 0.82f, 0.9f), weapon,
                        new Vector3(0f, 0f, 0.11f), Vector3.zero, Vector3.one, false, false, false, 1f, null, 0.8f);
                    // Emissive edge strip: glows with the breathing style color.
                    var edge = ProceduralMeshes.CreatePart("EdgeGlow", ProceduralMeshes.KatanaBlade(bladeLength * 0.98f, 0.012f, 0.012f, 0.035f),
                        MaterialFactory.Vfx(ProceduralTextures.SoftCircle, VfxBlend.Additive, 0f), weapon, new Vector3(0f, 0.012f, 0.12f), Quaternion.identity, Vector3.one);
                    _edgeGlow = edge.GetComponent<MeshRenderer>();
                    _edgeGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    WeaponBase.localPosition = new Vector3(0f, 0f, 0.14f);
                    WeaponTip.localPosition = new Vector3(0f, -0.035f * bladeLength, 0.11f + bladeLength);
                    break;
                }
                case RigWeapon.Kanabo:
                {
                    const float len = 1.35f;
                    Part("Grip", PrimitiveType.Cylinder, new Color(0.25f, 0.15f, 0.1f), weapon, new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.14f, 0.06f));
                    Part("Club", PrimitiveType.Cylinder, new Color(0.18f, 0.16f, 0.2f), weapon, new Vector3(0f, 0f, 0.14f + len * 0.5f), new Vector3(90f, 0f, 0f), new Vector3(0.15f, len * 0.5f, 0.15f), false, false, false, 1.2f);
                    for (int i = 0; i < 14; i++)
                    {
                        float z = 0.35f + (i / 13f) * (len - 0.3f);
                        float a = i * 137f;
                        var dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                        Part("Stud" + i, ProceduralMeshes.Cone(5), new Color(0.6f, 0.55f, 0.5f), weapon, new Vector3(dir.x * 0.07f, dir.y * 0.07f, z),
                            Quaternion.LookRotation(Vector3.forward, dir).eulerAngles, new Vector3(0.05f, 0.08f, 0.05f), false, false, false, 0.6f);
                    }
                    WeaponBase.localPosition = new Vector3(0f, 0f, 0.3f);
                    WeaponTip.localPosition = new Vector3(0f, 0f, 0.14f + len);
                    break;
                }
                case RigWeapon.Claws:
                    WeaponBase.localPosition = new Vector3(0f, 0f, 0.02f);
                    WeaponTip.localPosition = new Vector3(0f, 0f, 0.28f);
                    break;
                default:
                    WeaponBase.localPosition = Vector3.zero;
                    WeaponTip.localPosition = new Vector3(0f, 0f, 0.1f);
                    break;
            }
        }

        // ------------------------------------------------------------------ runtime controls

        /// <summary>Breathing style color (headband, haori trim).</summary>
        public void SetAccentColor(Color color)
        {
            _accent = color;
            _dirty = true;
        }

        public void SetEyeColor(Color color)
        {
            _eyeColor = color;
            _dirty = true;
        }

        /// <summary>White flash on hit (0..1).</summary>
        public void SetHitFlash(float amount, Color? color = null)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _hitFlash) && color == null) return;
            _hitFlash = amount;
            if (color.HasValue) _flashColor = color.Value;
            _dirty = true;
        }

        /// <summary>Death disintegration (0 = solid, 1 = gone).</summary>
        public void SetDissolve(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _dissolve)) return;
            _dissolve = amount;
            _dirty = true;
        }

        /// <summary>Glow on the blade edge (breathing energy). Black = off.</summary>
        public void SetWeaponGlow(Color hdrColor)
        {
            if (_edgeGlow == null) return;
            _edgeColor = hdrColor;
            _mpb.Clear();
            _mpb.SetColor(ShaderIds.TintColor, hdrColor);
            _edgeGlow.SetPropertyBlock(_mpb);
            _edgeGlow.enabled = _visible && hdrColor.maxColorComponent > 0.01f;
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            foreach (var e in _renderers) if (e.Renderer != null) e.Renderer.enabled = visible;
            if (_edgeGlow != null) _edgeGlow.enabled = visible && _edgeColor.maxColorComponent > 0.01f;
        }

        /// <summary>Moves head/torso renderers to a layer the first-person camera culls.</summary>
        public void SetFirstPersonHidden(bool hidden, int normalLayer)
        {
            foreach (var r in _firstPersonHidden)
                if (r != null) r.gameObject.layer = hidden ? Layers.PlayerHidden : normalLayer;
        }

        /// <summary>Current mesh/matrix pairs for afterimages.</summary>
        public void Snapshot(List<Mesh> meshes, List<Matrix4x4> matrices)
        {
            meshes.Clear();
            matrices.Clear();
            foreach (var mf in _meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                meshes.Add(mf.sharedMesh);
                matrices.Add(mf.transform.localToWorldMatrix);
            }
        }

        private void LateUpdate()
        {
            if (_dirty) ApplyProperties();
        }

        private void ApplyProperties()
        {
            _dirty = false;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Color shadeAccent = new Color(_accent.r * 0.45f, _accent.g * 0.45f, _accent.b * 0.55f, 1f);
            for (int i = 0; i < _renderers.Count; i++)
            {
                var e = _renderers[i];
                if (e.Renderer == null) continue;
                _mpb.Clear();
                if (e.Accent)
                {
                    _mpb.SetColor(ShaderIds.BaseColor, _accent);
                    _mpb.SetColor(ShaderIds.ShadeColor, shadeAccent);
                }
                if (e.Eye)
                {
                    _mpb.SetColor(ShaderIds.BaseColor, _eyeColor);
                    _mpb.SetColor(ShaderIds.EmissionColor, _eyeColor);
                }
                if (_hitFlash > 0f)
                {
                    _mpb.SetFloat(ShaderIds.HitFlash, _hitFlash);
                    _mpb.SetColor(ShaderIds.HitFlashColor, _flashColor);
                }
                if (_dissolve > 0f)
                {
                    _mpb.SetFloat(ShaderIds.Dissolve, _dissolve);
                    _mpb.SetColor(ShaderIds.DissolveColor, Profile != null && Profile.decoration != RigDecoration.Hero ? new Color(3f, 0.4f, 0.8f) : new Color(1f, 1.5f, 3f));
                }
                if (e.Accent || e.Eye || _hitFlash > 0f || _dissolve > 0f) e.Renderer.SetPropertyBlock(_mpb);
                else e.Renderer.SetPropertyBlock(null);
            }
        }
    }
}
