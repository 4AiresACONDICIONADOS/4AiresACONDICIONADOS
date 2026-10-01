using System;

namespace BreathOfEclipse.World
{
    public enum AmbienceLayer
    {
        Wind = 0,
        ForestDay = 1,
        ForestNight = 2,
        VillageDay = 3,
        VillageNight = 4,
        FieldsDay = 5,
        FieldsNight = 6,
        River = 7,
        Waterfall = 8,
        Danger = 9,
        Cave = 10
    }

    /// <summary>
    /// Procedural ambience loops (placeholders until recorded ambiences are added to the audio library): wind,
    /// leaves and birds, crickets, village murmur, insects, frog chorus, brook, waterfall roar, the wrong-feeling
    /// drone of cursed ground, cave drips. Pure math (no engine calls) so it renders on a worker thread.
    /// </summary>
    public static class WorldAmbienceSynth
    {
        public const int SampleRate = 22050;
        public const float Seconds = 12f;

        private sealed class Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return (_s & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
            public float Range(float a, float b) => a + (Next() * 0.5f + 0.5f) * (b - a);
        }

        private struct OnePole
        {
            private float _z;
            public float Low(float x, float a) { _z += a * (x - _z); return _z; }
        }

        private struct BandPass
        {
            private float _x1, _x2, _y1, _y2;
            public float Run(float x, float freq, float q)
            {
                double w0 = 2.0 * Math.PI * Math.Max(20.0, Math.Min(freq, SampleRate * 0.45)) / SampleRate;
                double alpha = Math.Sin(w0) / (2.0 * q);
                double a0 = 1.0 + alpha, a1 = -2.0 * Math.Cos(w0), a2 = 1.0 - alpha;
                double y = (alpha * x - alpha * _x2 - a1 * _y1 - a2 * _y2) / a0;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = (float)y;
                return (float)y;
            }
        }

        private static float Sin(double x) => (float)Math.Sin(x);
        private const double Tau = 2.0 * Math.PI;

        /// <summary>Renders a seamless loop (crossfaded ends), normalised to 0.8 peak.</summary>
        public static float[] Render(AmbienceLayer layer)
        {
            int n = (int)(SampleRate * Seconds);
            int xf = SampleRate; // 1 s crossfade
            var buf = new float[n + xf];
            var rng = new Rng(0x9E3779B9u ^ (uint)((int)layer * 7919 + 13));
            switch (layer)
            {
                case AmbienceLayer.Wind: Wind(buf, rng, 1f); break;
                case AmbienceLayer.ForestDay: Leaves(buf, rng); Birds(buf, rng, 0.55f, 1.7f); break;
                case AmbienceLayer.ForestNight: Wind(buf, rng, 0.35f); Crickets(buf, rng, 4, 0.35f); break;
                case AmbienceLayer.VillageDay: Murmur(buf, rng); Birds(buf, rng, 0.18f, 4f); break;
                case AmbienceLayer.VillageNight: Crickets(buf, rng, 2, 0.25f); Chimes(buf, rng); break;
                case AmbienceLayer.FieldsDay: Insects(buf, rng); Birds(buf, rng, 0.3f, 2.6f); break;
                case AmbienceLayer.FieldsNight: Frogs(buf, rng); Crickets(buf, rng, 3, 0.25f); break;
                case AmbienceLayer.River: Brook(buf, rng); break;
                case AmbienceLayer.Waterfall: Roar(buf, rng); break;
                case AmbienceLayer.Danger: Drone(buf, rng); break;
                case AmbienceLayer.Cave: Cave(buf, rng); break;
            }
            // Seamless loop: blend the tail over the head.
            var outBuf = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float v = buf[i];
                if (i < xf)
                {
                    float k = i / (float)xf;
                    v = buf[i] * k + buf[n + i] * (1f - k);
                }
                outBuf[i] = v;
                peak = Math.Max(peak, Math.Abs(v));
            }
            float norm = 0.8f / peak;
            for (int i = 0; i < n; i++) outBuf[i] *= norm;
            return outBuf;
        }

        private static void Wind(float[] b, Rng r, float gain)
        {
            OnePole lp = default, lp2 = default;
            BandPass whistle = default;
            float p1 = r.Range(0f, 6f), p2 = r.Range(0f, 6f);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)SampleRate;
                float gust = 0.55f + 0.3f * Sin(t * 0.47 + p1) + 0.15f * Sin(t * 1.13 + p2);
                float w = r.Next();
                float body = lp2.Low(lp.Low(w, 0.04f), 0.08f) * 6f;
                float hi = whistle.Run(w, 480f + 160f * Sin(t * 0.3), 6f) * 0.25f * gust * gust;
                b[i] += (body * gust + hi) * gain;
            }
        }

        private static void Leaves(float[] b, Rng r)
        {
            BandPass bp = default;
            OnePole env = default;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)SampleRate;
                float gust = Math.Max(0f, 0.4f + 0.6f * Sin(t * 0.6) * Sin(t * 0.23 + 1.0));
                float g = env.Low(gust, 0.0005f);
                b[i] += bp.Run(r.Next(), 3200f, 0.7f) * (0.15f + 0.85f * g) * 0.5f;
            }
        }

        private static void Birds(float[] b, Rng r, float gain, float meanGap)
        {
            double t = r.Range(0.2f, meanGap);
            double len = b.Length / (double)SampleRate;
            while (t < len - 1.0)
            {
                int chirps = (int)r.Range(2f, 6f);
                float baseF = r.Range(2200f, 3600f);
                float sweep = r.Range(-900f, 1600f);
                float dur = r.Range(0.05f, 0.11f);
                float vol = r.Range(0.4f, 1f) * gain;
                for (int c = 0; c < chirps; c++)
                {
                    double start = t + c * dur * r.Range(1.2f, 1.9f);
                    int s0 = (int)(start * SampleRate), s1 = Math.Min(b.Length, s0 + (int)(dur * SampleRate));
                    double phase = 0;
                    for (int i = s0; i < s1; i++)
                    {
                        float k = (i - s0) / (float)(s1 - s0);
                        phase += Tau * (baseF + sweep * k) / SampleRate;
                        b[i] += Sin(phase) * Sin(k * Math.PI) * vol;
                    }
                }
                t += chirps * 0.15 + r.Range(0.4f, meanGap * 1.6f);
            }
        }

        private static void Crickets(float[] b, Rng r, int count, float gain)
        {
            for (int c = 0; c < count; c++)
            {
                float f = r.Range(3900f, 5200f);
                float rate = r.Range(14f, 22f);
                float period = r.Range(0.6f, 1.4f);
                float duty = r.Range(0.25f, 0.5f);
                float off = r.Range(0f, period);
                float vol = r.Range(0.5f, 1f) * gain;
                for (int i = 0; i < b.Length; i++)
                {
                    double t = i / (double)SampleRate + off;
                    double cyc = t % period;
                    if (cyc > period * duty) continue;
                    float pulse = Math.Max(0f, Sin(Tau * rate * t));
                    float edge = Sin(cyc / (period * duty) * Math.PI);
                    b[i] += Sin(Tau * f * t) * pulse * pulse * edge * vol * 0.3f;
                }
            }
        }

        private static void Murmur(float[] b, Rng r)
        {
            // Distant voices: band-limited noise with syllable-rate modulation, plus soft work clatter.
            BandPass f1 = default, f2 = default;
            OnePole syl = default;
            float target = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                if (i % 2205 == 0) target = Math.Max(0f, r.Next()) ;
                float a = syl.Low(target, 0.002f);
                float w = r.Next();
                float v = f1.Run(w, 520f, 2.5f) * 0.9f + f2.Run(w, 1250f, 3f) * 0.5f;
                b[i] += v * (0.25f + a) * 0.35f;
            }
            double t = 0.5;
            double len = b.Length / (double)SampleRate;
            while (t < len - 0.5)
            {
                int s0 = (int)(t * SampleRate);
                float f = r.Range(300f, 900f);
                for (int i = s0; i < Math.Min(b.Length, s0 + SampleRate / 12); i++)
                {
                    double k = (i - s0) / (double)SampleRate;
                    b[i] += (Sin(Tau * f * k) * 0.5f + r.Next() * 0.4f) * (float)Math.Exp(-k * 45.0) * 0.18f;
                }
                t += r.Range(0.8f, 3f);
            }
        }

        private static void Chimes(float[] b, Rng r)
        {
            float[] scale = { 784f, 880f, 1046.5f, 1174.7f, 1318.5f };
            double t = r.Range(0.5f, 2f);
            double len = b.Length / (double)SampleRate;
            while (t < len - 2.0)
            {
                float f = scale[(int)r.Range(0f, 4.99f)];
                int s0 = (int)(t * SampleRate);
                for (int i = s0; i < Math.Min(b.Length, s0 + SampleRate * 2); i++)
                {
                    double k = (i - s0) / (double)SampleRate;
                    b[i] += (Sin(Tau * f * k) + 0.3f * Sin(Tau * f * 2.76 * k)) * (float)Math.Exp(-k * 2.2) * 0.12f;
                }
                t += r.Range(1.5f, 5f);
            }
        }

        private static void Insects(float[] b, Rng r)
        {
            // Summer field drone: high buzz with slow swells.
            float f = r.Range(5600f, 6600f);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)SampleRate;
                float swell = 0.5f + 0.5f * Sin(t * 0.55) * Sin(t * 0.21 + 0.7);
                float am = 0.5f + 0.5f * Sin(Tau * 92.0 * t);
                b[i] += Sin(Tau * f * t) * am * swell * 0.18f + r.Next() * 0.01f;
            }
        }

        private static void Frogs(float[] b, Rng r)
        {
            for (int c = 0; c < 5; c++)
            {
                float f = r.Range(330f, 620f);
                float croakRate = r.Range(6f, 11f);
                float period = r.Range(0.9f, 2.4f);
                float off = r.Range(0f, period);
                float vol = r.Range(0.4f, 1f);
                for (int i = 0; i < b.Length; i++)
                {
                    double t = i / (double)SampleRate + off;
                    double cyc = t % period;
                    if (cyc > 0.35) continue;
                    float pulse = Math.Max(0f, Sin(Tau * croakRate * t));
                    float env = Sin(cyc / 0.35 * Math.PI);
                    b[i] += (Sin(Tau * f * t) + 0.35f * Sin(Tau * f * 3.1 * t)) * pulse * env * vol * 0.25f;
                }
            }
        }

        private static void Brook(float[] b, Rng r)
        {
            OnePole lp = default;
            BandPass bp = default;
            for (int i = 0; i < b.Length; i++)
            {
                float w = r.Next();
                b[i] += lp.Low(w, 0.25f) * 0.6f + bp.Run(w, 1400f, 0.8f) * 0.4f;
            }
            // Babbling: many tiny bubbles.
            int bubbles = (int)(b.Length / (double)SampleRate * 26);
            for (int k = 0; k < bubbles; k++)
            {
                int s0 = (int)r.Range(0f, b.Length - 2000f);
                float f0 = r.Range(500f, 1600f);
                float vol = r.Range(0.1f, 0.35f);
                double phase = 0;
                for (int i = s0; i < s0 + 1800; i++)
                {
                    double tt = (i - s0) / (double)SampleRate;
                    phase += Tau * f0 * (1.0 + tt * 12.0) / SampleRate;
                    b[i] += Sin(phase) * (float)Math.Exp(-tt * 70.0) * vol;
                }
            }
        }

        private static void Roar(float[] b, Rng r)
        {
            OnePole lp = default, rumble = default;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)SampleRate;
                float w = r.Next();
                float swell = 0.85f + 0.15f * Sin(t * 0.9);
                b[i] += (lp.Low(w, 0.35f) * 0.8f + rumble.Low(w, 0.02f) * 5f) * swell;
            }
        }

        private static void Drone(float[] b, Rng r)
        {
            BandPass howl = default;
            OnePole lp = default;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)SampleRate;
                float drone = Sin(Tau * 48.0 * t) * 0.6f + Sin(Tau * 50.7 * t) * 0.5f + Sin(Tau * 97.0 * t) * 0.15f;
                float hf = 240f + 160f * Sin(t * 0.37);
                float h = howl.Run(lp.Low(r.Next(), 0.3f), hf, 4f) * (0.5f + 0.5f * Sin(t * 0.21 + 2.0)) * 1.4f;
                b[i] += drone * 0.5f + h;
            }
            // Slow, wrong heartbeat.
            double beat = 0.6;
            double len = b.Length / (double)SampleRate;
            while (beat < len - 0.5)
            {
                for (int pulse = 0; pulse < 2; pulse++)
                {
                    int s0 = (int)((beat + pulse * 0.28) * SampleRate);
                    for (int i = s0; i < Math.Min(b.Length, s0 + SampleRate / 5); i++)
                    {
                        double k = (i - s0) / (double)SampleRate;
                        b[i] += Sin(Tau * (52.0 - 20.0 * k) * k) * (float)Math.Exp(-k * 18.0) * (pulse == 0 ? 0.9f : 0.6f);
                    }
                }
                beat += r.Range(1.6f, 2.4f);
            }
        }

        private static void Cave(float[] b, Rng r)
        {
            OnePole lp = default;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)SampleRate;
                b[i] += lp.Low(r.Next(), 0.01f) * 2.5f + Sin(Tau * 61.0 * t) * 0.04f;
            }
            double d = 0.3;
            double len = b.Length / (double)SampleRate;
            while (d < len - 1.0)
            {
                float f = r.Range(1300f, 2600f);
                float vol = r.Range(0.3f, 0.8f);
                for (int echo = 0; echo < 3; echo++)
                {
                    int s0 = (int)((d + echo * 0.21) * SampleRate);
                    float ev = vol * (float)Math.Pow(0.4, echo);
                    for (int i = s0; i < Math.Min(b.Length, s0 + SampleRate / 6); i++)
                    {
                        double k = (i - s0) / (double)SampleRate;
                        b[i] += Sin(Tau * (f + 900.0 * Math.Exp(-k * 60.0)) * k) * (float)Math.Exp(-k * 30.0) * ev;
                    }
                }
                d += r.Range(0.6f, 2.2f);
            }
        }
    }
}
