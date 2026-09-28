using System;

namespace BreathOfEclipse.Combat
{
    /// <summary>Counts consecutive hits. Resets when no hit lands within <see cref="Timeout"/> seconds.</summary>
    public sealed class ComboCounter
    {
        public int Count { get; private set; }
        public int Best { get; private set; }
        public float Timeout { get; set; }
        public int MilestoneInterval { get; set; }
        public float TimeRemaining { get; private set; }

        /// <summary>(count)</summary>
        public event Action<int> Changed;
        /// <summary>(count) Fired every <see cref="MilestoneInterval"/> hits.</summary>
        public event Action<int> Milestone;
        /// <summary>(finalCount)</summary>
        public event Action<int> Ended;

        public ComboCounter(float timeout = 2.2f, int milestoneInterval = 10)
        {
            Timeout = timeout;
            MilestoneInterval = milestoneInterval;
        }

        public void RegisterHit(int hits = 1)
        {
            if (hits <= 0) return;
            int before = Count;
            Count += hits;
            TimeRemaining = Timeout;
            if (Count > Best) Best = Count;
            Changed?.Invoke(Count);
            if (MilestoneInterval > 0 && Count / MilestoneInterval > before / MilestoneInterval)
                Milestone?.Invoke(Count);
        }

        public void Tick(float deltaTime)
        {
            if (Count == 0) return;
            TimeRemaining -= deltaTime;
            if (TimeRemaining <= 0f) Break();
        }

        public void Break()
        {
            if (Count == 0) return;
            int final = Count;
            Count = 0;
            TimeRemaining = 0f;
            Ended?.Invoke(final);
            Changed?.Invoke(0);
        }
    }
}
