using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// THUNDER BREATH effects. Identity: jagged bolts, white-yellow flashes, luminous trajectory lines,
    /// afterimages and the delayed "the cut appears" explosion. Readable even when the action is instant.
    /// </summary>
    public static class ThunderVFX
    {
        private static void Crackle(VfxBuild b, string name, int count, float radius, float height, float duration, float width, float delay = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                Vector3 from = new Vector3(Mathf.Cos(a) * radius * 0.4f, Random.Range(0.2f, height), Mathf.Sin(a) * radius * 0.4f);
                Vector3 to = new Vector3(Mathf.Cos(a + 0.9f) * radius, Random.Range(0.1f, height * 1.1f), Mathf.Sin(a + 0.9f) * radius);
                var bolt = b.Bolt(name + i, from, to, width, duration, i % 2 == 0 ? b.Pal.Bright : b.Pal.Accent, delay + i * 0.03f, 1);
                bolt.Flicker = 0.03f;
                bolt.Jaggedness = 0.6f;
            }
        }

        public static void Register()
        {
            // --------------------------------------------------------- charge: crackling arcs around the body
            VFXLibrary.Register("thunder_charge", b =>
            {
                b.Lifetime = 0.9f;
                Crackle(b, "Arc", 7, 1.3f, 1.9f, 0.45f, 0.07f);
                b.Particles("Sparks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0f)).Duration(0.4f).Rate(90f)
                    .Shape(ParticleSystemShapeType.Sphere, 1.1f, 0f, 360f, null, null, null, 0.3f).Life(0.1f, 0.25f).Speed(2f, 6f)
                    .Size(0.03f, 0.06f).Color(b.Pal.Bright).Stretch(1.2f, 0.05f).Noise(2f, 4f).Fade(0.01f, 0.3f);
                CommonVFX.FlatRing(b, "GroundCrackle", b.Pal.Core * 0.6f, 2.5f, 0.45f, 0.05f, 0f, 0.93f);
                b.Light("Light", new Vector3(0f, 1.2f, 0f), b.Pal.Core, 6f, 4f, 0.45f);
                var l = b.Root.GetComponentInChildren<FlashLight>();
                if (l != null) l.Curve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(0.3f, 0.4f), new Keyframe(0.5f, 1f), new Keyframe(0.7f, 0.5f), new Keyframe(1f, 1f));
            });

            // --------------------------------------------------------- luminous trajectory line (authored along +Z 0..1)
            VFXLibrary.Register("thunder_path", b =>
            {
                b.Lifetime = 1f;
                var beam = b.Shape("Beam", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 1f, 0.5f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.35f, 0.5f, 0.35f), new Vector3(0.05f, 0.5f, 0.05f), 0.6f, b.Pal.Bright);
                beam.AlphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0.8f), new Keyframe(1f, 0f));
                var core = b.Shape("Core", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 1f, 0.5f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.5f, 0.08f), new Vector3(0.02f, 0.5f, 0.02f), 0.4f, Color.white * 5f);
                core.AlphaCurve = CommonVFX.QuickFade;
                var bolt = b.Bolt("PathBolt", new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), 0.12f, 0.35f, b.Pal.Bright, 0f, 2);
                bolt.Jaggedness = 0.25f;
                bolt.Points = 18;
                b.Particles("Sparks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0.5f)).Burst(40)
                    .Shape(ParticleSystemShapeType.Box, 0.2f, 0f, 360f, new Vector3(0.2f, 0.2f, 1f)).Life(0.2f, 0.5f).Speed(1f, 4f)
                    .Size(0.03f, 0.07f).Color(b.Pal.Bright, b.Pal.Accent).Stretch(1.5f, 0.05f).Gravity(0.3f).Fade(0.01f, 0.4f);
            });

            // --------------------------------------------------------- the cut appears: X flash + exploding arcs
            VFXLibrary.Register("thunder_cut", b =>
            {
                b.Lifetime = 1.2f;
                for (int i = 0; i < 2; i++)
                {
                    var slash = b.Shape("Slash" + i, ProceduralMeshes.Crescent(140f, 0.12f), b.Additive(ProceduralTextures.CrescentBand), Vector3.zero,
                        new Vector3(90f, 0f, i == 0 ? 35f : -35f), new Vector3(0.6f, 0.6f, 0.6f), new Vector3(3.2f, 3.2f, 3.2f), 0.25f, Color.white * 5f);
                    slash.ScaleCurve = CommonVFX.FastOut;
                    slash.AlphaCurve = CommonVFX.PopFade;
                    slash.Delay = i * 0.04f;
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f + Random.Range(-15f, 15f);
                    Vector3 dir = Quaternion.Euler(Random.Range(-40f, 40f), a, 0f) * Vector3.forward;
                    b.Bolt("Arc" + i, Vector3.zero, dir * Random.Range(1.8f, 3.2f), 0.09f, 0.4f, i % 2 == 0 ? b.Pal.Bright : b.Pal.Accent, 0.02f, 1);
                }
                CommonVFX.Glint(b, "Glint", Color.white * 6f, 4f, 0.25f);
                CommonVFX.Glow(b, "Glow", b.Pal.Core, 4f, 0.35f);
                CommonVFX.Sparks(b, "Sparks", 40, b.Pal.Bright, 8f, 18f, 0.4f, 0.07f, 0.4f);
                b.Light("Light", Vector3.zero, b.Pal.Bright, 10f, 12f, 0.3f);
            });

            VFXLibrary.Register("thunder_explosion", b =>
            {
                b.Lifetime = 1.3f;
                var dome = b.Shape("Dome", ProceduralMeshes.Primitive(PrimitiveType.Sphere), b.Additive(ProceduralTextures.Glow), Vector3.zero, Vector3.zero,
                    Vector3.one * 0.5f, Vector3.one * 5f, 0.3f, b.Pal.Core);
                dome.ScaleCurve = CommonVFX.FastOut;
                dome.AlphaCurve = CommonVFX.QuickFade;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) * 0.6f;
                    b.Bolt("Arc" + i, Vector3.zero, dir.normalized * Random.Range(2f, 3.5f), 0.1f, 0.45f, b.Pal.Bright, 0f, 2);
                }
                CommonVFX.FlatRing(b, "Ring", b.Pal.Bright, 6f, 0.35f, -0.9f);
                CommonVFX.Sparks(b, "Sparks", 50, b.Pal.Bright, 6f, 16f, 0.5f, 0.07f, 0.6f);
                b.Light("Light", Vector3.zero, b.Pal.Core, 12f, 10f, 0.4f);
            });

            // --------------------------------------------------------- generic bolt between two points (chains)
            VFXLibrary.Register("thunder_link", b =>
            {
                b.Lifetime = 0.5f;
                var bolt = b.Bolt("Link", Vector3.zero, Vector3.forward, 0.14f, 0.35f, b.Pal.Bright, 0f, 2);
                bolt.Points = 16;
                bolt.Jaggedness = 0.5f;
                var bolt2 = b.Bolt("Link2", Vector3.zero, Vector3.forward, 0.06f, 0.3f, b.Pal.Accent, 0.03f, 0);
                bolt2.Points = 12;
            });

            // --------------------------------------------------------- lightning from the sky
            VFXLibrary.Register("thunder_strike", b =>
            {
                b.Lifetime = 3f;
                var bolt = b.Bolt("Sky", new Vector3(0f, 16f, 0f), Vector3.zero, 0.5f, 0.35f, Color.white * 5f, 0f, 3);
                bolt.Points = 24;
                bolt.Jaggedness = 0.25f;
                var bolt2 = b.Bolt("Sky2", new Vector3(0.5f, 16f, 0.3f), new Vector3(0.2f, 0f, -0.1f), 0.22f, 0.3f, b.Pal.Core, 0.05f, 2);
                bolt2.Points = 20;
                CommonVFX.Glint(b, "Glint", Color.white * 6f, 5f, 0.3f, new Vector3(0f, 0.5f, 0f));
                CommonVFX.FlatRing(b, "Ring", b.Pal.Bright, 5f, 0.35f);
                CommonVFX.Sparks(b, "Sparks", 40, b.Pal.Bright, 5f, 12f, 0.5f, 0.07f, 1f, 70f, new Vector3(-90f, 0f, 0f));
                CommonVFX.Dust(b, "Dust", 10, 0.6f, 1f, new Color(0.35f, 0.35f, 0.4f, 0.45f), 3f);
                b.Decal("Scorch", ProceduralTextures.Crack, new Color(1f, 1f, 1f, 0.9f), 3f, 2.8f);
                b.Light("Light", new Vector3(0f, 2f, 0f), b.Pal.Bright, 18f, 14f, 0.35f);
            });

            VFXLibrary.Register("thunder_blink", b =>
            {
                b.Lifetime = 0.6f;
                CommonVFX.Glint(b, "Glint", b.Pal.Bright, 2.6f, 0.18f, new Vector3(0f, 1f, 0f));
                CommonVFX.Sparks(b, "Sparks", 20, b.Pal.Bright, 3f, 9f, 0.3f, 0.05f, 0.3f);
                Crackle(b, "Arc", 3, 0.9f, 1.8f, 0.25f, 0.05f);
            });

            // --------------------------------------------------------- ultimate finale: web of bolts
            VFXLibrary.Register("thunder_web", b =>
            {
                b.Lifetime = 1.6f;
                for (int i = 0; i < 14; i++)
                {
                    Vector3 from = Random.insideUnitSphere * 6f;
                    from.y = Mathf.Abs(from.y) * 0.5f + 0.5f;
                    Vector3 to = Random.insideUnitSphere * 6f;
                    to.y = Mathf.Abs(to.y) * 0.5f + 0.5f;
                    b.Bolt("Web" + i, from, to, 0.16f, 0.6f, i % 3 == 0 ? b.Pal.Accent : b.Pal.Bright, i * 0.015f, 2);
                }
                CommonVFX.Glow(b, "Glow", b.Pal.Core * 1.5f, 12f, 0.5f, Vector3.up);
                CommonVFX.FlatRing(b, "Ring", b.Pal.Bright, 14f, 0.5f);
                CommonVFX.Sparks(b, "Sparks", 90, b.Pal.Bright, 8f, 22f, 0.7f, 0.09f, 0.4f);
                b.Light("Light", Vector3.up * 2f, b.Pal.Bright, 25f, 16f, 0.6f);
            });
        }
    }
}
