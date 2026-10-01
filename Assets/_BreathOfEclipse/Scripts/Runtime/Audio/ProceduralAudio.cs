using System;
using UnityEngine;

namespace BreathOfEclipse.Audio
{
    /// <summary>
    /// Placeholder sound synthesis (no imported audio needed). Every clip is generated from noise, oscillators,
    /// filters and envelopes. Replace any id with a real clip through <see cref="Data.AudioLibraryData"/>.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 32000;

        private sealed class Rng
        {
            private uint _state;
            public Rng(uint seed) { _state = seed == 0 ? 1u : seed; }
            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        private struct OnePole
        {
            private float _z;
            public float LowPass(float x, float a) { _z += a * (x - _z); return _z; }
        }

        private struct Biquad
        {
            private float _x1, _x2, _y1, _y2;
            public float BandPass(float x, float freq, float q, int sr)
            {
                float w0 = 2f * Mathf.PI * Mathf.Clamp(freq, 20f, sr * 0.45f) / sr;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float b0 = alpha, b1 = 0f, b2 = -alpha;
                float a0 = 1f + alpha, a1 = -2f * Mathf.Cos(w0), a2 = 1f - alpha;
                float y = (b0 * x + b1 * _x1 + b2 * _x2 - a1 * _y1 - a2 * _y2) / a0;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
                return y;
            }
        }

        private static float Env(float t, float attack, float decay)
        {
            if (t < attack) return t / Mathf.Max(0.0001f, attack);
            return Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));
        }

        private static AudioClip Make(string name, float seconds, Func<float, int, float> sample)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                data[i] = sample(i / (float)SampleRate, i);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float norm = 0.85f / peak;
            int fade = Mathf.Min(n / 4, SampleRate / 100);
            for (int i = 0; i < n; i++)
            {
                data[i] *= norm;
                if (i > n - fade) data[i] *= (n - i) / (float)fade;
            }
            var clip = AudioClip.Create("BoE_" + name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Returns a synthesized clip for a sound id, or null if unknown.</summary>
        /// <summary>Creature vocalisation: saw growl through a low-pass with vibrato, breath noise and a pitch glide.</summary>
        private static AudioClip Growl(string id, float len, float baseFreq, float vibrato, float noise, float glide)
        {
            var rng = new Rng((uint)id.GetHashCode());
            OnePole lp = default;
            Biquad bp = default;
            return Make(id, len, (t, i) =>
            {
                float k = Mathf.Clamp01(t / len);
                float f = baseFreq * (1f + glide * k) * (1f + 0.07f * Mathf.Sin(t * vibrato));
                float saw = ((t * f) % 1f) * 2f - 1f;
                float body = lp.LowPass(saw + rng.Next() * noise, 0.12f) * 2.2f;
                float breath = bp.BandPass(rng.Next(), 900f + 600f * k, 2f, SampleRate) * noise * 0.4f;
                return (body + breath) * Mathf.Sin(k * Mathf.PI);
            });
        }

        /// <summary>
        /// Human inhale before a form: turbulent air through the mouth (pink-ish noise shaped by two vocal-tract
        /// formants that open up as the chest fills), a little flutter, rising intensity and a clean stop when the
        /// lungs are full. <paramref name="tract"/> &lt; 1 = deeper / fuller, &gt; 1 = sharper (quick nose breath).
        /// </summary>
        private static AudioClip Inhale(string id, float len, float tract, float hiss)
        {
            var rng = new Rng((uint)id.GetHashCode());
            OnePole pink = default;
            OnePole rumble = default;
            Biquad f1 = default, f2 = default, f3 = default;
            float flutterPhase = 0f;
            return Make(id, len, (t, i) =>
            {
                float k = Mathf.Clamp01(t / len);
                float white = rng.Next();
                float air = pink.LowPass(white, 0.35f) * 1.6f;
                float open = Mathf.Lerp(0.85f, 1.15f, k) * tract;
                float body = f1.BandPass(air, 900f * open, 1.6f, SampleRate) * 1.2f + f2.BandPass(air, 2300f * open, 2.4f, SampleRate) * 0.8f;
                float edge = f3.BandPass(white, 5200f * tract, 1.1f, SampleRate) * hiss;
                float low = rumble.LowPass(white, 0.02f) * 3f;
                flutterPhase += (9f + 4f * rng.Next()) / SampleRate;
                float flutter = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * flutterPhase);
                // Swell in, keep filling, stop quickly at the top of the breath.
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.28f)) * Mathf.Lerp(0.75f, 1f, k) * Mathf.Clamp01((1f - k) / 0.07f);
                return (body + edge + low) * flutter * env;
            });
        }

        public static AudioClip Generate(string id)
        {
            var rng = new Rng((uint)id.GetHashCode());
            OnePole lp = default;
            OnePole lp2 = default;
            Biquad bp = default;
            Biquad bp2 = default;
            switch (id)
            {
                case "slash":
                    return Make(id, 0.22f, (t, i) =>
                    {
                        float sweep = Mathf.Lerp(5200f, 900f, t / 0.22f);
                        return bp.BandPass(rng.Next(), sweep, 2.2f, SampleRate) * Env(t, 0.015f, 0.06f);
                    });
                case "slash_heavy":
                    return Make(id, 0.38f, (t, i) =>
                    {
                        float sweep = Mathf.Lerp(3000f, 350f, t / 0.38f);
                        return bp.BandPass(rng.Next(), sweep, 1.6f, SampleRate) * Env(t, 0.03f, 0.11f);
                    });
                case "whoosh":
                case "dash":
                case "dodge":
                    return Make(id, 0.35f, (t, i) =>
                    {
                        float f = 400f + Mathf.Sin(t / 0.35f * Mathf.PI) * 1600f;
                        return bp.BandPass(rng.Next(), f, 1.2f, SampleRate) * Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI);
                    });
                case "hit":
                    return Make(id, 0.2f, (t, i) =>
                    {
                        float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 60f, t / 0.2f) * t) * Env(t, 0.002f, 0.05f);
                        float crack = lp.LowPass(rng.Next(), 0.5f) * Env(t, 0.001f, 0.015f);
                        return body * 0.9f + crack * 0.7f;
                    });
                case "hit_heavy":
                    return Make(id, 0.4f, (t, i) =>
                    {
                        float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 40f, t / 0.4f) * t) * Env(t, 0.003f, 0.12f);
                        float crunch = lp.LowPass(rng.Next(), 0.3f) * Env(t, 0.001f, 0.04f);
                        return body + crunch * 0.8f;
                    });
                case "hit_crit":
                    return Make(id, 0.6f, (t, i) =>
                    {
                        float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 45f, t / 0.6f) * t) * Env(t, 0.002f, 0.15f);
                        float ring = Mathf.Sin(2f * Mathf.PI * 2400f * t) * Env(t, 0.001f, 0.12f) * 0.3f;
                        float noise = lp.LowPass(rng.Next(), 0.4f) * Env(t, 0.001f, 0.05f);
                        return body + ring + noise;
                    });
                case "block":
                    return Make(id, 0.5f, (t, i) =>
                    {
                        float metal = Mathf.Sin(2f * Mathf.PI * 523f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1311f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 2147f * t) * 0.25f;
                        return metal * Env(t, 0.001f, 0.09f) + rng.Next() * Env(t, 0.0005f, 0.01f) * 0.6f;
                    });
                case "parry":
                    return Make(id, 1.1f, (t, i) =>
                    {
                        float metal = Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2713f * t) * 0.35f
                            + Mathf.Sin(2f * Mathf.PI * 4410f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.3f;
                        float shimmer = 1f + 0.2f * Mathf.Sin(2f * Mathf.PI * 11f * t);
                        return metal * shimmer * Env(t, 0.001f, 0.28f) + rng.Next() * Env(t, 0.0005f, 0.012f) * 0.8f;
                    });
                case "perfect_dodge":
                    return Make(id, 0.9f, (t, i) =>
                    {
                        float rev = bp.BandPass(rng.Next(), Mathf.Lerp(300f, 3000f, t / 0.9f), 2f, SampleRate) * Mathf.Clamp01(t / 0.5f) * Env(Mathf.Max(0f, t - 0.5f), 0.001f, 0.12f);
                        float chime = Mathf.Sin(2f * Mathf.PI * 1318f * t) * Env(Mathf.Max(0f, t - 0.05f), 0.005f, 0.3f) * 0.4f;
                        return rev + chime;
                    });
                case "jump":
                    return Make(id, 0.15f, (t, i) => bp.BandPass(rng.Next(), 900f, 1.5f, SampleRate) * Env(t, 0.005f, 0.04f));
                case "land":
                case "footstep":
                    return Make(id, 0.14f, (t, i) => (lp.LowPass(rng.Next(), 0.15f) * 2f + Mathf.Sin(2f * Mathf.PI * 80f * t)) * Env(t, 0.002f, 0.03f));
                case "water":
                    return Make(id, 0.9f, (t, i) =>
                    {
                        float noise = bp.BandPass(rng.Next(), 1200f + 600f * Mathf.Sin(t * 30f), 1.1f, SampleRate) * Env(t, 0.02f, 0.25f);
                        float bubbles = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            float start = k * 0.12f;
                            float lt = t - start;
                            if (lt > 0f) bubbles += Mathf.Sin(2f * Mathf.PI * (500f + k * 170f + lt * 2200f) * lt) * Env(lt, 0.002f, 0.04f) * 0.4f;
                        }
                        return noise + bubbles;
                    });
                case "water_big":
                    return Make(id, 1.6f, (t, i) =>
                    {
                        float roar = lp.LowPass(rng.Next(), 0.08f) * 3f * Env(t, 0.08f, 0.5f);
                        float splash = bp.BandPass(rng.Next(), 2500f, 0.8f, SampleRate) * Env(t, 0.01f, 0.3f);
                        return roar + splash;
                    });
                case "fire":
                    return Make(id, 0.9f, (t, i) =>
                    {
                        float whoosh = lp.LowPass(rng.Next(), 0.12f) * 2.5f * Env(t, 0.05f, 0.3f);
                        float crackle = rng.Next() > 0.992f ? rng.Next() * 2f : 0f;
                        return whoosh + crackle * Env(t, 0.01f, 0.5f);
                    });
                case "fire_big":
                case "explosion":
                    return Make(id, 1.8f, (t, i) =>
                    {
                        float boom = lp.LowPass(rng.Next(), 0.04f) * 6f * Env(t, 0.005f, 0.5f);
                        float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 30f, t / 1.8f) * t) * Env(t, 0.005f, 0.35f);
                        float crackle = rng.Next() > 0.985f ? rng.Next() * 1.5f * Env(t, 0.05f, 0.8f) : 0f;
                        return boom + sub + crackle;
                    });
                case "thunder":
                    return Make(id, 0.7f, (t, i) =>
                    {
                        float zap = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (180f + 90f * Mathf.Sin(t * 70f)) * t)) * 0.4f * Env(t, 0.002f, 0.12f);
                        float crack = lp2.LowPass(rng.Next(), 0.9f) * Env(t, 0.0005f, 0.03f);
                        float buzz = bp.BandPass(rng.Next(), 3500f, 3f, SampleRate) * Env(t, 0.01f, 0.2f);
                        return zap + crack * 1.2f + buzz;
                    });
                case "thunder_big":
                    return Make(id, 2f, (t, i) =>
                    {
                        float crack = rng.Next() * Env(t, 0.0005f, 0.05f) * 1.5f;
                        float rumble = lp.LowPass(rng.Next(), 0.02f) * 8f * Env(t, 0.05f, 0.7f);
                        float zap = bp.BandPass(rng.Next(), 4000f, 4f, SampleRate) * Env(t, 0.002f, 0.2f);
                        return crack + rumble + zap;
                    });
                case "wind":
                    return Make(id, 0.8f, (t, i) => bp.BandPass(rng.Next(), 600f + 900f * Mathf.Sin(t / 0.8f * Mathf.PI), 2.5f, SampleRate) * Mathf.Sin(Mathf.Clamp01(t / 0.8f) * Mathf.PI));
                case "wind_big":
                    return Make(id, 2f, (t, i) =>
                    {
                        float f = 300f + 700f * Mathf.Sin(t * 3f) * Mathf.Sin(t * 1.3f) + 700f;
                        return (bp.BandPass(rng.Next(), f, 3f, SampleRate) + lp.LowPass(rng.Next(), 0.03f) * 2f) * Mathf.Sin(Mathf.Clamp01(t / 2f) * Mathf.PI);
                    });
                case "moon":
                    return Make(id, 1.2f, (t, i) =>
                    {
                        float bell = 0f;
                        float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
                        for (int k = 0; k < partials.Length; k++) bell += Mathf.Sin(2f * Mathf.PI * 660f * partials[k] * t) * Env(t, 0.002f, 0.4f / (k + 1)) / (k + 1);
                        float swish = bp.BandPass(rng.Next(), 2000f, 1.5f, SampleRate) * Env(t, 0.03f, 0.12f) * 0.5f;
                        return bell + swish;
                    });
                case "moon_big":
                    return Make(id, 2.4f, (t, i) =>
                    {
                        float bell = 0f;
                        float[] partials = { 1f, 2.76f, 5.4f };
                        for (int k = 0; k < partials.Length; k++) bell += Mathf.Sin(2f * Mathf.PI * 330f * partials[k] * t) * Env(t, 0.01f, 0.9f / (k + 1)) / (k + 1);
                        float pad = Mathf.Sin(2f * Mathf.PI * 110f * t) * Env(t, 0.3f, 0.8f) * 0.5f;
                        return bell + pad;
                    });
                case "charge":
                    return Make(id, 0.8f, (t, i) =>
                    {
                        float f = Mathf.Lerp(200f, 900f, t / 0.8f);
                        return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.4f + bp.BandPass(rng.Next(), f * 3f, 4f, SampleRate)) * Mathf.Clamp01(t / 0.8f);
                    });
                case "ultimate":
                    return Make(id, 2.6f, (t, i) =>
                    {
                        float sub = Mathf.Sin(2f * Mathf.PI * 38f * t) * Env(t, 0.02f, 1.2f);
                        float boom = lp.LowPass(rng.Next(), 0.03f) * 6f * Env(t, 0.005f, 0.6f);
                        float choir = (Mathf.Sin(2f * Mathf.PI * 220f * t) + Mathf.Sin(2f * Mathf.PI * 277f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.6f) * Env(t, 0.4f, 1f) * 0.25f;
                        return sub + boom + choir;
                    });
                case "callout":
                    return Make(id, 0.7f, (t, i) =>
                    {
                        float f = 196f * (1f + 0.02f * Mathf.Sin(t * 30f));
                        float voice = bp.BandPass(Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)), 800f, 3f, SampleRate) + bp2.BandPass(Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)), 1200f, 4f, SampleRate);
                        return voice * Env(t, 0.03f, 0.18f);
                    });
                case "enemy_swipe":
                    return Make(id, 0.3f, (t, i) => bp.BandPass(rng.Next(), Mathf.Lerp(2500f, 600f, t / 0.3f), 1.4f, SampleRate) * Env(t, 0.02f, 0.08f));
                case "enemy_roar":
                case "boss_roar":
                {
                    float len = id == "boss_roar" ? 2f : 1f;
                    float basef = id == "boss_roar" ? 55f : 95f;
                    return Make(id, len, (t, i) =>
                    {
                        float f = basef * (1f + 0.08f * Mathf.Sin(t * 23f));
                        float saw = ((t * f) % 1f) * 2f - 1f;
                        float growl = lp.LowPass(saw + rng.Next() * 0.6f, 0.08f) * 2f;
                        return growl * Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                    });
                }
                case "enemy_death":
                    return Make(id, 1.2f, (t, i) =>
                    {
                        float hiss = bp.BandPass(rng.Next(), Mathf.Lerp(3000f, 800f, t / 1.2f), 1.2f, SampleRate) * Env(t, 0.05f, 0.4f);
                        float low = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, t / 1.2f) * t) * Env(t, 0.01f, 0.3f);
                        return hiss + low;
                    });
                // Creature voices (EnemyVoice): short growls and pain cries per species.
                case "nightspawn_grunt": return Growl(id, 0.32f, 150f, 31f, 0.7f, 0.25f);
                case "nightspawn_hurt": return Growl(id, 0.28f, 230f, 45f, 0.9f, -0.35f);
                case "oni_grunt": return Growl(id, 0.55f, 66f, 17f, 0.5f, 0.2f);
                case "oni_hurt": return Growl(id, 0.45f, 95f, 26f, 0.7f, -0.3f);
                case "telegraph":
                    return Make(id, 0.45f, (t, i) => (Mathf.Sin(2f * Mathf.PI * 1480f * t) + Mathf.Sin(2f * Mathf.PI * 1568f * t)) * Env(t, 0.005f, 0.12f) * 0.5f);
                case "crate_break":
                    return Make(id, 0.5f, (t, i) =>
                    {
                        float crack = bp.BandPass(rng.Next(), 1600f, 1.2f, SampleRate) * Env(t, 0.001f, 0.06f);
                        float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(200f, 90f, t / 0.5f) * t) * Env(t, 0.002f, 0.07f);
                        float rattles = rng.Next() > 0.99f ? rng.Next() * Env(t, 0.05f, 0.2f) : 0f;
                        return crack + thud + rattles;
                    });
                case "inhale_short": return Inhale(id, 0.24f, 1.18f, 0.55f);
                case "inhale": return Inhale(id, 0.42f, 1f, 0.4f);
                case "inhale_deep": return Inhale(id, 0.66f, 0.86f, 0.32f);
                case "breath_focus":
                    return Make(id, 0.35f, (t, i) =>
                    {
                        // Held breath turning into focus: a soft low swell and a faint high shimmer.
                        float swell = Mathf.Sin(2f * Mathf.PI * 146.8f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.3f;
                        float shimmer = bp.BandPass(rng.Next(), 6200f, 3f, SampleRate) * 0.5f;
                        return (swell + shimmer) * Env(t, 0.03f, 0.12f);
                    });
                case "breath_full":
                    return Make(id, 0.8f, (t, i) => (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.3f) * Env(t, 0.01f, 0.25f) * 0.5f);
                case "ui_click":
                    return Make(id, 0.06f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1200f * t) * Env(t, 0.001f, 0.015f));
                case "ui_hover":
                    return Make(id, 0.05f, (t, i) => Mathf.Sin(2f * Mathf.PI * 2200f * t) * Env(t, 0.001f, 0.01f) * 0.5f);
                case "ui_confirm":
                    return Make(id, 0.35f, (t, i) => (Mathf.Sin(2f * Mathf.PI * (t < 0.1f ? 880f : 1320f) * t)) * Env(t, 0.002f, 0.12f));
                // ---- v0.5 world one-shots (ambient creatures, village work, bells).
                case "amb_bird":
                    return Make(id, 0.9f, (t, i) =>
                    {
                        // Three to five quick upward chirps.
                        float seg = t / 0.17f;
                        int n = (int)seg;
                        if (n > 4) return 0f;
                        float k = seg - n;
                        float f = 2600f + 1500f * k + 300f * n;
                        return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Sin(Mathf.Clamp01(k / 0.55f) * Mathf.PI) * 0.6f;
                    });
                case "amb_crow":
                    return Make(id, 0.5f, (t, i) =>
                    {
                        float k = t / 0.5f;
                        float saw = ((t * (520f - 140f * k)) % 1f) * 2f - 1f;
                        return bp.BandPass(saw + rng.Next() * 0.3f, 1300f, 1.4f, SampleRate) * Mathf.Sin(k * Mathf.PI) * 2f;
                    });
                case "amb_owl":
                    return Make(id, 1.4f, (t, i) =>
                    {
                        // "hoo — hoo-hoo": soft low sine bursts.
                        float a = t < 0.35f ? Mathf.Sin(t / 0.35f * Mathf.PI) : t > 0.6f && t < 0.85f ? Mathf.Sin((t - 0.6f) / 0.25f * Mathf.PI) : t > 0.95f && t < 1.35f ? Mathf.Sin((t - 0.95f) / 0.4f * Mathf.PI) : 0f;
                        return (Mathf.Sin(2f * Mathf.PI * 370f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 740f * t)) * a * a;
                    });
                case "amb_frog":
                    return Make(id, 0.45f, (t, i) =>
                    {
                        float pulse = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 22f * t));
                        float body = Mathf.Sin(2f * Mathf.PI * (420f - 90f * t) * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 1250f * t);
                        return body * pulse * Mathf.Sin(t / 0.45f * Mathf.PI);
                    });
                case "amb_growl_far":
                    return Make(id, 1.8f, (t, i) =>
                    {
                        float k = t / 1.8f;
                        float saw = ((t * (58f + 10f * Mathf.Sin(t * 9f))) % 1f) * 2f - 1f;
                        float body = lp.LowPass(saw + rng.Next() * 0.5f, 0.04f) * 3f;
                        return body * Mathf.Sin(k * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 13f));
                    });
                case "hammer_clink":
                    return Make(id, 0.7f, (t, i) =>
                    {
                        float ring = Mathf.Sin(2f * Mathf.PI * 1870f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 4320f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 6110f * t);
                        return (ring * Env(t, 0.001f, 0.16f) + rng.Next() * Env(t, 0.0005f, 0.01f)) * 0.6f;
                    });
                case "wood_chop":
                    return Make(id, 0.4f, (t, i) =>
                    {
                        float thud = Mathf.Sin(2f * Mathf.PI * (160f - 60f * t) * t) * Env(t, 0.001f, 0.06f);
                        float crack = bp.BandPass(rng.Next(), 1800f, 1.2f, SampleRate) * Env(t, 0.0005f, 0.03f) * 3f;
                        return thud + crack;
                    });
                case "amb_drip":
                    return Make(id, 1.2f, (t, i) =>
                    {
                        // Drop plink with two cave echoes.
                        float s0 = Mathf.Sin(2f * Mathf.PI * (1900f + 900f * Mathf.Exp(-t * 60f)) * t) * Env(t, 0.0005f, 0.05f);
                        float t1 = t - 0.23f, t2 = t - 0.47f;
                        float s1 = t1 > 0f ? Mathf.Sin(2f * Mathf.PI * 1900f * t1) * Env(t1, 0.001f, 0.07f) * 0.35f : 0f;
                        float s2 = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 1900f * t2) * Env(t2, 0.001f, 0.09f) * 0.15f : 0f;
                        return s0 + s1 + s2;
                    });
                case "bell":
                    return Make(id, 5f, (t, i) =>
                    {
                        // Temple bell: inharmonic partials, slow beating, long decay.
                        const float f = 196f;
                        float b = Mathf.Sin(2f * Mathf.PI * f * t) * (1f + 0.15f * Mathf.Sin(t * 5f)) + 0.6f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 0.9f)
                                  + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t * 1.8f) + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 8.93f * t) * Mathf.Exp(-t * 3f);
                        return b * Env(t, 0.004f, 1.6f) + rng.Next() * Env(t, 0.001f, 0.015f) * 0.4f;
                    });
                case "bell_alarm":
                    return Make(id, 1.1f, (t, i) =>
                    {
                        // Watchtower alarm: a hard strike on a small bronze bell.
                        const float f = 640f;
                        float b = Mathf.Sin(2f * Mathf.PI * f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * f * 2.4f * t) * Mathf.Exp(-t * 3f) + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 4.1f * t) * Mathf.Exp(-t * 6f);
                        return b * Env(t, 0.002f, 0.35f) + rng.Next() * Env(t, 0.0005f, 0.008f) * 0.6f;
                    });
                case "door_slide":
                    return Make(id, 0.55f, (t, i) =>
                    {
                        float k = t / 0.55f;
                        float rub = bp.BandPass(rng.Next(), 700f + 300f * k, 1.5f, SampleRate) * (0.6f + 0.4f * Mathf.Sin(t * 70f));
                        return rub * Mathf.Sin(k * Mathf.PI) + Mathf.Sin(2f * Mathf.PI * 140f * t) * (k > 0.88f ? Env(t - 0.484f, 0.001f, 0.04f) : 0f) * 1.5f;
                    });
                default:
                    return null;
            }
        }
    }
}
