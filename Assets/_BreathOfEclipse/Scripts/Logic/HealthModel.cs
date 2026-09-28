using System;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Pure health container. Owners (player, enemies, destructibles) wrap it in a component and
    /// translate its events into gameplay feedback.
    /// </summary>
    public sealed class HealthModel
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Normalized => Max <= 0f ? 0f : Current / Max;

        /// <summary>When true, damage is ignored entirely (god mode, cinematic invulnerability).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Raised after damage is applied: (amountApplied, currentHealth).</summary>
        public event Action<float, float> Damaged;
        public event Action<float, float> Healed;
        public event Action Died;
        public event Action Revived;

        public HealthModel(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Max health must be positive.");
            Max = max;
            Current = max;
        }

        /// <summary>Applies damage and returns how much was actually removed.</summary>
        public float ApplyDamage(float amount)
        {
            if (amount <= 0f || IsDead || Invulnerable) return 0f;
            float applied = MathUtil.Min(amount, Current);
            Current -= applied;
            Damaged?.Invoke(applied, Current);
            if (Current <= 0f)
            {
                Current = 0f;
                Died?.Invoke();
            }
            return applied;
        }

        public float Heal(float amount)
        {
            if (amount <= 0f || IsDead) return 0f;
            float applied = MathUtil.Min(amount, Max - Current);
            if (applied <= 0f) return 0f;
            Current += applied;
            Healed?.Invoke(applied, Current);
            return applied;
        }

        /// <summary>Restores the model to full (or a fraction of) health, even from death.</summary>
        public void Revive(float normalized = 1f)
        {
            bool wasDead = IsDead;
            Current = MathUtil.Clamp(Max * MathUtil.Clamp01(normalized), 1f, Max);
            if (wasDead) Revived?.Invoke();
            Healed?.Invoke(0f, Current);
        }

        public void SetMax(float max, bool refill)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = refill ? max : MathUtil.Min(Current, max);
        }
    }
}
