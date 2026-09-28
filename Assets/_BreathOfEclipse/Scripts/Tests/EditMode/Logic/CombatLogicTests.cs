using BreathOfEclipse.AI;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    public class DamageCalculatorTests
    {
        [Test]
        public void BaseDamage_TimesMultiplier()
        {
            var r = DamageRequest.Simple(20f, 1.5f);
            Assert.AreEqual(30, DamageCalculator.Calculate(r).Amount);
        }

        [Test]
        public void Critical_WhenRollBelowChance()
        {
            var r = DamageRequest.Simple(20f);
            r.CritChance = 0.5f;
            r.CritMultiplier = 2f;
            r.CritRoll = 0.1f;
            var result = DamageCalculator.Calculate(r);
            Assert.IsTrue(result.IsCritical);
            Assert.AreEqual(40, result.Amount);
        }

        [Test]
        public void NoCritical_WhenRollAboveChance()
        {
            var r = DamageRequest.Simple(20f);
            r.CritChance = 0.2f;
            r.CritRoll = 0.5f;
            Assert.IsFalse(DamageCalculator.Calculate(r).IsCritical);
        }

        [Test]
        public void ForceCritical_AlwaysCrits()
        {
            var r = DamageRequest.Simple(10f);
            r.ForceCritical = true;
            r.CritMultiplier = 2f;
            Assert.AreEqual(20, DamageCalculator.Calculate(r).Amount);
        }

        [Test]
        public void Weakness_And_Resistance()
        {
            var r = DamageRequest.Simple(100f);
            r.Element = Element.Fire;
            r.DefenderWeakness = Element.Fire;
            var weak = DamageCalculator.Calculate(r);
            Assert.IsTrue(weak.ElementalWeakness);
            Assert.AreEqual(135, weak.Amount);

            r.DefenderWeakness = Element.None;
            r.DefenderResistance = Element.Fire;
            var resist = DamageCalculator.Calculate(r);
            Assert.IsTrue(resist.Resisted);
            Assert.AreEqual(70, resist.Amount);
        }

        [Test]
        public void Defense_ReducesDamage()
        {
            var r = DamageRequest.Simple(100f);
            r.Defense = 100f;
            Assert.AreEqual(50, DamageCalculator.Calculate(r).Amount);
        }

        [Test]
        public void Block_ReducesDamage_ButNeverBelowOne()
        {
            var r = DamageRequest.Simple(10f);
            r.Blocked = true;
            r.BlockReduction = 1f;
            var result = DamageCalculator.Calculate(r);
            Assert.IsTrue(result.WasBlocked);
            Assert.AreEqual(1, result.Amount);
        }

        [Test]
        public void BonusPercent_Applies()
        {
            var r = DamageRequest.Simple(100f);
            r.BonusPercent = 0.25f;
            Assert.AreEqual(125, DamageCalculator.Calculate(r).Amount);
        }
    }

    public class ComboGraphTests
    {
        private static ComboGraph BuildDefault()
        {
            return new ComboGraph()
                .AddNode("L1", "L2", "L1H")
                .AddNode("L2", "L3", "L2H")
                .AddNode("L3", "L4")
                .AddNode("L4")
                .AddNode("L2H")
                .AddNode("L1H", null, "L1HH")
                .AddNode("L1HH")
                .AddNode("H1", null, "H2")
                .AddNode("H2")
                .AddNode("DashL")
                .AddNode("A1", "A2")
                .AddNode("A2", "A3")
                .AddNode("A3")
                .AddNode("AH")
                .AddNode("PDCounter")
                .AddNode("Riposte")
                .SetEntry(ComboEntry.GroundLight, "L1")
                .SetEntry(ComboEntry.GroundHeavy, "H1")
                .SetEntry(ComboEntry.DashLight, "DashL")
                .SetEntry(ComboEntry.AirLight, "A1")
                .SetEntry(ComboEntry.AirHeavy, "AH")
                .SetEntry(ComboEntry.PerfectDodgeLight, "PDCounter")
                .SetEntry(ComboEntry.ParryHeavy, "Riposte");
        }

        [Test]
        public void LLLL_String()
        {
            var g = BuildDefault();
            var ctx = ComboContext.Ground;
            string a = g.Resolve(null, ComboInput.Light, ctx);
            string b = g.Resolve(a, ComboInput.Light, ctx);
            string c = g.Resolve(b, ComboInput.Light, ctx);
            string d = g.Resolve(c, ComboInput.Light, ctx);
            Assert.AreEqual(new[] { "L1", "L2", "L3", "L4" }, new[] { a, b, c, d });
        }

        [Test]
        public void LLH_And_LHH_And_HH()
        {
            var g = BuildDefault();
            var ctx = ComboContext.Ground;
            Assert.AreEqual("L2H", g.Resolve("L2", ComboInput.Heavy, ctx));
            Assert.AreEqual("L1H", g.Resolve("L1", ComboInput.Heavy, ctx));
            Assert.AreEqual("L1HH", g.Resolve("L1H", ComboInput.Heavy, ctx));
            Assert.AreEqual("H1", g.Resolve(null, ComboInput.Heavy, ctx));
            Assert.AreEqual("H2", g.Resolve("H1", ComboInput.Heavy, ctx));
        }

        [Test]
        public void FinishedString_RestartsOnlyWhenAllowed()
        {
            var g = BuildDefault();
            Assert.AreEqual("L1", g.Resolve("L4", ComboInput.Light, ComboContext.Ground));
            var strict = new ComboContext { AllowRestart = false };
            Assert.IsNull(g.Resolve("L4", ComboInput.Light, strict));
        }

        [Test]
        public void ContextEntries()
        {
            var g = BuildDefault();
            Assert.AreEqual("DashL", g.Resolve(null, ComboInput.Light, new ComboContext { Dashing = true, AllowRestart = true }));
            Assert.AreEqual("H1", g.Resolve(null, ComboInput.Heavy, new ComboContext { Dashing = true, AllowRestart = true }), "Dash heavy falls back to ground heavy");
            Assert.AreEqual("A1", g.Resolve(null, ComboInput.Light, ComboContext.Air));
            Assert.AreEqual("A2", g.Resolve("A1", ComboInput.Light, ComboContext.Air));
            Assert.AreEqual("AH", g.Resolve(null, ComboInput.Heavy, ComboContext.Air));
        }

        [Test]
        public void Counters_TakePriority()
        {
            var g = BuildDefault();
            Assert.AreEqual("PDCounter", g.Resolve("L2", ComboInput.Light, new ComboContext { PerfectDodgeWindow = true }));
            Assert.AreEqual("Riposte", g.Resolve(null, ComboInput.Heavy, new ComboContext { ParryWindow = true }));
            Assert.AreEqual("L1", g.Resolve(null, ComboInput.Light, new ComboContext { ParryWindow = true, AllowRestart = true }), "Parry window only changes heavy");
        }
    }

    public class ComboCounterTests
    {
        [Test]
        public void CountsAndTimesOut()
        {
            var c = new ComboCounter(timeout: 1f, milestoneInterval: 3);
            int milestones = 0;
            int ended = -1;
            c.Milestone += _ => milestones++;
            c.Ended += n => ended = n;
            c.RegisterHit();
            c.RegisterHit();
            c.RegisterHit(2);
            Assert.AreEqual(4, c.Count);
            Assert.AreEqual(1, milestones);
            c.Tick(1.1f);
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(4, ended);
            Assert.AreEqual(4, c.Best);
        }
    }

    public class PoiseModelTests
    {
        [Test]
        public void BreaksAndRefills()
        {
            var p = new PoiseModel(30f);
            int broken = 0;
            p.Broken += () => broken++;
            Assert.IsFalse(p.ApplyDamage(20f));
            Assert.IsTrue(p.ApplyDamage(15f));
            Assert.AreEqual(1, broken);
            Assert.AreEqual(30f, p.Current, 0.001f);
        }
    }

    public class BuffCollectionTests
    {
        [Test]
        public void StacksAdditively_AndExpires()
        {
            var b = new BuffCollection();
            b.Apply("style", StatType.Damage, 0.1f, -1f);
            b.Apply("tailwind", StatType.MoveSpeed, 0.3f, 2f);
            b.Apply("rage", StatType.Damage, 0.2f, 1f);
            Assert.AreEqual(1.3f, b.GetMultiplier(StatType.Damage), 0.001f);
            b.Tick(1.5f);
            Assert.AreEqual(1.1f, b.GetMultiplier(StatType.Damage), 0.001f);
            Assert.AreEqual(1.3f, b.GetMultiplier(StatType.MoveSpeed), 0.001f);
            b.Tick(1f);
            Assert.AreEqual(1f, b.GetMultiplier(StatType.MoveSpeed), 0.001f);
            Assert.IsTrue(b.Has("style"), "Permanent buffs never expire");
        }

        [Test]
        public void Apply_RefreshesExisting()
        {
            var b = new BuffCollection();
            b.Apply("x", StatType.Damage, 0.1f, 1f);
            b.Apply("x", StatType.Damage, 0.5f, 1f);
            Assert.AreEqual(1, b.Count);
            Assert.AreEqual(1.5f, b.GetMultiplier(StatType.Damage), 0.001f);
        }
    }

    public class AttackTokenPoolTests
    {
        [Test]
        public void LimitsConcurrentAttackers()
        {
            var pool = new AttackTokenPool(2);
            Assert.IsTrue(pool.TryAcquire(1));
            Assert.IsTrue(pool.TryAcquire(2));
            Assert.IsFalse(pool.TryAcquire(3));
            Assert.IsTrue(pool.TryAcquire(1), "Holder re-acquiring is fine");
            pool.Release(1);
            Assert.IsTrue(pool.TryAcquire(3));
        }
    }

    public class InputBufferTests
    {
        [Test]
        public void BufferedPress_ExpiresAfterWindow()
        {
            var b = new InputBuffer(0.3f);
            b.Record(BufferedAction.LightAttack, 1f);
            Assert.IsTrue(b.Has(BufferedAction.LightAttack, 1.2f));
            Assert.IsFalse(b.Has(BufferedAction.LightAttack, 1.4f));
        }

        [Test]
        public void Consume_RemovesPress()
        {
            var b = new InputBuffer(0.3f);
            b.Record(BufferedAction.Dodge, 1f);
            Assert.IsTrue(b.Consume(BufferedAction.Dodge, 1.1f));
            Assert.IsFalse(b.Has(BufferedAction.Dodge, 1.1f));
        }

        [Test]
        public void TryGetLatest_PicksMostRecent()
        {
            var b = new InputBuffer(0.5f);
            b.Record(BufferedAction.LightAttack, 1f);
            b.Record(BufferedAction.HeavyAttack, 1.2f);
            Assert.IsTrue(b.TryGetLatest(new[] { BufferedAction.LightAttack, BufferedAction.HeavyAttack }, 1.3f, out var latest));
            Assert.AreEqual(BufferedAction.HeavyAttack, latest);
        }
    }
}
