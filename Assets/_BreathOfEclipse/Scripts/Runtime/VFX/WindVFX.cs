using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// GALE BREATH effects. Identity: pale translucent air blades, spinning tornado shells, swirling leaves,
    /// dust lifted from the ground and atmospheric distortion.
    /// </summary>
    public static class WindVFX
    {
        private static Material Air(VfxBuild b) =>
            MaterialFactory.Ribbon("wind_air", b.Pal.Core * 0.7f, b.Pal.Edge, VfxBlend.Additive, ProceduralTextures.Noise, 6f, 2.5f);

        private static void Leaves(VfxBuild b, string name, int count, Vector3 pos, float radius, float orbit, float life, float lift = 1f, float delay = 0f)
        {
            b.Particles(name, b.Alpha(ProceduralTextures.Leaf), pos).Burst(count).Delay(delay)
                .Shape(ParticleSystemShapeType.Sphere, radius).Life(life * 0.6f, life).Speed(0.5f, 2f).Size(0.1f, 0.2f)
                .Rotation(0f, 360f).Spin(-400f, 400f).Color(new Color(0.45f, 0.75f, 0.35f, 1f), new Color(0.85f, 0.7f, 0.3f, 1f))
                .Velocity(new Vector3(0f, lift, 0f), orbit, 0f).Noise(0.6f, 1f).Fade(0.05f, 0.7f);
        }

        private static void Streaks(VfxBuild b, string name, int count, Vector3 pos, Vector3 box, float speed, float life, float orbit = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Streak), pos).Burst(count)
                .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, box).Life(life * 0.5f, life).Speed(0f, 0.1f).Size(0.05f, 0.09f)
                .Color(b.Pal.Bright * 0.5f).Velocity(new Vector3(0f, 0f, speed), orbit, 0f).Stretch(4f, 0.08f).Fade(0.05f, 0.4f);
        }

        // =================================================================== IV / VI / VII
        // Gale Blades: volleys of air blades (projectile visual is wind_crescent).
        // Whirlwind Dive: an updraft, a coiled hang in the air, then a drilling dive.
        // Eye of the Storm: a wall of wind closes around the swordsman, a calm eye, then it bursts outward.

        private static void RegisterForms()
        {
            // Air gathering into a coil around the body while hanging in the air (follows the player).
            VFXLibrary.Register("wind_dive_charge", b =>
            {
                b.Lifetime = 0.8f;
                var coil = b.Tube("Coil", Air(b), PathUtil.Helix(2f, 1.3f, 0.5f, 2f, 0f), 0.06f, 0.2f, 0.15f, 0.3f);
                coil.Wobble = 0.05f;
                Streaks(b, "Inflow", VFXQuality.Secondary ? 30 : 14, new Vector3(0f, 1f, 0f), new Vector3(2.2f, 2f, 2.2f), 0f, 0.3f, 6f);
                Leaves(b, "Leaves", 12, new Vector3(0f, 1f, 0f), 1.4f, 5f, 0.7f, 0.5f);
            });

            // Downward drill of wind wrapped around the body during the dive (follows the player).
            VFXLibrary.Register("wind_dive_drill", b =>
            {
                b.Lifetime = 1.2f;
                for (int i = 0; i < 2; i++)
                {
                    var shell = b.Shape("Drill" + i, ProceduralMeshes.OpenCylinder(0.15f + i * 0.1f, 1.1f + i * 0.35f, 24, 8, 3f + i),
                        MaterialFactory.Ribbon("wind_shell", b.Pal.Core * 0.35f, b.Pal.Edge * 0.4f, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 3f),
                        new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), new Vector3(1f, 2.6f - i * 0.4f, 1f), 0.45f, Color.white);
                    shell.ColorProperty = "_ColorA";
                    shell.SpinDegreesPerSecond = new Vector3(0f, -900f + i * 200f, 0f);
                    shell.ScaleCurve = CommonVFX.FastOut;
                    shell.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
                }
                Streaks(b, "Rush", VFXQuality.Secondary ? 40 : 18, new Vector3(0f, 1.2f, 0f), new Vector3(1.2f, 1.6f, 1.2f), 0f, 0.3f, -8f);
                // Landing blast (0.18 s ≈ contact): a flat ring of wind and flying leaves.
                var ring = CommonVFX.FlatRing(b, "Landing", b.Pal.Bright * 0.7f, 6f, 0.45f, 0.05f, 0.18f);
                ring.AlphaCurve = CommonVFX.PopFade;
                Leaves(b, "Leaves", VFXQuality.Secondary ? 36 : 16, new Vector3(0f, 0.4f, 0f), 1.5f, 3f, 1.2f, 2.2f, 0.18f);
                CommonVFX.Dust(b, "Dust", 14, 1.8f, 1.2f, new Color(0.55f, 0.55f, 0.5f, 0.4f), 4f);
            });

            // Wall of wind closing around the swordsman (follows the player).
            VFXLibrary.Register("wind_storm_eye", b =>
            {
                b.Lifetime = 1.4f;
                var wall = b.Shape("Wall", ProceduralMeshes.OpenCylinder(5.5f, 6f, 48, 8, 1.5f),
                    MaterialFactory.Ribbon("wind_shell", b.Pal.Core * 0.35f, b.Pal.Edge * 0.4f, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 3f),
                    Vector3.zero, Vector3.zero, new Vector3(1.3f, 0.2f, 1.3f), new Vector3(0.75f, 3.2f, 0.75f), 0.95f, Color.white);
                wall.ColorProperty = "_ColorA";
                wall.SpinDegreesPerSecond = new Vector3(0f, 420f, 0f);
                wall.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
                var inner = b.Tube("Inflow", Air(b), PathUtil.Circle(4.2f, 1.2f, 0f, 540f, 0.6f, 0.2f, 4f), 0.08f, 0.5f, 0.2f, 0.3f);
                inner.Wobble = 0.1f;
                Streaks(b, "Pull", VFXQuality.Secondary ? 50 : 22, new Vector3(0f, 1f, 0f), new Vector3(8f, 2f, 8f), 0f, 0.5f, 5f);
                Leaves(b, "Leaves", VFXQuality.Secondary ? 50 : 20, new Vector3(0f, 1.2f, 0f), 5f, 6f, 1.3f, 0.8f);
                CommonVFX.Dust(b, "Dust", 16, 5f, 1.2f, new Color(0.55f, 0.55f, 0.5f, 0.35f), 2f);
            });

            // The eye bursts: blades of wind fly out in every direction.
            VFXLibrary.Register("wind_storm_burst", b =>
            {
                b.Lifetime = 1.2f;
                int blades = VFXQuality.Full ? 14 : VFXQuality.Secondary ? 10 : 6;
                for (int i = 0; i < blades; i++)
                {
                    float a = i * 360f / blades;
                    var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    var blade = b.Shape("Blade" + i, ProceduralMeshes.Crescent(140f, 0.16f), b.Additive(ProceduralTextures.CrescentBand), dir * 1.2f + Vector3.up * 1.1f,
                        new Vector3(0f, a, 0f), new Vector3(1.2f, 1f, 1.2f), new Vector3(2.4f, 1f, 2.4f), 0.5f, i % 2 == 0 ? b.Pal.Bright * 0.8f : b.Pal.Core);
                    blade.ScaleCurve = CommonVFX.FastOut;
                    blade.AlphaCurve = CommonVFX.PopFade;
                    blade.Delay = (i % 3) * 0.02f;
                }
                var shock = CommonVFX.FlatRing(b, "Shock", b.Pal.Bright * 0.8f, 9f, 0.45f, 0.6f);
                shock.AlphaCurve = CommonVFX.PopFade;
                Streaks(b, "Out", VFXQuality.Secondary ? 60 : 24, new Vector3(0f, 1f, 0f), new Vector3(1f, 1.5f, 1f), 0f, 0.35f, 0f);
                b.Particles("Radial", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0f)).Burst(VFXQuality.Secondary ? 50 : 20)
                    .Shape(ParticleSystemShapeType.Circle, 0.8f, 0f, 360f, null, new Vector3(-90f, 0f, 0f)).Life(0.25f, 0.45f).Speed(14f, 22f)
                    .Size(0.05f, 0.09f).Color(b.Pal.Bright * 0.6f).Stretch(4f, 0.05f).Fade(0.02f, 0.4f);
                Leaves(b, "Leaves", 30, new Vector3(0f, 1f, 0f), 1.5f, 0f, 1.2f, 1.2f);
                CommonVFX.Distortion(b, "Distort", 7f, 0.5f);
            });
        }

        public static void Register()
        {
            RegisterForms();
            // Flying air crescent (projectile visual, travels along +Z).
            VFXLibrary.Register("wind_crescent", b =>
            {
                b.Lifetime = 1.5f;
                var blade = b.Shape("Blade", ProceduralMeshes.Crescent(150f, 0.22f), b.Additive(ProceduralTextures.CrescentBand), new Vector3(0f, 1.1f, 0f), new Vector3(0f, 0f, 0f),
                    new Vector3(1.6f, 1f, 1.6f), new Vector3(2.4f, 1f, 2.4f), 1.4f, b.Pal.Core);
                blade.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.8f, 0.8f), new Keyframe(1f, 0f));
                var inner = b.Shape("Inner", ProceduralMeshes.Crescent(130f, 0.1f), b.Additive(ProceduralTextures.CrescentBand), new Vector3(0f, 1.1f, -0.1f), Vector3.zero,
                    new Vector3(1.3f, 1f, 1.3f), new Vector3(2f, 1f, 2f), 1.4f, b.Pal.Bright);
                inner.AlphaCurve = blade.AlphaCurve;
                b.Particles("Trail", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1.1f, 0f)).Duration(1.4f).Rate(60f)
                    .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, new Vector3(2.4f, 0.2f, 0.3f)).Life(0.2f, 0.35f).Speed(0f, 0.2f)
                    .Size(0.04f, 0.07f).Color(b.Pal.Bright * 0.5f).Stretch(3f, 0.2f).Fade(0.05f, 0.3f);
                Leaves(b, "Leaves", 10, new Vector3(0f, 1.1f, 0f), 0.8f, 0f, 0.8f, 0.2f);
            });

            VFXLibrary.Register("wind_slash", b =>
            {
                b.Lifetime = 0.7f;
                var blade = b.Shape("Cut", ProceduralMeshes.Crescent(160f, 0.18f), b.Additive(ProceduralTextures.CrescentBand), new Vector3(0f, 1.1f, 0.6f), Vector3.zero,
                    new Vector3(1.2f, 1f, 1.2f), new Vector3(3f, 1f, 3f), 0.3f, b.Pal.Bright * 0.7f);
                blade.ScaleCurve = CommonVFX.FastOut;
                blade.AlphaCurve = CommonVFX.PopFade;
                Streaks(b, "Gust", 20, new Vector3(0f, 1.1f, 1f), new Vector3(2f, 1f, 1f), 12f, 0.3f);
            });

            // Tornado shell around the player (Cyclone Dance).
            VFXLibrary.Register("wind_cyclone", b =>
            {
                b.Lifetime = 1.8f;
                for (int i = 0; i < 3; i++)
                {
                    var shell = b.Shape("Shell" + i, ProceduralMeshes.OpenCylinder(1.4f + i * 0.4f, 2f + i * 0.5f, 32, 8, 3f), MaterialFactory.Ribbon("wind_shell", b.Pal.Core * 0.35f, b.Pal.Edge * 0.4f, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 3f),
                        Vector3.zero, Vector3.zero, new Vector3(0.5f, 0.3f, 0.5f), new Vector3(1.4f, 2.6f + i * 0.4f, 1.4f), 1.4f, Color.white);
                    shell.ColorProperty = "_ColorA";
                    shell.SpinDegreesPerSecond = new Vector3(0f, 720f - i * 180f, 0f);
                    shell.ScaleCurve = CommonVFX.FastOut;
                    shell.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.75f, 0.8f), new Keyframe(1f, 0f));
                    shell.Delay = i * 0.05f;
                }
                var spiral = b.Tube("Spiral", Air(b), PathUtil.Helix(3f, 1.2f, 2.2f, 2.5f, 0f), 0.1f, 0.3f, 0.6f, 0.5f);
                spiral.Wobble = 0.1f;
                Leaves(b, "Leaves", 40, new Vector3(0f, 1f, 0f), 1.8f, 4f, 1.4f, 1.5f);
                CommonVFX.Dust(b, "Dust", 14, 1.4f, 1.2f, new Color(0.55f, 0.55f, 0.5f, 0.35f), 3f);
                CommonVFX.FlatRing(b, "Ring", b.Pal.Bright * 0.6f, 4.5f, 0.5f);
                CommonVFX.Distortion(b, "Distort", 5f, 1f);
            });

            VFXLibrary.Register("wind_step", b =>
            {
                b.Lifetime = 1f;
                Streaks(b, "Gust", 26, new Vector3(0f, 1f, 0f), new Vector3(1f, 1.6f, 1.2f), -14f, 0.35f);
                Leaves(b, "Leaves", 14, new Vector3(0f, 0.8f, 0f), 0.6f, 1f, 0.9f, 1f);
                var arc = b.Tube("Swirl", Air(b), PathUtil.Helix(1.8f, 0.6f, 0.9f, 1.2f, 0f), 0.06f, 0.15f, 0.15f, 0.35f);
                arc.Wobble = 0.05f;
                CommonVFX.Dust(b, "Dust", 6, 0.4f, 0.6f, new Color(0.55f, 0.55f, 0.5f, 0.3f), 2f);
            });

            // Tornado at a location that lifts enemies (Tempest Pillar).
            VFXLibrary.Register("wind_tornado", b =>
            {
                b.Lifetime = 2.8f;
                for (int i = 0; i < 4; i++)
                {
                    var shell = b.Shape("Funnel" + i, ProceduralMeshes.OpenCylinder(0.5f + i * 0.2f, 2.4f + i * 0.4f, 32, 10, 4f + i), MaterialFactory.Ribbon("wind_shell", b.Pal.Core * 0.35f, b.Pal.Edge * 0.4f, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 3f),
                        Vector3.zero, Vector3.zero, new Vector3(0.4f, 0.5f, 0.4f), new Vector3(1f, 6.5f - i * 0.6f, 1f), 2.5f, Color.white);
                    shell.ColorProperty = "_ColorA";
                    shell.SpinDegreesPerSecond = new Vector3(0f, 540f + i * 90f, 0f);
                    shell.ScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 1.05f));
                    shell.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.8f, 0.9f), new Keyframe(1f, 0f));
                }
                Leaves(b, "Leaves", 60, new Vector3(0f, 2f, 0f), 2f, 5f, 2.4f, 3f);
                b.Particles("Debris", b.Alpha(ProceduralTextures.SoftCircle), new Vector3(0f, 1f, 0f)).Burst(30).Shape(ParticleSystemShapeType.Circle, 1.5f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(1.5f, 2.4f).Speed(0.2f, 0.6f).Size(0.05f, 0.12f).Color(new Color(0.25f, 0.22f, 0.2f, 1f)).Velocity(new Vector3(0f, 2.2f, 0f), 4f, 0f).Fade(0.05f, 0.7f);
                CommonVFX.Dust(b, "Dust", 20, 1.8f, 1.6f, new Color(0.55f, 0.55f, 0.5f, 0.4f), 3f);
                CommonVFX.Distortion(b, "Distort", 5f, 2.2f);
            });

            // Ultimate: air blades raining over the area.
            VFXLibrary.Register("wind_blade_rain", b =>
            {
                b.Lifetime = 3.2f;
                b.Particles("Blades", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 14f, 0f)).Duration(2.2f).Rate(90f)
                    .Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(14f, 1f, 14f)).Life(0.45f, 0.6f).Speed(0f, 0f)
                    .Size(0.18f, 0.3f).Color(b.Pal.Bright * 0.8f).Velocity(new Vector3(0f, -26f, 0f), 0f, 0f, false).Stretch(3f, 0.06f).Fade(0.02f, 0.8f);
                b.Particles("Impacts", b.Alpha(ProceduralTextures.Smoke), new Vector3(0f, 0.2f, 0f)).Duration(2.2f).Rate(40f)
                    .Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(13f, 0.1f, 13f)).Life(0.4f, 0.7f).Speed(0.5f, 1.5f)
                    .Size(0.5f, 1f).Color(new Color(0.6f, 0.65f, 0.6f, 0.4f)).Fade(0.05f, 0.3f).Grow(0.5f, 1.4f);
                Leaves(b, "Leaves", 80, new Vector3(0f, 4f, 0f), 7f, 1.5f, 2.6f, 0.5f);
                var giant = b.Shape("GiantFunnel", ProceduralMeshes.OpenCylinder(1.2f, 7f, 40, 12, 6f), MaterialFactory.Ribbon("wind_shell", b.Pal.Core * 0.35f, b.Pal.Edge * 0.4f, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 3f),
                    Vector3.zero, Vector3.zero, new Vector3(0.5f, 1f, 0.5f), new Vector3(1.2f, 14f, 1.2f), 3f, Color.white);
                giant.ColorProperty = "_ColorA";
                giant.SpinDegreesPerSecond = new Vector3(0f, 360f, 0f);
            });
        }
    }
}
