using System.Collections.Generic;
using BreathOfEclipse.World;
using NUnit.Framework;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.Tests
{
    /// <summary>The Asagiri Frontier layout and its NPC life, simulated off-line (no engine).</summary>
    public class FrontierNavigationTests
    {
        private static NavGraph _graph;
        private static NavGraph Graph => _graph ?? (_graph = L.BuildNavGraph());

        [Test]
        public void EveryPlaceNode_IsReachable_FromThePlaza()
        {
            var lost = Graph.Unreachable("plaza");
            Assert.IsEmpty(lost, "unreachable: " + string.Join(", ", lost.ConvertAll(n => n.Id)));
        }

        [Test]
        public void NoNavEdge_CrossesTheRiver_OrThePond()
        {
            var bad = new List<string>();
            foreach (var n in Graph.Nodes)
                foreach (var e in n.Edges)
                {
                    var b = Graph.Get(e.To);
                    if (L.CrossesWater(n.X, n.Z, b.X, b.Z)) bad.Add($"{n.Id}->{b.Id}");
                }
            Assert.IsEmpty(bad, string.Join(", ", bad));
        }

        [Test]
        public void NoNavEdge_PassesThroughABuilding()
        {
            var bad = new List<string>();
            foreach (var n in Graph.Nodes)
                foreach (var e in n.Edges)
                {
                    var b = Graph.Get(e.To);
                    if (n.Index < b.Index && L.CrossesSolid(n.X, n.Z, b.X, b.Z)) bad.Add($"{n.Id}->{b.Id}");
                }
            Assert.IsEmpty(bad, string.Join(", ", bad));
        }

        [Test]
        public void EveryRoutineLocation_IsANavNode()
        {
            var missing = new List<string>();
            foreach (var d in L.Npcs)
            {
                var s = RoutineSchedule.ForRole(d.Role, d.Home, d.Work, d.Social);
                foreach (var e in s.Entries)
                    if (e.Location != null && Graph.Get(e.Location) == null) missing.Add($"{d.Id}:{e.Location}");
            }
            Assert.IsEmpty(missing, string.Join(", ", missing));
        }

        [Test]
        public void Slots_AreStandable_AndDifferBetweenNpcs()
        {
            var plaza = L.Place("plaza");
            var seen = new HashSet<(int, int)>();
            foreach (var d in L.Npcs)
            {
                NpcPlaces.Slot(plaza, NpcActivity.Talk, d.LookSeed, out float x, out float z, out _);
                Assert.IsTrue(NpcPlaces.Standable(x, z), $"{d.Id} slot at plaza ({x:0.0}, {z:0.0}) not standable");
                seen.Add(((int)(x * 2f), (int)(z * 2f)));
            }
            Assert.Greater(seen.Count, L.Npcs.Count * 3 / 4, "NPCs stack on the same slot");
        }

        [Test]
        public void BuildingFootprints_AreBlocked_DoorFrontsAreNot()
        {
            var inn = L.Place("inn");
            Assert.IsTrue(NpcPlaces.Blocked(inn.X, inn.Z));
            L.FrontPoint(inn, 1.2f, out float fx, out float fz);
            Assert.IsFalse(NpcPlaces.Blocked(fx, fz), "the inn's door front must be free");
        }

        [Test]
        public void Route_FollowsGraph_AndSamplesMonotonically()
        {
            var from = Graph.Get("house_g");
            var to = Graph.Get("chop_1");
            var r = NpcRoute.Plan(Graph, from.Index, from.X, from.Z, to.Index, to.X + 1f, to.Z);
            Assert.NotNull(r);
            Assert.Greater(r.Length, 50f);
            r.Sample(r.Length, out float x, out float z, out _);
            Assert.That(x, Is.EqualTo(to.X + 1f).Within(0.01f));
            Assert.That(z, Is.EqualTo(to.Z).Within(0.01f));
        }
    }

    public class NpcSimulationTests
    {
        private static NpcSimulation NewSim() => new NpcSimulation(L.BuildNavGraph(), L.Npcs, d => d.Role != NpcRole.Hunter);

        private static bool InsideBuilding(float x, float z) => NpcPlaces.Blocked(x, z, -0.2f);

        [Test]
        public void FullDay_NpcsNeverWalkIntoWaterOrBuildings()
        {
            var sim = NewSim();
            sim.PlaceAt(0f);
            var problems = new List<string>();
            const float stepHours = 0.02f;
            const float secondsPerHour = 90f;
            for (float h = 0f; h < 24f; h += stepHours)
            {
                sim.Tick(h, stepHours * secondsPerHour);
                foreach (var n in sim.Npcs)
                {
                    if (n.Indoors) continue;
                    bool water = L.IsWater(n.X, n.Z) && !L.InPaddy(n.X, n.Z)
                                 && !(System.Math.Abs(n.X) < 2.6f && n.Z > -66f && n.Z < -30f)
                                 && !(System.Math.Abs(n.X - 190f) < 1.6f && n.Z > -80f && n.Z < -30f);
                    if (water) problems.Add($"{n.Def.Id} in water at {h:0.00}h ({n.X:0.0},{n.Z:0.0})");
                    if (InsideBuilding(n.X, n.Z)) problems.Add($"{n.Def.Id} inside a building at {h:0.00}h ({n.X:0.0},{n.Z:0.0})");
                }
                if (problems.Count > 20) break;
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Night_VillagersAreHome_Morning_FarmersAtWork()
        {
            var sim = NewSim();
            sim.PlaceAt(17f);
            const float step = 0.02f;
            float h = 17f;
            for (; h < 23.5f; h += step) sim.Tick(h, step * 90f);
            foreach (var id in new[] { "genta", "mitsu", "ohara", "tetsuo", "haru", "kei", "shion" })
                Assert.IsTrue(sim.Find(id).Indoors, $"{id} should be inside at 23:30");
            // through the night into the morning
            for (float t = 23.5f; t < 34f; t += step) sim.Tick(t % 24f, step * 90f);
            var genta = sim.Find("genta");
            var field = L.Place("field_veg");
            Assert.IsFalse(genta.Indoors);
            Assert.AreEqual(NpcActivity.Farm, genta.Activity);
            Assert.That(System.Math.Abs(genta.X - field.X), Is.LessThan(field.Width * 0.5f + 0.5f));
            Assert.That(System.Math.Abs(genta.Z - field.Z), Is.LessThan(field.Depth * 0.5f + 0.5f));
        }

        [Test]
        public void Guards_StayOutside_AtNight()
        {
            var sim = NewSim();
            sim.PlaceAt(22f);
            var jubei = sim.Find("jubei");
            Assert.IsFalse(jubei.Indoors);
            Assert.AreEqual(NpcActivity.Guard, jubei.Activity);
        }

        [Test]
        public void TimeSkip_SnapsToTheRoutine()
        {
            var sim = NewSim();
            sim.PlaceAt(8f);
            var ohara = sim.Find("ohara");
            Assert.AreEqual(NpcActivity.Trade, ohara.Activity);
            sim.PlaceAt(21f);
            Assert.IsTrue(ohara.Indoors);
            Assert.AreEqual("shop", ohara.Place);
        }

        [Test]
        public void Flee_RunsInside_AndReleaseReturnsToRoutine()
        {
            var sim = NewSim();
            sim.PlaceAt(10f);
            var kei = sim.Find("kei");
            Assert.IsFalse(kei.Indoors);
            sim.Flee(kei);
            Assert.AreEqual(NpcMode.Fleeing, kei.Mode);
            for (int i = 0; i < 600 && !kei.Indoors; i++) sim.Tick(10f, 0.1f);
            Assert.IsTrue(kei.Indoors, "the child should reach home");
            sim.Release(kei, 10.5f);
            for (int i = 0; i < 1200; i++) sim.Tick(10.5f, 0.1f);
            Assert.IsFalse(kei.Indoors);
            Assert.AreEqual(NpcMode.Routine, kei.Mode);
        }

        [Test]
        public void EveryNpc_ArrivesSomewhere_EachDay()
        {
            var sim = NewSim();
            sim.PlaceAt(4f);
            var arrived = new Dictionary<string, int>();
            sim.EntryStarted += n => { };
            const float step = 0.02f;
            for (float h = 4f; h < 28f; h += step)
            {
                sim.Tick(h % 24f, step * 90f);
                foreach (var n in sim.Npcs)
                    if (n.Arrived && !n.Travelling)
                        arrived[n.Def.Id] = arrived.TryGetValue(n.Def.Id, out int c) ? c + 1 : 1;
            }
            foreach (var n in sim.Npcs)
                Assert.IsTrue(arrived.ContainsKey(n.Def.Id) && arrived[n.Def.Id] > 100, $"{n.Def.Id} never settled");
            foreach (var n in sim.Npcs)
                Assert.IsFalse(n.Travelling && n.Route != null && n.Route.Travelled > 2000f, $"{n.Def.Id} walked forever");
        }
    }
}
