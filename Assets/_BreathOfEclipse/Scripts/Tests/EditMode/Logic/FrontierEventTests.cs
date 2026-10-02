using BreathOfEclipse.World;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    public class FrontierEventTests
    {
        [Test]
        public void EveryStagedEvent_HasASpot_AndAValidWindow()
        {
            foreach (var d in FrontierEvents.Defs)
            {
                Assert.IsNotEmpty(d.Title);
                Assert.Greater(d.HardDeadline, d.SoftDeadline, d.Id);
                if (d.Id == FrontierEvents.Discovery) continue;
                Assert.NotNull(FrontierEvents.Spot(d.Id, 0), d.Id + " has no spot");
                Assert.NotNull(FrontierEvents.Spot(d.Id, 7), d.Id + " variant wraps");
            }
        }

        [Test]
        public void FiveDays_EventsStart_ResolveAndExpire_NeverWaitForThePlayer()
        {
            var db = new WorldStateDatabase();
            var report = WorldEventSimulator.Simulate(FrontierEvents.Defs, db, 0, 5 * 24,
                d => FrontierEvents.SuccessChance(d, 0f, 1), (d, r) => { }, (d, r) => FrontierEvents.ApplyOutcome(db, d, r, r.state == WorldEventState.ResolvedSuccess, false, r.resolvedAt));
            Assert.Greater(report.Started, 3, "the world should produce events on its own");
            Assert.GreaterOrEqual(report.Started, report.Resolved);
            foreach (var d in FrontierEvents.Defs)
            {
                var r = db.Event(d.Id);
                if (r.IsActive) Assert.Less(5 * 24 - r.startedAt, d.HardDeadline + 0.26, $"{d.Id} kept waiting past its hard deadline");
            }
            Assert.AreEqual(0, db.Event(FrontierEvents.ExceptionalPresence).triggerCount, "manual-only events never start on their own");
        }

        [Test]
        public void CaravanLost_LeavesAWreck_ThatIsClearedOverTime()
        {
            var db = new WorldStateDatabase();
            var def = FrontierEvents.Def(FrontierEvents.CaravanAttack);
            var r = db.Event(def.Id);
            WorldEventRules.Start(def, r, 20, 0);
            WorldEventRules.Resolve(r, false, 20.5, true);
            FrontierEvents.ApplyOutcome(db, def, r, false, false, 20.5);
            var spot = FrontierEvents.Spot(def.Id, 0);
            string cart = $"cart_{spot.X:0}_{spot.Z:0}";
            Assert.AreEqual(1, db.GetFact("Lost_Caravan_001"));
            Assert.AreEqual(RecoveryStage.Damaged, db.StructureStage(cart, 21));
            Assert.AreEqual(RecoveryStage.Repairing, db.StructureStage(cart, 20.5 + WorldStateDatabase.RepairStartsAfter + 1));
            Assert.AreEqual(RecoveryStage.Repaired, db.StructureStage(cart, 20.5 + WorldStateDatabase.RepairedAfter + 1));
        }

        [Test]
        public void VillageAttack_Failure_BreaksTheFence_SuccessWithPlayerIsRemembered()
        {
            var db = new WorldStateDatabase();
            var def = FrontierEvents.Def(FrontierEvents.VillageAttack);
            var r = db.Event(def.Id);
            WorldEventRules.Start(def, r, 46, 1);
            WorldEventRules.Resolve(r, false, 46.4, true);
            FrontierEvents.ApplyOutcome(db, def, r, false, false, 46.4);
            Assert.AreEqual(RecoveryStage.Damaged, db.StructureStage("fence_e", 47));
            Assert.IsTrue(db.SectorFlag("village", "attacked"));

            var db2 = new WorldStateDatabase();
            var r2 = db2.Event(def.Id);
            WorldEventRules.Start(def, r2, 46, 0);
            WorldEventRules.Resolve(r2, true, 46.3, false);
            FrontierEvents.ApplyOutcome(db2, def, r2, true, true, 46.3);
            Assert.AreEqual(1, db2.GetFact("Defended_Village"));
            Assert.AreEqual(RecoveryStage.Intact, db2.StructureStage("fence_ne", 47));
        }

        [Test]
        public void RestingThroughAnActiveEvent_ResolvesItOffscreen()
        {
            var db = new WorldStateDatabase();
            var def = FrontierEvents.Def(FrontierEvents.FamilyPursued);
            var r = db.Event(def.Id);
            WorldEventRules.Start(def, r, 18, 0);
            int resolved = 0;
            WorldEventSimulator.Simulate(FrontierEvents.Defs, db, 18, 30, d => 1f, null, (d, rec) => resolved++, false);
            Assert.AreEqual(1, resolved);
            Assert.AreEqual(WorldEventState.Expired, r.state);
            Assert.IsTrue(r.resolvedOffscreen);
        }

        [Test]
        public void LostChild_Failure_IsASearch_NotATragedy()
        {
            var db = new WorldStateDatabase();
            var def = FrontierEvents.Def(FrontierEvents.LostChild);
            var r = db.Event(def.Id);
            WorldEventRules.Start(def, r, 38, 0);
            WorldEventRules.Resolve(r, false, 41, true);
            FrontierEvents.ApplyOutcome(db, def, r, false, false, 41);
            Assert.AreEqual(1, db.GetFact("Child_Night_Search"));
            Assert.IsTrue(db.NpcAlive("kei") && db.NpcAlive("mio"));
        }
    }
}
