using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// TIDAL BREATH effects. Identity: liquid tubes with foam cores, serpent/dragon silhouettes, droplets that
    /// hang in the air, splash crowns and ripples. Rising Serpent is the reference composition for all techniques.
    /// </summary>
    public static class WaterVFX
    {
        private static Material Body(VfxBuild b) =>
            MaterialFactory.Ribbon("water_body", b.Pal.Core, b.Pal.Edge, VfxBlend.AlphaBlend, ProceduralTextures.Caustics, 2.2f, 1.6f);

        private static Material Foam(VfxBuild b) =>
            MaterialFactory.Ribbon("water_foam", b.Pal.Bright, b.Pal.Core, VfxBlend.Additive, ProceduralTextures.Caustics, 3.2f, 1f);

        private static void Droplets(VfxBuild b, string name, int count, Vector3 pos, float speedMin, float speedMax, float gravity, float delay = 0f, float cone = 35f, Vector3 euler = default)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Droplet), pos, euler).Burst(count).Delay(delay)
                .Shape(ParticleSystemShapeType.Cone, 0.25f, cone, 360f, null, new Vector3(-90f, 0f, 0f))
                .Life(0.5f, 1.0f).Speed(speedMin, speedMax).Size(0.06f, 0.16f).Color(b.Pal.Bright * 0.7f, b.Pal.Core)
                .Gravity(gravity).Drag(0.05f).Fade(0.01f, 0.6f).Stretch(0.6f, 0.03f);
        }

        private static void Foamy(VfxBuild b, string name, int count, Vector3 pos, float radius, float delay = 0f)
        {
            b.Particles(name, b.Alpha(ProceduralTextures.Smoke), pos).Burst(count).Delay(delay)
                .Shape(ParticleSystemShapeType.Sphere, radius).Life(0.5f, 1.0f).Speed(0.4f, 1.5f).Size(0.4f, 0.9f)
                .Color(new Color(0.85f, 0.95f, 1f, 0.55f)).Gravity(-0.05f).Fade(0.03f, 0.3f).Grow(0.4f, 1.5f).Rotation(0f, 360f);
        }

        /// <summary>Serpent head: stretched liquid bulb, horn-fins and glinting eyes.</summary>
        public static void Register()
        {
            // --------------------------------------------------------- anticipation: water gathers around the blade
            VFXLibrary.Register("water_charge", b =>
            {
                b.Lifetime = 1.1f;
                var helix = b.Tube("Helix", Foam(b), u => new Vector3(Mathf.Cos(u * Mathf.PI * 7f) * 0.13f, Mathf.Sin(u * Mathf.PI * 7f) * 0.13f, 0.08f + u * 1.0f), 0.03f, 0.22f, 0.45f, 0.3f);
                helix.RadialSegments = 6;
                helix.Segments = 60;
                var sheath = b.Tube("Sheath", Body(b), u => new Vector3(0f, 0.02f, 0.1f + u * 0.98f), 0.06f, 0.18f, 0.5f, 0.3f);
                sheath.Wobble = 0.02f;
                sheath.WobbleFrequency = 5f;
                b.Particles("Gather", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 0f, 0.5f)).Duration(0.5f).Rate(70f)
                    .Shape(ParticleSystemShapeType.Sphere, 0.9f, 0f, 360f, null, null, null, 0.2f)
                    .Life(0.25f, 0.4f).Speed(-3.5f, -2f).Size(0.04f, 0.09f).Color(b.Pal.Bright * 0.8f).Local().Fade(0.1f, 0.6f);
                CommonVFX.Glow(b, "Glow", b.Pal.Core * 0.5f, 1.6f, 0.7f, new Vector3(0f, 0f, 0.55f));
                b.Light("Light", new Vector3(0f, 0f, 0.5f), b.Pal.Core, 4f, 3f, 0.8f);
            });

            VFXLibrary.Register("water_ground_ripple", b =>
            {
                b.Lifetime = 1.2f;
                var r1 = CommonVFX.FlatRing(b, "Ripple", b.Pal.Core * 0.7f, 3f, 0.8f, 0.04f, 0f, 0.9f);
                r1.ScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                CommonVFX.FlatRing(b, "Ripple2", b.Pal.Bright * 0.5f, 2.2f, 0.8f, 0.05f, 0.15f, 0.92f);
                b.Particles("Mist", b.Alpha(ProceduralTextures.Smoke), new Vector3(0f, 0.2f, 0f)).Burst(6).Shape(ParticleSystemShapeType.Circle, 0.8f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.8f, 1.1f).Speed(0.2f, 0.5f).Size(0.8f, 1.4f).Color(new Color(0.7f, 0.85f, 1f, 0.25f)).Fade(0.1f, 0.4f).Grow(0.6f, 1.3f);
            });

            // --------------------------------------------------------- dash: wake of spray behind the feet
            VFXLibrary.Register("water_dash_wake", b =>
            {
                b.Lifetime = 1f;
                b.Particles("Spray", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 0.3f, 0f)).Duration(0.2f).Rate(160f)
                    .Shape(ParticleSystemShapeType.Cone, 0.3f, 35f, 360f, null, new Vector3(-150f, 0f, 0f))
                    .Life(0.3f, 0.6f).Speed(2f, 5f).Size(0.05f, 0.12f).Color(b.Pal.Bright * 0.7f).Gravity(1.4f).Fade(0.01f, 0.5f).Stretch(0.5f, 0.04f);
                b.Particles("Foam", b.Alpha(ProceduralTextures.Smoke), new Vector3(0f, 0.2f, 0f)).Duration(0.2f).Rate(40f)
                    .Shape(ParticleSystemShapeType.Sphere, 0.3f).Life(0.4f, 0.7f).Speed(0.3f, 0.8f).Size(0.4f, 0.8f)
                    .Color(new Color(0.8f, 0.92f, 1f, 0.45f)).Fade(0.02f, 0.3f).Grow(0.5f, 1.4f);
                var wake = b.Tube("Wake", Body(b), u => new Vector3(Mathf.Sin(u * 9f) * 0.15f, 0.35f + Mathf.Sin(u * Mathf.PI) * 0.3f, -u * 2.8f), 0.16f, 0.12f, 0.1f, 0.35f);
                wake.Wobble = 0.08f;
            });

            // --------------------------------------------------------- RISING SERPENT: the reference technique
            VFXLibrary.Register("water_serpent", b =>
            {
                b.Lifetime = 2.3f;
                // Diagonal rising cut: a bright water arc sweeps from low-left to high-right first.
                var tilt = Quaternion.AngleAxis(35f, Vector3.forward);
                var arcPath = WaterSpline.SlashArc(1.6f, -75f, 85f, 1.05f, 35f);
                var arc = b.WaterRibbon("SlashArc", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Edge, false), arcPath, 0.75f, 0.09f, 0.16f, 0.3f);
                arc.PlaneNormal = tilt * Vector3.up;
                arc.Thickness = 0.3f;
                arc.Curl = 40f;
                AnimeFoam.Crest(b, "ArcFoam", arcPath, tilt * new Vector3(0f, 0.12f, 0f), 0.2f, 0.03f, 0.09f, 0.14f, 0.28f, tilt * Vector3.up);

                // The serpent: born from the arc, coils once and rises past the launched enemy.
                Vector3 p0 = new Vector3(-0.9f, 0.2f, 0.5f), p1 = new Vector3(-0.5f, 0.3f, 2.4f), p2 = new Vector3(0.7f, 2.9f, 2.8f), p3 = new Vector3(1.4f, 4.8f, 1.5f);
                var mainPath = WaterSpline.EvenSpeed(PathUtil.CoiledBezier(p0, p1, p2, p3, 1.6f, 0.4f, 0.7f));
                var serpent = b.WaterSerpent("Serpent", mainPath, 0.5f, 0.28f, 0.6f, 0.5f, 1.45f);
                serpent.StartDelay = 0.04f;
                serpent.Twist = 620f;
                serpent.Slither = 0.2f;

                if (VFXQuality.Secondary)
                {
                    // A foam current braided around the body.
                    var braid = b.WaterRibbon("Braid", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Foam, false),
                        WaterSpline.EvenSpeed(PathUtil.CoiledBezier(p0, p1, p2, p3, 3.2f, 0.85f, 0.5f, Mathf.PI)), 0.16f, 0.3f, 0.45f, 0.4f);
                    braid.StartDelay = 0.06f;
                    braid.Twist = 360f;
                    braid.Thickness = 0.1f;
                    // Spray follows the head along the path (few, directional).
                    for (int i = 1; i <= 4; i++)
                    {
                        float u = i / 4f;
                        Droplets(b, "Spray" + i, 10, mainPath(u), 2f, 5f, 1.4f, 0.04f + u * 0.26f, 55f);
                    }
                }
                AnimeFoam.Impact(b, "BaseFoam", new Vector3(-0.6f, 0.05f, 0.8f), 1.3f, 0.02f);
                var ripple = CommonVFX.FlatRing(b, "Ripple", b.Pal.Core, 4f, 0.6f, 0.05f, 0f, 0.88f);
                ripple.transform.localPosition = new Vector3(-0.6f, 0.05f, 0.8f);
                b.Light("Light", new Vector3(0.2f, 2.2f, 2.2f), b.Pal.Core, 9f, 7f, 1f);
                CommonVFX.Glow(b, "HeadFlash", b.Pal.Bright * 0.6f, 3.5f, 0.35f, new Vector3(1.2f, 4.4f, 1.8f), 0.3f);
            });

            VFXLibrary.Register("water_splash", b =>
            {
                b.Lifetime = 1.4f;
                Droplets(b, "Burst", 36, Vector3.zero, 3f, 8f, 1.6f, 0f, 70f);
                Foamy(b, "Foam", 12, Vector3.zero, 0.5f);
                CommonVFX.Glint(b, "Glint", b.Pal.Bright, 2.2f, 0.2f);
                CommonVFX.Glow(b, "Glow", b.Pal.Core, 2.6f, 0.3f);
                var ring = b.Shape("Ring", ProceduralMeshes.Ring(0.85f), b.Additive(ProceduralTextures.Band), Vector3.zero, new Vector3(90f, 0f, 0f),
                    Vector3.one * 0.3f, Vector3.one * 3f, 0.35f, b.Pal.Bright);
                ring.ScaleCurve = CommonVFX.FastOut;
                ring.AlphaCurve = CommonVFX.PopFade;
                CommonVFX.Sparks(b, "Streaks", 14, b.Pal.Bright, 6f, 12f, 0.35f, 0.06f, 1f);
            });

            // Droplets that hang in the air after the serpent passes (slow, sparkling).
            VFXLibrary.Register("water_suspended", b =>
            {
                b.Lifetime = 3f;
                b.Particles("Hanging", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 1.2f, 0f)).Burst(46)
                    .Shape(ParticleSystemShapeType.Sphere, 1.8f).Life(1.8f, 2.6f).Speed(0.02f, 0.12f).Size(0.05f, 0.14f)
                    .Color(b.Pal.Bright * 0.6f, b.Pal.Core).Gravity(0.02f).Noise(0.08f, 0.5f, 0.2f).Fade(0.05f, 0.7f);
                b.Particles("Glints", b.Additive(ProceduralTextures.Star), new Vector3(0f, 1.2f, 0f)).Duration(1.8f).Rate(10f)
                    .Shape(ParticleSystemShapeType.Sphere, 1.6f).Life(0.2f, 0.35f).Speed(0f, 0f).Size(0.2f, 0.35f)
                    .Color(Color.white * 3f).Fade(0.1f, 0.3f);
                b.Particles("Mist", b.Alpha(ProceduralTextures.Smoke), new Vector3(0f, 1f, 0f)).Burst(8).Shape(ParticleSystemShapeType.Sphere, 1.2f)
                    .Life(1.4f, 2f).Speed(0.05f, 0.2f).Size(1f, 1.8f).Color(new Color(0.6f, 0.8f, 1f, 0.18f)).Fade(0.2f, 0.5f);
            });

            // --------------------------------------------------------- Second Form: Crescent Tide (360 wave)
            VFXLibrary.Register("water_crescent_tide", b =>
            {
                b.Lifetime = 1.5f;
                // A great crescent of water sweeps around the swordsman and throws its crest outward.
                var sweep = WaterSpline.Ring(2.1f, 0.85f, 0.92f, 0.25f, -40f);
                var wave = b.WaterRibbon("Crescent", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, false), sweep, 1.15f, 0.22f, 0.22f, 0.4f);
                wave.Thickness = 0.45f;
                wave.Curl = 70f;
                wave.WaveHeight = 0.12f;
                wave.Segments = 64;
                AnimeFoam.Crest(b, "Crest", WaterSpline.Ring(2.55f, 1.25f, 0.92f, 0.25f, -40f), Vector3.zero, 0.28f, 0.05f, 0.22f, 0.18f, 0.35f, Vector3.up);
                if (VFXQuality.Secondary)
                {
                    b.Particles("Spray", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 0.7f, 0f)).Burst(VFXQuality.Count(36))
                        .Shape(ParticleSystemShapeType.Circle, 2.2f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                        .Life(0.4f, 0.8f).Speed(1f, 3f).Size(0.06f, 0.14f).Color(b.Pal.Bright * 0.7f).Gravity(1.3f).Velocity(new Vector3(0f, 0f, 0f), 0f, 4f).Fade(0.01f, 0.5f);
                }
                CommonVFX.FlatRing(b, "Ring", b.Pal.Core, 5.5f, 0.45f, 0.06f);
                AnimeFoam.Impact(b, "GroundFoam", Vector3.zero, 2.2f, 0.12f);
            });

            // --------------------------------------------------------- Third Form: Flowing Current (per step)
            VFXLibrary.Register("water_flow_step", b =>
            {
                b.Lifetime = 1.1f;
                // Wandering current: each step leaves a snaking flow; three steps draw the zigzag.
                var flowPath = WaterSpline.Wave(2.8f, 0.45f, 1.1f, 1.0f, 0.4f);
                var flow = b.WaterRibbon("Flow", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, false), u => flowPath(u) + new Vector3(0f, 0f, -1.4f), 0.55f, 0.12f, 0.14f, 0.35f);
                flow.Twist = 120f;
                flow.Thickness = 0.35f;
                flow.WaveHeight = 0.08f;
                var edge = b.WaterRibbon("Edge", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Edge, false), u => flowPath(u) + new Vector3(0f, 0.18f, -1.4f), 0.14f, 0.12f, 0.1f, 0.28f);
                edge.Twist = 120f;
                edge.StartDelay = 0.02f;
                if (VFXQuality.Secondary) Droplets(b, "Spray", 12, new Vector3(0.4f, 1f, 1.2f), 2f, 5f, 1.2f, 0.08f, 60f);
            });

            // --------------------------------------------------------- Advanced: Whirlpool Fang (drill)
            VFXLibrary.Register("water_whirlpool", b =>
            {
                b.Lifetime = 1.8f;
                // Abyss Fang: three twisting spiral sheets form a drill that bores forward.
                for (int i = 0; i < 3; i++)
                {
                    var look = i == 0 ? MaterialFactory.WaterLook.Foam : MaterialFactory.WaterLook.Body;
                    var spiral = b.WaterRibbon("Spiral" + i, MaterialFactory.TidalWater(look, false),
                        WaterSpline.Spiral(6f, 1.3f, 2.5f, 1.1f, i * Mathf.PI * 2f / 3f), i == 0 ? 0.3f : 0.7f, 0.3f, 0.5f, 0.5f);
                    spiral.Twist = 540f;
                    spiral.Thickness = 0.3f;
                    spiral.PlaneNormal = Vector3.forward;
                    spiral.Segments = 64;
                }
                var core = b.WaterRibbon("Core", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Edge, false),
                    WaterSpline.Spiral(6.4f, 0.35f, 4f, 1.1f), 0.22f, 0.26f, 0.5f, 0.4f);
                core.PlaneNormal = Vector3.forward;
                if (VFXQuality.Secondary)
                {
                    b.Particles("Vortex", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 1.1f, 3f)).Duration(0.8f).Rate(VFXQuality.Count(70))
                        .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, new Vector3(2f, 2f, 5f)).Life(0.4f, 0.8f).Speed(0.1f, 0.4f).Size(0.06f, 0.14f)
                        .Color(b.Pal.Bright * 0.7f).Velocity(new Vector3(0f, 0f, 3f), 0f, -2f).Fade(0.05f, 0.5f);
                }
                AnimeFoam.Flecks(b, "TipFoam", new Vector3(0f, 1.1f, 5.6f), 16, 0.6f, 7f, 0.35f);
                b.Light("Light", new Vector3(0f, 1.2f, 3f), b.Pal.Core, 8f, 5f, 1f);
            });

            // --------------------------------------------------------- Ultimate: Leviathan's Requiem
            VFXLibrary.Register("water_leviathan", b =>
            {
                b.Lifetime = 3.6f;
                // 1) The battlefield becomes a whirlpool: two rising ring currents around the target.
                for (int i = 0; i < 2; i++)
                {
                    var ring = b.WaterRibbon("Current" + i, MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, false),
                        WaterSpline.Ring(6.5f - i * 1.6f, 0.4f + i * 0.6f, 1.6f, 2.2f, i * 150f), 1.6f - i * 0.4f, 0.9f, 1.0f, 0.8f);
                    ring.Thickness = 0.5f;
                    ring.Curl = 80f;
                    ring.WaveHeight = 0.3f;
                    ring.Segments = 80;
                    ring.StartDelay = i * 0.15f;
                }
                // 2) Water columns erupt around the circle.
                int columns = VFXQuality.Secondary ? 4 : 2;
                for (int i = 0; i < columns; i++)
                {
                    float a = (i / (float)columns * 360f + 30f) * Mathf.Deg2Rad;
                    Vector3 basePos = new Vector3(Mathf.Sin(a) * 5f, 0f, Mathf.Cos(a) * 5f);
                    var column = b.WaterRibbon("Column" + i, MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, false),
                        WaterSpline.CatmullRom(basePos, basePos + new Vector3(0f, 3f, 0f), basePos + new Vector3(0f, 6.5f, 0f), basePos * 0.8f + new Vector3(0f, 8f, 0f)),
                        1.3f, 0.35f, 0.9f, 0.6f);
                    column.PlaneNormal = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                    column.Twist = 360f;
                    column.Thickness = 0.6f;
                    column.StartDelay = 0.3f + i * 0.12f;
                }
                // 3) The leviathan coils up the whirlpool and dives onto the target.
                System.Func<float, Vector3> path = u =>
                {
                    if (u < 0.65f)
                    {
                        float k = u / 0.65f;
                        float ang = (k * 1.35f * 360f + 200f) * Mathf.Deg2Rad;
                        float r = Mathf.Lerp(7.5f, 5.5f, k);
                        return new Vector3(Mathf.Sin(ang) * r, Mathf.Lerp(0.5f, 10f, k), Mathf.Cos(ang) * r);
                    }
                    float d = (u - 0.65f) / 0.35f;
                    float endA = (1.35f * 360f + 200f) * Mathf.Deg2Rad;
                    Vector3 top = new Vector3(Mathf.Sin(endA) * 5.5f, 10f, Mathf.Cos(endA) * 5.5f);
                    Vector3 apex = top + Vector3.up * 2f;
                    return PathUtil.Bezier(top, apex, new Vector3(0f, 6f, 0f), new Vector3(0f, -0.5f, 0f), d);
                };
                var dragon = b.WaterSerpent("Leviathan", WaterSpline.EvenSpeed(path, 96), 1.25f, 1.85f, 0.5f, 0.8f, 1.5f);
                dragon.Segments = VFXQuality.Secondary ? 96 : 48;
                dragon.RadialSegments = VFXQuality.Secondary ? 14 : 9;
                dragon.Slither = 0.5f;
                dragon.SlitherFrequency = 3f;
                dragon.SlitherSpeed = 9f;
                dragon.Twist = 900f;
                // 4) Massive impact where it lands.
                AnimeFoam.Impact(b, "ImpactFoam", Vector3.zero, 4.5f, 1.85f);
                b.Particles("Crown", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 0.2f, 0f)).Burst(VFXQuality.Count(40)).Delay(1.85f)
                    .Shape(ParticleSystemShapeType.Circle, 1.5f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.5f, 0.9f).Speed(8f, 14f).Size(0.12f, 0.24f).Color(b.Pal.Bright * 0.9f).Gravity(1.8f).Stretch(1.6f, 0.06f).Fade(0.01f, 0.5f)
                    .Velocity(Vector3.zero, 0f, 3f);
                if (VFXQuality.Secondary)
                {
                    b.Particles("Rain", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 12f, 0f)).Duration(3f).Rate(VFXQuality.Count(90))
                        .Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(16f, 0.5f, 16f)).Life(0.8f, 1.2f).Speed(0f, 0f)
                        .Size(0.05f, 0.1f).Color(b.Pal.Bright * 0.5f).Gravity(1.6f).Stretch(2f, 0.06f).Fade(0.05f, 0.7f);
                }
                b.Light("Light", new Vector3(0f, 6f, 0f), b.Pal.Core, 25f, 6f, 2.6f);
                b.Light("ImpactLight", new Vector3(0f, 1.5f, 0f), b.Pal.Bright, 14f, 9f, 0.8f, 1.85f);
            });

            // --------------------------------------------------------- Form I: Tide Cutter (thin, clean arc)
            VFXLibrary.Register("water_tide_cut", b =>
            {
                b.Lifetime = 0.9f;
                var path = WaterSpline.SlashArc(1.7f, -80f, 85f, 1.15f, -8f);
                var cut = b.WaterRibbon("Cut", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Edge, false), path, 0.34f, 0.07f, 0.1f, 0.25f);
                cut.Thickness = 0.15f;
                cut.WidthProfile = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.35f));
                AnimeFoam.Crest(b, "Foam", path, new Vector3(0f, 0.08f, 0f), 0.1f, 0.03f, 0.07f, 0.08f, 0.22f, Vector3.up);
                if (VFXQuality.Secondary) AnimeFoam.Flecks(b, "EndFlecks", new Vector3(1.5f, 1.15f, 0.3f), 8, 0.2f, 5f, 0.08f);
            });

            // --------------------------------------------------------- Form IV: Parting Cascade (vertical waterfall)
            VFXLibrary.Register("water_cascade", b =>
            {
                b.Lifetime = 1.4f;
                for (int s = -1; s <= 1; s += 2)
                {
                    var fall = b.WaterRibbon("Fall" + (s < 0 ? "L" : "R"), MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, false),
                        WaterSpline.Cascade(3.2f, 2.2f, 0.45f * s), 0.95f, 0.16f, 0.22f, 0.4f);
                    fall.PlaneNormal = Vector3.right;
                    fall.Thickness = 0.4f;
                    fall.Curl = 25f * s;
                    AnimeFoam.Crest(b, "FallFoam" + (s < 0 ? "L" : "R"), WaterSpline.Cascade(3.35f, 2.25f, 0.5f * s), Vector3.zero, 0.22f, 0.04f, 0.16f, 0.18f, 0.35f, Vector3.right);
                }
                var core = b.WaterRibbon("Blade", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Edge, false), WaterSpline.Cascade(3f, 2.1f), 0.3f, 0.14f, 0.12f, 0.3f);
                core.PlaneNormal = Vector3.right;
                AnimeFoam.Impact(b, "Impact", new Vector3(0f, 0.05f, 2.1f), 1.8f, 0.14f);
            });

            // --------------------------------------------------------- Form V: Ring of Tides (circular current)
            VFXLibrary.Register("water_ring_current", b =>
            {
                b.Lifetime = 1.3f;
                for (int i = 0; i < 3; i++)
                {
                    var look = i == 1 ? MaterialFactory.WaterLook.Foam : MaterialFactory.WaterLook.Body;
                    var ring = b.WaterRibbon("Current" + i, MaterialFactory.TidalWater(look, false),
                        WaterSpline.Ring(3.2f - i * 0.6f, 0.35f + i * 0.45f, 1.25f, 0.3f, i * 120f), i == 1 ? 0.3f : 0.8f, 0.3f, 0.35f, 0.4f);
                    ring.Thickness = 0.35f;
                    ring.Curl = -50f;
                    ring.WaveHeight = 0.1f;
                    ring.Segments = 64;
                    ring.StartDelay = i * 0.05f;
                }
                if (VFXQuality.Secondary)
                {
                    b.Particles("Pull", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 0.8f, 0f)).Duration(0.4f).Rate(VFXQuality.Count(50))
                        .Shape(ParticleSystemShapeType.Circle, 3.4f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                        .Life(0.4f, 0.6f).Speed(-4f, -2f).Size(0.06f, 0.12f).Color(b.Pal.Bright * 0.7f).Fade(0.05f, 0.5f);
                }
            });
        }
    }
}
