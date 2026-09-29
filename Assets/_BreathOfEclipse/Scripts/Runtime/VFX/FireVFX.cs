using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// EMBER BREATH effects. Identity: flame tongues and flame ribbons, heavy directional explosions with smoke,
    /// embers that linger, heat distortion and scorch marks.
    /// </summary>
    public static class FireVFX
    {
        private static Material FlameRibbon(VfxBuild b) =>
            MaterialFactory.Ribbon("fire_body", b.Pal.Core, b.Pal.Edge, VfxBlend.Additive, ProceduralTextures.Noise, 4f, 1.2f);

        private static Material HotCore(VfxBuild b) =>
            MaterialFactory.Ribbon("fire_core", b.Pal.Bright, b.Pal.Core, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 0.8f);

        private static void Flames(VfxBuild b, string name, int count, Vector3 pos, float radius, float speedMin, float speedMax, float sizeMin, float sizeMax, float life, float delay = 0f, Vector3 euler = default, ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere, float cone = 25f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Flame), pos, euler).Burst(count).Delay(delay)
                .Shape(shape, radius, cone).Life(life * 0.6f, life).Speed(speedMin, speedMax).Size(sizeMin, sizeMax)
                .Rotation(-15f, 15f).Color(b.Pal.Core, b.Pal.Bright).Gravity(-0.4f).Drag(0.1f).Noise(0.5f, 1.5f)
                .Fade(0.03f, 0.35f).SizeOverLife(new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.2f)));
        }

        private static void Embers(VfxBuild b, string name, int count, Vector3 pos, float radius, float life, float delay = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.SoftCircle), pos).Burst(count).Delay(delay)
                .Shape(ParticleSystemShapeType.Sphere, radius).Life(life * 0.5f, life).Speed(1f, 4f).Size(0.03f, 0.08f)
                .Color(b.Pal.Accent, b.Pal.Bright).Gravity(-0.25f).Noise(0.8f, 1.2f).Fade(0.02f, 0.6f);
        }

        private static void Smoke(VfxBuild b, string name, int count, Vector3 pos, float radius, float delay = 0f)
        {
            b.Particles(name, b.Alpha(ProceduralTextures.Smoke), pos).Burst(count).Delay(delay)
                .Shape(ParticleSystemShapeType.Sphere, radius).Life(1f, 1.8f).Speed(0.5f, 1.8f).Size(0.8f, 1.8f).Rotation(0f, 360f)
                .Color(new Color(0.08f, 0.05f, 0.05f, 0.55f)).Gravity(-0.08f).Drag(0.1f).Fade(0.15f, 0.4f).Grow(0.6f, 1.8f);
        }

        // =================================================================== IV / V / VII
        // Magma Hammer: the smash splits the ground and three geysers erupt in a line.
        // Ember Wall: a curved wall of fire that stays and burns whoever crosses it.
        // Volcano's Heart: the ground glows and cracks under the swordsman, then erupts all around.

        private static void RegisterForms()
        {
            VFXLibrary.Register("fire_magma_line", b =>
            {
                b.Lifetime = 2.2f;
                bool extra = VFXQuality.Secondary;
                for (int i = 0; i < 3; i++)
                {
                    float z = 1.6f + i * 1.7f;
                    float delay = i * 0.1f;
                    Flames(b, "Geyser" + i, extra ? 24 : 12, new Vector3(0f, 0.1f, z), 0.3f, 5f, 9f, 0.4f, 0.9f, 0.55f, delay, new Vector3(-90f, 0f, 0f), ParticleSystemShapeType.Cone, 12f);
                    var column = b.Tube("Column" + i, HotCore(b), u => new Vector3(Mathf.Sin(u * 7f) * 0.08f, u * (2.2f - i * 0.3f), z), 0.22f, 0.12f, 0.2f, 0.4f);
                    column.StartDelay = delay;
                    column.RadiusProfile = new AnimationCurve(new Keyframe(0f, 1.2f), new Keyframe(1f, 0.3f));
                    b.Decal("Crack" + i, ProceduralTextures.Crack, new Color(1f, 0.55f, 0.2f, 0.9f), 2f, 2.2f, new Vector3(0f, 0f, z));
                    if (extra) Embers(b, "Embers" + i, 14, new Vector3(0f, 1f, z), 0.6f, 1.2f, delay);
                    b.Light("Light" + i, new Vector3(0f, 1f, z), b.Pal.Core, 6f, 5f, 0.5f, delay);
                }
                var fissure = b.Tube("Fissure", FlameRibbon(b), u => new Vector3(Mathf.Sin(u * 11f) * 0.15f, 0.05f, 0.6f + u * 5f), 0.12f, 0.2f, 0.6f, 0.6f);
                fissure.RadialSegments = 6;
                Smoke(b, "Smoke", extra ? 10 : 5, new Vector3(0f, 1.2f, 3.2f), 1.5f, 0.25f);
            });

            VFXLibrary.Register("fire_wall", b =>
            {
                b.Lifetime = 2.4f;
                bool extra = VFXQuality.Secondary;
                // Curved wall across the front, 5 m wide, burning for ~1.6 s.
                b.Particles("Wall", b.Additive(ProceduralTextures.Flame), new Vector3(0f, 0.2f, 0f)).Duration(1.6f).Rate(extra ? 140f : 60f)
                    .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, new Vector3(5f, 0.1f, 0.5f)).Life(0.5f, 0.9f).Speed(0.3f, 0.8f)
                    .Size(0.5f, 1.1f).Rotation(-15f, 15f).Color(b.Pal.Core, b.Pal.Bright).Velocity(new Vector3(0f, 2.4f, 0f)).Noise(0.6f, 1.5f)
                    .Fade(0.08f, 0.5f).SizeOverLife(new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.25f)));
                var baseLine = b.Tube("Base", FlameRibbon(b), u => new Vector3(Mathf.Lerp(-2.6f, 2.6f, u), 0.15f, -Mathf.Pow(Mathf.Lerp(-1f, 1f, u), 2f) * 0.5f), 0.18f, 0.15f, 1.3f, 0.5f);
                baseLine.Wobble = 0.08f;
                var crest = b.Tube("Crest", HotCore(b), u => new Vector3(Mathf.Lerp(-2.4f, 2.4f, u), 1.4f + Mathf.Sin(u * 9f) * 0.2f, -Mathf.Pow(Mathf.Lerp(-1f, 1f, u), 2f) * 0.5f), 0.1f, 0.2f, 1f, 0.5f);
                crest.Wobble = 0.15f;
                crest.WobbleSpeed = 12f;
                Embers(b, "Embers", extra ? 40 : 16, new Vector3(0f, 1.2f, 0f), 2.4f, 1.6f);
                if (extra) Smoke(b, "Smoke", 8, new Vector3(0f, 2.2f, 0f), 2f, 0.4f);
                CommonVFX.Distortion(b, "Heat", 3f, 1.6f);
                b.Light("Light", new Vector3(0f, 1f, 0f), b.Pal.Core, 9f, 5f, 1.8f);
            });

            VFXLibrary.Register("fire_heart_build", b =>
            {
                b.Lifetime = 1.2f;
                var cracks = b.Shape("Cracks", ProceduralMeshes.GroundQuad(), b.Additive(ProceduralTextures.Crack), new Vector3(0f, 0.04f, 0f), Vector3.zero,
                    new Vector3(1f, 1f, 1f), new Vector3(4.5f, 1f, 4.5f), 0.7f, b.Pal.Core);
                cracks.ScaleCurve = CommonVFX.FastOut;
                cracks.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0.7f), new Keyframe(0.6f, 0.4f), new Keyframe(1f, 1f));
                b.Particles("Rise", b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 0.1f, 0f)).Duration(0.6f).Rate(VFXQuality.Secondary ? 70f : 30f)
                    .Shape(ParticleSystemShapeType.Circle, 2f, 0f, 360f, null, new Vector3(-90f, 0f, 0f)).Life(0.5f, 0.9f).Speed(0.5f, 1.5f)
                    .Size(0.03f, 0.07f).Color(b.Pal.Accent, b.Pal.Bright).Velocity(new Vector3(0f, 1.8f, 0f), 1.5f, -0.8f).Fade(0.05f, 0.6f);
                CommonVFX.Distortion(b, "Heat", 3.5f, 0.8f);
                b.Light("Pulse", new Vector3(0f, 0.6f, 0f), b.Pal.Core, 6f, 5f, 0.7f);
                var l = b.Root.GetComponentInChildren<FlashLight>();
                if (l != null) l.Curve = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.3f, 0.6f), new Keyframe(0.45f, 0.3f), new Keyframe(0.7f, 0.9f), new Keyframe(0.8f, 0.5f), new Keyframe(1f, 1f));
            });

            VFXLibrary.Register("fire_eruption", b =>
            {
                b.Lifetime = 2.6f;
                bool extra = VFXQuality.Secondary, full = VFXQuality.Full;
                Flames(b, "Core", extra ? 50 : 24, new Vector3(0f, 0.2f, 0f), 0.6f, 8f, 14f, 0.7f, 1.5f, 0.7f, 0f, new Vector3(-90f, 0f, 0f), ParticleSystemShapeType.Cone, 18f);
                var ring = b.Shape("FireRing", ProceduralMeshes.Ring(0.82f), FlameRibbon(b), new Vector3(0f, 0.08f, 0f), Vector3.zero,
                    Vector3.one * 0.3f, new Vector3(5.5f, 1f, 5.5f), 0.55f, Color.white);
                ring.ColorProperty = "_ColorA";
                ring.ScaleCurve = CommonVFX.FastOut;
                ring.AlphaCurve = CommonVFX.PopFade;
                int pillars = full ? 8 : extra ? 6 : 4;
                for (int i = 0; i < pillars; i++)
                {
                    float a = i / (float)pillars * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * 3.2f, 0.1f, Mathf.Sin(a) * 3.2f);
                    Flames(b, "Pillar" + i, extra ? 14 : 8, p, 0.3f, 6f, 10f, 0.4f, 0.9f, 0.5f, 0.08f + i * 0.02f, new Vector3(-90f, 0f, 0f), ParticleSystemShapeType.Cone, 10f);
                }
                Embers(b, "Embers", extra ? 70 : 30, new Vector3(0f, 1.5f, 0f), 2.5f, 2f, 0.1f);
                Smoke(b, "Smoke", extra ? 16 : 8, new Vector3(0f, 2.5f, 0f), 2.5f, 0.2f);
                CommonVFX.Dust(b, "Dust", 14, 2.5f, 1.4f, new Color(0.25f, 0.2f, 0.18f, 0.5f), 3.5f);
                b.Decal("Scorch", ProceduralTextures.Scorch, new Color(0.2f, 0.1f, 0.05f, 0.9f), 6f, 4f);
                b.Light("Light", new Vector3(0f, 1.5f, 0f), b.Pal.Core, 16f, 12f, 0.9f);
            });
        }

        public static void Register()
        {
            RegisterForms();
            VFXLibrary.Register("fire_charge", b =>
            {
                b.Lifetime = 1f;
                b.Particles("Blade", b.Additive(ProceduralTextures.Flame), new Vector3(0f, 0f, 0.55f)).Duration(0.5f).Rate(90f)
                    .Shape(ParticleSystemShapeType.Box, 0.05f, 0f, 360f, new Vector3(0.06f, 0.06f, 1f)).Life(0.2f, 0.4f).Speed(0.4f, 1.2f)
                    .Size(0.12f, 0.3f).Color(b.Pal.Core, b.Pal.Bright).Gravity(-0.6f).Fade(0.05f, 0.4f).Shrink(1f, 0.2f);
                Embers(b, "Embers", 20, new Vector3(0f, 0f, 0.5f), 0.4f, 0.9f);
                CommonVFX.Glow(b, "Glow", b.Pal.Core * 0.6f, 1.6f, 0.6f, new Vector3(0f, 0f, 0.55f));
                CommonVFX.Distortion(b, "Heat", 1.6f, 0.6f);
                b.Light("Light", new Vector3(0f, 0f, 0.5f), b.Pal.Core, 5f, 4f, 0.7f);
            });

            // Vertical crescent of fire following an overhead cut.
            VFXLibrary.Register("fire_arc", b =>
            {
                b.Lifetime = 1.4f;
                var arc = b.Tube("Arc", FlameRibbon(b), PathUtil.VerticalArc(1.9f, 1.1f, 0.4f, 150f, -40f), 0.32f, 0.12f, 0.25f, 0.45f);
                arc.Wobble = 0.12f;
                arc.WobbleSpeed = 16f;
                arc.RadiusProfile = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.6f, 1.1f), new Keyframe(1f, 0.6f));
                var core = b.Tube("Core", HotCore(b), PathUtil.VerticalArc(1.9f, 1.1f, 0.4f, 150f, -40f), 0.12f, 0.1f, 0.2f, 0.35f);
                core.Wobble = 0.08f;
                for (int i = 0; i < 5; i++)
                {
                    float u = i / 4f;
                    Vector3 p = PathUtil.VerticalArc(1.9f, 1.1f, 0.4f, 150f, -40f)(u);
                    Flames(b, "Lick" + i, 10, p, 0.2f, 0.5f, 2f, 0.3f, 0.7f, 0.6f, u * 0.12f);
                }
                Embers(b, "Embers", 30, new Vector3(0f, 1.2f, 1.5f), 1f, 1.3f, 0.1f);
                b.Light("Light", new Vector3(0f, 1.5f, 1.5f), b.Pal.Core, 8f, 6f, 0.6f);
            });

            // Directional explosion (forward +Z).
            VFXLibrary.Register("fire_explosion", b =>
            {
                b.Lifetime = 2.2f;
                Flames(b, "Blast", 46, new Vector3(0f, 1f, 0f), 0.4f, 6f, 14f, 0.6f, 1.4f, 0.55f, 0f, new Vector3(0f, 0f, 0f), ParticleSystemShapeType.Cone, 28f);
                Flames(b, "Core", 18, new Vector3(0f, 1f, 1.5f), 0.8f, 0.5f, 2f, 1f, 2f, 0.4f);
                Smoke(b, "Smoke", 14, new Vector3(0f, 1.3f, 2.5f), 1.2f, 0.15f);
                Embers(b, "Embers", 40, new Vector3(0f, 1f, 2f), 1.2f, 1.6f, 0.05f);
                var ring = b.Shape("Ring", ProceduralMeshes.Ring(0.8f), b.Additive(ProceduralTextures.Band), new Vector3(0f, 1f, 1f), new Vector3(90f, 0f, 0f),
                    Vector3.one * 0.3f, Vector3.one * 3.5f, 0.3f, b.Pal.Bright);
                ring.ScaleCurve = CommonVFX.FastOut;
                ring.AlphaCurve = CommonVFX.PopFade;
                CommonVFX.Glint(b, "Flash", b.Pal.Bright, 3.5f, 0.2f, new Vector3(0f, 1f, 1f));
                CommonVFX.Distortion(b, "Heat", 4.5f, 0.5f);
                b.Decal("Scorch", ProceduralTextures.Scorch, new Color(1f, 1f, 1f, 0.8f), 3.5f, 4f, new Vector3(0f, 0f, 2f));
                b.Light("Light", new Vector3(0f, 1.2f, 2f), b.Pal.Core, 12f, 10f, 0.7f);
            });

            // Rolling fire wheel (projectile visual).
            VFXLibrary.Register("fire_wheel", b =>
            {
                b.Lifetime = 2f;
                var ring = b.Shape("Wheel", ProceduralMeshes.Ring(0.72f), b.Additive(ProceduralTextures.Band), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 90f),
                    Vector3.one * 1.1f, Vector3.one * 1.2f, 2f, b.Pal.Core);
                ring.SpinDegreesPerSecond = new Vector3(0f, 900f, 0f);
                ring.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
                b.Particles("Flames", b.Additive(ProceduralTextures.Flame), new Vector3(0f, 1f, 0f), new Vector3(0f, 90f, 0f)).Duration(2f, true).Rate(90f)
                    .Shape(ParticleSystemShapeType.Circle, 1.1f, 0f, 360f).Life(0.2f, 0.4f).Speed(0.2f, 0.8f).Size(0.3f, 0.6f)
                    .Color(b.Pal.Core, b.Pal.Bright).Gravity(-0.5f).Fade(0.05f, 0.4f).Shrink(1f, 0.2f);
                Embers(b, "Trail", 30, new Vector3(0f, 1f, 0f), 1f, 1f);
                b.Light("Light", new Vector3(0f, 1f, 0f), b.Pal.Core, 6f, 4f, 2f);
                var l = b.Root.GetComponentInChildren<FlashLight>();
                if (l != null) l.Curve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.1f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f));
            });

            VFXLibrary.Register("fire_cone", b =>
            {
                b.Lifetime = 1.6f;
                Flames(b, "Jet", 70, new Vector3(0f, 1.1f, 0.4f), 0.2f, 9f, 16f, 0.4f, 1.1f, 0.45f, 0f, Vector3.zero, ParticleSystemShapeType.Cone, 18f);
                Smoke(b, "Smoke", 10, new Vector3(0f, 1.3f, 4f), 1f, 0.2f);
                Embers(b, "Embers", 30, new Vector3(0f, 1.1f, 3f), 1.5f, 1.2f);
                CommonVFX.Distortion(b, "Heat", 3.5f, 0.5f);
                b.Light("Light", new Vector3(0f, 1.2f, 3f), b.Pal.Core, 10f, 8f, 0.6f);
            });

            VFXLibrary.Register("fire_phoenix", b =>
            {
                b.Lifetime = 2f;
                for (int i = 0; i < 2; i++)
                {
                    var helix = b.Tube("Helix" + i, i == 0 ? FlameRibbon(b) : HotCore(b), PathUtil.Helix(4.5f, 1.4f, 0.4f, 1.6f, i * Mathf.PI), i == 0 ? 0.3f : 0.12f, 0.35f, 0.3f, 0.5f);
                    helix.Wobble = 0.1f;
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    var wing = b.Shape("Wing", ProceduralMeshes.Crescent(100f, 0.45f), b.Additive(ProceduralTextures.CrescentBand), new Vector3(0.4f * s, 3.8f, 0f),
                        new Vector3(0f, 90f * s, 70f * s), Vector3.one * 0.5f, Vector3.one * 3.2f, 0.9f, b.Pal.Core);
                    wing.Delay = 0.3f;
                    wing.ScaleCurve = CommonVFX.FastOut;
                    wing.AlphaCurve = CommonVFX.PopFade;
                }
                Flames(b, "Pillar", 40, new Vector3(0f, 1f, 0f), 1f, 3f, 7f, 0.5f, 1.2f, 0.7f, 0f, new Vector3(-90f, 0f, 0f), ParticleSystemShapeType.Cone, 12f);
                Embers(b, "Embers", 50, new Vector3(0f, 3f, 0f), 1.5f, 1.8f, 0.2f);
                b.Light("Light", new Vector3(0f, 3f, 0f), b.Pal.Core, 12f, 8f, 1.2f);
            });

            VFXLibrary.Register("fire_sun", b =>
            {
                b.Lifetime = 3f;
                var sun = b.Shape("Sun", ProceduralMeshes.Primitive(PrimitiveType.Sphere), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 5f, 0f), Vector3.zero,
                    Vector3.one * 0.3f, Vector3.one * 5f, 1.8f, b.Pal.Bright);
                sun.ScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                sun.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
                b.Particles("Corona", b.Additive(ProceduralTextures.Flame), new Vector3(0f, 5f, 0f)).Duration(1.8f).Rate(110f)
                    .Shape(ParticleSystemShapeType.Sphere, 1.8f, 0f, 360f, null, null, null, 0.1f).Life(0.4f, 0.8f).Speed(1f, 3f).Size(0.8f, 1.6f)
                    .Color(b.Pal.Core, b.Pal.Accent).Fade(0.1f, 0.4f).Shrink(1f, 0.2f);
                b.Particles("Gather", b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 5f, 0f)).Duration(1.4f).Rate(80f)
                    .Shape(ParticleSystemShapeType.Sphere, 7f, 0f, 360f, null, null, null, 0.1f).Life(0.5f, 0.8f).Speed(-10f, -7f).Size(0.08f, 0.16f)
                    .Color(b.Pal.Bright).Fade(0.1f, 0.6f);
                CommonVFX.Distortion(b, "Heat", 9f, 1.8f);
                b.Light("Light", new Vector3(0f, 5f, 0f), b.Pal.Core, 30f, 10f, 2f);
            });

            VFXLibrary.Register("fire_column", b =>
            {
                b.Lifetime = 2.5f;
                var shell = b.Shape("Shell", ProceduralMeshes.OpenCylinder(1.2f, 0.6f, 24, 6, 2f), b.Additive(ProceduralTextures.Flame), Vector3.zero, Vector3.zero,
                    new Vector3(1f, 0.2f, 1f), new Vector3(1.6f, 9f, 1.6f), 1f, b.Pal.Core);
                shell.SpinDegreesPerSecond = new Vector3(0f, 240f, 0f);
                shell.ScaleCurve = CommonVFX.FastOut;
                shell.AlphaCurve = CommonVFX.PopFade;
                Flames(b, "Pillar", 90, new Vector3(0f, 0.3f, 0f), 1.2f, 6f, 14f, 0.7f, 1.6f, 0.9f, 0f, new Vector3(-90f, 0f, 0f), ParticleSystemShapeType.Cone, 8f);
                Smoke(b, "Smoke", 16, new Vector3(0f, 6f, 0f), 2f, 0.4f);
                Embers(b, "Embers", 60, new Vector3(0f, 3f, 0f), 2f, 2f, 0.2f);
                CommonVFX.FlatRing(b, "Ring", b.Pal.Bright, 9f, 0.45f);
                b.Decal("Scorch", ProceduralTextures.Scorch, Color.white, 6f, 5f);
                b.Light("Light", new Vector3(0f, 3f, 0f), b.Pal.Core, 20f, 14f, 1.2f);
            });

            VFXLibrary.Register("burn_status", b =>
            {
                b.Lifetime = 2.8f;
                b.Particles("Flames", b.Additive(ProceduralTextures.Flame), new Vector3(0f, 0.8f, 0f)).Duration(2.4f).Rate(26f)
                    .Shape(ParticleSystemShapeType.Box, 0.2f, 0f, 360f, new Vector3(0.5f, 1.2f, 0.4f)).Life(0.3f, 0.6f).Speed(0.5f, 1.2f).Size(0.2f, 0.4f)
                    .Color(b.Pal.Core, b.Pal.Bright).Gravity(-0.5f).Fade(0.1f, 0.4f).Shrink(1f, 0.2f);
            });
        }
    }
}
