using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Characters
{
    /// <summary>Base poses and locomotion tuning of one body archetype.</summary>
    public sealed class LocomotionProfile
    {
        public MotionPose Guard;
        public MotionPose Relaxed;
        public MotionPose Sprint;
        public MotionPose Block;
        public MotionPose AirRise;
        public MotionPose AirFall;
        public MotionPose Down;
        public MotionPose Dead;
        public float StrideLength = 1.05f;
        public float LeanPerSpeed = 1.2f;
        public float SprintLean = 18f;
        public float StanceWidth = 0.14f;
    }

    /// <summary>
    /// Procedural "animation clips". Swings are generated from slash-plane geometry so every cut reads clearly:
    /// tilt = angle of the cut plane (0 horizontal, 90 vertical), angle = blade position inside that plane
    /// (0 forward, +90 right, -90 left). Swap for Mecanim clips later through <see cref="ICharacterAnimator"/>.
    /// </summary>
    public static class MotionLibrary
    {
        private static Dictionary<string, MotionClip> _clips;
        private static LocomotionProfile _hero, _demon, _oni;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _clips = null;
            _hero = _demon = _oni = null;
        }

        public static MotionClip Get(string id)
        {
            if (_clips == null) Build();
            if (string.IsNullOrEmpty(id)) return null;
            return _clips.TryGetValue(id, out var clip) ? clip : null;
        }

        public static bool Has(string id) => Get(id) != null;

        public static IEnumerable<string> AllIds
        {
            get
            {
                if (_clips == null) Build();
                return _clips.Keys;
            }
        }

        public static LocomotionProfile For(RigDecoration decoration)
        {
            if (_clips == null) Build();
            switch (decoration)
            {
                case RigDecoration.Demon: return _demon;
                case RigDecoration.Oni: return _oni;
                default: return _hero;
            }
        }

        // ================================================================== helpers

        private static MotionPose HeroBase()
        {
            return new MotionPose
            {
                handR = new Vector3(0.16f, 1.1f, 0.34f),
                bladeDir = new Vector3(-0.08f, 0.6f, 0.8f).normalized,
                bladeEdge = new Vector3(0f, -0.8f, 0.6f),
                handL = new Vector3(-0.22f, 1.0f, 0.2f),
                twoHand = 1f,
                elbowHintR = new Vector3(0.6f, -0.8f, -0.3f),
                elbowHintL = new Vector3(-0.6f, -0.8f, -0.3f),
                hips = new Vector3(0f, -0.04f, 0f),
                hipsEuler = new Vector3(0f, -12f, 0f),
                spineEuler = new Vector3(4f, 4f, 0f),
                chestEuler = new Vector3(0f, 6f, 0f),
                headEuler = new Vector3(0f, 2f, 0f),
                footL = new Vector3(-0.13f, 0f, 0.14f),
                footR = new Vector3(0.15f, 0f, -0.14f),
                kneeHint = new Vector3(0f, 0f, 1f)
            };
        }

        /// <summary>Places the sword on the slash plane. <paramref name="swingSign"/> = direction the blade will move (edge leads).</summary>
        private static MotionPose Slash(MotionPose p, float tilt, float angle, float swingSign = 1f, float radius = -1f, float twoHand = 1f)
        {
            float a = angle * Mathf.Deg2Rad;
            Quaternion plane = Quaternion.Euler(0f, 0f, tilt);
            Vector3 r = plane * new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 tangent = plane * new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * Mathf.Sign(swingSign == 0f ? 1f : swingSign);
            if (radius < 0f) radius = Mathf.Lerp(0.36f, 0.52f, (r.x + 1f) * 0.5f);
            Vector3 pivot = MotionPose.ArcPivot + new Vector3(0f, p.hips.y, 0f);
            p.handR = pivot + r * radius;
            p.bladeDir = (r + Vector3.forward * 0.16f).normalized;
            p.bladeEdge = Vector3.ProjectOnPlane(tangent, p.bladeDir).normalized;
            if (p.bladeEdge.sqrMagnitude < 0.01f) p.bladeEdge = Vector3.up;
            p.twoHand = twoHand;
            // Torso follows the blade: twist for flat cuts, bend for vertical cuts.
            float flat = Mathf.Cos(tilt * Mathf.Deg2Rad);
            float vertical = Mathf.Abs(Mathf.Sin(tilt * Mathf.Deg2Rad));
            float side = Mathf.Clamp(angle, -150f, 150f);
            p.chestEuler = new Vector3(p.chestEuler.x - Mathf.Sin(a) * 7f * vertical, Mathf.Clamp(side * 0.22f, -38f, 38f) * flat, p.chestEuler.z);
            p.spineEuler = new Vector3(p.spineEuler.x - Mathf.Sin(a) * 9f * vertical, Mathf.Clamp(side * 0.12f, -22f, 22f) * flat, p.spineEuler.z);
            p.headEuler = new Vector3(p.headEuler.x, -p.chestEuler.y * 0.7f - p.spineEuler.y * 0.7f, 0f);
            // Elbows: out and down, forward when the sword is raised.
            float up = Mathf.Clamp01(r.y);
            p.elbowHintR = Vector3.Lerp(new Vector3(0.6f, -0.8f, -0.3f), new Vector3(0.7f, 0f, 0.6f), up);
            p.elbowHintL = Vector3.Lerp(new Vector3(-0.6f, -0.8f, -0.3f), new Vector3(-0.7f, 0f, 0.6f), up);
            return p;
        }

        private static MotionPose Stance(MotionPose p, Vector3 footL, Vector3 footR, float drop, float hipsYaw = -12f)
        {
            p.footL = footL;
            p.footR = footR;
            // Hands move with the body so the sword keeps its place relative to the chest.
            float delta = -drop - p.hips.y;
            p.handR.y += delta;
            p.handL.y += delta;
            p.hips = new Vector3(p.hips.x, -drop, p.hips.z);
            p.hipsEuler = new Vector3(p.hipsEuler.x, hipsYaw, p.hipsEuler.z);
            return p;
        }

        private static MotionPose Lean(MotionPose p, float spinePitch, float chestPitch = 0f)
        {
            p.spineEuler.x += spinePitch;
            p.chestEuler.x += chestPitch;
            p.headEuler.x -= (spinePitch + chestPitch) * 0.6f;
            return p;
        }

        private static MotionPose Visual(MotionPose p, Vector3 euler, Vector3 offset)
        {
            p.visualEuler = euler;
            p.visualOffset = offset;
            return p;
        }

        private static MotionPose FreeLeft(MotionPose p, Vector3 handL)
        {
            p.handL = handL;
            p.twoHand = 0f;
            return p;
        }

        private static MotionPose AirLegs(MotionPose p)
        {
            p.footL = new Vector3(-0.13f, 0.38f, 0.12f);
            p.footR = new Vector3(0.14f, 0.22f, -0.18f);
            p.handR.y -= p.hips.y;
            p.handL.y -= p.hips.y;
            p.hips = new Vector3(0f, 0f, 0f);
            return p;
        }

        private static readonly Vector3 LungeL = new Vector3(-0.15f, 0f, 0.42f);
        private static readonly Vector3 LungeR = new Vector3(0.17f, 0f, -0.34f);
        private static readonly Vector3 WideL = new Vector3(-0.24f, 0f, 0.3f);
        private static readonly Vector3 WideR = new Vector3(0.26f, 0f, -0.3f);

        /// <summary>Builds an attack clip: [windup, strike keys..., follow-through].</summary>
        private static void Attack(string id, params MotionPose[] poses)
        {
            var keys = new MotionKey[poses.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                float t = poses.Length == 1 ? 1f : i / (float)(poses.Length - 1);
                keys[i] = new MotionKey(t, poses[i], i == 0 ? Ease.Out : Ease.Linear, i > 0);
            }
            _clips[id] = new MotionClip(id, keys);
        }

        private static void Clip(string id, params MotionKey[] keys) => _clips[id] = new MotionClip(id, keys);

        private static MotionKey K(float t, MotionPose p, Ease e = Ease.InOut, bool autoEdge = false) => new MotionKey(t, p, e, autoEdge);

        // ================================================================== content

        private static void Build()
        {
            _clips = new Dictionary<string, MotionClip>();
            var b = HeroBase();

            // ------------------------------------------------ hero locomotion poses
            var relaxed = b;
            relaxed.handR = new Vector3(0.3f, 0.98f, 0.18f);
            relaxed.bladeDir = new Vector3(0.12f, -0.28f, 1f).normalized;
            relaxed.bladeEdge = new Vector3(0f, -1f, -0.28f).normalized;
            relaxed.twoHand = 0f;
            relaxed.handL = new Vector3(-0.26f, 0.92f, 0.04f);
            relaxed.hipsEuler = new Vector3(0f, -4f, 0f);
            relaxed.chestEuler = new Vector3(0f, 2f, 0f);
            relaxed.footL = new Vector3(-0.13f, 0f, 0.05f);
            relaxed.footR = new Vector3(0.14f, 0f, -0.05f);

            var sprint = b;
            sprint.handR = new Vector3(0.3f, 1.02f, -0.34f);
            sprint.bladeDir = new Vector3(0.12f, 0.32f, -1f).normalized;
            sprint.bladeEdge = new Vector3(0f, 1f, 0.32f).normalized;
            sprint.twoHand = 0f;
            sprint.handL = new Vector3(-0.3f, 1.02f, -0.34f);
            sprint.elbowHintR = new Vector3(0.6f, 0.2f, -0.8f);
            sprint.elbowHintL = new Vector3(-0.6f, 0.2f, -0.8f);
            sprint.hipsEuler = Vector3.zero;
            sprint.chestEuler = new Vector3(8f, 0f, 0f);
            sprint.headEuler = new Vector3(-14f, 0f, 0f);

            var block = b;
            block.handR = new Vector3(0.24f, 1.48f, 0.34f);
            block.bladeDir = new Vector3(-0.95f, 0.22f, 0.2f).normalized;
            block.bladeEdge = new Vector3(0.05f, 0.3f, 1f).normalized;
            block.twoHand = 0f;
            block.handL = new Vector3(-0.3f, 1.52f, 0.4f);
            block.elbowHintR = new Vector3(0.8f, -0.4f, 0f);
            block.elbowHintL = new Vector3(-0.8f, -0.4f, 0f);
            block = Stance(block, new Vector3(-0.16f, 0f, 0.2f), new Vector3(0.18f, 0f, -0.22f), 0.12f, -6f);
            block = Lean(block, 6f, 4f);

            var airRise = AirLegs(b);
            airRise.handR = new Vector3(0.34f, 1.4f, 0.05f);
            airRise.bladeDir = new Vector3(0.3f, 0.5f, -0.8f).normalized;
            airRise.bladeEdge = new Vector3(0f, 0.85f, 0.5f).normalized;
            airRise.twoHand = 0f;
            airRise.handL = new Vector3(-0.4f, 1.3f, 0.1f);
            airRise = Lean(airRise, -4f);

            var airFall = b;
            airFall.footL = new Vector3(-0.16f, 0.12f, 0.1f);
            airFall.footR = new Vector3(0.17f, 0.05f, -0.1f);
            airFall.handR = new Vector3(0.42f, 1.22f, 0.1f);
            airFall.bladeDir = new Vector3(0.4f, -0.2f, 0.9f).normalized;
            airFall.bladeEdge = new Vector3(0f, -1f, -0.2f).normalized;
            airFall.twoHand = 0f;
            airFall.handL = new Vector3(-0.45f, 1.25f, 0.05f);

            var down = b;
            down.visualEuler = new Vector3(-84f, 0f, 0f);
            down.visualOffset = new Vector3(0f, 0.14f, 0f);
            down.twoHand = 0f;
            down.handR = new Vector3(0.45f, 1.4f, 0.1f);
            down.handL = new Vector3(-0.45f, 1.4f, 0.1f);
            down.bladeDir = new Vector3(1f, 0f, 0f);
            down.bladeEdge = Vector3.forward;
            down.footL = new Vector3(-0.18f, 0f, 0.05f);
            down.footR = new Vector3(0.2f, 0f, -0.02f);
            down.hips = Vector3.zero;
            down.hipsEuler = Vector3.zero;
            down.spineEuler = Vector3.zero;
            down.chestEuler = Vector3.zero;
            down.headEuler = new Vector3(-10f, 20f, 0f);

            var dead = down;
            dead.visualEuler = new Vector3(84f, 10f, 0f);
            dead.headEuler = new Vector3(20f, -30f, 0f);

            _hero = new LocomotionProfile
            {
                Guard = b, Relaxed = relaxed, Sprint = sprint, Block = block, AirRise = airRise, AirFall = airFall, Down = down, Dead = dead,
                StrideLength = 1.05f, LeanPerSpeed = 1.2f, SprintLean = 20f, StanceWidth = 0.14f
            };

            // ------------------------------------------------ hero light string
            Attack("L1",
                Stance(Slash(b, 40f, 128f, -1f), b.footL, b.footR, 0.06f),
                Stance(Slash(b, 40f, 20f, -1f), LungeL, LungeR, 0.14f),
                Stance(Slash(b, 40f, -115f, -1f), LungeL, LungeR, 0.16f));
            Attack("L2",
                Stance(Slash(b, -10f, -128f, 1f), LungeL, LungeR, 0.14f),
                Stance(Slash(b, -10f, 0f, 1f), LungeL, LungeR, 0.12f, -20f),
                Stance(Slash(b, -10f, 122f, 1f), LungeL, LungeR, 0.12f, -25f));
            Attack("L3",
                Stance(Slash(b, -40f, 118f, -1f), WideL, WideR, 0.2f),
                Stance(Slash(b, -40f, 0f, -1f), LungeL, LungeR, 0.12f),
                Lean(Stance(Slash(b, -40f, -128f, -1f), LungeL, LungeR, 0.04f), -8f));
            // Finisher: whirlwind spin with a small hop.
            Attack("L4",
                Stance(Slash(b, 4f, -125f, 1f), WideL, WideR, 0.24f, 20f),
                Visual(Stance(Slash(b, 4f, 50f, 1f), WideL, WideR, 0.12f), new Vector3(0f, 80f, 0f), new Vector3(0f, 0.18f, 0f)),
                Visual(Stance(Slash(b, 4f, 85f, 1f, 0.55f, 0f), b.footL, b.footR, 0.05f), new Vector3(0f, 220f, 0f), new Vector3(0f, 0.32f, 0f)),
                Visual(Stance(Slash(b, 4f, 95f, 1f, 0.55f, 0f), WideL, WideR, 0.26f), new Vector3(0f, 360f, 0f), Vector3.zero));

            // ------------------------------------------------ heavies
            Attack("H1",
                Lean(Stance(Slash(b, 90f, 158f, -1f), b.footL, b.footR, 0.04f), -8f, -6f),
                Stance(Slash(b, 90f, 40f, -1f), LungeL, LungeR, 0.1f),
                Lean(Stance(Slash(b, 90f, -62f, -1f), LungeL, LungeR, 0.22f), 16f, 4f));
            Attack("H2",
                Visual(Lean(Stance(Slash(b, 90f, 172f, -1f), WideL, WideR, 0.26f), -6f), Vector3.zero, Vector3.zero),
                Visual(AirLegs(Slash(b, 90f, 120f, -1f)), new Vector3(-10f, 0f, 0f), new Vector3(0f, 0.85f, 0f)),
                Visual(AirLegs(Slash(b, 90f, 10f, -1f)), new Vector3(10f, 0f, 0f), new Vector3(0f, 0.55f, 0f)),
                Visual(Lean(Stance(Slash(b, 90f, -68f, -1f), WideL, WideR, 0.32f), 24f, 6f), Vector3.zero, Vector3.zero));
            // L L H: rising launcher (the attacker rises with the target).
            Attack("L2H",
                Stance(Slash(b, 80f, -84f, 1f), WideL, WideR, 0.28f),
                Stance(Slash(b, 80f, 10f, 1f), LungeL, LungeR, 0.12f),
                Visual(Lean(Stance(Slash(b, 80f, 152f, 1f), b.footL, b.footR, 0f), -12f, -6f), Vector3.zero, new Vector3(0f, 0.15f, 0f)));
            // L H: cross cut.
            Attack("L1H",
                Stance(Slash(b, -45f, 104f, -1f), WideL, WideR, 0.24f, 15f),
                Stance(Slash(b, -45f, -10f, -1f), LungeL, LungeR, 0.16f),
                Lean(Stance(Slash(b, -45f, -132f, -1f), LungeL, LungeR, 0.1f), -6f));
            // L H H: crescent slam.
            Attack("L1HH",
                Visual(Lean(Stance(Slash(b, 55f, 168f, -1f), WideL, WideR, 0.2f), -10f), Vector3.zero, new Vector3(0f, 0.2f, 0f)),
                Visual(AirLegs(Slash(b, 55f, 70f, -1f)), new Vector3(0f, 0f, 0f), new Vector3(0f, 0.45f, 0f)),
                Visual(Lean(Stance(Slash(b, 55f, -78f, -1f), WideL, WideR, 0.34f), 22f), Vector3.zero, Vector3.zero));

            // ------------------------------------------------ dash / air / counters
            var thrustBack = b;
            thrustBack.handR = new Vector3(0.28f, 1.08f, -0.14f);
            thrustBack.bladeDir = new Vector3(-0.05f, 0.05f, 1f).normalized;
            thrustBack.bladeEdge = Vector3.up;
            thrustBack.twoHand = 0f;
            thrustBack.handL = new Vector3(-0.1f, 1.2f, 0.45f);
            thrustBack = Lean(Stance(thrustBack, WideL, WideR, 0.26f, 25f), 14f);
            var thrustOut = thrustBack;
            thrustOut.handR = new Vector3(0.08f, 1.26f, 0.66f);
            thrustOut.bladeDir = new Vector3(-0.06f, 0.02f, 1f).normalized;
            thrustOut.handL = new Vector3(-0.35f, 1.1f, -0.25f);
            thrustOut.elbowHintR = new Vector3(0.5f, -0.6f, 0f);
            thrustOut = Stance(thrustOut, new Vector3(-0.14f, 0f, 0.6f), new Vector3(0.2f, 0f, -0.48f), 0.3f, -30f);
            Attack("DashL", thrustBack, thrustOut, thrustOut);
            _clips["SkillThrust"] = new MotionClip("SkillThrust", K(0.35f, thrustBack, Ease.Out), K(0.55f, thrustOut, Ease.Snap), K(1f, thrustOut, Ease.Linear));

            Attack("A1", AirLegs(Slash(b, 15f, 118f, -1f)), AirLegs(Slash(b, 15f, 0f, -1f)), AirLegs(Slash(b, 15f, -112f, -1f)));
            Attack("A2", AirLegs(Slash(b, -15f, -118f, 1f)), AirLegs(Slash(b, -15f, 0f, 1f)), AirLegs(Slash(b, -15f, 116f, 1f)));
            Attack("A3",
                AirLegs(Slash(b, 90f, 160f, -1f)),
                Visual(AirLegs(Slash(b, 90f, 60f, -1f)), new Vector3(120f, 0f, 0f), Vector3.zero),
                Visual(AirLegs(Slash(b, 90f, -20f, -1f)), new Vector3(240f, 0f, 0f), Vector3.zero),
                Visual(AirLegs(Slash(b, 90f, -60f, -1f)), new Vector3(360f, 0f, 0f), Vector3.zero));

            var plungeUp = AirLegs(b);
            plungeUp.handR = new Vector3(0.08f, 1.75f, 0.18f);
            plungeUp.bladeDir = new Vector3(0.05f, -1f, 0.22f).normalized;
            plungeUp.bladeEdge = Vector3.forward;
            plungeUp.elbowHintR = new Vector3(0.7f, 0.2f, 0.4f);
            plungeUp.elbowHintL = new Vector3(-0.7f, 0.2f, 0.4f);
            var plungeDown = plungeUp;
            plungeDown.handR = new Vector3(0.05f, 1.02f, 0.42f);
            plungeDown.bladeDir = new Vector3(0f, -0.92f, 0.4f).normalized;
            plungeDown = Lean(Stance(plungeDown, WideL, WideR, 0.36f), 26f);
            Attack("AH", plungeUp, plungeDown, plungeDown);
            _clips["SkillPlunge"] = new MotionClip("SkillPlunge", K(0.3f, plungeUp, Ease.Out), K(1f, plungeDown, Ease.In, true));

            Attack("PDCounter",
                Stance(Slash(b, 5f, 140f, -1f), WideL, WideR, 0.22f, 25f),
                Stance(Slash(b, 5f, 0f, -1f), LungeL, LungeR, 0.16f),
                Visual(Stance(Slash(b, 5f, -140f, -1f), LungeL, LungeR, 0.18f), new Vector3(0f, -25f, 0f), Vector3.zero));
            Attack("Riposte",
                Stance(Slash(b, 35f, -142f, 1f), WideL, WideR, 0.3f, -20f),
                Stance(Slash(b, 35f, 0f, 1f), LungeL, LungeR, 0.16f),
                Lean(Stance(Slash(b, 35f, 150f, 1f), LungeL, LungeR, 0.06f), -10f));

            // Parry flick (played as a short motion).
            var parry = block;
            parry = Slash(parry, -60f, -40f, 1f, 0.5f, 0f);
            parry.handL = new Vector3(-0.35f, 1.35f, 0.1f);
            var parryEnd = Slash(block, -60f, 70f, 1f, 0.5f, 0f);
            parryEnd.handL = new Vector3(-0.4f, 1.3f, 0f);
            Clip("Parry", K(0.2f, parry, Ease.Out), K(0.55f, parryEnd, Ease.Snap, true), K(1f, parryEnd));

            // ------------------------------------------------ technique motions
            var lowStance = Slash(b, -25f, 152f, 1f, 0.5f, 0f);
            lowStance = FreeLeft(lowStance, new Vector3(-0.16f, 0.95f, 0.58f));
            lowStance = Lean(Stance(lowStance, new Vector3(-0.26f, 0f, 0.48f), new Vector3(0.3f, 0f, -0.4f), 0.34f, 30f), 20f);
            lowStance.chestEuler.y = 36f;
            lowStance.headEuler = new Vector3(-12f, -40f, 0f);
            Clip("SkillLowStance", K(1f, lowStance, Ease.Out));

            var dash = Slash(b, -18f, 165f, 1f, 0.5f, 0f);
            dash = FreeLeft(dash, new Vector3(-0.35f, 0.95f, -0.25f));
            dash = Visual(Stance(dash, new Vector3(-0.14f, 0.1f, 0.35f), new Vector3(0.16f, 0.2f, -0.45f), 0.22f, 0f), new Vector3(26f, 0f, 0f), Vector3.zero);
            dash.headEuler = new Vector3(-24f, 0f, 0f);
            Clip("SkillDash", K(1f, dash, Ease.Out));

            var riseA = Stance(Slash(b, 45f, -122f, 1f), WideL, WideR, 0.3f);
            var riseB = Stance(Slash(b, 45f, 0f, 1f), LungeL, LungeR, 0.14f);
            var riseC = Visual(Lean(AirLegs(Slash(b, 45f, 140f, 1f)), -10f, -6f), Vector3.zero, new Vector3(0f, 0.3f, 0f));
            Clip("SkillRisingCut", K(0.15f, riseA, Ease.Out), K(0.45f, riseB, Ease.In, true), K(0.8f, riseC, Ease.Out, true), K(1f, riseC));

            var airReady = airRise;
            Clip("SkillAirReady", K(1f, airReady, Ease.Out));

            var sheathe = b;
            sheathe.handR = new Vector3(-0.1f, 0.82f, 0.22f);
            sheathe.bladeDir = new Vector3(-0.2f, -0.2f, -1f).normalized;
            sheathe.bladeEdge = Vector3.up;
            sheathe.twoHand = 0f;
            sheathe.handL = new Vector3(-0.2f, 0.8f, 0.1f);
            sheathe.elbowHintR = new Vector3(0.6f, -0.2f, 0.6f);
            sheathe = Lean(Stance(sheathe, new Vector3(-0.2f, 0f, 0.42f), new Vector3(0.26f, 0f, -0.36f), 0.24f, -25f), 16f);
            sheathe.chestEuler.y = -30f;
            sheathe.headEuler = new Vector3(-10f, 30f, 0f);
            Clip("SkillSheathe", K(1f, sheathe, Ease.Out));

            // Visible inhale before a form: the stance settles, then the chest opens and rises, shoulders lift a
            // little (hands follow) and the chin comes up slightly. Subtle on purpose: 0.15-0.6 s.
            var inhaleA = Stance(b, new Vector3(-0.16f, 0f, 0.2f), new Vector3(0.18f, 0f, -0.2f), 0.1f, -14f);
            inhaleA.spineEuler.x -= 2f;
            inhaleA.chestEuler.x -= 5f;
            inhaleA.headEuler.x -= 3f;
            var inhaleB = Stance(b, new Vector3(-0.16f, 0f, 0.2f), new Vector3(0.18f, 0f, -0.2f), 0.06f, -14f);
            inhaleB.spineEuler.x -= 4f;
            inhaleB.chestEuler = new Vector3(inhaleB.chestEuler.x - 12f, inhaleB.chestEuler.y, 0f);
            inhaleB.headEuler = new Vector3(inhaleB.headEuler.x - 8f, inhaleB.headEuler.y, 0f);
            inhaleB.handR.y += 0.05f;
            inhaleB.handL.y += 0.06f;
            inhaleB.elbowHintR = new Vector3(0.75f, -0.6f, -0.3f);
            inhaleB.elbowHintL = new Vector3(-0.75f, -0.6f, -0.3f);
            Clip("SkillInhale", K(0.35f, inhaleA, Ease.Out), K(1f, inhaleB, Ease.InOut));

            var drawA = FreeLeft(Stance(Slash(b, -6f, -70f, 1f, 0.45f, 0f), new Vector3(-0.18f, 0f, 0.62f), new Vector3(0.24f, 0f, -0.5f), 0.3f, -10f), new Vector3(-0.3f, 0.85f, 0.05f));
            var drawB = FreeLeft(Stance(Slash(b, -6f, 40f, 1f, 0.52f, 0f), new Vector3(-0.18f, 0f, 0.62f), new Vector3(0.24f, 0f, -0.5f), 0.28f, 10f), new Vector3(-0.4f, 0.9f, -0.1f));
            var drawC = FreeLeft(Stance(Slash(b, -6f, 135f, 1f, 0.55f, 0f), new Vector3(-0.18f, 0f, 0.62f), new Vector3(0.24f, 0f, -0.5f), 0.3f, 25f), new Vector3(-0.45f, 0.95f, -0.2f));
            Clip("SkillIaiDraw", K(0.05f, sheathe, Ease.Linear), K(0.3f, drawA, Ease.Snap, true), K(0.55f, drawB, Ease.Linear, true), K(0.8f, drawC, Ease.Out, true), K(1f, drawC));

            var spinA = Stance(Slash(b, 2f, -120f, 1f), WideL, WideR, 0.26f, 20f);
            var spinB = Visual(Stance(Slash(b, 2f, 80f, 1f, 0.55f, 0f), WideL, WideR, 0.2f), new Vector3(0f, 180f, 0f), new Vector3(0f, 0.1f, 0f));
            var spinC = Visual(Stance(Slash(b, 2f, 90f, 1f, 0.55f, 0f), WideL, WideR, 0.24f), new Vector3(0f, 360f, 0f), Vector3.zero);
            Clip("SkillSpin", K(0.2f, spinA, Ease.Out), K(0.6f, spinB, Ease.Linear, true), K(0.9f, spinC, Ease.Out, true), K(1f, spinC));
            // Double spin for multi-hit techniques.
            var spin2 = Visual(spinC, new Vector3(0f, 540f, 0f), Vector3.zero);
            var spin3 = Visual(spinC, new Vector3(0f, 720f, 0f), Vector3.zero);
            Clip("SkillSpinLong", K(0.12f, spinA, Ease.Out), K(0.4f, spinB, Ease.Linear, true), K(0.6f, spinC, Ease.Linear, true), K(0.8f, spin2, Ease.Linear, true), K(1f, spin3, Ease.Out, true));

            var ohA = Lean(Stance(Slash(b, 90f, 165f, -1f), WideL, WideR, 0.1f), -10f, -6f);
            var ohB = Lean(Stance(Slash(b, 90f, -65f, -1f), LungeL, LungeR, 0.3f), 22f, 6f);
            Clip("SkillOverhead", K(0.4f, ohA, Ease.Out), K(0.6f, ohB, Ease.Snap, true), K(1f, ohB));

            var raise = b;
            raise.handR = new Vector3(0.1f, 2.02f, 0.12f);
            raise.bladeDir = new Vector3(0.02f, 1f, 0.06f).normalized;
            raise.bladeEdge = Vector3.forward;
            raise.twoHand = 0f;
            raise.handL = new Vector3(-0.35f, 1.0f, 0.2f);
            raise.elbowHintR = new Vector3(0.8f, 0f, 0.3f);
            raise.headEuler = new Vector3(-28f, 0f, 0f);
            raise.chestEuler = new Vector3(-8f, 0f, 0f);
            raise = Stance(raise, new Vector3(-0.2f, 0f, 0.15f), new Vector3(0.22f, 0f, -0.15f), 0.04f, 0f);
            Clip("SkillRaise", K(1f, raise, Ease.Out));

            var focus = b;
            focus.handR = new Vector3(0.04f, 1.28f, 0.32f);
            focus.bladeDir = new Vector3(0f, 1f, 0.05f).normalized;
            focus.bladeEdge = Vector3.forward;
            focus.twoHand = 1f;
            focus.elbowHintR = new Vector3(0.7f, -0.6f, 0f);
            focus.elbowHintL = new Vector3(-0.7f, -0.6f, 0f);
            focus.headEuler = new Vector3(8f, 0f, 0f);
            focus.hipsEuler = Vector3.zero;
            focus.chestEuler = Vector3.zero;
            focus = Stance(focus, new Vector3(-0.2f, 0f, 0.05f), new Vector3(0.2f, 0f, -0.05f), 0.08f, 0f);
            Clip("SkillFocus", K(1f, focus, Ease.Out));

            var whirl = b;
            whirl.handR = new Vector3(0.05f, 1.92f, 0.1f);
            whirl.twoHand = 0f;
            whirl.handL = new Vector3(-0.45f, 1.2f, 0.1f);
            whirl.elbowHintR = new Vector3(0.8f, 0f, 0.2f);
            whirl.headEuler = new Vector3(-10f, 0f, 0f);
            var wk = new MotionKey[5];
            var dirs = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.right };
            for (int i = 0; i < 5; i++)
            {
                var w = whirl;
                w.bladeDir = (dirs[i] + Vector3.up * 0.1f).normalized;
                w.bladeEdge = Vector3.Cross(Vector3.up, dirs[i]);
                wk[i] = K(i / 4f, w, Ease.Linear, i > 0);
            }
            _clips["SkillWhirl"] = new MotionClip("SkillWhirl", wk);

            var throwA = Stance(Slash(b, 12f, 130f, -1f), WideL, WideR, 0.2f, 25f);
            var throwB = FreeLeft(Stance(Slash(b, 12f, -40f, -1f, 0.56f, 0f), LungeL, LungeR, 0.14f), new Vector3(-0.3f, 1.3f, 0.5f));
            Clip("SkillThrow", K(0.35f, throwA, Ease.Out), K(0.6f, throwB, Ease.Snap, true), K(1f, throwB));

            var flipA = Stance(Slash(b, 90f, -40f, 1f), WideL, WideR, 0.2f);
            var flipB = Visual(AirLegs(Slash(b, 90f, 90f, 1f)), new Vector3(-180f, 0f, 0f), new Vector3(0f, 0.9f, 0f));
            var flipC = Visual(AirLegs(Slash(b, 90f, 170f, 1f)), new Vector3(-360f, 0f, 0f), new Vector3(0f, 0.2f, 0f));
            Clip("SkillFlip", K(0.2f, flipA, Ease.Out), K(0.6f, flipB, Ease.Linear, true), K(0.9f, flipC, Ease.Out, true), K(1f, Visual(flipC, new Vector3(-360f, 0f, 0f), Vector3.zero)));

            var hover = airRise;
            hover = Slash(hover, 20f, 110f, -1f, 0.5f, 0f);
            hover.handL = new Vector3(-0.45f, 1.35f, 0.2f);
            Clip("SkillHover", K(1f, hover, Ease.Out));

            // ------------------------------------------------ hit reactions / misc hero
            var dodge = b;
            dodge = Stance(dodge, new Vector3(-0.16f, 0.05f, 0.25f), new Vector3(0.18f, 0.15f, -0.3f), 0.28f, 0f);
            dodge.twoHand = 0f;
            dodge.handR = new Vector3(0.36f, 1.0f, -0.2f);
            dodge.bladeDir = new Vector3(0.2f, 0.1f, -1f).normalized;
            dodge.bladeEdge = Vector3.up;
            dodge.handL = new Vector3(-0.35f, 1.05f, -0.15f);
            Clip("Dodge", K(1f, dodge, Ease.Out));

            BuildDemon();
            BuildOni();
        }

        private static void BuildDemon()
        {
            var b = HeroBase();
            b.twoHand = 0f;
            b.hips = new Vector3(0f, -0.1f, 0f);
            b.hipsEuler = Vector3.zero;
            b.spineEuler = new Vector3(22f, 0f, 0f);
            b.chestEuler = new Vector3(12f, 0f, 0f);
            b.headEuler = new Vector3(-26f, 0f, 0f);
            b.handR = new Vector3(0.32f, 0.9f, 0.32f);
            b.handL = new Vector3(-0.32f, 0.9f, 0.32f);
            b.bladeDir = new Vector3(0.1f, -0.5f, 1f).normalized;
            b.bladeEdge = new Vector3(0f, 1f, 0.5f).normalized;
            b.elbowHintR = new Vector3(0.8f, -0.2f, -0.4f);
            b.elbowHintL = new Vector3(-0.8f, -0.2f, -0.4f);
            b.footL = new Vector3(-0.17f, 0f, 0.1f);
            b.footR = new Vector3(0.17f, 0f, -0.1f);

            var sprint = b;
            sprint.handR = new Vector3(0.3f, 0.85f, -0.3f);
            sprint.handL = new Vector3(-0.3f, 0.85f, -0.3f);
            sprint.bladeDir = new Vector3(0f, -0.2f, -1f).normalized;
            var block = b;
            block.handR = new Vector3(-0.08f, 1.42f, 0.38f);
            block.handL = new Vector3(0.1f, 1.38f, 0.36f);
            block.spineEuler = new Vector3(10f, 0f, 0f);
            block.elbowHintR = new Vector3(0.9f, -0.3f, 0f);
            block.elbowHintL = new Vector3(-0.9f, -0.3f, 0f);
            var air = b;
            air.footL = new Vector3(-0.15f, 0.3f, 0.1f);
            air.footR = new Vector3(0.15f, 0.25f, -0.05f);
            air.handR = new Vector3(0.5f, 1.3f, 0.0f);
            air.handL = new Vector3(-0.5f, 1.3f, 0.0f);
            air.spineEuler = new Vector3(-20f, 0f, 0f);
            var down = _hero.Down;
            down.twoHand = 0f;
            var dead = down;
            dead.visualEuler = new Vector3(-86f, 15f, 0f);
            _demon = new LocomotionProfile
            {
                Guard = b, Relaxed = b, Sprint = sprint, Block = block, AirRise = air, AirFall = air, Down = down, Dead = dead,
                StrideLength = 1.0f, LeanPerSpeed = 2.2f, SprintLean = 24f, StanceWidth = 0.17f
            };

            MotionPose Claw(float tilt, float angle, float sign, float drop)
            {
                var p = Slash(b, tilt, angle, sign, 0.6f, 0f);
                p.handL = b.handL;
                p.spineEuler.x += 12f;
                p.hips = new Vector3(0f, -drop, 0f);
                p.headEuler = new Vector3(-24f, p.headEuler.y, 0f);
                return p;
            }

            Attack("EnemySwipe", Claw(25f, 125f, -1f, 0.12f), Claw(25f, 0f, -1f, 0.18f), Claw(25f, -105f, -1f, 0.2f));
            Attack("EnemySwipe2", Claw(-20f, -115f, 1f, 0.16f), Claw(-20f, 0f, 1f, 0.18f), Claw(-20f, 112f, 1f, 0.18f));
            var lungeA = b;
            lungeA.handR = new Vector3(0.3f, 1.05f, -0.25f);
            lungeA.handL = new Vector3(-0.3f, 1.05f, -0.25f);
            lungeA.hips = new Vector3(0f, -0.3f, 0f);
            lungeA.spineEuler = new Vector3(30f, 0f, 0f);
            var lungeB = lungeA;
            lungeB.handR = new Vector3(0.14f, 1.2f, 0.75f);
            lungeB.handL = new Vector3(-0.14f, 1.2f, 0.75f);
            lungeB.bladeDir = Vector3.forward;
            lungeB.hips = new Vector3(0f, -0.2f, 0.1f);
            lungeB.visualEuler = new Vector3(14f, 0f, 0f);
            lungeB.footL = new Vector3(-0.15f, 0f, 0.5f);
            lungeB.footR = new Vector3(0.17f, 0f, -0.45f);
            Attack("EnemyLunge", lungeA, lungeB, lungeB);
            var leapA = lungeA;
            leapA.handR = new Vector3(0.45f, 1.75f, -0.1f);
            leapA.handL = new Vector3(-0.45f, 1.75f, -0.1f);
            leapA.bladeDir = new Vector3(0f, 0.6f, -0.8f).normalized;
            var leapB = leapA;
            leapB.handR = new Vector3(0.2f, 0.75f, 0.7f);
            leapB.handL = new Vector3(-0.2f, 0.75f, 0.7f);
            leapB.bladeDir = new Vector3(0f, -0.8f, 0.6f).normalized;
            leapB.spineEuler = new Vector3(40f, 0f, 0f);
            leapB.hips = new Vector3(0f, -0.35f, 0f);
            Attack("EnemyLeap", leapA, leapB, leapB);
            var roar = b;
            roar.handR = new Vector3(0.62f, 1.3f, 0.15f);
            roar.handL = new Vector3(-0.62f, 1.3f, 0.15f);
            roar.spineEuler = new Vector3(-8f, 0f, 0f);
            roar.chestEuler = new Vector3(-14f, 0f, 0f);
            roar.headEuler = new Vector3(-30f, 0f, 0f);
            Clip("EnemyRoar", K(0.25f, roar, Ease.Out), K(1f, roar));
            Clip("EnemyBlock", K(1f, block, Ease.Out));
            var hop = b;
            hop.visualEuler = new Vector3(-18f, 0f, 0f);
            hop.hips = new Vector3(0f, -0.25f, 0f);
            Clip("EnemyDodge", K(1f, hop, Ease.Out));
        }

        private static void BuildOni()
        {
            var b = HeroBase();
            b.hipsEuler = new Vector3(0f, -8f, 0f);
            b.spineEuler = new Vector3(8f, 0f, 0f);
            b.chestEuler = new Vector3(4f, 10f, 0f);
            b.handR = new Vector3(0.42f, 1.02f, 0.2f);
            b.bladeDir = new Vector3(0.25f, 0.55f, 0.8f).normalized;
            b.bladeEdge = new Vector3(0f, 0.8f, -0.55f).normalized;
            b.twoHand = 0f;
            b.handL = new Vector3(-0.42f, 0.98f, 0.12f);
            b.footL = new Vector3(-0.2f, 0f, 0.12f);
            b.footR = new Vector3(0.2f, 0f, -0.12f);
            b.elbowHintR = new Vector3(0.9f, -0.4f, -0.2f);
            b.elbowHintL = new Vector3(-0.9f, -0.4f, -0.2f);
            var block = b;
            block.handR = new Vector3(0.3f, 1.5f, 0.35f);
            block.bladeDir = new Vector3(-1f, 0.3f, 0.1f).normalized;
            block.bladeEdge = Vector3.forward;
            var air = b;
            air.footL = new Vector3(-0.2f, 0.25f, 0.1f);
            air.footR = new Vector3(0.2f, 0.2f, -0.1f);
            var sprint = b;
            sprint.spineEuler = new Vector3(18f, 0f, 0f);
            var down = _hero.Down;
            down.twoHand = 0f;
            var dead = down;
            dead.visualEuler = new Vector3(-88f, -20f, 0f);
            _oni = new LocomotionProfile
            {
                Guard = b, Relaxed = b, Sprint = sprint, Block = block, AirRise = air, AirFall = air, Down = down, Dead = dead,
                StrideLength = 1.1f, LeanPerSpeed = 1.6f, SprintLean = 16f, StanceWidth = 0.2f
            };

            MotionPose Club(float tilt, float angle, float sign, float drop, float twoHand)
            {
                var p = Slash(b, tilt, angle, sign, 0.58f, twoHand);
                if (twoHand < 0.5f) p.handL = new Vector3(-0.45f, 1.1f, 0.2f);
                p.hips = new Vector3(0f, -drop, 0f);
                return p;
            }

            Attack("OniSmash",
                Lean(Club(90f, 172f, -1f, 0.05f, 1f), -12f, -8f),
                Club(90f, 60f, -1f, 0.15f, 1f),
                Lean(Stance(Club(90f, -58f, -1f, 0.34f, 1f), LungeL * 1.2f, LungeR * 1.2f, 0.34f), 26f, 8f));
            Attack("OniSweep",
                Stance(Club(4f, 132f, -1f, 0.18f, 0f), WideL * 1.2f, WideR * 1.2f, 0.18f, 30f),
                Club(4f, 0f, -1f, 0.2f, 0f),
                Stance(Club(4f, -142f, -1f, 0.22f, 0f), WideL * 1.2f, WideR * 1.2f, 0.22f, -30f));
            var stompA = b;
            stompA.footR = new Vector3(0.24f, 0.55f, 0.22f);
            stompA.handR = new Vector3(0.6f, 1.45f, 0.1f);
            stompA.handL = new Vector3(-0.6f, 1.45f, 0.1f);
            stompA.spineEuler = new Vector3(-8f, 0f, 0f);
            var stompB = b;
            stompB.footR = new Vector3(0.24f, 0f, 0.3f);
            stompB.hips = new Vector3(0f, -0.25f, 0f);
            stompB.spineEuler = new Vector3(18f, 0f, 0f);
            stompB.handR = new Vector3(0.55f, 0.95f, 0.3f);
            stompB.handL = new Vector3(-0.55f, 0.95f, 0.3f);
            Attack("OniStomp", stompA, stompB, stompB);
            var charge = Club(10f, 40f, 1f, 0.2f, 0f);
            charge.visualEuler = new Vector3(20f, 0f, 0f);
            charge.headEuler = new Vector3(-20f, 0f, 0f);
            Attack("OniCharge", charge, charge, charge);
            var throwA = b;
            throwA.handL = new Vector3(-0.45f, 1.6f, -0.35f);
            throwA.chestEuler = new Vector3(0f, -25f, 0f);
            var throwB = b;
            throwB.handL = new Vector3(-0.2f, 1.45f, 0.75f);
            throwB.chestEuler = new Vector3(6f, 25f, 0f);
            throwB.elbowHintL = new Vector3(-0.6f, -0.5f, 0.2f);
            Attack("OniThrow", throwA, throwB, throwB);
            var roar = b;
            roar.handR = new Vector3(0.75f, 1.55f, 0.2f);
            roar.handL = new Vector3(-0.75f, 1.55f, 0.2f);
            roar.chestEuler = new Vector3(-18f, 0f, 0f);
            roar.headEuler = new Vector3(-30f, 0f, 0f);
            Clip("OniRoar", K(0.2f, roar, Ease.Out), K(1f, roar));
            var cleaveA = Lean(Club(90f, 175f, -1f, 0.3f, 1f), -14f, -8f);
            var cleaveB = Lean(Stance(Club(90f, -62f, -1f, 0.4f, 1f), WideL * 1.2f, WideR * 1.2f, 0.4f), 30f, 10f);
            Attack("OniCleave", cleaveA, Club(90f, 80f, -1f, 0.2f, 1f), cleaveB);
            Clip("OniCleaveHold", K(1f, cleaveA, Ease.Out));
        }
    }
}
