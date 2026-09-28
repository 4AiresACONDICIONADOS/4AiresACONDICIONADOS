using System;

namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Stamina resource used by dodge, sprint, block and some techniques.
    /// Regeneration pauses briefly after spending, then refills quickly so combat stays fast.
    /// When fully drained the model becomes "exhausted" until it recovers a threshold,
    /// which prevents dodge spam without making regeneration feel slow.
    /// </summary>
    public sealed class StaminaModel
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public float RegenPerSecond { get; set; }
        public float RegenDelay { get; set; }
        public float ExhaustRecoverThreshold { get; set; }
        public float RegenMultiplier { get; set; } = 1f;
        public bool Infinite { get; set; }
        public bool IsExhausted { get; private set; }
        public float Normalized => Max <= 0f ? 0f : Current / Max;

        private float _delayTimer;

        public event Action<float, float> Changed;
        public event Action Exhausted;
        public event Action Recovered;

        public StaminaModel(float max, float regenPerSecond = 38f, float regenDelay = 0.45f, float exhaustRecoverThreshold = 0.3f)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = max;
            RegenPerSecond = regenPerSecond;
            RegenDelay = regenDelay;
            ExhaustRecoverThreshold = exhaustRecoverThreshold;
        }

        /// <summary>True when the pool can pay the cost right now.</summary>
        public bool CanSpend(float amount)
        {
            if (Infinite || amount <= 0f) return true;
            if (IsExhausted) return false;
            // Allow the last action to overdraw slightly so the final dodge is not eaten.
            return Current > 0f && Current >= amount * 0.5f;
        }

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            Spend(amount);
            return true;
        }

        /// <summary>Unconditional spending (used for continuous drains like sprint or blocking hits).</summary>
        public void Spend(float amount)
        {
            if (Infinite || amount <= 0f) return;
            Current = MathUtil.Max(0f, Current - amount);
            _delayTimer = RegenDelay;
            if (Current <= 0f && !IsExhausted)
            {
                IsExhausted = true;
                Exhausted?.Invoke();
            }
            Changed?.Invoke(Current, Max);
        }

        public void Drain(float perSecond, float deltaTime) => Spend(perSecond * deltaTime);

        public void Tick(float deltaTime)
        {
            if (Infinite)
            {
                if (Current < Max)
                {
                    Current = Max;
                    Changed?.Invoke(Current, Max);
                }
                if (IsExhausted)
                {
                    IsExhausted = false;
                    Recovered?.Invoke();
                }
                return;
            }

            if (_delayTimer > 0f)
            {
                _delayTimer -= deltaTime;
                return;
            }

            if (Current >= Max) return;
            float rate = RegenPerSecond * RegenMultiplier * (IsExhausted ? 0.8f : 1f);
            Current = MathUtil.Min(Max, Current + rate * deltaTime);
            if (IsExhausted && Current >= Max * ExhaustRecoverThreshold)
            {
                IsExhausted = false;
                Recovered?.Invoke();
            }
            Changed?.Invoke(Current, Max);
        }

        public void Refill()
        {
            Current = Max;
            _delayTimer = 0f;
            if (IsExhausted)
            {
                IsExhausted = false;
                Recovered?.Invoke();
            }
            Changed?.Invoke(Current, Max);
        }
    }
}
