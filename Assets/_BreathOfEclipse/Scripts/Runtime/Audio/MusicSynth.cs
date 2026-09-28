using System;

namespace BreathOfEclipse.Audio
{
    /// <summary>
    /// Procedural placeholder music (thread-safe, pure math). Generates seamless loops: a calm night theme,
    /// combat taiko groove and two boss phases (phase 2 faster, harsher, more percussion).
    /// Generated on a worker thread; the AudioClip is created on the main thread.
    /// </summary>
    public static class MusicSynth
    {
        public const int SampleRate = 22050;

        // Minor pentatonic-ish scale (in semitones from root) for an original, vaguely eastern mood.
        private static readonly int[] Scale = { 0, 3, 5, 7, 10, 12, 15, 17 };

        private struct TrackSpec
        {
            public float Bpm;
            public int Bars;
            public float Root;
            public float DrumAmount;
            public float PadAmount;
            public float LeadAmount;
            public float BassAmount;
            public float Grit;
            public int Seed;
        }

        private static TrackSpec Spec(string id)
        {
            switch (id)
            {
                case "menu": return new TrackSpec { Bpm = 72, Bars = 8, Root = 110f, DrumAmount = 0.15f, PadAmount = 1f, LeadAmount = 0.6f, BassAmount = 0.3f, Seed = 3 };
                case "forest": return new TrackSpec { Bpm = 80, Bars = 8, Root = 98f, DrumAmount = 0.25f, PadAmount = 0.9f, LeadAmount = 0.55f, BassAmount = 0.4f, Seed = 7 };
                case "combat": return new TrackSpec { Bpm = 128, Bars = 8, Root = 98f, DrumAmount = 1f, PadAmount = 0.45f, LeadAmount = 0.6f, BassAmount = 0.8f, Grit = 0.2f, Seed = 11 };
                case "boss_phase1": return new TrackSpec { Bpm = 112, Bars = 8, Root = 82.4f, DrumAmount = 1.1f, PadAmount = 0.7f, LeadAmount = 0.5f, BassAmount = 1f, Grit = 0.35f, Seed = 17 };
                case "boss_phase2": return new TrackSpec { Bpm = 146, Bars = 8, Root = 87.3f, DrumAmount = 1.3f, PadAmount = 0.6f, LeadAmount = 0.8f, BassAmount = 1.1f, Grit = 0.6f, Seed = 23 };
                case "victory": return new TrackSpec { Bpm = 90, Bars = 4, Root = 130.8f, DrumAmount = 0.4f, PadAmount = 1f, LeadAmount = 0.9f, BassAmount = 0.4f, Seed = 29 };
                default: return new TrackSpec { Bpm = 90, Bars = 8, Root = 110f, DrumAmount = 0.5f, PadAmount = 0.8f, LeadAmount = 0.5f, BassAmount = 0.5f, Seed = 1 };
            }
        }

        private static float Note(float root, int degree) => root * (float)Math.Pow(2.0, degree / 12.0);

        public static float[] Generate(string id)
        {
            var s = Spec(id);
            float beat = 60f / s.Bpm;
            int beatsTotal = s.Bars * 4;
            int n = (int)(beatsTotal * beat * SampleRate);
            var data = new float[n];
            var rng = new Random(s.Seed);

            // Pre-compose a lead melody (one note per half beat) and chord roots per bar.
            int steps = beatsTotal * 2;
            var melody = new int[steps];
            int deg = 2;
            for (int i = 0; i < steps; i++)
            {
                deg = Math.Max(0, Math.Min(Scale.Length - 1, deg + rng.Next(-2, 3)));
                melody[i] = rng.NextDouble() < 0.3 ? -1 : Scale[deg];
            }
            int[] chordRoots = { 0, 0, -4, -2, 0, 3, -2, -5 };

            float lpState = 0f;
            uint noise = (uint)(s.Seed * 7919 + 1);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float beatPos = t / beat;
                int beatIndex = (int)beatPos;
                float inBeat = beatPos - beatIndex;
                int bar = (beatIndex / 4) % chordRoots.Length;
                float chordRoot = Note(s.Root, chordRoots[bar]);

                // Pad: detuned sines on the chord (root, minor third, fifth), slow swell per bar.
                float barPos = (beatPos % 4f) / 4f;
                float swell = 0.6f + 0.4f * (float)Math.Sin(barPos * Math.PI);
                float pad = 0f;
                pad += (float)Math.Sin(2 * Math.PI * chordRoot * t) + (float)Math.Sin(2 * Math.PI * chordRoot * 1.004f * t);
                pad += 0.7f * (float)Math.Sin(2 * Math.PI * chordRoot * 1.189f * t);
                pad += 0.6f * (float)Math.Sin(2 * Math.PI * chordRoot * 1.498f * t);
                pad *= 0.12f * s.PadAmount * swell;

                // Bass: pulses on each beat.
                float bassEnv = (float)Math.Exp(-inBeat * 5f);
                float bassF = chordRoot * 0.5f;
                float bassWave = (float)Math.Sin(2 * Math.PI * bassF * t);
                bassWave += s.Grit * (float)Math.Sin(2 * Math.PI * bassF * 2f * t) * 0.5f;
                float bass = bassWave * bassEnv * 0.35f * s.BassAmount;

                // Lead: breathy flute-like sine with vibrato.
                int step = (int)(beatPos * 2f) % steps;
                float inStep = beatPos * 2f - (int)(beatPos * 2f);
                float lead = 0f;
                if (melody[step] >= 0)
                {
                    float f = Note(s.Root * 4f, melody[step]);
                    float vib = 1f + 0.006f * (float)Math.Sin(2 * Math.PI * 5.5f * t);
                    float env = Math.Min(1f, inStep * 12f) * (float)Math.Exp(-inStep * 2.2f);
                    lead = ((float)Math.Sin(2 * Math.PI * f * vib * t) + 0.25f * (float)Math.Sin(2 * Math.PI * f * 2f * vib * t)) * env * 0.16f * s.LeadAmount;
                }

                // Drums: taiko on beats 1 and 3 (+ syncopation), rim clicks on off-beats.
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float white = (noise & 0xFFFF) / 32768f - 1f;
                float drums = 0f;
                int beatInBar = beatIndex % 4;
                bool taiko = beatInBar == 0 || beatInBar == 2 || (s.DrumAmount > 1f && beatInBar == 3 && inBeat > 0.5f);
                if (taiko)
                {
                    float lt = inBeat * beat;
                    if (beatInBar == 3) lt = (inBeat - 0.5f) * beat;
                    float drumEnv = (float)Math.Exp(-lt * 9f);
                    drums += (float)Math.Sin(2 * Math.PI * (70f + 60f * (float)Math.Exp(-lt * 30f)) * lt) * drumEnv * 0.55f;
                    drums += white * (float)Math.Exp(-lt * 60f) * 0.2f;
                }
                float off = (beatPos * 2f) % 1f;
                if (s.DrumAmount > 0.5f)
                {
                    float clickT = off * beat * 0.5f;
                    drums += white * (float)Math.Exp(-clickT * 90f) * 0.08f;
                }
                drums *= s.DrumAmount;

                float mix = pad + bass + lead + drums;
                // Gentle low-pass to soften synthetic edges.
                lpState += 0.35f * (mix - lpState);
                data[i] = lpState;
            }

            // Normalize and crossfade the loop point.
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(data[i]));
            float norm = 0.7f / peak;
            int xf = Math.Min(SampleRate / 4, n / 8);
            for (int i = 0; i < n; i++) data[i] *= norm;
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                data[i] = data[i] * k + data[n - xf + i] * (1f - k);
            }
            var trimmed = new float[n - xf];
            Array.Copy(data, trimmed, trimmed.Length);
            return trimmed;
        }
    }
}
