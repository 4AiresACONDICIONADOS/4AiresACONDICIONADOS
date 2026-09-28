using System;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Generic damage receiver used by the player, enemies and dummies. Resolves a <see cref="HitData"/> through
    /// interceptors (block/parry/dodge), the damage formula and reactors (animation, AI, knockback), then raises
    /// global <see cref="GameEvents"/>.
    /// </summary>
    public sealed class Damageable : MonoBehaviour, IDamageable
    {
        [SerializeField] private Team team = Team.Enemy;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float defense;
        [SerializeField] private Element weakness = Element.None;
        [SerializeField] private Element resistance = Element.None;
        [SerializeField] private Transform center = null;

        private readonly List<IHitInterceptor> _interceptors = new List<IHitInterceptor>();
        private readonly List<IHitReactor> _reactors = new List<IHitReactor>();

        public HealthModel Health { get; private set; }
        public Team Team => team;
        public bool IsAlive => Health != null && !Health.IsDead;
        public Transform Transform => transform;
        public Vector3 CenterPoint => center != null ? center.position : transform.position + Vector3.up * 1.1f;
        public float Defense { get => defense; set => defense = value; }
        public Element Weakness { get => weakness; set => weakness = value; }
        public Element Resistance { get => resistance; set => resistance = value; }
        /// <summary>Ignores hits entirely (cinematics, debug god mode, spawn protection).</summary>
        public bool Invulnerable { get; set; }
        /// <summary>Receives damage but never dies (training dummies, god mode).</summary>
        public bool Immortal { get; set; }
        /// <summary>Multiplier applied to incoming damage (difficulty, debug).</summary>
        public float IncomingDamageMultiplier { get; set; } = 1f;
        public float LastHitTime { get; private set; } = -100f;

        /// <summary>Raised after a hit is resolved (any outcome).</summary>
        public event Action<HitData, HitResult> HitResolved;
        /// <summary>Raised once on death, with the killing hit.</summary>
        public event Action<HitData> Died;

        private void Awake()
        {
            if (Health == null) Health = new HealthModel(Mathf.Max(1f, maxHealth));
        }

        /// <summary>Runtime setup (procedurally built characters).</summary>
        public void Configure(Team newTeam, float newMaxHealth, Transform centerPoint, float newDefense = 0f,
            Element newWeakness = Element.None, Element newResistance = Element.None)
        {
            team = newTeam;
            maxHealth = Mathf.Max(1f, newMaxHealth);
            center = centerPoint;
            defense = newDefense;
            weakness = newWeakness;
            resistance = newResistance;
            Health = new HealthModel(maxHealth);
        }

        public void AddInterceptor(IHitInterceptor interceptor)
        {
            if (interceptor == null || _interceptors.Contains(interceptor)) return;
            _interceptors.Add(interceptor);
            _interceptors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void AddReactor(IHitReactor reactor)
        {
            if (reactor != null && !_reactors.Contains(reactor)) _reactors.Add(reactor);
        }

        public HitResult ReceiveHit(HitData hit)
        {
            if (!IsAlive || Invulnerable || !enabled) return HitResult.Ignored;
            if (hit.AttackerTeam == team && team != Team.Neutral) return HitResult.Ignored;

            var result = new HitResult { Target = gameObject, HitPoint = hit.HitPoint, Outcome = HitOutcome.Hit };
            float blockReduction = 0f;
            bool blocked = false;

            for (int i = 0; i < _interceptors.Count; i++)
            {
                var outcome = _interceptors[i].Intercept(ref hit, out float reduction);
                if (outcome == HitOutcome.Hit || outcome == HitOutcome.Ignored) continue;
                if (outcome == HitOutcome.Blocked)
                {
                    blocked = true;
                    blockReduction = reduction;
                    break;
                }
                // Parried / evaded: no damage at all.
                result.Outcome = outcome;
                Notify(hit, result);
                return result;
            }

            var request = new DamageRequest
            {
                BaseDamage = hit.BaseDamage,
                Multiplier = hit.Multiplier <= 0f ? 1f : hit.Multiplier,
                BonusPercent = hit.BonusPercent,
                CritChance = hit.CritChance,
                CritMultiplier = hit.CritMultiplier <= 0f ? 1.5f : hit.CritMultiplier,
                ForceCritical = hit.ForceCritical,
                Element = hit.Element,
                DefenderWeakness = weakness,
                DefenderResistance = resistance,
                Defense = defense,
                Blocked = blocked,
                BlockReduction = blockReduction,
                CritRoll = UnityEngine.Random.value
            };
            var dmg = DamageCalculator.Calculate(request);
            float amount = dmg.Amount * IncomingDamageMultiplier;
            if (Immortal) amount = Mathf.Min(amount, Health.Current - 1f);

            Health.ApplyDamage(Mathf.Max(0f, amount));
            LastHitTime = Time.time;
            result.Damage = Mathf.RoundToInt(Mathf.Max(0f, amount));
            result.IsCritical = dmg.IsCritical;
            result.Weakness = dmg.ElementalWeakness;
            result.Outcome = blocked ? HitOutcome.Blocked : HitOutcome.Hit;
            if (Health.IsDead)
            {
                result.Outcome = HitOutcome.Killed;
                result.Killed = true;
            }

            Notify(hit, result);
            if (result.Killed) Died?.Invoke(hit);
            return result;
        }

        private void Notify(HitData hit, HitResult result)
        {
            for (int i = 0; i < _reactors.Count; i++) _reactors[i].OnHitResolved(hit, result);
            HitResolved?.Invoke(hit, result);
            GameEvents.RaiseDamage(hit, result);
        }

        /// <summary>Kills instantly (debug "Kill Enemies").</summary>
        public void Kill(GameObject killer = null)
        {
            if (!IsAlive) return;
            var hit = new HitData
            {
                Attacker = killer,
                AttackerTeam = team == Team.Player ? Team.Enemy : Team.Player,
                BaseDamage = Health.Current + 1f,
                Multiplier = 1f,
                Direction = -transform.forward,
                HitPoint = CenterPoint,
                Reaction = HitReaction.Knockback,
                Knockback = 3f
            };
            bool wasImmortal = Immortal;
            bool wasInvulnerable = Invulnerable;
            Immortal = false;
            Invulnerable = false;
            var saved = new List<IHitInterceptor>(_interceptors);
            _interceptors.Clear();
            ReceiveHit(hit);
            _interceptors.AddRange(saved);
            Immortal = wasImmortal;
            Invulnerable = wasInvulnerable;
        }
    }
}
