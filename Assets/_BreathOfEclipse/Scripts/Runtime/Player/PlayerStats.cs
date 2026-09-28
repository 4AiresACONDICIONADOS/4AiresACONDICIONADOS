using BreathOfEclipse.Audio;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Player
{
    /// <summary>Player resources: health (via Damageable), stamina, BREATH gauge, buffs and combo counter.</summary>
    public sealed class PlayerStats : MonoBehaviour
    {
        public Damageable Damageable { get; private set; }
        public HealthModel Health => Damageable != null ? Damageable.Health : null;
        public StaminaModel Stamina { get; private set; }
        public BreathGaugeModel Breath { get; private set; }
        public BuffCollection Buffs { get; } = new BuffCollection();
        public ComboCounter Combo { get; } = new ComboCounter(2.4f, 10);

        private bool _godMode;

        public bool GodMode
        {
            get => _godMode;
            set
            {
                _godMode = value;
                if (Damageable != null) Damageable.Immortal = value;
            }
        }

        public bool InfiniteStamina
        {
            get => Stamina != null && Stamina.Infinite;
            set { if (Stamina != null) Stamina.Infinite = value; }
        }

        public bool InfiniteBreath
        {
            get => Breath != null && Breath.Infinite;
            set
            {
                if (Breath == null) return;
                Breath.Infinite = value;
                if (value) Breath.SetValue(Breath.Max);
            }
        }

        public void Initialize(PlayerData data, Damageable damageable)
        {
            Damageable = damageable;
            Stamina = new StaminaModel(data.maxStamina, data.staminaRegen, data.staminaRegenDelay, 0.3f);
            Breath = new BreathGaugeModel(data.maxBreath, 0f);
            Breath.Changed += (cur, max, delta, source) => GameEvents.RaiseBreathChanged(cur, max, delta);
            Breath.Filled += () =>
            {
                Sfx.Play2D("breath_full", 0.7f);
                GameEvents.Notify("BREATH GAUGE FULL — Ultimate ready [R]");
            };
            Combo.Changed += GameEvents.RaiseComboChanged;
            Combo.Milestone += _ => Breath.Gain(BreathSource.ComboMilestone);
            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        private void OnDestroy() => GameEvents.EnemyKilled -= OnEnemyKilled;

        private void OnEnemyKilled(GameObject victim, GameObject killer)
        {
            if (Breath != null) Breath.Gain(BreathSource.Kill);
        }

        /// <summary>Called for every landed player hit.</summary>
        public void OnHitLanded(HitResult result, float breathMultiplier)
        {
            if (result.Outcome != HitOutcome.Hit && result.Outcome != HitOutcome.Killed) return;
            Combo.RegisterHit();
            Breath.Gain(BreathSource.Hit, breathMultiplier * (result.IsCritical ? 1.5f : 1f));
        }

        public void OnDamageTaken(float amount)
        {
            Combo.Break();
            Breath.Gain(BreathSource.DamageTaken, amount);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Stamina.RegenMultiplier = Buffs.GetMultiplier(StatType.StaminaRegen);
            Breath.GainMultiplier = Buffs.GetMultiplier(StatType.BreathGain);
            Stamina.Tick(dt);
            Buffs.Tick(dt);
            Combo.Tick(dt);
        }
    }
}
