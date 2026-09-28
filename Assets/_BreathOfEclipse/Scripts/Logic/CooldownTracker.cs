using System.Collections.Generic;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Tracks cooldowns by id against an externally supplied clock, which keeps it deterministic and testable.
    /// </summary>
    public sealed class CooldownTracker
    {
        private struct Entry
        {
            public float Start;
            public float Duration;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        /// <summary>Multiplier applied to newly started cooldowns (e.g. 0.8 = 20% cooldown reduction).</summary>
        public float DurationMultiplier { get; set; } = 1f;

        /// <summary>When true every cooldown reports ready (debug).</summary>
        public bool Disabled { get; set; }

        public void Start(string id, float duration, float now)
        {
            if (string.IsNullOrEmpty(id) || duration <= 0f) return;
            _entries[id] = new Entry { Start = now, Duration = duration * DurationMultiplier };
        }

        public bool IsReady(string id, float now) => Remaining(id, now) <= 0f;

        public float Remaining(string id, float now)
        {
            if (Disabled || string.IsNullOrEmpty(id)) return 0f;
            if (!_entries.TryGetValue(id, out var e)) return 0f;
            float remaining = e.Start + e.Duration - now;
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>1 = just started, 0 = ready.</summary>
        public float NormalizedRemaining(string id, float now)
        {
            if (Disabled || string.IsNullOrEmpty(id)) return 0f;
            if (!_entries.TryGetValue(id, out var e) || e.Duration <= 0f) return 0f;
            return MathUtil.Clamp01(Remaining(id, now) / e.Duration);
        }

        public void Reset(string id) => _entries.Remove(id);

        public void ResetAll() => _entries.Clear();

        /// <summary>Shortens every running cooldown by <paramref name="seconds"/> (e.g. on parry).</summary>
        public void ReduceAll(float seconds)
        {
            if (seconds <= 0f) return;
            var keys = new List<string>(_entries.Keys);
            foreach (var key in keys)
            {
                var e = _entries[key];
                e.Start -= seconds;
                _entries[key] = e;
            }
        }
    }
}
