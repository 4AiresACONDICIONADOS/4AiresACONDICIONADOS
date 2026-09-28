using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Player;
using UnityEngine;

namespace BreathOfEclipse.Playtest
{
    /// <summary>Records every attack the player starts (polled each frame), including repeats of the same attack.</summary>
    public sealed class AttackWatcher
    {
        public readonly List<string> Started = new List<string>();
        private object _lastAttack;
        private float _lastElapsed;
        private bool _wasAttacking;

        public void Reset()
        {
            Started.Clear();
            var pc = PlayerController.Instance;
            _lastAttack = pc != null ? pc.Combat.Current : null;
            _wasAttacking = pc != null && pc.Combat.IsAttacking;
            _lastElapsed = pc != null ? pc.Combat.Elapsed : 0f;
        }

        public void Poll()
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            var c = pc.Combat;
            if (c.IsAttacking && c.Current != null)
            {
                bool isNew = !_wasAttacking || !ReferenceEquals(c.Current, _lastAttack) || c.Elapsed + 0.0001f < _lastElapsed;
                if (isNew) Started.Add(c.Current.attackId);
                _lastAttack = c.Current;
                _lastElapsed = c.Elapsed;
            }
            _wasAttacking = c.IsAttacking;
        }

        public string Last => Started.Count > 0 ? Started[Started.Count - 1] : null;
        public string Sequence => Started.Count > 0 ? string.Join(">", Started) : "(none)";
    }

    /// <summary>Per-frame observations that events do not cover (trails, sounds playing, BREATH, player states).</summary>
    public sealed class FrameSampler
    {
        public bool SwordTrail;
        public bool ElementTrail;
        public bool SfxPlaying;
        public bool Sprinted;
        public bool Airborne;
        public float BreathMax;
        public readonly HashSet<PlayerState> States = new HashSet<PlayerState>();

        public void Reset()
        {
            SwordTrail = ElementTrail = SfxPlaying = Sprinted = Airborne = false;
            BreathMax = 0f;
            States.Clear();
        }

        public void Tick()
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            SwordTrail |= pc.SwordTrailEmitting;
            ElementTrail |= pc.ElementTrailEmitting;
            Sprinted |= pc.IsSprinting;
            Airborne |= !pc.Motor.Grounded;
            BreathMax = Mathf.Max(BreathMax, pc.Stats.Breath.Current);
            States.Add(pc.State);
            var audio = AudioManager.Instance;
            if (audio != null && audio.PlayingSfxCount > 0) SfxPlaying = true;
        }
    }

    public static class SyntheticHits
    {
        /// <summary>
        /// Delivers an enemy-team hit to the player through the real damage pipeline (Damageable → PlayerDefense
        /// interceptors → reactors → events). Used to time parries and perfect dodges deterministically.
        /// </summary>
        public static HitResult HitPlayerFrom(GameObject attacker, float damage = 8f)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return HitResult.Ignored;
            Vector3 from = attacker != null ? attacker.transform.position : pc.transform.position + pc.transform.forward * 2f;
            Vector3 dir = pc.transform.position - from;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : -pc.transform.forward;
            var hit = new HitData
            {
                Attacker = attacker,
                AttackerTeam = Team.Enemy,
                SourceId = "playtest_synthetic",
                AttackInstanceId = Random.Range(1_000_000, int.MaxValue),
                Category = DamageCategory.EnemyLight,
                BaseDamage = damage,
                Multiplier = 1f,
                CritChance = 0f,
                Reaction = HitReaction.Light,
                HitPoint = pc.Damageable.CenterPoint,
                Direction = dir,
                Knockback = 1f,
                Parryable = true
            };
            return pc.Damageable.ReceiveHit(hit);
        }
    }
}
