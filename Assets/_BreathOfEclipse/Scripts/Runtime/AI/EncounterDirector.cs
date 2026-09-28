using System;
using System.Collections.Generic;
using UnityEngine;

namespace BreathOfEclipse.AI
{
    /// <summary>
    /// Coordinates enemies in a fight: attack tokens (max simultaneous attackers), alive counts and
    /// "group alert" so a pack reacts together.
    /// </summary>
    public static class EncounterDirector
    {
        public static readonly AttackTokenPool Tokens = new AttackTokenPool(2);
        private static readonly List<EnemyController> Enemies = new List<EnemyController>();

        public static IReadOnlyList<EnemyController> All => Enemies;
        public static int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var e in Enemies) if (e != null && e.IsAlive) n++;
                return n;
            }
        }

        /// <summary>Raised when an enemy dies (enemy).</summary>
        public static event Action<EnemyController> EnemyDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tokens.Clear();
            Tokens.Capacity = 2;
            Enemies.Clear();
            EnemyDied = null;
        }

        public static void Register(EnemyController e)
        {
            if (e != null && !Enemies.Contains(e)) Enemies.Add(e);
        }

        public static void Unregister(EnemyController e)
        {
            Enemies.Remove(e);
            if (e != null) Tokens.Release(e.GetInstanceID());
        }

        public static void NotifyDeath(EnemyController e)
        {
            if (e != null) Tokens.Release(e.GetInstanceID());
            EnemyDied?.Invoke(e);
        }

        /// <summary>Alerts every enemy within a radius (one sees the player, the pack joins).</summary>
        public static void AlertAround(Vector3 position, float radius)
        {
            float r2 = radius * radius;
            foreach (var e in Enemies)
            {
                if (e == null || !e.IsAlive) continue;
                if ((e.transform.position - position).sqrMagnitude <= r2) e.Alert();
            }
        }

        public static void KillAll()
        {
            foreach (var e in Enemies.ToArray())
                if (e != null && e.IsAlive) e.Damageable.Kill();
        }
    }
}
