using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Shared building blocks and generic combat effects (impacts, parry sparks, dust, shockwaves, deaths).
    /// Element recipes reuse the static helpers but add their own signature shapes.
    /// </summary>
    public static class CommonVFX
    {
        public static AnimationCurve FastOut => new AnimationCurve(new Keyframe(0f, 0f, 0f, 6f), new Keyframe(0.35f, 0.9f), new Keyframe(1f, 1f));
        public static AnimationCurve PopFade => new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.4f, 0.7f), new Keyframe(1f, 0f));
        public static AnimationCurve QuickFade => new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));

        // ------------------------------------------------------------------ building blocks

        public static void Sparks(VfxBuild b, string name, int count, Color color, float speedMin, float speedMax, float life, float size = 0.06f,
            float gravity = 0.8f, float coneAngle = 180f, Vector3 euler = default, float delay = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Streak), Vector3.zero, euler)
                .Burst(count).Delay(delay)
                .Shape(coneAngle >= 180f ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone, 0.05f, Mathf.Min(coneAngle, 89f))
                .Life(life * 0.5f, life).Speed(speedMin, speedMax).Size(size * 0.6f, size)
                .Color(color).Gravity(gravity).Drag(0.08f).Fade(0.01f, 0.4f)
                .Stretch(1.2f, 0.035f);
        }

        public static void Glow(VfxBuild b, string name, Color color, float size, float life, Vector3 pos = default, float delay = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Glow), pos)
                .Burst(1).Delay(delay).NoShape().Speed(0f, 0f).Life(life, life).Size(size, size)
                .Color(color).Local().Fade(0.02f, 0.2f).Grow(0.6f, 1.25f);
        }

        public static void Glint(VfxBuild b, string name, Color color, float size, float life, Vector3 pos = default, float delay = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Star), pos)
                .Burst(1).Delay(delay).NoShape().Speed(0f, 0f).Life(life, life).Size(size, size)
                .Rotation(0f, 45f).Color(color).Local().Fade(0.01f, 0.25f).SizeOverLife(new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1.2f), new Keyframe(1f, 0f)));
        }

        public static ExpandingMesh FlatRing(VfxBuild b, string name, Color color, float endRadius, float duration, float y = 0.05f, float delay = 0f, float thickness = 0.8f)
        {
            var e = b.Shape(name, ProceduralMeshes.Ring(thickness), b.Additive(ProceduralTextures.Band), new Vector3(0f, y, 0f), Vector3.zero,
                Vector3.one * 0.1f, Vector3.one * endRadius, duration, color);
            e.ScaleCurve = FastOut;
            e.AlphaCurve = PopFade;
            e.Delay = delay;
            return e;
        }

        public static void Dust(VfxBuild b, string name, int count, float radius, float life, Color color, float speed = 2f)
        {
            b.Particles(name, b.Alpha(ProceduralTextures.Smoke), new Vector3(0f, 0.15f, 0f))
                .Burst(count).Shape(ParticleSystemShapeType.Circle, radius, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                .Life(life * 0.6f, life).Speed(speed * 0.5f, speed).Size(0.6f, 1.4f).Rotation(0f, 360f).Spin(-40f, 40f)
                .Color(color).Drag(0.15f).Gravity(-0.02f).Fade(0.05f, 0.3f).Grow(0.5f, 1.6f);
        }

        public static void Debris(VfxBuild b, string name, int count, float speed, Color color)
        {
            b.Particles(name, b.Alpha(ProceduralTextures.SoftCircle), new Vector3(0f, 0.1f, 0f))
                .Burst(count).Shape(ParticleSystemShapeType.Cone, 0.3f, 50f, 360f, null, new Vector3(-90f, 0f, 0f))
                .Life(0.6f, 1.1f).Speed(speed * 0.5f, speed).Size(0.06f, 0.16f).Color(color).Gravity(2.2f).Fade(0.01f, 0.8f);
        }

        public static void Distortion(VfxBuild b, string name, float endScale, float duration, float delay = 0f)
        {
            var e = b.Shape(name, ProceduralMeshes.Primitive(PrimitiveType.Sphere), MaterialFactory.Distortion(), Vector3.up * 0.6f, Vector3.zero,
                Vector3.one * 0.2f, Vector3.one * endScale, duration, new Color(1f, 1f, 1f, 1f));
            e.ColorProperty = "_BaseColor";
            e.ScaleCurve = FastOut;
            e.AlphaCurve = QuickFade;
            e.Delay = delay;
        }

        // ------------------------------------------------------------------ registry

        public static void Register()
        {
            VFXLibrary.Register("impact_slash", b =>
            {
                b.Lifetime = 0.9f;
                Glint(b, "Glint", b.Pal.Bright, 1.2f, 0.16f);
                Glow(b, "Glow", b.Pal.Core * 0.6f, 1.4f, 0.2f);
                Sparks(b, "Sparks", 14, b.Pal.Bright, 5f, 11f, 0.35f, 0.07f, 0.6f);
                Sparks(b, "ElemSparks", 10, b.Pal.Core, 3f, 7f, 0.5f, 0.05f, 0.2f);
                var slash = b.Shape("Cut", ProceduralMeshes.Crescent(120f, 0.18f), b.Additive(ProceduralTextures.CrescentBand), Vector3.zero,
                    new Vector3(90f, 0f, Random.Range(-60f, 60f)), new Vector3(0.6f, 0.6f, 0.6f), new Vector3(1.6f, 1.6f, 1.6f), 0.14f, b.Pal.Bright);
                slash.ScaleCurve = FastOut;
                slash.AlphaCurve = PopFade;
            });

            VFXLibrary.Register("impact_heavy", b =>
            {
                b.Lifetime = 1.2f;
                Glint(b, "Glint", b.Pal.Bright, 2.2f, 0.22f);
                Glow(b, "Glow", b.Pal.Core, 2.6f, 0.3f);
                Sparks(b, "Sparks", 24, b.Pal.Bright, 6f, 15f, 0.45f, 0.09f, 0.8f);
                Sparks(b, "Embers", 18, b.Pal.Core, 2f, 6f, 0.9f, 0.05f, -0.1f);
                var ring = b.Shape("Ring", ProceduralMeshes.Ring(0.86f), b.Additive(ProceduralTextures.Band), Vector3.zero, new Vector3(90f, 0f, 0f),
                    Vector3.one * 0.2f, Vector3.one * 2.4f, 0.28f, b.Pal.Bright);
                ring.ScaleCurve = FastOut;
                ring.AlphaCurve = PopFade;
                b.Light("Light", Vector3.zero, b.Pal.Core, 6f, 5f, 0.22f);
            });

            VFXLibrary.Register("impact_crit", b =>
            {
                b.Lifetime = 1.3f;
                Glint(b, "Glint", b.Pal.Bright * 1.4f, 3.4f, 0.28f);
                Glint(b, "Glint2", b.Pal.Core, 2.2f, 0.4f, Vector3.zero, 0.04f);
                Glow(b, "Glow", b.Pal.Core * 1.2f, 3.4f, 0.35f);
                Sparks(b, "Sparks", 36, b.Pal.Bright, 8f, 20f, 0.5f, 0.1f, 0.5f);
                for (int i = 0; i < 3; i++)
                {
                    var ring = b.Shape("Ring" + i, ProceduralMeshes.Ring(0.9f), b.Additive(ProceduralTextures.Band), Vector3.zero,
                        new Vector3(90f + i * 35f, i * 50f, 0f), Vector3.one * 0.2f, Vector3.one * (2.6f + i * 0.8f), 0.32f, b.Pal.Bright);
                    ring.ScaleCurve = FastOut;
                    ring.AlphaCurve = PopFade;
                    ring.Delay = i * 0.03f;
                }
                b.Light("Light", Vector3.zero, b.Pal.Bright, 8f, 9f, 0.25f);
            });

            VFXLibrary.Register("impact_dark", b =>
            {
                b.Lifetime = 0.9f;
                var pal = ElementPalette.Get(Combat.Element.Dark);
                Glint(b, "Glint", pal.Bright, 1.4f, 0.16f);
                Sparks(b, "Sparks", 16, pal.Core, 4f, 10f, 0.4f, 0.07f, 0.8f);
                Glow(b, "Glow", pal.Edge, 1.8f, 0.25f);
            });

            VFXLibrary.Register("demon_hit", b =>
            {
                b.Lifetime = 1.2f;
                var pal = ElementPalette.Get(Combat.Element.Dark);
                b.Particles("Ash", b.Alpha(ProceduralTextures.Smoke)).Burst(8).Shape(ParticleSystemShapeType.Sphere, 0.2f)
                    .Life(0.5f, 1f).Speed(1f, 3f).Size(0.2f, 0.45f).Color(new Color(0.05f, 0.02f, 0.06f, 0.8f)).Gravity(-0.15f).Fade(0.02f, 0.3f).Grow(0.5f, 1.4f);
                Sparks(b, "Embers", 10, pal.Core, 1f, 4f, 0.9f, 0.04f, -0.2f);
            });

            VFXLibrary.Register("parry_spark", b =>
            {
                b.Lifetime = 1.2f;
                Color gold = new Color(4f, 3f, 1.2f);
                Glint(b, "Glint", gold * 1.3f, 3f, 0.3f);
                Glint(b, "Glint2", Color.white * 3f, 1.5f, 0.12f);
                Sparks(b, "Sparks", 40, gold, 7f, 18f, 0.5f, 0.08f, 1.2f);
                var ring = b.Shape("Ring", ProceduralMeshes.Ring(0.92f), b.Additive(ProceduralTextures.Band), Vector3.zero, new Vector3(90f, 0f, 0f),
                    Vector3.one * 0.2f, Vector3.one * 3f, 0.3f, gold);
                ring.ScaleCurve = FastOut;
                ring.AlphaCurve = PopFade;
                b.Light("Light", Vector3.zero, gold, 7f, 10f, 0.25f);
            });

            VFXLibrary.Register("block_spark", b =>
            {
                b.Lifetime = 0.7f;
                Color c = new Color(3f, 1.6f, 0.5f);
                Sparks(b, "Sparks", 14, c, 3f, 8f, 0.3f, 0.05f, 1.4f, 50f);
                Glint(b, "Glint", c, 0.9f, 0.1f);
            });

            VFXLibrary.Register("dust_puff", b =>
            {
                b.Lifetime = 1.2f;
                Dust(b, "Dust", 7, 0.35f, 0.8f, new Color(0.45f, 0.45f, 0.5f, 0.35f), 1.5f);
            });

            VFXLibrary.Register("ground_impact", b =>
            {
                b.Lifetime = 4.5f;
                Dust(b, "Dust", 16, 0.8f, 1.1f, new Color(0.4f, 0.4f, 0.46f, 0.45f), 4f);
                Debris(b, "Rocks", 18, 7f, new Color(0.22f, 0.2f, 0.22f, 1f));
                FlatRing(b, "Shock", b.Pal.Core * 0.8f + Color.white * 0.5f, 5f, 0.35f);
                FlatRing(b, "Shock2", Color.white * 1.2f, 3.2f, 0.22f, 0.08f, 0.04f, 0.9f);
                b.Decal("Crack", ProceduralTextures.Crack, new Color(1f, 1f, 1f, 0.9f), 3.2f, 4f);
                Distortion(b, "Heat", 5f, 0.35f);
            });

            VFXLibrary.Register("shockwave", b =>
            {
                b.Lifetime = 0.8f;
                FlatRing(b, "Ring", b.Pal.Bright, 6f, 0.4f);
                FlatRing(b, "Ring2", b.Pal.Core, 4.5f, 0.5f, 0.1f, 0.05f, 0.7f);
                Distortion(b, "Distort", 6f, 0.4f);
            });

            VFXLibrary.Register("speed_burst", b =>
            {
                b.Lifetime = 0.6f;
                b.Particles("Lines", b.Additive(ProceduralTextures.Streak)).Burst(26).Shape(ParticleSystemShapeType.Sphere, 0.6f)
                    .Life(0.15f, 0.3f).Speed(14f, 26f).Size(0.05f, 0.09f).Color(b.Pal.Bright).Stretch(3f, 0.02f).Fade(0.01f, 0.2f);
            });

            VFXLibrary.Register("dodge_wind", b =>
            {
                b.Lifetime = 0.7f;
                b.Particles("Streaks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0f)).Burst(14)
                    .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, new Vector3(0.6f, 1.4f, 0.4f))
                    .Life(0.15f, 0.3f).Speed(0.2f, 0.5f).Size(0.04f, 0.06f).Color(new Color(1.4f, 1.6f, 2f, 0.7f)).Stretch(6f, 0.1f).Fade(0.01f, 0.2f)
                    .Velocity(new Vector3(0f, 0f, -12f));
                Dust(b, "Dust", 5, 0.3f, 0.6f, new Color(0.5f, 0.5f, 0.55f, 0.3f), 1.2f);
            });

            VFXLibrary.Register("perfect_dodge", b =>
            {
                b.Lifetime = 1.4f;
                Color c = new Color(1.2f, 2f, 3.5f);
                var ring = FlatRing(b, "TimeRing", c, 7f, 0.6f, 1f);
                ring.transform.localRotation = Quaternion.identity;
                Glint(b, "Glint", Color.white * 3f, 2.5f, 0.3f, new Vector3(0f, 1.2f, 0f));
                b.Particles("Motes", b.Additive(ProceduralTextures.Star), new Vector3(0f, 1f, 0f)).Burst(30).Shape(ParticleSystemShapeType.Sphere, 1.5f)
                    .Life(0.8f, 1.3f).Speed(0.1f, 0.6f).Size(0.08f, 0.18f).Color(c).Gravity(-0.05f).Fade(0.1f, 0.5f);
                Distortion(b, "Ripple", 8f, 0.5f);
            });

            VFXLibrary.Register("demon_death", b =>
            {
                b.Lifetime = 3f;
                var pal = ElementPalette.Get(Combat.Element.Dark);
                b.Particles("Ash", b.Alpha(ProceduralTextures.Smoke), new Vector3(0f, 1f, 0f)).Burst(40).Shape(ParticleSystemShapeType.Box, 0.3f, 0f, 360f, new Vector3(0.6f, 1.6f, 0.4f))
                    .Life(1.2f, 2.2f).Speed(0.3f, 1.2f).Size(0.15f, 0.4f).Color(new Color(0.04f, 0.02f, 0.05f, 0.9f)).Gravity(-0.25f).Noise(0.6f, 0.8f).Fade(0.05f, 0.4f).Shrink(1f, 0.2f);
                b.Particles("Embers", b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 1f, 0f)).Burst(50).Shape(ParticleSystemShapeType.Box, 0.3f, 0f, 360f, new Vector3(0.7f, 1.8f, 0.5f))
                    .Life(1f, 2.4f).Speed(0.2f, 1f).Size(0.03f, 0.08f).Color(pal.Core, pal.Bright).Gravity(-0.35f).Noise(0.8f, 1f).Fade(0.05f, 0.5f);
                Glow(b, "Flash", pal.Core, 3f, 0.4f, new Vector3(0f, 1f, 0f));
                b.Light("Light", new Vector3(0f, 1f, 0f), pal.Core, 6f, 4f, 0.6f);
            });

            VFXLibrary.Register("debris_wood", b =>
            {
                b.Lifetime = 2f;
                Dust(b, "Dust", 8, 0.4f, 0.9f, new Color(0.5f, 0.45f, 0.38f, 0.4f), 2f);
                b.Particles("Splinters", b.Alpha(ProceduralTextures.Streak)).Burst(20).Shape(ParticleSystemShapeType.Sphere, 0.3f)
                    .Life(0.6f, 1.2f).Speed(3f, 7f).Size(0.1f, 0.2f).Rotation(0f, 360f).Spin(-360f, 360f).Color(new Color(0.45f, 0.3f, 0.18f, 1f)).Gravity(1.8f).Fade(0.01f, 0.7f);
            });

            VFXLibrary.Register("telegraph_glint", b =>
            {
                b.Lifetime = 0.6f;
                Color red = new Color(4f, 0.4f, 0.3f);
                Glint(b, "Glint", red, 1.2f, 0.35f);
                Glow(b, "Glow", red * 0.5f, 1.2f, 0.4f);
            });

            VFXLibrary.Register("enemy_swipe", b =>
            {
                b.Lifetime = 0.6f;
                var pal = ElementPalette.Get(Combat.Element.Dark);
                for (int i = 0; i < 3; i++)
                {
                    var c = b.Shape("Claw" + i, ProceduralMeshes.Crescent(110f, 0.1f), b.Additive(ProceduralTextures.CrescentBand), new Vector3(0f, 1.2f + (i - 1) * 0.12f, 0.5f),
                        new Vector3(0f, 0f, (i - 1) * 6f), new Vector3(1.2f, 1f, 1.2f), new Vector3(1.9f, 1f, 1.9f), 0.22f, pal.Core);
                    c.ScaleCurve = FastOut;
                    c.AlphaCurve = PopFade;
                    c.Delay = i * 0.02f;
                }
            });

            VFXLibrary.Register("spawn_portal", b =>
            {
                b.Lifetime = 2.2f;
                var pal = ElementPalette.Get(Combat.Element.Dark);
                b.Decal("Circle", ProceduralTextures.RingSprite, new Color(pal.Core.r, pal.Core.g, pal.Core.b, 1f), 3f, 2f);
                b.Particles("Rise", b.Additive(ProceduralTextures.SoftCircle)).Duration(1f).Rate(60f).Shape(ParticleSystemShapeType.Circle, 1.2f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.6f, 1.2f).Speed(0.5f, 1f).Size(0.05f, 0.12f).Color(pal.Core).Gravity(-0.6f).Fade(0.1f, 0.5f);
                b.Particles("Smoke", b.Alpha(ProceduralTextures.Smoke)).Burst(14).Shape(ParticleSystemShapeType.Circle, 0.8f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.8f, 1.4f).Speed(0.3f, 1f).Size(0.6f, 1.2f).Color(new Color(0.05f, 0.01f, 0.06f, 0.8f)).Gravity(-0.1f).Fade(0.1f, 0.3f).Grow(0.5f, 1.5f);
                b.Light("Light", Vector3.up, pal.Core, 5f, 3f, 1f);
            });

            VFXLibrary.Register("boss_aura", b =>
            {
                b.Lifetime = 60f;
                var pal = ElementPalette.Get(Combat.Element.Dark);
                b.Particles("Flames", b.Additive(ProceduralTextures.Flame), new Vector3(0f, 0.2f, 0f)).Duration(2f, true).Rate(45f)
                    .Shape(ParticleSystemShapeType.Circle, 0.9f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.6f, 1.1f).Speed(1.5f, 3f).Size(0.5f, 1.1f).Color(pal.Core, pal.Accent).Fade(0.1f, 0.4f).Shrink(1f, 0.2f).Noise(0.5f, 1.2f);
                b.Particles("Embers", b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 1f, 0f)).Duration(2f, true).Rate(20f)
                    .Shape(ParticleSystemShapeType.Sphere, 1.2f).Life(1f, 2f).Speed(0.2f, 0.8f).Size(0.05f, 0.1f).Color(pal.Bright).Gravity(-0.3f).Fade(0.1f, 0.5f);
            });

            VFXLibrary.Register("footstep_splash", b =>
            {
                b.Lifetime = 0.8f;
                b.Particles("Drops", b.Additive(ProceduralTextures.Droplet)).Burst(8).Shape(ParticleSystemShapeType.Cone, 0.1f, 30f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.3f, 0.5f).Speed(1f, 2.5f).Size(0.04f, 0.08f).Color(new Color(0.6f, 0.8f, 1f, 0.8f)).Gravity(1.5f).Fade(0.01f, 0.6f);
            });

            RegisterAuras();
        }

        /// <summary>Looping sword auras (one per element) and small elemental hit sparks.</summary>
        private static void RegisterAuras()
        {
            foreach (var id in new[] { "aura_water", "aura_fire", "aura_thunder", "aura_wind", "aura_moon" })
            {
                string captured = id;
                VFXLibrary.Register(captured, b =>
                {
                    b.Lifetime = 3600f;
                    var tex = captured == "aura_fire" ? ProceduralTextures.Flame : captured == "aura_thunder" ? ProceduralTextures.Streak :
                        captured == "aura_wind" ? ProceduralTextures.Leaf : captured == "aura_moon" ? ProceduralTextures.Star : ProceduralTextures.Droplet;
                    var p = b.Particles("Aura", b.Additive(tex), new Vector3(0f, 0f, 0.55f)).Duration(1f, true).Rate(16f)
                        .Shape(ParticleSystemShapeType.Box, 0.05f, 0f, 360f, new Vector3(0.04f, 0.04f, 0.9f))
                        .Life(0.3f, 0.6f).Speed(0.05f, 0.25f).Size(0.03f, 0.07f).Color(b.Pal.Core * 0.8f).Fade(0.1f, 0.4f).Gravity(captured == "aura_fire" ? -0.3f : 0f);
                    if (captured == "aura_thunder") p.Stretch(0.8f, 0.05f).Noise(1.2f, 3f);
                });
            }

            foreach (var id in new[] { "spark_water", "spark_fire", "spark_thunder", "spark_wind", "spark_moon" })
            {
                string captured = id;
                VFXLibrary.Register(captured, b =>
                {
                    b.Lifetime = 0.9f;
                    var tex = captured == "spark_fire" ? ProceduralTextures.Flame : captured == "spark_water" ? ProceduralTextures.Droplet :
                        captured == "spark_wind" ? ProceduralTextures.Leaf : captured == "spark_moon" ? ProceduralTextures.Star : ProceduralTextures.Streak;
                    var p = b.Particles("Burst", b.Additive(tex)).Burst(12).Shape(ParticleSystemShapeType.Sphere, 0.15f)
                        .Life(0.25f, 0.55f).Speed(2f, 6f).Size(0.06f, 0.14f).Color(b.Pal.Core, b.Pal.Bright).Gravity(captured == "spark_water" ? 1.2f : 0f)
                        .Rotation(0f, 360f).Fade(0.01f, 0.4f);
                    if (captured == "spark_thunder") p.Stretch(1.5f, 0.04f);
                });
            }
        }
    }
}
