using BreathOfEclipse.Core;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    public class HealthModelTests
    {
        [Test]
        public void ApplyDamage_ReducesHealth_AndReportsApplied()
        {
            var h = new HealthModel(100f);
            float applied = h.ApplyDamage(30f);
            Assert.AreEqual(30f, applied, 0.001f);
            Assert.AreEqual(70f, h.Current, 0.001f);
            Assert.IsFalse(h.IsDead);
        }

        [Test]
        public void ApplyDamage_BeyondZero_ClampsAndDiesOnce()
        {
            var h = new HealthModel(50f);
            int deaths = 0;
            h.Died += () => deaths++;
            float applied = h.ApplyDamage(80f);
            Assert.AreEqual(50f, applied, 0.001f);
            Assert.IsTrue(h.IsDead);
            h.ApplyDamage(10f);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void Invulnerable_IgnoresDamage()
        {
            var h = new HealthModel(100f) { Invulnerable = true };
            Assert.AreEqual(0f, h.ApplyDamage(40f));
            Assert.AreEqual(100f, h.Current);
        }

        [Test]
        public void Revive_RestoresFromDeath()
        {
            var h = new HealthModel(100f);
            bool revived = false;
            h.Revived += () => revived = true;
            h.ApplyDamage(200f);
            h.Revive(0.5f);
            Assert.IsTrue(revived);
            Assert.AreEqual(50f, h.Current, 0.001f);
        }

        [Test]
        public void Heal_DoesNotExceedMax_AndNotWhenDead()
        {
            var h = new HealthModel(100f);
            h.ApplyDamage(10f);
            Assert.AreEqual(10f, h.Heal(50f), 0.001f);
            h.ApplyDamage(500f);
            Assert.AreEqual(0f, h.Heal(10f));
        }
    }

    public class StaminaModelTests
    {
        [Test]
        public void TrySpend_ConsumesStamina()
        {
            var s = new StaminaModel(100f);
            Assert.IsTrue(s.TrySpend(25f));
            Assert.AreEqual(75f, s.Current, 0.001f);
        }

        [Test]
        public void Regen_WaitsForDelay_ThenRefills()
        {
            var s = new StaminaModel(100f, regenPerSecond: 50f, regenDelay: 0.5f);
            s.TrySpend(50f);
            s.Tick(0.4f);
            Assert.AreEqual(50f, s.Current, 0.001f, "Should not regenerate during delay");
            s.Tick(0.2f); // finishes delay
            s.Tick(0.5f);
            Assert.AreEqual(75f, s.Current, 0.01f);
        }

        [Test]
        public void Drained_BecomesExhausted_UntilThreshold()
        {
            var s = new StaminaModel(100f, regenPerSecond: 100f, regenDelay: 0f, exhaustRecoverThreshold: 0.3f);
            s.Spend(100f);
            Assert.IsTrue(s.IsExhausted);
            Assert.IsFalse(s.CanSpend(10f));
            s.Tick(0.5f); // 80/s while exhausted -> 40
            Assert.IsFalse(s.IsExhausted);
            Assert.IsTrue(s.CanSpend(10f));
        }

        [Test]
        public void LastAction_CanOverdrawSlightly()
        {
            var s = new StaminaModel(100f);
            s.Spend(88f); // 12 left
            Assert.IsTrue(s.TrySpend(20f), "Half the cost is enough for the final action");
            Assert.AreEqual(0f, s.Current, 0.001f);
            Assert.IsTrue(s.IsExhausted);
        }

        [Test]
        public void Infinite_NeverDrains()
        {
            var s = new StaminaModel(100f) { Infinite = true };
            s.Spend(80f);
            Assert.AreEqual(100f, s.Current);
            Assert.IsTrue(s.TrySpend(1000f));
        }
    }

    public class BreathGaugeModelTests
    {
        [Test]
        public void Gain_UsesSourceTable_AndClampsAtMax()
        {
            var b = new BreathGaugeModel(100f);
            b.Gain(BreathSource.Parry);
            Assert.AreEqual(b.ParryGain, b.Current, 0.001f);
            for (int i = 0; i < 20; i++) b.Gain(BreathSource.Parry);
            Assert.AreEqual(100f, b.Current, 0.001f);
            Assert.IsTrue(b.IsFull);
        }

        [Test]
        public void Filled_FiresOnceWhenReachingMax()
        {
            var b = new BreathGaugeModel(100f, 90f);
            int filled = 0;
            b.Filled += () => filled++;
            b.Add(20f, BreathSource.Hit);
            b.Add(20f, BreathSource.Hit);
            Assert.AreEqual(1, filled);
        }

        [Test]
        public void TrySpend_FailsWhenInsufficient()
        {
            var b = new BreathGaugeModel(100f, 40f);
            Assert.IsFalse(b.TrySpend(50f));
            Assert.AreEqual(40f, b.Current, 0.001f);
            Assert.IsTrue(b.TrySpend(40f));
            Assert.AreEqual(0f, b.Current, 0.001f);
        }

        [Test]
        public void GainMultiplier_ScalesGains()
        {
            var b = new BreathGaugeModel(100f) { GainMultiplier = 2f };
            b.Gain(BreathSource.Hit);
            Assert.AreEqual(b.HitGain * 2f, b.Current, 0.001f);
        }
    }

    public class CooldownTrackerTests
    {
        [Test]
        public void Cooldown_ReadyAfterDuration()
        {
            var c = new CooldownTracker();
            c.Start("skill", 4f, now: 10f);
            Assert.IsFalse(c.IsReady("skill", 12f));
            Assert.AreEqual(2f, c.Remaining("skill", 12f), 0.001f);
            Assert.AreEqual(0.5f, c.NormalizedRemaining("skill", 12f), 0.001f);
            Assert.IsTrue(c.IsReady("skill", 14.01f));
        }

        [Test]
        public void UnknownId_IsReady()
        {
            var c = new CooldownTracker();
            Assert.IsTrue(c.IsReady("never_used", 0f));
        }

        [Test]
        public void DurationMultiplier_AndReduceAll()
        {
            var c = new CooldownTracker { DurationMultiplier = 0.5f };
            c.Start("a", 10f, 0f);
            Assert.AreEqual(5f, c.Remaining("a", 0f), 0.001f);
            c.ReduceAll(2f);
            Assert.AreEqual(3f, c.Remaining("a", 0f), 0.001f);
        }

        [Test]
        public void Disabled_EverythingReady()
        {
            var c = new CooldownTracker();
            c.Start("a", 10f, 0f);
            c.Disabled = true;
            Assert.IsTrue(c.IsReady("a", 1f));
        }
    }
}
