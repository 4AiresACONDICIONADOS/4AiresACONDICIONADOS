using System;
using System.Collections.Generic;

namespace BreathOfEclipse.Combat
{
    /// <summary>A timed stat modifier. Magnitude is additive percent (0.25 = +25%).</summary>
    public struct Buff
    {
        public string Id;
        public StatType Stat;
        public float Magnitude;
        public float Remaining;
        /// <summary>Negative duration = permanent until removed (breathing style passives).</summary>
        public bool Permanent => Remaining < 0f;
    }

    /// <summary>Holds active buffs and answers "what is my multiplier for stat X".</summary>
    public sealed class BuffCollection
    {
        private readonly List<Buff> _buffs = new List<Buff>();

        public event Action Changed;

        public int Count => _buffs.Count;

        /// <summary>Adds or refreshes a buff with the given id.</summary>
        public void Apply(string id, StatType stat, float magnitude, float duration)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id == id && _buffs[i].Stat == stat)
                {
                    var b = _buffs[i];
                    b.Magnitude = magnitude;
                    b.Remaining = duration;
                    _buffs[i] = b;
                    Changed?.Invoke();
                    return;
                }
            }
            _buffs.Add(new Buff { Id = id, Stat = stat, Magnitude = magnitude, Remaining = duration });
            Changed?.Invoke();
        }

        public void Remove(string id)
        {
            int removed = _buffs.RemoveAll(b => b.Id == id);
            if (removed > 0) Changed?.Invoke();
        }

        public bool Has(string id)
        {
            for (int i = 0; i < _buffs.Count; i++) if (_buffs[i].Id == id) return true;
            return false;
        }

        /// <summary>Sum of additive percentages for a stat.</summary>
        public float GetBonus(StatType stat)
        {
            float total = 0f;
            for (int i = 0; i < _buffs.Count; i++) if (_buffs[i].Stat == stat) total += _buffs[i].Magnitude;
            return total;
        }

        /// <summary>1 + bonus, clamped to be non negative.</summary>
        public float GetMultiplier(StatType stat)
        {
            float m = 1f + GetBonus(stat);
            return m < 0f ? 0f : m;
        }

        public void Tick(float deltaTime)
        {
            bool changed = false;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var b = _buffs[i];
                if (b.Permanent) continue;
                b.Remaining -= deltaTime;
                if (b.Remaining <= 0f)
                {
                    _buffs.RemoveAt(i);
                    changed = true;
                }
                else _buffs[i] = b;
            }
            if (changed) Changed?.Invoke();
        }

        public void Clear()
        {
            if (_buffs.Count == 0) return;
            _buffs.Clear();
            Changed?.Invoke();
        }
    }
}
