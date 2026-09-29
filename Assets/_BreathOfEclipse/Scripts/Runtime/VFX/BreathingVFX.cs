using BreathOfEclipse.Combat;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// BREATH → FORM. The visible inhale before a technique: thin streams of the element's air converging on the
    /// mouth plus element motes (Water droplets, Thunder micro sparks, Ember embers, Gale visible air, Moonlight
    /// violet-white motes) and a faint glow on the chest. When the breath is held, the energy concentrates on the
    /// blade (breath_focus) and the body (breath_focus_body). Kept small: no smoke clouds.
    /// inhale: authored at the mouth, +Z = facing, spawned following the head.
    /// breath_focus: authored along the blade, +Z = base → tip, spawned following the blade base.
    /// </summary>
    public static class BreathingVFX
    {
        private static Material Stream(VfxBuild b, float strength) =>
            MaterialFactory.Ribbon("breath_stream", b.Pal.Core * strength, b.Pal.Edge * strength, VfxBlend.Additive, ProceduralTextures.Noise, 5f, 2.2f);

        public static void Register()
        {
            VFXLibrary.Register("inhale", b =>
            {
                b.Lifetime = 1.2f;
                bool extra = VFXQuality.Secondary, full = VFXQuality.Full;
                bool fast = b.Element == Element.Thunder || b.Element == Element.Wind;

                // Streams: start around the upper body (front and sides, some from below), curl in to the mouth.
                int streams = full ? 5 : extra ? 4 : 2;
                var mat = Stream(b, b.Element == Element.Wind ? 0.45f : 0.6f);
                for (int i = 0; i < streams; i++)
                {
                    float side = streams == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(streams - 1));
                    float a = side * 100f * Mathf.Deg2Rad;
                    float r = Random.Range(0.8f, 1.1f);
                    Vector3 start = new Vector3(Mathf.Sin(a) * r, Random.Range(-0.75f, 0.2f), Mathf.Cos(a) * r * 0.9f + 0.1f);
                    Vector3 lateral = new Vector3(-Mathf.Cos(a), 0f, Mathf.Sin(a)) * (i % 2 == 0 ? 0.28f : -0.22f);
                    Vector3 c1 = start * 0.55f + lateral + Vector3.up * 0.12f;
                    Vector3 c2 = new Vector3(start.x * 0.12f, -0.02f, 0.26f) + lateral * 0.3f;
                    Vector3 mouth = new Vector3(0f, -0.01f, 0.05f);
                    var tube = b.Tube("Stream" + i, mat, u => PathUtil.Bezier(start, c1, c2, mouth, u), 0.011f, fast ? 0.16f : 0.26f, 0.04f, fast ? 0.1f : 0.16f);
                    tube.RadialSegments = 5;
                    tube.Segments = extra ? 24 : 14;
                    tube.RadiusProfile = new AnimationCurve(new Keyframe(0f, 1.4f), new Keyframe(1f, 0.35f));
                    tube.StartDelay = i * (fast ? 0.02f : 0.045f);
                    tube.Opacity = 0.75f;
                    tube.Wobble = b.Element == Element.Wind ? 0.05f : 0.02f;
                }

                // Element motes pulled toward the face.
                var center = new Vector3(0f, -0.25f, 0.25f);
                switch (b.Element)
                {
                    case Element.Water:
                        b.Particles("Droplets", b.Additive(ProceduralTextures.Droplet), center).Duration(0.3f).Rate(extra ? 45f : 20f)
                            .Shape(ParticleSystemShapeType.Sphere, 0.85f, 25f, 360f, null, null, null, 0.2f).Life(0.3f, 0.45f).Speed(0f, 0f)
                            .Velocity(Vector3.zero, 0f, -2.4f).Size(0.018f, 0.035f).Color(b.Pal.Accent * 0.8f, b.Pal.Bright * 0.6f).Fade(0.1f, 0.5f).Shrink(1f, 0.3f);
                        break;
                    case Element.Thunder:
                        b.Particles("Sparks", b.Additive(ProceduralTextures.Streak), center).Duration(0.18f).Rate(extra ? 110f : 50f)
                            .Shape(ParticleSystemShapeType.Sphere, 0.75f, 25f, 360f, null, null, null, 0.2f).Life(0.1f, 0.2f).Speed(0f, 0f)
                            .Velocity(Vector3.zero, 0f, -4.5f).Size(0.012f, 0.025f).Color(b.Pal.Bright, Color.white * 3f).Stretch(1.6f, 0.04f).Fade(0.01f, 0.4f);
                        for (int i = 0; i < (extra ? 2 : 1); i++)
                        {
                            float s = i == 0 ? 1f : -1f;
                            var arc = b.Bolt("Arc" + i, new Vector3(0.38f * s, -0.45f, 0.2f), new Vector3(0.03f * s, -0.04f, 0.08f), 0.012f, 0.12f, b.Pal.Bright, 0.04f + i * 0.06f, 0);
                            arc.Points = 8;
                            arc.Jaggedness = 0.7f;
                            arc.Flicker = 0.02f;
                        }
                        break;
                    case Element.Fire:
                        b.Particles("Embers", b.Additive(ProceduralTextures.SoftCircle), center + Vector3.down * 0.2f).Duration(0.3f).Rate(extra ? 40f : 18f)
                            .Shape(ParticleSystemShapeType.Sphere, 0.8f, 25f, 360f, null, null, null, 0.2f).Life(0.3f, 0.5f).Speed(0f, 0f)
                            .Velocity(new Vector3(0f, 0.6f, 0f), 0f, -2f).Size(0.02f, 0.04f).Color(b.Pal.Core, b.Pal.Accent).Noise(0.6f, 2f).Fade(0.05f, 0.5f);
                        break;
                    case Element.Wind:
                        b.Particles("Air", b.Alpha(ProceduralTextures.Smoke), center).Duration(0.22f).Rate(extra ? 26f : 12f)
                            .Shape(ParticleSystemShapeType.Sphere, 0.9f, 25f, 360f, null, null, null, 0.2f).Life(0.22f, 0.32f).Speed(0f, 0f)
                            .Velocity(Vector3.zero, 1.5f, -3f).Size(0.12f, 0.22f).Rotation(0f, 360f).Color(new Color(0.85f, 0.95f, 0.9f, 0.12f)).Fade(0.1f, 0.4f).Shrink(1f, 0.2f);
                        break;
                    case Element.Moon:
                        b.Particles("Motes", b.Additive(ProceduralTextures.Star), center).Duration(0.32f).Rate(extra ? 36f : 16f)
                            .Shape(ParticleSystemShapeType.Sphere, 0.85f, 25f, 360f, null, null, null, 0.2f).Life(0.32f, 0.5f).Speed(0f, 0f)
                            .Velocity(Vector3.zero, 0.8f, -2.1f).Size(0.02f, 0.04f).Rotation(0f, 45f).Color(b.Pal.Core * 0.8f, Color.white * 1.6f).Fade(0.1f, 0.5f);
                        break;
                    default:
                        b.Particles("Motes", b.Additive(ProceduralTextures.SoftCircle), center).Duration(0.3f).Rate(20f)
                            .Shape(ParticleSystemShapeType.Sphere, 0.8f).Life(0.3f, 0.45f).Speed(0f, 0f).Velocity(Vector3.zero, 0f, -2.2f)
                            .Size(0.02f, 0.03f).Color(b.Pal.Core * 0.7f).Fade(0.1f, 0.5f);
                        break;
                }

                // Faint energy on the chest while the lungs fill.
                CommonVFX.Glow(b, "Chest", b.Pal.Core * 0.22f, 0.6f, 0.5f, new Vector3(0f, -0.38f, 0.06f), 0.05f);
            });

            // Breath held → energy runs up the blade to the tip (connects BREATH and FORM).
            VFXLibrary.Register("breath_focus", b =>
            {
                b.Lifetime = 0.8f;
                var run = b.Shape("Run", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 0f, 0.55f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.02f, 0.09f), new Vector3(0.05f, 0.5f, 0.05f), 0.28f, b.Pal.Core * 0.8f);
                run.ScaleCurve = CommonVFX.FastOut;
                run.AlphaCurve = CommonVFX.PopFade;
                CommonVFX.Glint(b, "Tip", b.Pal.Bright * 0.9f, 0.45f, 0.2f, new Vector3(0f, 0f, 1.1f), 0.1f);
                b.Particles("Along", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 0f, 0.5f)).Burst(VFXQuality.Secondary ? 16 : 8)
                    .Shape(ParticleSystemShapeType.Box, 0.05f, 0f, 360f, new Vector3(0.03f, 0.03f, 0.9f)).Life(0.15f, 0.3f).Speed(0f, 0f)
                    .Velocity(new Vector3(0f, 0f, 3f)).Size(0.012f, 0.025f).Color(b.Pal.Bright * 0.7f).Stretch(1.4f, 0.04f).Fade(0.01f, 0.4f).Local();
            });

            // Breath held → a short pulse through the torso and a faint ring at the feet (authored at the feet).
            VFXLibrary.Register("breath_focus_body", b =>
            {
                b.Lifetime = 0.8f;
                CommonVFX.Glow(b, "Torso", b.Pal.Core * 0.3f, 1.1f, 0.3f, new Vector3(0f, 1.15f, 0.05f));
                var ring = CommonVFX.FlatRing(b, "Ring", b.Pal.Core * 0.35f, 1.3f, 0.35f, 0.04f, 0f, 0.9f);
                ring.AlphaCurve = CommonVFX.QuickFade;
            });
        }
    }
}
