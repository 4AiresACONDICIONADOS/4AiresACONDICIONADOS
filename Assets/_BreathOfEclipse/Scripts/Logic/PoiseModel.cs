using System;
using BreathOfEclipse.Core;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Poise (stagger resistance). Hits reduce poise; when it breaks the owner is staggered and poise refills.
    /// </summary>
    public sealed class PoiseModel
    {
        public float Max { get; set; }
        public float Current { get; private set; }
        public float RegenPerSecond { get; set; }
        public float RegenDelay { get; set; }
        public float Normalized => Max <= 0f ? 0f : Current / Max;

        private float _delay;

        public event Action Broken;

        public PoiseModel(float max, float regenPerSecond = 20f, float regenDelay = 1.5f)
        {
            Max = max;
            Current = max;
            RegenPerSecond = regenPerSecond;
            RegenDelay = regenDelay;
        }

        /// <summary>Applies poise damage. Returns true when poise broke with this hit.</summary>
        public bool ApplyDamage(float amount)
        {
            if (Max <= 0f || amount <= 0f) return false;
            Current -= amount;
            _delay = RegenDelay;
            if (Current > 0f) return false;
            Current = Max;
            Broken?.Invoke();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (Current >= Max) return;
            if (_delay > 0f)
            {
                _delay -= deltaTime;
                return;
            }
            Current = MathUtil.Min(Max, Current + RegenPerSecond * deltaTime);
        }

        public void Reset()
        {
            Current = Max;
            _delay = 0f;
        }
    }
}
