using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// MOONLIGHT BREATH effects. Identity: crescent moon blades that linger and cut after a delay, celestial
    /// star particles, violet-blue energy and white flashes; the ultimate raises an eclipsed moon.
    /// </summary>
    public static class MoonVFX
    {
        private static ExpandingMesh Crescent(VfxBuild b, string name, Vector3 pos, Vector3 euler, float size, float duration, Color color, float delay = 0f)
        {
            var c = b.Shape(name, ProceduralMeshes.Crescent(160f, 0.24f), b.Additive(ProceduralTextures.CrescentBand), pos, euler,
                Vector3.one * size * 0.7f, Vector3.one * size, duration, color);
            c.Delay = delay;
            c.ScaleCurve = CommonVFX.FastOut;
            c.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.06f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f));
            return c;
        }

        private static void Stars(VfxBuild b, string name, int count, Vector3 pos, float radius, float life, float delay = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Star), pos).Burst(count).Delay(delay).Shape(ParticleSystemShapeType.Sphere, radius)
                .Life(life * 0.5f, life).Speed(0.1f, 0.8f).Size(0.08f, 0.22f).Rotation(0f, 45f).Color(b.Pal.Bright, b.Pal.Accent)
                .Gravity(-0.03f).Fade(0.1f, 0.5f);
        }

        public static void Register()
        {
            // Crescent that lingers in the air, spinning slowly (Crescent Veil).
            VFXLibrary.Register("moon_crescent", b =>
            {
                b.Lifetime = 1.4f;
                var main = Crescent(b, "Blade", new Vector3(0f, 1.2f, 0.8f), new Vector3(90f, 0f, 0f), 2.4f, 1.1f, b.Pal.Core);
                main.SpinDegreesPerSecond = new Vector3(0f, 45f, 0f);
                var inner = Crescent(b, "Inner", new Vector3(0f, 1.2f, 0.75f), new Vector3(90f, 0f, 0f), 2f, 1f, b.Pal.Bright, 0.03f);
                inner.SpinDegreesPerSecond = new Vector3(0f, 45f, 0f);
                Stars(b, "Stars", 24, new Vector3(0f, 1.2f, 1f), 1.2f, 1.1f);
                CommonVFX.Glow(b, "Glow", b.Pal.Core * 0.5f, 3f, 0.8f, new Vector3(0f, 1.2f, 1f));
                b.Light("Light", new Vector3(0f, 1.2f, 1f), b.Pal.Core, 6f, 4f, 1f);
            });

            // Delayed echo slash that pops into existence at a point (Waning Echo).
            VFXLibrary.Register("moon_echo", b =>
            {
                b.Lifetime = 1f;
                float tilt = Random.Range(-50f, 50f);
                var c = Crescent(b, "Echo", new Vector3(0f, 1.1f, 0f), new Vector3(90f, 0f, tilt), 2.2f, 0.45f, b.Pal.Bright);
                c.StartScale = Vector3.one * 0.4f;
                var c2 = Crescent(b, "EchoCore", new Vector3(0f, 1.1f, -0.05f), new Vector3(90f, 0f, tilt), 1.8f, 0.35f, Color.white * 3f, 0.02f);
                c2.StartScale = Vector3.one * 0.3f;
                CommonVFX.Glint(b, "Glint", Color.white * 4f, 2f, 0.2f, new Vector3(0f, 1.1f, 0f));
                Stars(b, "Stars", 18, new Vector3(0f, 1.1f, 0f), 0.8f, 0.8f);
            });

            // Ring of crescents orbiting the user (Lunar Halo).
            VFXLibrary.Register("moon_halo", b =>
            {
                b.Lifetime = 1.6f;
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(b.Root, false);
                pivot.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                var spin = pivot.AddComponent<ExpandingMeshSpinner>();
                spin.DegreesPerSecond = new Vector3(0f, 480f, 0f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f;
                    var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    var c = b.Shape("Moon" + i, ProceduralMeshes.Crescent(120f, 0.3f), b.Additive(ProceduralTextures.CrescentBand), dir * 2f,
                        new Vector3(0f, a, 90f), Vector3.one * 0.4f, Vector3.one * 1.4f, 1.3f, i % 2 == 0 ? b.Pal.Core : b.Pal.Bright);
                    c.transform.SetParent(pivot.transform, false);
                    c.transform.localPosition = dir * 2f;
                    c.ScaleCurve = CommonVFX.FastOut;
                    c.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.8f, 0.9f), new Keyframe(1f, 0f));
                    c.Delay = i * 0.03f;
                }
                CommonVFX.FlatRing(b, "Ring", b.Pal.Core, 5f, 0.6f);
                Stars(b, "Stars", 40, new Vector3(0f, 1.1f, 0f), 2.4f, 1.4f);
                b.Light("Light", new Vector3(0f, 1.2f, 0f), b.Pal.Core, 8f, 5f, 1.2f);
            });

            // Crescent projectile (travels along +Z).
            VFXLibrary.Register("moon_wave", b =>
            {
                b.Lifetime = 1.6f;
                var c = Crescent(b, "Wave", new Vector3(0f, 1.1f, 0f), new Vector3(0f, 0f, 0f), 1.8f, 1.5f, b.Pal.Core);
                c.StartScale = Vector3.one * 1.6f;
                c.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.85f, 0.9f), new Keyframe(1f, 0f));
                var core = Crescent(b, "Core", new Vector3(0f, 1.1f, -0.05f), Vector3.zero, 1.5f, 1.5f, b.Pal.Bright);
                core.StartScale = Vector3.one * 1.3f;
                core.AlphaCurve = c.AlphaCurve;
                b.Particles("StarTrail", b.Additive(ProceduralTextures.Star), new Vector3(0f, 1.1f, 0f)).Duration(1.4f).Rate(40f)
                    .Shape(ParticleSystemShapeType.Box, 0.5f, 0f, 360f, new Vector3(2f, 0.2f, 0.2f)).Life(0.3f, 0.6f).Speed(0f, 0.3f)
                    .Size(0.08f, 0.16f).Color(b.Pal.Bright, b.Pal.Accent).Fade(0.1f, 0.4f);
            });

            // Ultimate: eclipsed moon rises behind the swordsman, crescents converge on the target.
            VFXLibrary.Register("moon_eclipse", b =>
            {
                b.Lifetime = 5.5f;
                var moon = b.Shape("Moon", ProceduralMeshes.Primitive(PrimitiveType.Sphere), b.Additive(ProceduralTextures.Glow), new Vector3(0f, 9f, -10f), Vector3.zero,
                    Vector3.one * 2f, Vector3.one * 14f, 5f, b.Pal.Bright * 0.8f);
                moon.ScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1f));
                moon.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
                var shadow = b.Shape("Eclipse", ProceduralMeshes.Primitive(PrimitiveType.Sphere), MaterialFactory.Vfx(ProceduralTextures.SoftCircle, VfxBlend.AlphaBlend, 0f),
                    new Vector3(-1.2f, 9.4f, -9.6f), Vector3.zero, Vector3.one * 1.5f, Vector3.one * 9.5f, 5f, new Color(0.01f, 0.0f, 0.03f, 1f));
                shadow.ScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1f));
                shadow.AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0.95f), new Keyframe(0.85f, 0.95f), new Keyframe(1f, 0f));
                var corona = b.Shape("Corona", ProceduralMeshes.Ring(0.82f), b.Additive(ProceduralTextures.Band), new Vector3(0f, 9f, -9.8f), new Vector3(90f, 0f, 0f),
                    Vector3.one * 1f, Vector3.one * 8f, 5f, b.Pal.Core);
                corona.ScaleCurve = moon.ScaleCurve;
                corona.AlphaCurve = moon.AlphaCurve;
                corona.SpinDegreesPerSecond = new Vector3(0f, 20f, 0f);
                b.Particles("Converge", b.Additive(ProceduralTextures.Star), new Vector3(0f, 1.5f, 0f)).Duration(3f).Rate(120f).Delay(1f)
                    .Shape(ParticleSystemShapeType.Sphere, 12f, 0f, 360f, null, null, null, 0.05f).Life(0.6f, 0.9f).Speed(-18f, -12f)
                    .Size(0.15f, 0.35f).Color(b.Pal.Bright, b.Pal.Accent).Fade(0.1f, 0.6f);
                b.Particles("Starfield", b.Additive(ProceduralTextures.Star), new Vector3(0f, 8f, -6f)).Burst(80)
                    .Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(30f, 12f, 6f)).Life(3.5f, 5f).Speed(0f, 0.1f)
                    .Size(0.1f, 0.3f).Color(Color.white * 2f).Fade(0.2f, 0.7f);
                b.Light("Light", new Vector3(0f, 6f, -4f), b.Pal.Core, 30f, 5f, 5f);
            });

            // Three echo slashes appearing one after another along the facing direction (Waning Echo).
            VFXLibrary.Register("moon_echo_line", b =>
            {
                b.Lifetime = 1.4f;
                float[] dist = { 2.5f, 4.5f, 6.5f };
                float[] side = { 0f, -0.9f, 0.8f };
                for (int i = 0; i < 3; i++)
                {
                    float delay = i * 0.2f;
                    float tilt = (i - 1) * 35f;
                    var pos = new Vector3(side[i], 1.1f, dist[i]);
                    var c = Crescent(b, "Echo" + i, pos, new Vector3(90f, 0f, tilt), 2.2f, 0.45f, b.Pal.Bright, delay);
                    c.StartScale = Vector3.one * 0.4f;
                    var core = Crescent(b, "EchoCore" + i, pos + new Vector3(0f, 0f, -0.05f), new Vector3(90f, 0f, tilt), 1.8f, 0.35f, Color.white * 3f, delay + 0.02f);
                    core.StartScale = Vector3.one * 0.3f;
                    CommonVFX.Glint(b, "Glint" + i, Color.white * 4f, 2f, 0.2f, pos, delay);
                    Stars(b, "Stars" + i, 14, pos, 0.8f, 0.8f, delay);
                }
            });

            VFXLibrary.Register("moon_sparkle", b =>
            {
                b.Lifetime = 1.2f;
                Stars(b, "Stars", 20, new Vector3(0f, 1f, 0f), 0.8f, 1f);
            });
        }
    }

    /// <summary>Constant rotation for effect pivots (orbiting crescents).</summary>
    public sealed class ExpandingMeshSpinner : MonoBehaviour, IVfxPart
    {
        public Vector3 DegreesPerSecond;
        private Quaternion _base = Quaternion.identity;
        private bool _init;

        public void Restart()
        {
            if (!_init)
            {
                _base = transform.localRotation;
                _init = true;
            }
            transform.localRotation = _base;
        }

        private void Update() => transform.localRotation *= Quaternion.Euler(DegreesPerSecond * Time.deltaTime);
    }
}
