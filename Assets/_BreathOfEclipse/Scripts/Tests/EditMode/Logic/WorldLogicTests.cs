using System.Collections.Generic;
using BreathOfEclipse.World;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    public class WorldClockTests
    {
        [Test]
        public void Tick_AdvancesHours_WithPaceAndMultiplier()
        {
            var c = new WorldClock(1, 6f) { SecondsPerHour = 60f, Multiplier = 5f };
            c.Tick(60f);
            Assert.That(c.Hour, Is.EqualTo(11f).Within(1e-3f));
        }

        [Test]
        public void Tick_PastMidnight_IncrementsDay_AndRaisesNewDay()
        {
            var c = new WorldClock(1, 23.5f) { SecondsPerHour = 10f };
            int newDay = 0;
            c.NewDay += d => newDay = d;
            c.Tick(10f);
            Assert.AreEqual(2, c.Day);
            Assert.AreEqual(2, newDay);
            Assert.That(c.Hour, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(c.TotalHours, Is.EqualTo(24.5).Within(1e-3));
        }

        [Test]
        public void Paused_DoesNotAdvance()
        {
            var c = new WorldClock(1, 12f) { Paused = true };
            c.Tick(1000f);
            Assert.AreEqual(12f, c.Hour);
        }

        [Test]
        public void Phases_FollowTheDay()
        {
            Assert.AreEqual(DayPhase.LateNight, WorldClock.PhaseOf(2f));
            Assert.AreEqual(DayPhase.Dawn, WorldClock.PhaseOf(6f));
            Assert.AreEqual(DayPhase.Morning, WorldClock.PhaseOf(8f));
            Assert.AreEqual(DayPhase.Day, WorldClock.PhaseOf(12f));
            Assert.AreEqual(DayPhase.Afternoon, WorldClock.PhaseOf(15f));
            Assert.AreEqual(DayPhase.Sunset, WorldClock.PhaseOf(17.5f));
            Assert.AreEqual(DayPhase.Night, WorldClock.PhaseOf(20f));
        }

        [Test]
        public void PhaseChanged_IsRaisedOnTransitions()
        {
            var c = new WorldClock(1, 16.9f) { SecondsPerHour = 1f };
            DayPhase from = DayPhase.Day, to = DayPhase.Day;
            c.PhaseChanged += (a, b) => { from = a; to = b; };
            c.Tick(0.2f);
            Assert.AreEqual(DayPhase.Afternoon, from);
            Assert.AreEqual(DayPhase.Sunset, to);
        }

        [Test]
        public void Daylight_IsSmoothAndBounded()
        {
            Assert.AreEqual(0f, WorldClock.Daylight(0f));
            Assert.AreEqual(1f, WorldClock.Daylight(12f));
            float dawn = WorldClock.Daylight(6.2f);
            Assert.That(dawn, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(WorldClock.Daylight(18f), Is.GreaterThan(WorldClock.Daylight(19f)));
        }

        [Test]
        public void InWindow_WrapsPastMidnight()
        {
            Assert.IsTrue(WorldClock.InWindow(23f, 20f, 4f));
            Assert.IsTrue(WorldClock.InWindow(2f, 20f, 4f));
            Assert.IsFalse(WorldClock.InWindow(12f, 20f, 4f));
            Assert.IsTrue(WorldClock.InWindow(12f, 10f, 14f));
        }

        [Test]
        public void SetHourForward_NeverRewinds()
        {
            var c = new WorldClock(3, 20f);
            c.SetHourForward(6f);
            Assert.AreEqual(4, c.Day);
            Assert.That(c.Hour, Is.EqualTo(6f).Within(1e-3f));
        }

        [Test]
        public void Format_PrintsHoursAndMinutes()
        {
            Assert.AreEqual("17:30", WorldClock.Format(17.5f));
            Assert.AreEqual("06:00", WorldClock.Format(6f));
        }
    }

    public class RoutineScheduleTests
    {
        [Test]
        public void Farmer_FollowsTheDay()
        {
            var s = RoutineSchedule.ForRole(NpcRole.Farmer, "home", "field", "plaza");
            Assert.AreEqual(NpcActivity.Sleep, s.At(3f).Activity);
            Assert.AreEqual(NpcActivity.Farm, s.At(8f).Activity);
            Assert.AreEqual("field", s.At(8f).Location);
            Assert.AreEqual(NpcActivity.Eat, s.At(12.5f).Activity);
            Assert.AreEqual(NpcActivity.Farm, s.At(15f).Activity);
            Assert.AreEqual(NpcActivity.Sleep, s.At(22f).Activity);
            Assert.IsTrue(s.At(22f).Indoors);
        }

        [Test]
        public void Merchant_ClosesAtSunset()
        {
            var s = RoutineSchedule.ForRole(NpcRole.Merchant, "home", "shop", "plaza");
            Assert.AreEqual(NpcActivity.Trade, s.At(12f).Activity);
            Assert.AreNotEqual(NpcActivity.Trade, s.At(18f).Activity);
            Assert.AreNotEqual(NpcActivity.Trade, s.At(23f).Activity);
        }

        [Test]
        public void BeforeFirstEntry_UsesYesterdaysLastEntry()
        {
            var s = new RoutineSchedule().Add(6f, NpcActivity.Work, "a").Add(20f, NpcActivity.Sleep, "b", true);
            Assert.AreEqual(NpcActivity.Sleep, s.At(3f).Activity);
            Assert.That(s.HoursToNext(3f), Is.EqualTo(3f).Within(1e-3f));
        }
    }

    public class WorldEventModelTests
    {
        private static WorldEventDef Caravan() => new WorldEventDef
        {
            Id = "caravan",
            WindowStart = 8f,
            WindowEnd = 19f,
            SoftDeadline = 0.25f,
            HardDeadline = 0.85f,
            CooldownHours = 20f,
            BaseSuccessChance = 0.5f
        };

        [Test]
        public void Eligibility_FollowsTimeWindow()
        {
            var def = Caravan();
            var r = new WorldEventRecord { id = def.Id };
            WorldEventRules.UpdateEligibility(def, r, 1, 3f, 3);
            Assert.AreEqual(WorldEventState.Dormant, r.state);
            WorldEventRules.UpdateEligibility(def, r, 1, 10f, 10);
            Assert.AreEqual(WorldEventState.Eligible, r.state);
        }

        [Test]
        public void NightOnly_IsNeverEligibleByDay()
        {
            var def = new WorldEventDef { Id = "raid", NightOnly = true };
            var r = new WorldEventRecord { id = def.Id };
            WorldEventRules.UpdateEligibility(def, r, 1, 13f, 13);
            Assert.AreEqual(WorldEventState.Dormant, r.state);
            WorldEventRules.UpdateEligibility(def, r, 1, 22f, 22);
            Assert.AreEqual(WorldEventState.Eligible, r.state);
        }

        [Test]
        public void UnattendedEvent_ResolvesAtSoftDeadline_ThenExpiresAtHard()
        {
            var def = Caravan();
            var r = new WorldEventRecord { id = def.Id };
            WorldEventRules.Start(def, r, 18.0, 0);
            Assert.AreEqual(WorldEventState.Active, r.state);
            Assert.AreEqual(WorldEventRules.Step.None, WorldEventRules.Advance(def, r, 18.1, false, 0.5f));
            Assert.AreEqual(WorldEventRules.Step.ResolvedOffscreen, WorldEventRules.Advance(def, r, 18.3, false, 0.5f));
            Assert.IsTrue(r.IsResolved);
            Assert.IsTrue(r.resolvedOffscreen);
            Assert.AreEqual(WorldEventRules.Step.Expired, WorldEventRules.Advance(def, r, 18.9, false, 0.5f));
            Assert.AreEqual(WorldEventState.Expired, r.state);
            Assert.That(r.cooldownUntil, Is.EqualTo(38.9).Within(1e-6));
        }

        [Test]
        public void PlayerPresent_KeepsEventActiveUntilHardDeadline()
        {
            var def = Caravan();
            var r = new WorldEventRecord { id = def.Id };
            WorldEventRules.Start(def, r, 10.0, 0);
            Assert.AreEqual(WorldEventRules.Step.None, WorldEventRules.Advance(def, r, 10.5, true, 0.5f));
            Assert.IsTrue(r.IsActive);
            Assert.AreEqual(WorldEventRules.Step.ResolvedOffscreen, WorldEventRules.Advance(def, r, 10.9, true, 0.5f));
            Assert.AreEqual(WorldEventState.Expired, r.state, "past the hard deadline it resolves and clears at once");
        }

        [Test]
        public void ResolvedByPlayer_IsNotOverriddenOffscreen()
        {
            var def = Caravan();
            var r = new WorldEventRecord { id = def.Id };
            WorldEventRules.Start(def, r, 10.0, 0);
            WorldEventRules.Resolve(r, true, 10.1, false);
            WorldEventRules.Advance(def, r, 10.3, false, 0f);
            Assert.AreEqual(WorldEventState.ResolvedSuccess, r.state);
            Assert.IsFalse(r.resolvedOffscreen);
        }

        [Test]
        public void SuccessChance_ExtremesAreRespected()
        {
            var def = Caravan();
            for (int i = 0; i < 20; i++)
            {
                var a = new WorldEventRecord { id = def.Id };
                WorldEventRules.Start(def, a, 10, i);
                WorldEventRules.Advance(def, a, 11, false, 1f);
                Assert.AreEqual(1, a.successes);
                var b = new WorldEventRecord { id = def.Id };
                WorldEventRules.Start(def, b, 10, i);
                WorldEventRules.Advance(def, b, 11, false, 0f);
                Assert.AreEqual(1, b.failures);
            }
        }

        [Test]
        public void Roll_IsDeterministic()
        {
            Assert.AreEqual(WorldEventRules.Roll("x", 3, 4), WorldEventRules.Roll("x", 3, 4));
            Assert.AreNotEqual(WorldEventRules.Roll("x", 3, 4), WorldEventRules.Roll("x", 4, 4));
        }

        [Test]
        public void TimeSkip_StartsResolvesAndExpiresEvents()
        {
            var defs = new List<WorldEventDef> { new WorldEventDef { Id = "always", ChancePerCheck = 1f, SoftDeadline = 0.3f, HardDeadline = 1f, CooldownHours = 3f } };
            var db = new WorldStateDatabase();
            int resolvedCallbacks = 0;
            var report = WorldEventSimulator.Simulate(defs, db, 22.0, 30.0, d => 0.5f, null, (d, r) => resolvedCallbacks++);
            Assert.That(report.Started, Is.GreaterThanOrEqualTo(2), "an 8 hour sleep sees the event happen more than once");
            Assert.AreEqual(report.Resolved, resolvedCallbacks);
            Assert.That(report.Expired, Is.GreaterThanOrEqualTo(1));
            var r = db.Event("always");
            Assert.AreEqual(report.Started, r.triggerCount);
            Assert.AreEqual(r.triggerCount, r.successes + r.failures + (r.IsActive ? 1 : 0));
        }

        [Test]
        public void TimeSkip_ResolvesAnEventThatWasActiveBeforeSleeping()
        {
            var def = Caravan();
            var defs = new List<WorldEventDef> { def };
            var db = new WorldStateDatabase();
            WorldEventRules.Start(def, db.Event(def.Id), 17.9, 0);
            WorldEventSimulator.Simulate(defs, db, 17.9, 26.0, d => 0.5f, allowStarts: false);
            var r = db.Event(def.Id);
            Assert.AreEqual(WorldEventState.Expired, r.state);
            Assert.AreEqual(1, r.successes + r.failures);
        }
    }

    public class WorldStateTests
    {
        [Test]
        public void Facts_SetGetAndNotify()
        {
            var db = new WorldStateDatabase();
            string seen = null;
            db.FactChanged += (k, v) => seen = k;
            db.SetFact("Saved_Caravan_001", 1, 12.5);
            Assert.IsTrue(db.HasFact("Saved_Caravan_001"));
            Assert.AreEqual(1, db.GetFact("Saved_Caravan_001"));
            Assert.AreEqual("Saved_Caravan_001", seen);
            db.AddFact("Saved_Caravan_001", 2, 13);
            Assert.AreEqual(3, db.GetFact("Saved_Caravan_001"));
        }

        [Test]
        public void Migrate_FillsMissingListsOfOldSaves()
        {
            var data = new WorldStateData { version = 0, facts = null, npcs = null, events = null, demons = null, sectors = null, structures = null, discoveredAreas = null, hour = 30f };
            var db = new WorldStateDatabase(data);
            Assert.IsNotNull(db.Data.facts);
            Assert.IsNotNull(db.Data.events);
            Assert.AreEqual(WorldStateData.CurrentVersion, db.Data.version);
            Assert.That(db.Data.hour, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void Demons_GetUniquePersistentIds()
        {
            var db = new WorldStateDatabase();
            var a = db.NewDemon("nightspawn", "forest", 1);
            var b = db.NewDemon("nightspawn", "forest", 1);
            Assert.AreNotEqual(a.persistentId, b.persistentId);
            a.escaped = true;
            Assert.AreSame(a, db.Demon(a.persistentId));
            CollectionAssert.Contains(new List<DemonRecordData>(db.EscapedDemons()), a);
        }

        [Test]
        public void Structures_RecoverInStages()
        {
            var db = new WorldStateDatabase();
            Assert.AreEqual(RecoveryStage.Intact, db.StructureStage("fence_n", 10));
            db.DamageStructure("fence_n", "raid", 20);
            Assert.AreEqual(RecoveryStage.Damaged, db.StructureStage("fence_n", 25));
            Assert.AreEqual(RecoveryStage.Repairing, db.StructureStage("fence_n", 40));
            Assert.AreEqual(RecoveryStage.Repaired, db.StructureStage("fence_n", 60));
        }

        [Test]
        public void Discover_OnlyOnce()
        {
            var db = new WorldStateDatabase();
            Assert.IsTrue(db.Discover("cave"));
            Assert.IsFalse(db.Discover("cave"));
            Assert.IsTrue(db.IsDiscovered("cave"));
        }
    }

    public class SectorStreamingTests
    {
        private static SectorStreamingModel Model()
        {
            var m = new SectorStreamingModel { LoadDistance = 50f, UnloadDistance = 80f };
            m.Add("a", new WorldRect(0, 0, 100, 100));
            m.Add("b", new WorldRect(100, 0, 200, 100));
            m.Add("c", new WorldRect(300, 0, 400, 100));
            return m;
        }

        [Test]
        public void LoadsCurrentAndNearby_NotFar()
        {
            var m = Model();
            var load = new List<SectorStreamingModel.Sector>();
            var unload = new List<SectorStreamingModel.Sector>();
            m.Plan(60, 50, load, unload);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, load.ConvertAll(s => s.Id));
            Assert.AreEqual("a", load[0].Id, "the sector the player stands in loads first");
        }

        [Test]
        public void Hysteresis_KeepsLoadedSectorsUntilUnloadDistance()
        {
            var m = Model();
            var b = m.Find("b");
            b.State = SectorLoadState.Loaded;
            var load = new List<SectorStreamingModel.Sector>();
            var unload = new List<SectorStreamingModel.Sector>();
            m.Plan(30, 50, load, unload); // 70 m from b: beyond load distance, inside unload distance
            Assert.IsFalse(unload.Contains(b));
            m.Plan(10, 50, load, unload); // 90 m away
            Assert.IsTrue(unload.Contains(b));
        }

        [Test]
        public void At_FindsContainingSector()
        {
            var m = Model();
            Assert.AreEqual("b", m.At(150, 20).Id);
            Assert.IsNull(m.At(250, 20));
        }

        [Test]
        public void Pinned_StaysLoaded_SuppressedClearsWhenEntered()
        {
            var m = Model();
            var c = m.Find("c");
            c.Pinned = true;
            Assert.IsTrue(m.ShouldBeLoaded(c, 0, 0));
            var a = m.Find("a");
            a.Suppressed = true;
            var load = new List<SectorStreamingModel.Sector>();
            var unload = new List<SectorStreamingModel.Sector>();
            m.Plan(120, 50, load, unload);
            Assert.IsFalse(load.Contains(a));
            m.Plan(50, 50, load, unload);
            Assert.IsTrue(load.Contains(a));
        }
    }

    public class NavGraphTests
    {
        private static NavGraph Graph()
        {
            var g = new NavGraph();
            g.AddNode("village", 0, 0, 0);
            g.AddNode("road1", 0, 0, 50);
            g.AddNode("bridge", 0, 0, 100);
            g.AddNode("forest", 40, 0, 60);
            g.AddNode("camp", 0, 0, 150);
            g.Chain(PathKind.Road, "village", "road1", "bridge", "camp");
            g.Link("village", "forest", PathKind.Forest);
            g.Link("forest", "camp", PathKind.Forest);
            g.AddNode("island", 500, 0, 500);
            return g;
        }

        [Test]
        public void Villagers_PreferRoads()
        {
            var g = Graph();
            var path = g.FindPath("village", "camp");
            CollectionAssert.AreEqual(new[] { "village", "road1", "bridge", "camp" }, path.ConvertAll(i => g.Get(i).Id));
        }

        [Test]
        public void Unreachable_ReturnsEmptyPath_AndIsReported()
        {
            var g = Graph();
            Assert.IsEmpty(g.FindPath("village", "island"));
            var lost = g.Unreachable("village");
            Assert.AreEqual(1, lost.Count);
            Assert.AreEqual("island", lost[0].Id);
        }

        [Test]
        public void PointAlong_InterpolatesThePath()
        {
            var g = Graph();
            var path = g.FindPath("village", "bridge");
            g.PointAlong(path, 75f, out float x, out float y, out float z);
            Assert.That(z, Is.EqualTo(75f).Within(1e-3f));
            Assert.That(g.PathLength(path), Is.EqualTo(100f).Within(1e-3f));
        }

        [Test]
        public void Nearest_IgnoresUnlinkedNodes()
        {
            var g = Graph();
            Assert.AreEqual("bridge", g.Nearest(5, 98).Id);
            Assert.AreNotEqual("island", g.Nearest(480, 480).Id, "the unlinked island node is never a travel target");
            Assert.AreEqual("island", g.Nearest(480, 480, false).Id);
        }
    }

    public class DemonWorldRulesTests
    {
        [Test]
        public void Population_NightIsDangerous_VillageIsSafe()
        {
            Assert.AreEqual(0, DemonWorldRules.DesiredPopulation(12f, 0, false));
            Assert.AreEqual(0, DemonWorldRules.DesiredPopulation(23f, 0, false));
            Assert.AreEqual(0, DemonWorldRules.DesiredPopulation(12f, 1, false));
            Assert.That(DemonWorldRules.DesiredPopulation(23f, 2, false), Is.GreaterThan(DemonWorldRules.DesiredPopulation(12f, 2, false)));
            Assert.That(DemonWorldRules.DesiredPopulation(23f, 2, true), Is.GreaterThan(DemonWorldRules.DesiredPopulation(23f, 2, false)));
            Assert.That(DemonWorldRules.DesiredPopulation(12f, 3, false), Is.GreaterThan(0), "cursed ground is never empty");
        }

        [Test]
        public void Spawn_NeverCloseOrInSight()
        {
            Assert.IsFalse(DemonWorldRules.CanSpawnAt(3f, false));
            Assert.IsFalse(DemonWorldRules.CanSpawnAt(50f, true));
            Assert.IsTrue(DemonWorldRules.CanSpawnAt(50f, false));
            Assert.IsTrue(DemonWorldRules.CanSpawnAt(90f, true));
        }

        [Test]
        public void Flee_OnlyWhenBadlyHurt()
        {
            Assert.IsFalse(DemonWorldRules.ShouldFlee(0.8f, 1, false, false, 0f));
            Assert.IsTrue(DemonWorldRules.ShouldFlee(0.2f, 1, false, false, 0.1f));
            Assert.IsFalse(DemonWorldRules.ShouldFlee(0.2f, 1, false, false, 0.99f), "some fight to the end");
        }

        [Test]
        public void Leash_StopsLongChases()
        {
            Assert.IsFalse(DemonWorldRules.ShouldDisengage(20f, 25f, 1f, false));
            Assert.IsTrue(DemonWorldRules.ShouldDisengage(200f, 25f, 0f, false));
            Assert.IsTrue(DemonWorldRules.ShouldDisengage(10f, 25f, 12f, false));
        }
    }
}
