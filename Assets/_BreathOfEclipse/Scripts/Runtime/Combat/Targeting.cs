using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>Something the player can lock on to.</summary>
    public interface ITargetable
    {
        Transform LockOnPoint { get; }
        bool IsTargetable { get; }
        bool IsBoss { get; }
        IDamageable Damageable { get; }
    }

    /// <summary>Global list of lock-on candidates (enemies register while enabled).</summary>
    public static class TargetRegistry
    {
        private static readonly List<ITargetable> Targets = new List<ITargetable>();

        public static IReadOnlyList<ITargetable> All => Targets;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Targets.Clear();

        public static void Register(ITargetable t)
        {
            if (t != null && !Targets.Contains(t)) Targets.Add(t);
        }

        public static void Unregister(ITargetable t) => Targets.Remove(t);

        /// <summary>Closest valid target to a point (auto-aim for techniques).</summary>
        public static ITargetable Closest(Vector3 point, float maxDistance, Vector3 preferredDirection = default, float directionWeight = 0f)
        {
            ITargetable best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t == null || !t.IsTargetable || t.LockOnPoint == null) continue;
                Vector3 to = t.LockOnPoint.position - point;
                to.y = 0f;
                float d = to.magnitude;
                if (d > maxDistance) continue;
                float score = d;
                if (directionWeight > 0f && preferredDirection.sqrMagnitude > 0.001f && d > 0.01f)
                {
                    float dot = Vector3.Dot(to / d, preferredDirection.normalized);
                    score += (1f - dot) * directionWeight;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }

        public static void InRadius(Vector3 point, float radius, List<ITargetable> results)
        {
            results.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t == null || !t.IsTargetable || t.LockOnPoint == null) continue;
                if ((t.LockOnPoint.position - point).sqrMagnitude <= r2) results.Add(t);
            }
        }
    }
}
