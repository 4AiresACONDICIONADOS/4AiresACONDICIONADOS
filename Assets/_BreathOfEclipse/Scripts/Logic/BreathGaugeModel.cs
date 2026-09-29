using System;

namespace BreathOfEclipse.Core
{
    /// <summary>Where breath energy came from. Used for tuning and HUD feedback.</summary>
    public enum BreathSource
    {
        Hit = 0,
        ComboMilestone = 1,
        PerfectDodge = 2,
        Parry = 3,
        DamageTaken = 4,
        Kill = 5,
        Debug = 6,
        /// <summary>Slow recovery from normal breathing (up to <see cref="BreathGaugeModel.RestingCap"/>).</summary>
        Resting = 7,
        /// <summary>A technique interrupted before it committed gives its cost back.</summary>
        Refund = 8
    }

    /// <summary>
    /// The BREATH gauge. Charged by aggressive and skillful play, spent on advanced techniques and ultimates.
    /// </summary>
    public sealed class BreathGaugeModel
    {
        public float Max { get; }
        public float Current { get; private set; }
        public bool Infinite { get; set; }
        public float GainMultiplier { get; set; } = 1f;
        public float Normalized => Current / Max;
        public bool IsFull => Current >= Max - 0.001f;

        public float HitGain { get; set; } = 2.2f;
        public float ComboMilestoneGain { get; set; } = 6f;
        public float PerfectDodgeGain { get; set; } = 14f;
        public float ParryGain { get; set; } = 18f;
        public float DamageTakenGainPerHp { get; set; } = 0.12f;
        public float KillGain { get; set; } = 5f;
        /// <summary>Breath recovered per second by normal breathing, only while below <see cref="RestingCap"/>.</summary>
        public float RestingRegen { get; set; } = 4f;
        /// <summary>Fraction of the gauge normal breathing can refill; the rest must be earned in combat.</summary>
        public float RestingCap { get; set; } = 0.4f;

        /// <summary>(current, max, delta, source)</summary>
        public event Action<float, float, float, BreathSource> Changed;
        public event Action Filled;

        public BreathGaugeModel(float max = 100f, float initial = 0f)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = MathUtil.Clamp(initial, 0f, max);
        }

        public float BaseGainFor(BreathSource source, float magnitude = 1f)
        {
            switch (source)
            {
                case BreathSource.Hit: return HitGain * magnitude;
                case BreathSource.ComboMilestone: return ComboMilestoneGain * magnitude;
                case BreathSource.PerfectDodge: return PerfectDodgeGain * magnitude;
                case BreathSource.Parry: return ParryGain * magnitude;
                case BreathSource.DamageTaken: return DamageTakenGainPerHp * magnitude;
                case BreathSource.Kill: return KillGain * magnitude;
                default: return magnitude;
            }
        }

        /// <summary>Adds breath using the tuning table for the given source.</summary>
        public float Gain(BreathSource source, float magnitude = 1f)
        {
            return Add(BaseGainFor(source, magnitude) * GainMultiplier, source);
        }

        public float Add(float amount, BreathSource source)
        {
            if (amount <= 0f) return 0f;
            bool wasFull = IsFull;
            float before = Current;
            Current = MathUtil.Min(Max, Current + amount);
            float delta = Current - before;
            if (delta > 0f) Changed?.Invoke(Current, Max, delta, source);
            if (!wasFull && IsFull) Filled?.Invoke();
            return delta;
        }

        public bool CanSpend(float amount) => Infinite || amount <= 0f || Current >= amount - 0.001f;

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            if (Infinite || amount <= 0f) return true;
            Current = MathUtil.Max(0f, Current - amount);
            Changed?.Invoke(Current, Max, -amount, BreathSource.Debug);
            return true;
        }

        /// <summary>Resting recovery: forms always come back, the ultimate still needs combat.</summary>
        public float Tick(float dt)
        {
            if (dt <= 0f || RestingRegen <= 0f) return 0f;
            float cap = Max * MathUtil.Clamp(RestingCap, 0f, 1f);
            if (Current >= cap) return 0f;
            return Add(MathUtil.Min(RestingRegen * dt, cap - Current), BreathSource.Resting);
        }

        public void SetValue(float value)
        {
            float before = Current;
            Current = MathUtil.Clamp(value, 0f, Max);
            Changed?.Invoke(Current, Max, Current - before, BreathSource.Debug);
            if (IsFull && before < Max) Filled?.Invoke();
        }
    }
}
