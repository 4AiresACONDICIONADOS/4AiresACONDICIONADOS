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
        private static Transform SerpentHead(VfxBuild b, Transform parent, float size)
        {
            var head = new GameObject("SerpentHead").transform;
            head.SetParent(parent, false);
            head.gameObject.layer = Core.Layers.VFX;
            var bulb = ProceduralMeshes.CreatePart("Bulb", ProceduralMeshes.Primitive(PrimitiveType.Sphere), Foam(b), head,
                new Vector3(0f, 0f, 0.1f * size), Quaternion.identity, new Vector3(0.9f, 0.7f, 1.5f) * size);
            bulb.layer = Core.Layers.VFX;
            bulb.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int s = -1; s <= 1; s += 2)
            {
                var fin = ProceduralMeshes.CreatePart("Fin", ProceduralMeshes.Crescent(80f, 0.35f), MaterialFactory.Vfx(ProceduralTextures.CrescentBand, VfxBlend.Additive, 0f), head,
                    new Vector3(0.25f * s * size, 0.2f * size, -0.25f * size), Quaternion.Euler(-20f, 180f + 30f * s, 70f * s), Vector3.one * 0.9f * size);
                fin.layer = Core.Layers.VFX;
                var r = fin.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ShaderIds.TintColor, b.Pal.Bright);
                r.SetPropertyBlock(mpb);
            }
            var eyes = ParticleBuilder.Create("Eyes", head, MaterialFactory.Vfx(ProceduralTextures.Star, VfxBlend.Additive, 0f), new Vector3(0f, 0.18f * size, 0.45f * size));
            eyes.Duration(0.5f, true).Rate(8f).Shape(ParticleSystemShapeType.Box, 0f, 0f, 360f, new Vector3(0.4f * size, 0f, 0f))
                .Life(0.15f, 0.25f).Speed(0f, 0f).Size(0.25f * size, 0.35f * size).Color(Color.white * 4f).Local().PlayOnAwake().Fade(0.05f, 0.3f);
            return head;
        }

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
                b.Lifetime = 2.2f;
                Vector3 p0 = new Vector3(-0.9f, 0.2f, 0.5f), p1 = new Vector3(-0.5f, 0.3f, 2.4f), p2 = new Vector3(0.7f, 2.9f, 2.8f), p3 = new Vector3(1.4f, 4.8f, 1.5f);
                var mainPath = PathUtil.CoiledBezier(p0, p1, p2, p3, 1.6f, 0.4f, 0.7f);
                var profile = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.5f, 0.9f), new Keyframe(0.85f, 1.25f), new Keyframe(0.95f, 1.1f), new Keyframe(1f, 0.6f));

                // Grow 0.24 s, hold 0.55 s, dissolve 0.55 s: the serpent must be readable, not a flash.
                var body = b.Tube("SerpentBody", Body(b), mainPath, 0.52f, 0.24f, 0.55f, 0.55f);
                body.RadiusProfile = profile;
                body.Wobble = 0.2f;
                body.WobbleFrequency = 2.2f;
                body.WobbleSpeed = 11f;
                body.Segments = 64;
                body.RadialSegments = 12;
                body.Head = SerpentHead(b, body.transform, 0.75f);

                var core = b.Tube("FoamCore", Foam(b), PathUtil.CoiledBezier(p0, p1, p2, p3, 1.6f, 0.4f, 0.7f, 0.25f), 0.2f, 0.22f, 0.45f, 0.45f);
                core.RadiusProfile = profile;
                core.Wobble = 0.2f;
                core.WobbleFrequency = 2.2f;
                core.WobbleSpeed = 11f;

                // Secondary current braided around the serpent.
                var braid = b.Tube("Braid", Foam(b), PathUtil.CoiledBezier(p0, p1, p2, p3, 3.2f, 0.85f, 0.5f, Mathf.PI), 0.08f, 0.3f, 0.25f, 0.4f);
                braid.Opacity = 0.8f;

                // Spray bursts follow the head along the path.
                for (int i = 1; i <= 6; i++)
                {
                    float u = i / 6f;
                    Vector3 pos = mainPath(u);
                    Droplets(b, "Spray" + i, 12, pos, 2f, 5f, 1.4f, u * 0.24f, 60f);
                }
                Foamy(b, "BaseFoam", 10, new Vector3(-0.6f, 0.3f, 0.8f), 0.6f);
                b.Particles("Crown", b.Additive(ProceduralTextures.Streak), new Vector3(-0.8f, 0.1f, 0.6f)).Burst(22)
                    .Shape(ParticleSystemShapeType.Circle, 0.6f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.35f, 0.6f).Speed(4f, 8f).Size(0.08f, 0.14f).Color(b.Pal.Bright * 0.8f).Gravity(1.6f).Stretch(1.4f, 0.05f).Fade(0.01f, 0.5f)
                    .Velocity(Vector3.zero, 0f, 1.5f);
                var ripple = CommonVFX.FlatRing(b, "Ripple", b.Pal.Core, 4f, 0.6f, 0.05f, 0f, 0.88f);
                ripple.transform.localPosition = new Vector3(-0.6f, 0.05f, 0.8f);
                b.Light("Light", new Vector3(0.2f, 2.2f, 2.2f), b.Pal.Core, 9f, 7f, 0.9f);
                CommonVFX.Glow(b, "HeadFlash", b.Pal.Bright * 0.6f, 3.5f, 0.35f, new Vector3(1.2f, 4.4f, 1.8f), 0.22f);
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
                b.Lifetime = 1.4f;
                var wave = b.Tube("Wave", Body(b), PathUtil.Circle(2.3f, 0.7f, -30f, 380f, 0f, 0.25f, 3f), 0.34f, 0.24f, 0.2f, 0.45f);
                wave.Wobble = 0.12f;
                wave.Segments = 72;
                var crest = b.Tube("Crest", Foam(b), PathUtil.Circle(2.35f, 1.0f, -30f, 380f, 0f, 0.25f, 3f), 0.12f, 0.22f, 0.15f, 0.35f);
                crest.Segments = 72;
                b.Particles("Spray", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 0.7f, 0f)).Burst(60)
                    .Shape(ParticleSystemShapeType.Circle, 2.2f, 0f, 360f, null, new Vector3(-90f, 0f, 0f))
                    .Life(0.4f, 0.8f).Speed(1f, 3f).Size(0.06f, 0.14f).Color(b.Pal.Bright * 0.7f).Gravity(1.3f).Velocity(new Vector3(0f, 0f, 0f), 0f, 4f).Fade(0.01f, 0.5f);
                CommonVFX.FlatRing(b, "Ring", b.Pal.Core, 5.5f, 0.45f, 0.06f);
                Foamy(b, "Foam", 14, new Vector3(0f, 0.3f, 0f), 2f);
            });

            // --------------------------------------------------------- Third Form: Flowing Current (per step)
            VFXLibrary.Register("water_flow_step", b =>
            {
                b.Lifetime = 1.2f;
                var arc = b.Tube("Arc", Body(b), u => new Vector3(Mathf.Sin(u * Mathf.PI) * 1.1f, 1.0f + Mathf.Sin(u * Mathf.PI * 2f) * 0.25f, -1.4f + u * 2.8f), 0.22f, 0.12f, 0.15f, 0.4f);
                arc.Wobble = 0.1f;
                var arc2 = b.Tube("Arc2", Foam(b), u => new Vector3(Mathf.Sin(u * Mathf.PI) * 1.2f, 1.1f + Mathf.Sin(u * Mathf.PI * 2f) * 0.25f, -1.4f + u * 2.8f), 0.07f, 0.12f, 0.1f, 0.3f);
                arc2.Opacity = 0.9f;
                Droplets(b, "Spray", 18, new Vector3(0.9f, 1f, 0f), 2f, 5f, 1.2f, 0.08f, 60f);
            });

            // --------------------------------------------------------- Advanced: Whirlpool Fang (drill)
            VFXLibrary.Register("water_whirlpool", b =>
            {
                b.Lifetime = 1.8f;
                for (int i = 0; i < 3; i++)
                {
                    var t = b.Tube("Spiral" + i, i == 0 ? Foam(b) : Body(b), PathUtil.ForwardSpiral(6f, 1.3f, 2.5f, i * Mathf.PI * 2f / 3f), i == 0 ? 0.14f : 0.28f, 0.3f, 0.5f, 0.5f);
                    t.Wobble = 0.1f;
                    t.WobbleSpeed = 14f;
                    t.Segments = 64;
                }
                b.Particles("Vortex", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 1.1f, 3f)).Duration(0.8f).Rate(120f)
                    .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, new Vector3(2f, 2f, 5f)).Life(0.4f, 0.8f).Speed(0.1f, 0.4f).Size(0.06f, 0.14f)
                    .Color(b.Pal.Bright * 0.7f).Velocity(new Vector3(0f, 0f, 3f), 0f, -2f).Fade(0.05f, 0.5f);
                Foamy(b, "Foam", 16, new Vector3(0f, 1.1f, 3f), 1.4f, 0.2f);
                b.Light("Light", new Vector3(0f, 1.2f, 3f), b.Pal.Core, 8f, 5f, 1f);
            });

            // --------------------------------------------------------- Ultimate: Leviathan's Requiem
            VFXLibrary.Register("water_leviathan", b =>
            {
                b.Lifetime = 3.6f;
                System.Func<float, Vector3> path = u =>
                {
                    if (u < 0.65f)
                    {
                        float k = u / 0.65f;
                        float a = (k * 1.35f * 360f + 200f) * Mathf.Deg2Rad;
                        float r = Mathf.Lerp(7.5f, 5.5f, k);
                        return new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(0.5f, 10f, k), Mathf.Cos(a) * r);
                    }
                    float d = (u - 0.65f) / 0.35f;
                    float endA = (1.35f * 360f + 200f) * Mathf.Deg2Rad;
                    Vector3 top = new Vector3(Mathf.Sin(endA) * 5.5f, 10f, Mathf.Cos(endA) * 5.5f);
                    Vector3 apex = top + Vector3.up * 2f;
                    return PathUtil.Bezier(top, apex, new Vector3(0f, 6f, 0f), new Vector3(0f, -0.5f, 0f), d);
                };
                var profile = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(0.9f, 1.2f), new Keyframe(1f, 0.8f));
                var dragon = b.Tube("Leviathan", Body(b), path, 1.25f, 1.45f, 0.55f, 0.9f);
                dragon.RadiusProfile = profile;
                dragon.Wobble = 0.5f;
                dragon.WobbleFrequency = 3f;
                dragon.WobbleSpeed = 9f;
                dragon.Segments = 96;
                dragon.RadialSegments = 14;
                dragon.Head = SerpentHead(b, dragon.transform, 2.1f);
                var foam = b.Tube("Foam", Foam(b), path, 0.5f, 1.4f, 0.45f, 0.8f);
                foam.RadiusProfile = profile;
                foam.Wobble = 0.5f;
                foam.WobbleFrequency = 3f;
                foam.WobbleSpeed = 9f;
                foam.Segments = 96;
                b.Particles("Rain", b.Additive(ProceduralTextures.Droplet), new Vector3(0f, 12f, 0f)).Duration(3f).Rate(140f)
                    .Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(16f, 0.5f, 16f)).Life(0.8f, 1.2f).Speed(0f, 0f)
                    .Size(0.05f, 0.1f).Color(b.Pal.Bright * 0.5f).Gravity(1.6f).Stretch(2f, 0.06f).Fade(0.05f, 0.7f);
                b.Light("Light", new Vector3(0f, 6f, 0f), b.Pal.Core, 25f, 6f, 2.6f);
            });
        }
    }
}
