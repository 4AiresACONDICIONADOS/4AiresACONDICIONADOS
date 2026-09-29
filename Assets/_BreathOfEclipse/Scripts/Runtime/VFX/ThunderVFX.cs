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

            RegisterFlashBreaker();
            RegisterForms();
        }

        // =================================================================== IV / VI / VII
        // Lightning Fang: a spear of light that pierces a line, the line discharges a beat later.
        // Searing Thread: five flash cuts around one enemy, then the threads snap shut on it.
        // Broken Horizon: a long crossing, a silent sheathe, then the horizon itself cracks open.

        private static void RegisterForms()
        {
            // Spearhead carried in front of the body during the piercing dash (+Z = facing).
            VFXLibrary.Register("thunder_fang_head", b =>
            {
                b.Lifetime = 0.6f;
                var tip = b.Shape("Tip", ProceduralMeshes.Cone(8), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 1.05f, 0.9f), new Vector3(90f, 0f, 0f),
                    new Vector3(0.3f, 0.6f, 0.3f), new Vector3(0.18f, 1.1f, 0.18f), 0.35f, Color.white * 4f);
                tip.AlphaCurve = CommonVFX.QuickFade;
                CommonVFX.Glint(b, "Point", Color.white * 5f, 1.1f, 0.2f, new Vector3(0f, 1.05f, 1.4f));
                b.Particles("Wake", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0.4f)).Duration(0.25f).Rate(VFXQuality.Secondary ? 160f : 70f)
                    .Shape(ParticleSystemShapeType.Sphere, 0.35f).Life(0.1f, 0.22f).Speed(0f, 0f).Velocity(new Vector3(0f, 0f, -14f))
                    .Size(0.02f, 0.05f).Color(b.Pal.Bright, Color.white * 3f).Stretch(3f, 0.03f).Fade(0.01f, 0.3f);
                for (int i = 0; i < 2; i++)
                {
                    var arc = b.Bolt("Arc" + i, new Vector3(i == 0 ? 0.3f : -0.3f, 0.9f, 0.2f), new Vector3(0f, 1.05f, 1.3f), 0.04f, 0.25f, b.Pal.Bright, i * 0.05f, 1);
                    arc.Jaggedness = 0.8f;
                }
            });

            // The pierced line discharges (authored along +Z 0..1, stretched by LastPath).
            VFXLibrary.Register("thunder_fang_burst", b =>
            {
                b.Lifetime = 1.3f;
                bool extra = VFXQuality.Secondary;
                int pillars = extra ? 6 : 3;
                for (int i = 0; i < pillars; i++)
                {
                    float z = (i + 0.5f) / pillars;
                    var p = b.Bolt("Pillar" + i, new Vector3(Random.Range(-0.2f, 0.2f), 0.05f, z), new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(2f, 2.8f), z),
                        0.12f, 0.35f, i % 2 == 0 ? Color.white * 4f : b.Pal.Bright, i * 0.035f, 2);
                    p.Points = 14;
                    p.Jaggedness = 0.35f;
                }
                var line = b.Bolt("Line", new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), 0.1f, 0.4f, b.Pal.Bright, 0f, 2);
                line.Points = 22;
                line.Jaggedness = 2.2f;
                var ground = b.Bolt("Ground", new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.05f, 1f), 0.06f, 0.6f, b.Pal.Accent, 0.05f, 1);
                ground.Points = 18;
                ground.Jaggedness = 3f;
                ground.Flat = true;
                var sheet = b.Shape("Glow", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 1f, 0.5f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.9f, 0.5f, 0.9f), new Vector3(0.2f, 0.5f, 0.2f), 0.45f, b.Pal.Core);
                sheet.AlphaCurve = CommonVFX.PopFade;
                b.Particles("Sparks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0.5f)).Burst(extra ? 70 : 30)
                    .Shape(ParticleSystemShapeType.Box, 0.2f, 0f, 360f, new Vector3(0.4f, 0.6f, 1f)).Life(0.25f, 0.6f).Speed(3f, 9f)
                    .Size(0.03f, 0.07f).Color(b.Pal.Bright, b.Pal.Accent).Stretch(1.5f, 0.05f).Gravity(0.8f).Fade(0.01f, 0.4f);
                b.Decal("Scorch", ProceduralTextures.Crack, new Color(1f, 0.95f, 0.7f, 0.7f), 1.6f, 2f, new Vector3(0f, 0f, 0.5f));
                b.Light("Light", new Vector3(0f, 1f, 0.5f), b.Pal.Bright, 12f, 10f, 0.35f);
            });

            // Threads snap shut on the target (authored at the target, centred at the chest).
            VFXLibrary.Register("thunder_thread_snap", b =>
            {
                b.Lifetime = 1.2f;
                int threads = VFXQuality.Secondary ? 8 : 5;
                for (int i = 0; i < threads; i++)
                {
                    float a = i / (float)threads * Mathf.PI * 2f;
                    Vector3 from = new Vector3(Mathf.Cos(a) * 2.4f, Random.Range(-0.6f, 1.2f), Mathf.Sin(a) * 2.4f);
                    var t = b.Bolt("Thread" + i, from, Vector3.zero, 0.07f, 0.3f, i % 2 == 0 ? Color.white * 4f : b.Pal.Bright, i * 0.012f, 1);
                    t.Points = 12;
                    t.Jaggedness = 0.45f;
                }
                var cage = b.Shape("Cage", ProceduralMeshes.Primitive(PrimitiveType.Sphere), b.Additive(ProceduralTextures.Glow), Vector3.zero, Vector3.zero,
                    Vector3.one * 4.5f, Vector3.one * 0.6f, 0.25f, b.Pal.Core * 0.7f);
                cage.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
                CommonVFX.Glint(b, "Snap", Color.white * 7f, 3.5f, 0.25f, Vector3.zero, 0.22f);
                CommonVFX.Sparks(b, "Burst", VFXQuality.Secondary ? 60 : 25, b.Pal.Bright, 8f, 18f, 0.45f, 0.06f, 0.5f, 180f, default, 0.24f);
                CommonVFX.FlatRing(b, "Ring", b.Pal.Bright, 4f, 0.3f, -1f, 0.24f);
                b.Light("Light", Vector3.zero, b.Pal.Bright, 10f, 12f, 0.3f, 0.22f);
            });

            // The horizon breaks (authored along +Z 0..1, stretched over the crossing).
            VFXLibrary.Register("thunder_horizon", b =>
            {
                b.Lifetime = 1.8f;
                bool extra = VFXQuality.Secondary, full = VFXQuality.Full;
                var line = b.Shape("Horizon", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 1.15f, 0.5f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.5f, 0.05f), new Vector3(0.02f, 0.62f, 0.02f), 0.6f, Color.white * 9f);
                line.ScaleCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 10f), new Keyframe(0.1f, 1f), new Keyframe(1f, 1f));
                line.AlphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f));
                // A flat sheet of light opening above and below the line: the sky splits.
                var split = b.Shape("Split", ProceduralMeshes.Primitive(PrimitiveType.Quad), b.Additive(ProceduralTextures.Band), new Vector3(0f, 1.15f, 0.5f),
                    new Vector3(0f, 90f, 0f), new Vector3(1f, 0.02f, 1f), new Vector3(1f, 2.6f, 1f), 0.5f, b.Pal.Core);
                split.ScaleCurve = CommonVFX.FastOut;
                split.AlphaCurve = CommonVFX.PopFade;
                split.Delay = 0.05f;
                int branches = full ? 14 : extra ? 9 : 5;
                for (int i = 0; i < branches; i++)
                {
                    float z = (i + Random.Range(0.2f, 0.8f)) / branches;
                    float up = i % 2 == 0 ? 1f : -0.5f;
                    var br = b.Bolt("Branch" + i, new Vector3(0f, 1.15f, z), new Vector3(Random.Range(-1.2f, 1.2f), 1.15f + up * Random.Range(1.2f, 2.6f), z + Random.Range(-0.04f, 0.04f)),
                        0.09f, 0.45f, i % 3 == 0 ? b.Pal.Accent : Color.white * 4f, 0.08f + Random.Range(0f, 0.08f), 2);
                    br.Points = 14;
                    br.Jaggedness = 0.7f;
                }
                var crack = b.Bolt("Crack", new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0.05f, 1f), 0.1f, 1f, b.Pal.Bright, 0.1f, 2);
                crack.Points = 26;
                crack.Jaggedness = 3.5f;
                crack.Flat = true;
                b.Particles("Sparks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1.15f, 0.5f)).Burst(extra ? 120 : 45).Delay(0.08f)
                    .Shape(ParticleSystemShapeType.Box, 0.2f, 0f, 360f, new Vector3(0.2f, 0.3f, 1f)).Life(0.3f, 0.8f).Speed(4f, 14f)
                    .Size(0.03f, 0.08f).Color(b.Pal.Bright, Color.white * 3f).Stretch(1.6f, 0.05f).Gravity(0.7f).Fade(0.01f, 0.4f);
                if (extra) CommonVFX.Dust(b, "Dust", 12, 1f, 1.2f, new Color(0.35f, 0.35f, 0.4f, 0.45f), 3f);
                b.Decal("Scar", ProceduralTextures.Crack, new Color(1f, 0.95f, 0.7f, 0.85f), 2.4f, 3f, new Vector3(0f, 0f, 0.5f));
                b.Light("Light", new Vector3(0f, 1.2f, 0.5f), b.Pal.Bright, 18f, 16f, 0.45f, 0.05f);
            });
        }

        // =================================================================== FLASH BREAKER (First Form) — hero set
        // Visual language: static crawling over the ground, arcs climbing the body while the blade is sheathed, one
        // blinding straight trajectory with parallel discharges and speed streaks, then a single horizontal iai line
        // that appears a beat later and splits into branching lightning. No water shapes, no rings of liquid.

        /// <summary>Static crawling radially over the ground (flat zigzag, never dips under the floor).</summary>
        private static void GroundCrawl(VfxBuild b, string name, int count, float radius, float duration, float delay, float width)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(0f, 0.6f)) / count * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var bolt = b.Bolt(name + i, dir * radius * 0.12f + Vector3.up * 0.05f, dir * radius * Random.Range(0.65f, 1f) + Vector3.up * 0.05f,
                    width, duration, i % 3 == 0 ? b.Pal.Accent : b.Pal.Bright, delay + i * 0.03f, 2);
                bolt.Points = 12;
                bolt.Jaggedness = 1.3f;
                bolt.Flicker = 0.045f;
                bolt.Flat = true;
            }
        }

        /// <summary>Arcs climbing from the feet to the shoulders and the sheath at the left hip.</summary>
        private static void BodyArcs(VfxBuild b, string name, int count, float duration, float delay, float width)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                Vector3 foot = new Vector3(Mathf.Cos(a) * 0.45f, 0.08f, Mathf.Sin(a) * 0.45f);
                Vector3 top = new Vector3(Mathf.Cos(a + 1.3f) * 0.3f, Random.Range(1.1f, 1.7f), Mathf.Sin(a + 1.3f) * 0.3f);
                var bolt = b.Bolt(name + i, foot, top, width, duration, i % 2 == 0 ? b.Pal.Bright : Color.white * 3f, delay + i * 0.05f, 1);
                bolt.Points = 10;
                bolt.Jaggedness = 0.9f;
                bolt.Flicker = 0.03f;
            }
        }

        private static void RegisterFlashBreaker()
        {
            // ----------------------------------------------------------- stance: hand on the hilt, the ground starts to hum
            VFXLibrary.Register("thunder_flash_stance", b =>
            {
                b.Lifetime = 0.8f;
                bool extra = VFXQuality.Secondary;
                GroundCrawl(b, "Crawl", extra ? 6 : 4, 2.2f, 0.5f, 0.05f, 0.05f);
                b.Particles("Static", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 0.1f, 0f)).Duration(0.5f).Rate(extra ? 60f : 30f)
                    .Shape(ParticleSystemShapeType.Circle, 1.4f, 0f, 360f, null, new Vector3(-90f, 0f, 0f)).Life(0.1f, 0.2f).Speed(0.5f, 2f)
                    .Size(0.02f, 0.05f).Color(b.Pal.Bright).Stretch(1.4f, 0.04f).Noise(3f, 5f).Fade(0.01f, 0.3f);
                // Sheath glint at the left hip: the only bright point while the rest of the screen desaturates.
                CommonVFX.Glint(b, "Tsuba", Color.white * 4f, 0.7f, 0.25f, new Vector3(-0.28f, 1f, 0.15f), 0.1f);
                b.Light("Light", new Vector3(0f, 0.6f, 0f), b.Pal.Core, 5f, 2.5f, 0.6f);
            });

            // ----------------------------------------------------------- gather: arcs climb the body, dust lifts, light stutters
            VFXLibrary.Register("thunder_flash_charge", b =>
            {
                b.Lifetime = 1f;
                bool extra = VFXQuality.Secondary, full = VFXQuality.Full;
                BodyArcs(b, "Climb", extra ? 7 : 4, 0.35f, 0f, 0.06f);
                GroundCrawl(b, "Crawl", full ? 10 : extra ? 7 : 4, 3.2f, 0.45f, 0.08f, 0.07f);
                b.Particles("Rising", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 0.1f, 0f)).Duration(0.45f).Rate(extra ? 120f : 50f)
                    .Shape(ParticleSystemShapeType.Circle, 0.9f, 0f, 360f, null, new Vector3(-90f, 0f, 0f)).Life(0.2f, 0.45f).Speed(2f, 6f)
                    .Size(0.02f, 0.05f).Color(b.Pal.Bright, Color.white * 3f).Stretch(2f, 0.05f).Gravity(-0.4f).Fade(0.01f, 0.4f);
                if (extra) CommonVFX.Dust(b, "Lift", 8, 1.2f, 0.8f, new Color(0.3f, 0.3f, 0.36f, 0.35f), 1.2f);
                var ring = CommonVFX.FlatRing(b, "Static", b.Pal.Core * 0.7f, 3.2f, 0.4f, 0.05f, 0.05f, 0.94f);
                ring.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.3f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
                b.Light("Light", new Vector3(0f, 1f, 0f), b.Pal.Core, 7f, 5f, 0.5f);
                var l = b.Root.GetComponentInChildren<FlashLight>();
                if (l != null) l.Curve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f), new Keyframe(0.25f, 0.3f), new Keyframe(0.45f, 1f),
                    new Keyframe(0.6f, 0.4f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
            });

            // ----------------------------------------------------------- trajectory (authored along +Z 0..1, stretched by SpawnBetween)
            VFXLibrary.Register("thunder_flash_path", b =>
            {
                b.Lifetime = 1.4f;
                bool extra = VFXQuality.Secondary, full = VFXQuality.Full;
                // One blinding core line, a soft halo, and a wide faint sheet on the ground.
                var core = b.Shape("Core", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, 1f, 0.5f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.14f, 0.5f, 0.14f), new Vector3(0.02f, 0.5f, 0.02f), 0.5f, Color.white * 7f);
                core.AlphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.9f), new Keyframe(1f, 0f));
                var halo = b.Shape("Halo", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 1f, 0.5f),
                    new Vector3(90f, 0f, 0f), new Vector3(0.7f, 0.5f, 0.7f), new Vector3(0.1f, 0.5f, 0.1f), 0.7f, b.Pal.Core);
                halo.AlphaCurve = CommonVFX.QuickFade;
                var sheet = b.Shape("Scorch", ProceduralMeshes.GroundQuad(), b.Additive(ProceduralTextures.Band), new Vector3(0f, 0.04f, 0.5f),
                    Vector3.zero, new Vector3(0.8f, 1f, 1f), new Vector3(0.2f, 1f, 1f), 1.1f, b.Pal.Core * 0.6f);
                sheet.AlphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.5f), new Keyframe(1f, 0f));
                // Main discharge plus parallel side discharges (they read as "the air split twice").
                var main = b.Bolt("Main", new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), 0.16f, 0.4f, Color.white * 5f, 0f, 3);
                main.Points = 24;
                main.Jaggedness = 2.5f;
                main.Flicker = 0.025f;
                int sides = extra ? 4 : 2;
                for (int i = 0; i < sides; i++)
                {
                    float x = (i % 2 == 0 ? 1f : -1f) * (0.35f + 0.2f * (i / 2));
                    float y = 0.55f + 0.5f * (i / 2);
                    var side = b.Bolt("Side" + i, new Vector3(x, y, 0.05f), new Vector3(x * 0.6f, y + 0.2f, 0.95f), 0.06f, 0.3f, i % 2 == 0 ? b.Pal.Bright : b.Pal.Accent, 0.03f + i * 0.02f, 1);
                    side.Points = 18;
                    side.Jaggedness = 3f;
                }
                // Static left on the ground along the whole line.
                int crawl = full ? 6 : extra ? 4 : 2;
                for (int i = 0; i < crawl; i++)
                {
                    float z = (i + 0.5f) / crawl;
                    var g = b.Bolt("Ground" + i, new Vector3(0f, 0.05f, z - 0.08f), new Vector3((i % 2 == 0 ? 1f : -1f) * Random.Range(0.6f, 1.1f), 0.05f, z + 0.06f),
                        0.05f, 0.6f, b.Pal.Bright, 0.05f + i * 0.03f, 1);
                    g.Points = 8;
                    g.Jaggedness = 1.2f;
                    g.Flat = true;
                    g.Flicker = 0.05f;
                }
                // Speed streaks: long, thin, all running along the trajectory.
                b.Particles("Streaks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 1f, 0.5f)).Burst(extra ? 60 : 24)
                    .Shape(ParticleSystemShapeType.Box, 0.2f, 0f, 360f, new Vector3(1.2f, 1.4f, 1f)).Life(0.15f, 0.35f).Speed(0f, 0f)
                    .Velocity(new Vector3(0f, 0f, 30f)).Size(0.02f, 0.05f).Color(Color.white * 3f, b.Pal.Bright).Stretch(6f, 0.02f).Fade(0.01f, 0.2f);
                b.Particles("Sparks", b.Additive(ProceduralTextures.Streak), new Vector3(0f, 0.9f, 0.5f)).Burst(extra ? 50 : 20)
                    .Shape(ParticleSystemShapeType.Box, 0.2f, 0f, 360f, new Vector3(0.3f, 0.4f, 1f)).Life(0.25f, 0.6f).Speed(1f, 5f)
                    .Size(0.03f, 0.07f).Color(b.Pal.Bright, b.Pal.Accent).Stretch(1.5f, 0.05f).Gravity(0.8f).Fade(0.01f, 0.4f);
                if (extra) CommonVFX.Dust(b, "Kick", 6, 0.4f, 0.7f, new Color(0.3f, 0.3f, 0.35f, 0.4f), 2.5f);
                b.Light("Light", new Vector3(0f, 1f, 0.5f), b.Pal.Bright, 12f, 9f, 0.3f);
            });

            // ----------------------------------------------------------- dramatic pause: after-draw pose, residual static on the hero
            VFXLibrary.Register("thunder_flash_pause", b =>
            {
                b.Lifetime = 0.9f;
                BodyArcs(b, "Residual", VFXQuality.Secondary ? 4 : 2, 0.4f, 0f, 0.035f);
                b.Particles("Drip", b.Additive(ProceduralTextures.Streak), new Vector3(0.35f, 1.1f, 0.4f)).Duration(0.3f).Rate(50f)
                    .Shape(ParticleSystemShapeType.Sphere, 0.3f).Life(0.15f, 0.35f).Speed(0.5f, 2f)
                    .Size(0.02f, 0.04f).Color(Color.white * 3f, b.Pal.Bright).Stretch(1.2f, 0.04f).Gravity(1.2f).Fade(0.01f, 0.3f);
                CommonVFX.Glint(b, "Edge", Color.white * 5f, 1.1f, 0.2f, new Vector3(0.5f, 1.2f, 0.5f));
            });

            // ----------------------------------------------------------- the cut appears: one iai line, then it splits into lightning
            VFXLibrary.Register("thunder_flash_cut", b =>
            {
                b.Lifetime = 1.6f;
                bool extra = VFXQuality.Secondary, full = VFXQuality.Full;
                const float h = 1.15f;
                var line = b.Shape("IaiLine", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.SoftCircle), new Vector3(0f, h, 0f),
                    new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.4f, 0.05f), new Vector3(0.02f, 3.4f, 0.02f), 0.45f, Color.white * 9f);
                line.ScaleCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 12f), new Keyframe(0.12f, 1f), new Keyframe(1f, 1f));
                line.AlphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
                var bloom = b.Shape("LineGlow", ProceduralMeshes.Primitive(PrimitiveType.Cylinder), b.Additive(ProceduralTextures.Glow), new Vector3(0f, h, 0f),
                    new Vector3(0f, 0f, 90f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.25f, 3.6f, 0.25f), 0.5f, b.Pal.Core);
                bloom.ScaleCurve = CommonVFX.FastOut;
                bloom.AlphaCurve = CommonVFX.PopFade;
                // Split: branching lightning erupting from points along the line a beat after it appears.
                int split = full ? 12 : extra ? 8 : 5;
                for (int i = 0; i < split; i++)
                {
                    float x = Mathf.Lerp(-2.6f, 2.6f, (i + 0.5f) / split) + Random.Range(-0.2f, 0.2f);
                    float up = i % 2 == 0 ? 1f : -0.7f;
                    Vector3 from = new Vector3(x, h, 0f);
                    Vector3 to = from + new Vector3(Random.Range(-0.8f, 0.8f), up * Random.Range(1.2f, 2.4f), Random.Range(-1f, 1f));
                    to.y = Mathf.Max(0.05f, to.y);
                    var bolt = b.Bolt("Split" + i, from, to, 0.1f, 0.42f, i % 3 == 0 ? b.Pal.Accent : Color.white * 4f, 0.07f + Random.Range(0f, 0.05f), 2);
                    bolt.Points = 14;
                    bolt.Jaggedness = 0.8f;
                }
                GroundCrawl(b, "Ground", extra ? 8 : 4, 4f, 0.6f, 0.1f, 0.08f);
                CommonVFX.Glint(b, "Glint", Color.white * 7f, 4.5f, 0.22f, new Vector3(0f, h, 0f));
                CommonVFX.Glow(b, "Flash", b.Pal.Core * 1.3f, 5f, 0.3f, new Vector3(0f, h, 0f), 0.06f);
                CommonVFX.Sparks(b, "Sparks", extra ? 60 : 25, b.Pal.Bright, 8f, 20f, 0.45f, 0.07f, 0.5f, 180f, default, 0.06f);
                CommonVFX.FlatRing(b, "Shock", b.Pal.Bright, 5.5f, 0.35f, 0.05f, 0.08f);
                if (extra) CommonVFX.Dust(b, "Dust", 10, 0.8f, 1f, new Color(0.35f, 0.35f, 0.4f, 0.45f), 3f);
                b.Decal("Scorch", ProceduralTextures.Crack, new Color(1f, 0.95f, 0.7f, 0.85f), 3.2f, 2.5f);
                b.Light("Light", new Vector3(0f, h, 0f), b.Pal.Bright, 14f, 14f, 0.35f, 0.02f);
            });

            // ----------------------------------------------------------- re-sheathe click: tiny spark at the sheath mouth
            VFXLibrary.Register("thunder_flash_click", b =>
            {
                b.Lifetime = 0.5f;
                CommonVFX.Glint(b, "Click", Color.white * 4f, 0.5f, 0.15f, new Vector3(-0.28f, 1f, 0.15f));
                CommonVFX.Sparks(b, "Sparks", 8, b.Pal.Bright, 1f, 3f, 0.25f, 0.03f, 0.5f);
            });
        }
    }
}
