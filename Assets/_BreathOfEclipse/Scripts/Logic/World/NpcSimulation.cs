using System;
using System.Collections.Generic;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>Why an NPC is not simply following its routine.</summary>
    public enum NpcMode
    {
        Routine = 0,
        /// <summary>Running home / to the inn and staying inside (night attack, demon nearby).</summary>
        Fleeing = 1,
        /// <summary>Frozen in fear where it stands.</summary>
        Afraid = 2,
        /// <summary>Down and hurt; can be helped.</summary>
        Injured = 3,
        /// <summary>Following someone (a found child following the player, travellers following a hunter).</summary>
        Following = 4,
        /// <summary>Taken by a world event (staged by the event director).</summary>
        Event = 5,
        Dead = 6
    }

    /// <summary>Logical state of one NPC (valid on and off screen).</summary>
    public sealed class NpcSimState
    {
        public NpcDef Def;
        public RoutineSchedule Schedule;
        public float X, Z, Heading;
        /// <summary>Nav node the NPC stands at / last passed.</summary>
        public int AnchorNode = -1;
        public int EntryIndex = -1;
        public RoutineEntry Entry;
        public NpcActivity Activity;
        public string Place;
        public bool Indoors;
        public bool Travelling;
        public NpcRoute Route;
        public float SlotX, SlotZ, SlotYaw;
        public float BaseSpeed = 1.25f;
        public float SpeedMultiplier = 1f;
        public NpcMode Mode;
        public bool Alive = true;
        public bool Injured;
        /// <summary>Talking to the player / blocked: no progress this tick.</summary>
        public bool Held;
        /// <summary>Wandering (patrol, play, carry): seconds until the next small move.</summary>
        public float WanderTimer;
        public bool Wandering;
        public string EventId;
        /// <summary>Visible to the player (drives nothing here; set by the runtime for statistics / speed rules).</summary>
        public bool Visible;
        /// <summary>Arrived at the slot of the current entry.</summary>
        public bool Arrived;
        public int Seed => Def.LookSeed;

        public float Speed => (Mode == NpcMode.Fleeing ? 4.2f : Injured ? 0.6f : Activity == NpcActivity.Play && Wandering ? 2.6f : BaseSpeed) * SpeedMultiplier;
        public bool OnRoute => Travelling && Route != null;
    }

    /// <summary>
    /// NPC routines on the region's nav graph. Every NPC follows its data-driven schedule: when an entry starts it
    /// plans a route to the entry's place, walks it, then does the activity in its own slot (or goes inside and is
    /// hidden). Patrols, children's play and carrying wander around the place. The same simulation runs for visible
    /// NPCs every frame and for off-screen ones at a low rate; <see cref="PlaceAt"/> snaps everyone to where their
    /// routine says after a time skip.
    /// </summary>
    public sealed class NpcSimulation
    {
        public NavGraph Graph { get; }
        public readonly List<NpcSimState> Npcs = new List<NpcSimState>();
        /// <summary>An NPC started a routine entry (state, previous activity).</summary>
        public event Action<NpcSimState> EntryStarted;
        /// <summary>An NPC went inside a building (door handling).</summary>
        public event Action<NpcSimState> WentInside;
        /// <summary>An NPC came out of a building.</summary>
        public event Action<NpcSimState> CameOut;
        public int Replans { get; private set; }
        public int Snaps { get; private set; }

        private readonly Random _rng = new Random(777);

        public NpcSimulation(NavGraph graph, IEnumerable<NpcDef> defs, Func<NpcDef, bool> include = null)
        {
            Graph = graph;
            foreach (var d in defs)
            {
                if (include != null && !include(d)) continue;
                var s = new NpcSimState
                {
                    Def = d,
                    Schedule = RoutineSchedule.ForRole(d.Role, d.Home, d.Work, d.Social),
                    BaseSpeed = d.Child ? 1.45f : d.Elder ? 0.95f : 1.2f + (d.LookSeed % 5) * 0.03f
                };
                Npcs.Add(s);
            }
        }

        public NpcSimState Find(string id)
        {
            foreach (var n in Npcs) if (n.Def.Id == id) return n;
            return null;
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Puts every NPC where its routine has it at <paramref name="hour"/> (scene start, rest, debug time).</summary>
        public void PlaceAt(float hour)
        {
            foreach (var n in Npcs) Snap(n, hour);
        }

        public void Snap(NpcSimState n, float hour)
        {
            Snaps++;
            n.Travelling = false;
            n.Route = null;
            n.Wandering = false;
            n.Held = false;
            n.EntryIndex = n.Schedule.IndexAt(hour);
            n.Entry = n.Schedule.At(hour);
            if (n.Mode == NpcMode.Dead || !n.Alive) return;
            var place = n.Entry.Location != null ? L.Place(n.Entry.Location) : null;
            if (place == null && n.Entry.Location != null)
            {
                // patrol points and other plain nav nodes
                var node = Graph.Get(n.Entry.Location);
                if (node != null)
                {
                    SetSlot(n, node, null, n.Entry.Activity);
                    n.X = n.SlotX;
                    n.Z = n.SlotZ;
                    n.Heading = n.SlotYaw;
                    n.AnchorNode = node.Index;
                    n.Place = n.Entry.Location;
                    n.Activity = ArrivedActivity(n, null);
                    n.Indoors = false;
                    n.Arrived = true;
                }
                return;
            }
            if (place == null) return;
            var target = Graph.Get(place.Id);
            n.Place = place.Id;
            n.AnchorNode = target != null ? target.Index : -1;
            SetSlot(n, target, place, n.Entry.Activity);
            n.X = n.SlotX;
            n.Z = n.SlotZ;
            n.Heading = n.SlotYaw;
            n.Activity = ArrivedActivity(n, place);
            n.Indoors = n.Entry.Indoors;
            n.Arrived = true;
        }

        private void SetSlot(NpcSimState n, NavGraph.Node node, PlaceDef place, NpcActivity activity)
        {
            if (place != null)
            {
                if (n.Entry.Indoors)
                {
                    // the door: walk up to it, then go in
                    L.FrontPoint(place, 0.4f, out n.SlotX, out n.SlotZ);
                    n.SlotYaw = place.Yaw + 180f;
                    return;
                }
                NpcPlaces.Slot(place, activity, n.Seed, out n.SlotX, out n.SlotZ, out n.SlotYaw);
                return;
            }
            if (node == null) return;
            var rng = new Random(n.Seed * 31 + node.Index);
            for (int i = 0; i < 8; i++)
            {
                double a = rng.NextDouble() * Math.PI * 2.0;
                float r = 1.5f + (float)rng.NextDouble() * 2.5f;
                float x = node.X + (float)Math.Sin(a) * r, z = node.Z + (float)Math.Cos(a) * r;
                if (!NpcPlaces.Standable(x, z)) continue;
                n.SlotX = x;
                n.SlotZ = z;
                n.SlotYaw = (float)(a * 180.0 / Math.PI);
                return;
            }
            n.SlotX = node.X;
            n.SlotZ = node.Z;
            n.SlotYaw = 0f;
        }

        /// <summary>What the NPC does once it is at the entry's place.</summary>
        private static NpcActivity ArrivedActivity(NpcSimState n, PlaceDef place)
        {
            var a = n.Entry.Activity;
            if (a == NpcActivity.Walk) return NpcActivity.Idle;
            if (a == NpcActivity.Travel) return place != null && place.Id == "stall" ? NpcActivity.Trade : NpcActivity.Talk;
            return a;
        }

        // ------------------------------------------------------------------ tick

        /// <summary>
        /// Advances every NPC. <paramref name="seconds"/> is real time; <paramref name="speedScale"/> compresses
        /// travel when the clock runs fast (debug 5× / 20×).
        /// </summary>
        public void Tick(float hour, float seconds, float speedScale = 1f)
        {
            foreach (var n in Npcs) Tick(n, hour, seconds, speedScale);
        }

        public void Tick(NpcSimState n, float hour, float seconds, float speedScale = 1f)
        {
            // Events may still move an NPC (GoTo) but its routine waits.
            if (!n.Alive || n.Mode == NpcMode.Dead || n.Mode == NpcMode.Injured || n.Mode == NpcMode.Afraid) return;
            if (n.Mode == NpcMode.Routine)
            {
                int idx = n.Schedule.IndexAt(hour);
                if (idx != n.EntryIndex) BeginEntry(n, hour, idx);
            }
            if (n.Held) return;
            if (n.Travelling && n.Route != null)
            {
                n.Route.Travelled += n.Speed * seconds * Math.Max(1f, speedScale);
                n.Route.Sample(n.Route.Travelled, out n.X, out n.Z, out n.Heading);
                int last = n.Route.LastNodeAt(n.Route.Travelled);
                if (last >= 0) n.AnchorNode = last;
                if (n.Route.Done) Arrive(n);
                return;
            }
            if (n.Wandering && n.Arrived && n.Mode == NpcMode.Routine)
            {
                n.WanderTimer -= seconds * Math.Max(1f, speedScale);
                if (n.WanderTimer <= 0f) WanderStep(n);
            }
        }

        private void BeginEntry(NpcSimState n, float hour, int idx)
        {
            n.EntryIndex = idx;
            n.Entry = n.Schedule.Entries[idx];
            n.Wandering = false;
            EntryStarted?.Invoke(n);
            GoTo(n, n.Entry.Location, n.Entry.Activity, NpcSpeed.Normal);
        }

        public enum NpcSpeed { Normal, Run }

        /// <summary>Sends the NPC to a place (or plain nav node) for an activity; leaves its building first.</summary>
        public void GoTo(NpcSimState n, string location, NpcActivity activity, NpcSpeed speed)
        {
            if (string.IsNullOrEmpty(location)) return;
            var place = L.Place(location);
            var node = Graph.Get(location);
            if (node == null) return;
            if (n.Indoors)
            {
                // Same building and still inside: nothing to do.
                if (n.Entry.Indoors && n.Place == location && n.Mode == NpcMode.Routine) return;
                ComeOut(n);
            }
            bool here = n.Arrived && !n.Travelling && n.Place == location;
            n.Place = location;
            n.Arrived = false;
            SetSlot(n, node, place, activity);
            if (here && !n.Entry.Indoors)
            {
                // already here: stay where we stand, change what we do
                n.SlotX = n.X;
                n.SlotZ = n.Z;
                Arrive(n);
                return;
            }
            int from = n.AnchorNode >= 0 ? n.AnchorNode : (Graph.Nearest(n.X, n.Z)?.Index ?? -1);
            var route = NpcRoute.Plan(Graph, from, n.X, n.Z, node.Index, n.SlotX, n.SlotZ);
            Replans++;
            if (route == null || route.Length < 0.3f)
            {
                n.X = n.SlotX;
                n.Z = n.SlotZ;
                n.Heading = n.SlotYaw;
                n.AnchorNode = node.Index;
                Arrive(n);
                return;
            }
            n.Route = route;
            n.Travelling = true;
            n.Activity = n.Entry.Activity == NpcActivity.Carry ? NpcActivity.Carry : NpcActivity.Walk;
        }

        private void ComeOut(NpcSimState n)
        {
            n.Indoors = false;
            var home = n.Place != null ? L.Place(n.Place) : null;
            if (home != null)
            {
                L.FrontPoint(home, 0.4f, out n.X, out n.Z);
                n.Heading = home.Yaw;
            }
            CameOut?.Invoke(n);
        }

        private void Arrive(NpcSimState n)
        {
            n.Travelling = false;
            n.Route = null;
            n.X = n.SlotX;
            n.Z = n.SlotZ;
            n.Heading = n.SlotYaw;
            n.Arrived = true;
            var place = n.Place != null ? L.Place(n.Place) : null;
            if (n.Mode == NpcMode.Fleeing)
            {
                n.Indoors = true;
                n.Activity = NpcActivity.Idle;
                WentInside?.Invoke(n);
                return;
            }
            n.Activity = ArrivedActivity(n, place);
            if (n.Entry.Indoors && n.Mode == NpcMode.Routine)
            {
                n.Indoors = true;
                WentInside?.Invoke(n);
                return;
            }
            var a = n.Entry.Activity;
            n.Wandering = a == NpcActivity.Patrol || a == NpcActivity.Play || a == NpcActivity.Carry;
            n.WanderTimer = 4f + (float)_rng.NextDouble() * 8f;
        }

        /// <summary>Patrols wander between nearby nodes, children run around, carriers shuttle.</summary>
        private void WanderStep(NpcSimState n)
        {
            var home = Graph.Get(n.Place);
            if (home == null)
            {
                n.Wandering = false;
                return;
            }
            float tx, tz;
            int toNode = home.Index;
            var a = n.Entry.Activity;
            if (a == NpcActivity.Patrol)
            {
                // a node within ~50 m of the patrol place
                var candidates = new List<NavGraph.Node>();
                foreach (var node in Graph.Nodes)
                {
                    float d = (node.X - home.X) * (node.X - home.X) + (node.Z - home.Z) * (node.Z - home.Z);
                    if (d < 50f * 50f && d > 8f * 8f && node.Edges.Count > 0) candidates.Add(node);
                }
                if (candidates.Count == 0)
                {
                    n.WanderTimer = 10f;
                    return;
                }
                var pick = candidates[_rng.Next(candidates.Count)];
                toNode = pick.Index;
                tx = pick.X;
                tz = pick.Z;
            }
            else
            {
                float r = a == NpcActivity.Play ? 4.5f : 5f;
                tx = n.SlotX;
                tz = n.SlotZ;
                for (int i = 0; i < 6; i++)
                {
                    double ang = _rng.NextDouble() * Math.PI * 2.0;
                    float x = home.X + (float)Math.Sin(ang) * r * (0.4f + (float)_rng.NextDouble() * 0.6f);
                    float z = home.Z + (float)Math.Cos(ang) * r * (0.4f + (float)_rng.NextDouble() * 0.6f);
                    if (!NpcPlaces.Standable(x, z) || L.CrossesSolid(n.X, n.Z, x, z) || L.CrossesWater(n.X, n.Z, x, z)) continue;
                    tx = x;
                    tz = z;
                    break;
                }
            }
            var route = new NpcRoute();
            route.AddPoint(n.X, n.Z);
            if (a == NpcActivity.Patrol)
            {
                var plan = NpcRoute.Plan(Graph, n.AnchorNode, n.X, n.Z, toNode, tx, tz);
                if (plan != null) route = plan;
                else route.AddPoint(tx, tz, toNode);
            }
            else route.AddPoint(tx, tz, home.Index);
            n.SlotX = tx;
            n.SlotZ = tz;
            n.SlotYaw = n.Heading;
            if (route.Length < 0.3f)
            {
                n.WanderTimer = 3f;
                return;
            }
            n.Route = route;
            n.Travelling = true;
            n.Arrived = false;
            n.Activity = a == NpcActivity.Carry ? NpcActivity.Carry : a == NpcActivity.Play ? NpcActivity.Play : NpcActivity.Walk;
        }

        // ------------------------------------------------------------------ overrides (events)

        /// <summary>Runs to safety (home, or the inn for visitors) and stays inside until <see cref="Release"/>.</summary>
        public void Flee(NpcSimState n, string shelter = null)
        {
            if (!n.Alive || n.Mode == NpcMode.Dead || n.Mode == NpcMode.Injured) return;
            if (n.Indoors) return;
            n.Mode = NpcMode.Fleeing;
            string target = shelter ?? (L.Place(n.Def.Home) != null && NpcPlaces.Solid(L.Place(n.Def.Home)) ? n.Def.Home : "inn");
            var place = L.Place(target);
            var node = Graph.Get(target);
            if (place == null || node == null) return;
            n.Place = target;
            n.Wandering = false;
            L.FrontPoint(place, 0.4f, out n.SlotX, out n.SlotZ);
            n.SlotYaw = place.Yaw + 180f;
            int from = n.AnchorNode >= 0 ? n.AnchorNode : (Graph.Nearest(n.X, n.Z)?.Index ?? -1);
            var route = NpcRoute.Plan(Graph, from, n.X, n.Z, node.Index, n.SlotX, n.SlotZ);
            if (route == null)
            {
                n.Indoors = true;
                WentInside?.Invoke(n);
                return;
            }
            n.Route = route;
            n.Travelling = true;
            n.Arrived = false;
            n.Activity = NpcActivity.Walk;
        }

        /// <summary>Back to the routine (re-plans to where the schedule has the NPC now).</summary>
        public void Release(NpcSimState n, float hour)
        {
            if (!n.Alive) return;
            n.Mode = NpcMode.Routine;
            n.Held = false;
            n.EventId = null;
            n.EntryIndex = -1;
            if (!n.Indoors) return;
            // Still inside: if the routine wants it inside the same place, stay; otherwise BeginEntry on next tick.
            var e = n.Schedule.At(hour);
            if (e.Indoors && e.Location == n.Place)
            {
                n.EntryIndex = n.Schedule.IndexAt(hour);
                n.Entry = e;
            }
        }

        public void SetInjured(NpcSimState n, bool injured)
        {
            n.Injured = injured;
            if (injured)
            {
                n.Mode = NpcMode.Injured;
                n.Travelling = false;
                n.Route = null;
            }
            else if (n.Mode == NpcMode.Injured) n.Mode = NpcMode.Routine;
        }

        public void Kill(NpcSimState n)
        {
            n.Alive = false;
            n.Mode = NpcMode.Dead;
            n.Travelling = false;
            n.Route = null;
        }
    }
}
