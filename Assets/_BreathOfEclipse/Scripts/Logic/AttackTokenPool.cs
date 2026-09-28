using System.Collections.Generic;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Limits how many enemies may attack the player simultaneously. Keeps group fights readable:
    /// the player can always see who is attacking and from where.
    /// </summary>
    public sealed class AttackTokenPool
    {
        private readonly HashSet<int> _holders = new HashSet<int>();

        public int Capacity { get; set; }
        public int InUse => _holders.Count;

        public AttackTokenPool(int capacity = 2)
        {
            Capacity = capacity;
        }

        public bool TryAcquire(int ownerId)
        {
            if (_holders.Contains(ownerId)) return true;
            if (_holders.Count >= Capacity) return false;
            _holders.Add(ownerId);
            return true;
        }

        /// <summary>Bosses ignore the capacity but still occupy a slot.</summary>
        public void ForceAcquire(int ownerId) => _holders.Add(ownerId);

        public bool Holds(int ownerId) => _holders.Contains(ownerId);

        public void Release(int ownerId) => _holders.Remove(ownerId);

        public void Clear() => _holders.Clear();
    }
}
