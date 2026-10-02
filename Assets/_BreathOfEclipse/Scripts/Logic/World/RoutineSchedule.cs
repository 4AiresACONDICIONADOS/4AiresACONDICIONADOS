using System;
using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    /// <summary>What an NPC is doing (drives its animation, props and dialogue).</summary>
    public enum NpcActivity
    {
        Idle = 0,
        Sleep = 1,
        Walk = 2,
        Work = 3,
        Farm = 4,
        Chop = 5,
        Fish = 6,
        Trade = 7,
        Talk = 8,
        Sit = 9,
        Eat = 10,
        Patrol = 11,
        Guard = 12,
        Camp = 13,
        Travel = 14,
        Play = 15,
        Carry = 16,
        Smith = 17,
        Pray = 18
    }

    /// <summary>One line of a routine: from <see cref="Start"/> (hour) do <see cref="Activity"/> at <see cref="Location"/>.</summary>
    [Serializable]
    public struct RoutineEntry
    {
        public float Start;
        public NpcActivity Activity;
        /// <summary>Navigation node / named spot (e.g. "home_3", "field_w", "plaza").</summary>
        public string Location;
        /// <summary>Inside a building: the NPC is not drawn (doors close behind it).</summary>
        public bool Indoors;

        public RoutineEntry(float start, NpcActivity activity, string location, bool indoors = false)
        {
            Start = start;
            Activity = activity;
            Location = location;
            Indoors = indoors;
        }
    }

    /// <summary>
    /// A data-driven daily routine: entries sorted by start hour; the active entry is the last one that started,
    /// wrapping around midnight (an entry at 21:00 is still active at 03:00 if nothing starts before).
    /// </summary>
    public sealed class RoutineSchedule
    {
        private readonly List<RoutineEntry> _entries = new List<RoutineEntry>();

        public IReadOnlyList<RoutineEntry> Entries => _entries;

        public RoutineSchedule(params RoutineEntry[] entries)
        {
            foreach (var e in entries) Add(e);
        }

        public RoutineSchedule Add(RoutineEntry entry)
        {
            entry.Start = WorldClock.Wrap(entry.Start);
            _entries.Add(entry);
            _entries.Sort((a, b) => a.Start.CompareTo(b.Start));
            return this;
        }

        public RoutineSchedule Add(float start, NpcActivity activity, string location, bool indoors = false) =>
            Add(new RoutineEntry(start, activity, location, indoors));

        public int IndexAt(float hour)
        {
            if (_entries.Count == 0) return -1;
            hour = WorldClock.Wrap(hour);
            int index = _entries.Count - 1; // before the first start: still yesterday's last entry
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Start <= hour) index = i;
                else break;
            }
            return index;
        }

        public RoutineEntry At(float hour)
        {
            int i = IndexAt(hour);
            return i >= 0 ? _entries[i] : new RoutineEntry(0f, NpcActivity.Idle, null);
        }

        /// <summary>Hours until the next entry starts (24 when the routine has a single entry).</summary>
        public float HoursToNext(float hour)
        {
            if (_entries.Count <= 1) return WorldClock.HoursPerDay;
            int i = IndexAt(hour);
            var next = _entries[(i + 1) % _entries.Count];
            float h = WorldClock.HoursUntil(hour, next.Start);
            return h <= 0f ? WorldClock.HoursPerDay : h;
        }

        // ------------------------------------------------------------------ archetypes

        /// <summary>Default routine of an NPC archetype, filled with its own places.</summary>
        public static RoutineSchedule ForRole(NpcRole role, string home, string work, string social)
        {
            var s = new RoutineSchedule();
            switch (role)
            {
                case NpcRole.Farmer:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(5.5f, NpcActivity.Walk, work).Add(6f, NpcActivity.Farm, work)
                     .Add(12f, NpcActivity.Eat, social).Add(13f, NpcActivity.Farm, work).Add(17.5f, NpcActivity.Walk, social)
                     .Add(18.25f, NpcActivity.Talk, social).Add(19.25f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Merchant:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(6.5f, NpcActivity.Walk, work).Add(7f, NpcActivity.Trade, work)
                     .Add(17.5f, NpcActivity.Carry, work).Add(18.5f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Lumberjack:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(6f, NpcActivity.Walk, work).Add(6.75f, NpcActivity.Chop, work)
                     .Add(12f, NpcActivity.Eat, work).Add(12.75f, NpcActivity.Chop, work).Add(17f, NpcActivity.Carry, social)
                     .Add(18f, NpcActivity.Talk, social).Add(19f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Fisher:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(5f, NpcActivity.Walk, work).Add(5.5f, NpcActivity.Fish, work)
                     .Add(11.5f, NpcActivity.Carry, social).Add(12.5f, NpcActivity.Eat, social).Add(14f, NpcActivity.Fish, work)
                     .Add(17.25f, NpcActivity.Walk, social).Add(18.5f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Parent:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(6.5f, NpcActivity.Work, home).Add(9f, NpcActivity.Walk, social)
                     .Add(9.5f, NpcActivity.Talk, social).Add(11f, NpcActivity.Work, work).Add(15f, NpcActivity.Talk, social)
                     .Add(17f, NpcActivity.Work, home).Add(19f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Child:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(7.5f, NpcActivity.Play, social).Add(12f, NpcActivity.Eat, home)
                     .Add(13f, NpcActivity.Play, work).Add(17f, NpcActivity.Play, social).Add(18f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Guard:
                    s.Add(0f, NpcActivity.Guard, work).Add(6f, NpcActivity.Patrol, social).Add(10f, NpcActivity.Guard, work)
                     .Add(14f, NpcActivity.Sleep, home, true).Add(19f, NpcActivity.Guard, work);
                    break;
                case NpcRole.Hunter:
                    s.Add(0f, NpcActivity.Patrol, work).Add(4f, NpcActivity.Walk, home).Add(5f, NpcActivity.Sleep, home, true)
                     .Add(12f, NpcActivity.Sit, social).Add(16.5f, NpcActivity.Walk, work).Add(17.5f, NpcActivity.Patrol, work);
                    break;
                case NpcRole.Traveler:
                    s.Add(0f, NpcActivity.Camp, home).Add(7f, NpcActivity.Travel, work).Add(17.5f, NpcActivity.Travel, home)
                     .Add(19f, NpcActivity.Camp, home);
                    break;
                case NpcRole.Camper:
                    // A wanderer living rough by a wayside shrine: prays at dawn, visits the camp, fishes, back at dusk.
                    s.Add(0f, NpcActivity.Camp, home).Add(6f, NpcActivity.Pray, work).Add(8f, NpcActivity.Walk, social)
                     .Add(9.5f, NpcActivity.Sit, social).Add(13f, NpcActivity.Walk, work).Add(14f, NpcActivity.Fish, work)
                     .Add(18f, NpcActivity.Camp, home);
                    break;
                case NpcRole.Smith:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(6.5f, NpcActivity.Smith, work).Add(12f, NpcActivity.Eat, social)
                     .Add(13f, NpcActivity.Smith, work).Add(18f, NpcActivity.Talk, social).Add(19.5f, NpcActivity.Sleep, home, true);
                    break;
                case NpcRole.Priest:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(5f, NpcActivity.Pray, work).Add(9f, NpcActivity.Walk, social)
                     .Add(10f, NpcActivity.Talk, social).Add(13f, NpcActivity.Pray, work).Add(18.5f, NpcActivity.Sleep, home, true);
                    break;
                default:
                    s.Add(0f, NpcActivity.Sleep, home, true).Add(7f, NpcActivity.Walk, social).Add(8f, NpcActivity.Work, work)
                     .Add(12f, NpcActivity.Eat, social).Add(13f, NpcActivity.Work, work).Add(17.5f, NpcActivity.Talk, social)
                     .Add(19f, NpcActivity.Sleep, home, true);
                    break;
            }
            return s;
        }
    }

    public enum NpcRole
    {
        Villager = 0,
        Farmer = 1,
        Merchant = 2,
        Lumberjack = 3,
        Fisher = 4,
        Parent = 5,
        Child = 6,
        Traveler = 7,
        Hunter = 8,
        Guard = 9,
        Camper = 10,
        Smith = 11,
        Priest = 12
    }
}
