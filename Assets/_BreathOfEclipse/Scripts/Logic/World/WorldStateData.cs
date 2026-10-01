using System;
using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    /// <summary>A significant thing that happened ("Saved_Caravan_001" = 1). Future reputation / story read these.</summary>
    [Serializable]
    public sealed class WorldFact
    {
        public string key;
        public int value;
        public double totalHours;
    }

    [Serializable]
    public sealed class SectorStateData
    {
        public string id;
        public bool discovered;
        public int visits;
        /// <summary>Free-form significant flags ("camp_destroyed", "cave_found").</summary>
        public List<string> flags = new List<string>();
    }

    [Serializable]
    public sealed class NpcStateData
    {
        public string id;
        public bool alive = true;
        public bool injured;
        public bool savedByPlayer;
        public bool missing;
        public string lastKnownSector;
        public string eventId;
        public double injuredAt = -1;
    }

    /// <summary>A demon the world remembers (escaped ones may come back changed in later versions).</summary>
    [Serializable]
    public sealed class DemonRecordData
    {
        public string persistentId;
        public string archetype;
        public bool alive = true;
        public bool escaped;
        public bool scarred;
        /// <summary>0..1 conceptual health when it escaped.</summary>
        public float health = 1f;
        public int encounters;
        public string lastSector;
        /// <summary>Breathing styles the demon saw the player use (style ids).</summary>
        public List<string> observedBreathing = new List<string>();
        public double lastSeenHours;
    }

    /// <summary>A place that can be damaged and repaired (fence, cart, house wall).</summary>
    [Serializable]
    public sealed class StructureStateData
    {
        public string id;
        /// <summary>-1 = intact.</summary>
        public double damagedAt = -1;
        public string cause;
    }

    public enum RecoveryStage
    {
        Intact = 0,
        Damaged = 1,
        Repairing = 2,
        Repaired = 3
    }

    /// <summary>Everything the world remembers. Saved as JSON (versioned); only significant state, never every stone.</summary>
    [Serializable]
    public sealed class WorldStateData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int day = 1;
        public float hour = 7f;
        public bool hasPlayerPosition;
        public float playerX, playerY, playerZ, playerYaw;
        public string lastSector;
        public List<WorldFact> facts = new List<WorldFact>();
        public List<SectorStateData> sectors = new List<SectorStateData>();
        public List<NpcStateData> npcs = new List<NpcStateData>();
        public List<DemonRecordData> demons = new List<DemonRecordData>();
        public List<WorldEventRecord> events = new List<WorldEventRecord>();
        public List<StructureStateData> structures = new List<StructureStateData>();
        /// <summary>Map areas the player found (secret places are only added by visiting them).</summary>
        public List<string> discoveredAreas = new List<string>();
        public int nextDemonSerial = 1;
    }

    /// <summary>
    /// Typed access to <see cref="WorldStateData"/> (get-or-create records, facts, recovery stages) plus migration of
    /// older / missing data. Pure C#: the runtime saves <see cref="Data"/> with JsonUtility.
    /// </summary>
    public sealed class WorldStateDatabase
    {
        /// <summary>Recovery timeline after damage (hours): damaged, then villagers repair, then fixed.</summary>
        public const double RepairStartsAfter = 10.0;
        public const double RepairedAfter = 30.0;

        public WorldStateData Data { get; private set; }

        /// <summary>Raised when a fact is set (key, value): hook for v0.6 reputation / story / relationships.</summary>
        public event Action<string, int> FactChanged;

        public WorldStateDatabase(WorldStateData data = null)
        {
            Data = data ?? new WorldStateData();
            Migrate();
        }

        /// <summary>Fills missing lists (older saves), clamps values, bumps the version.</summary>
        public void Migrate()
        {
            var d = Data;
            if (d.facts == null) d.facts = new List<WorldFact>();
            if (d.sectors == null) d.sectors = new List<SectorStateData>();
            if (d.npcs == null) d.npcs = new List<NpcStateData>();
            if (d.demons == null) d.demons = new List<DemonRecordData>();
            if (d.events == null) d.events = new List<WorldEventRecord>();
            if (d.structures == null) d.structures = new List<StructureStateData>();
            if (d.discoveredAreas == null) d.discoveredAreas = new List<string>();
            d.facts.RemoveAll(f => f == null || string.IsNullOrEmpty(f.key));
            d.sectors.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
            d.npcs.RemoveAll(n => n == null || string.IsNullOrEmpty(n.id));
            d.demons.RemoveAll(n => n == null || string.IsNullOrEmpty(n.persistentId));
            d.events.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));
            d.structures.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
            foreach (var s in d.sectors) if (s.flags == null) s.flags = new List<string>();
            foreach (var m in d.demons) if (m.observedBreathing == null) m.observedBreathing = new List<string>();
            if (d.day < 1) d.day = 1;
            if (float.IsNaN(d.hour) || d.hour < 0f || d.hour >= 24f) d.hour = WorldClock.Wrap(float.IsNaN(d.hour) ? 7f : d.hour);
            if (d.nextDemonSerial < 1) d.nextDemonSerial = 1;
            d.version = WorldStateData.CurrentVersion;
        }

        // ------------------------------------------------------------------ facts

        public bool HasFact(string key) => FindFact(key) != null;

        public int GetFact(string key, int fallback = 0)
        {
            var f = FindFact(key);
            return f != null ? f.value : fallback;
        }

        public void SetFact(string key, int value, double totalHours)
        {
            if (string.IsNullOrEmpty(key)) return;
            var f = FindFact(key);
            if (f == null)
            {
                f = new WorldFact { key = key };
                Data.facts.Add(f);
            }
            f.value = value;
            f.totalHours = totalHours;
            FactChanged?.Invoke(key, value);
        }

        public void AddFact(string key, int delta, double totalHours) => SetFact(key, GetFact(key) + delta, totalHours);

        private WorldFact FindFact(string key)
        {
            foreach (var f in Data.facts) if (f.key == key) return f;
            return null;
        }

        // ------------------------------------------------------------------ records

        public SectorStateData Sector(string id)
        {
            foreach (var s in Data.sectors) if (s.id == id) return s;
            var n = new SectorStateData { id = id };
            Data.sectors.Add(n);
            return n;
        }

        public bool SectorFlag(string sector, string flag)
        {
            foreach (var s in Data.sectors) if (s.id == sector) return s.flags.Contains(flag);
            return false;
        }

        public void SetSectorFlag(string sector, string flag, bool on)
        {
            var s = Sector(sector);
            if (on && !s.flags.Contains(flag)) s.flags.Add(flag);
            else if (!on) s.flags.Remove(flag);
        }

        public NpcStateData Npc(string id)
        {
            foreach (var n in Data.npcs) if (n.id == id) return n;
            var r = new NpcStateData { id = id };
            Data.npcs.Add(r);
            return r;
        }

        public bool NpcAlive(string id)
        {
            foreach (var n in Data.npcs) if (n.id == id) return n.alive;
            return true;
        }

        public DemonRecordData Demon(string persistentId)
        {
            foreach (var d in Data.demons) if (d.persistentId == persistentId) return d;
            return null;
        }

        /// <summary>Registers a new demon with a unique persistent id ("nightspawn_0007").</summary>
        public DemonRecordData NewDemon(string archetype, string sector, double totalHours)
        {
            var d = new DemonRecordData
            {
                persistentId = $"{archetype}_{Data.nextDemonSerial++:0000}",
                archetype = archetype,
                lastSector = sector,
                lastSeenHours = totalHours
            };
            Data.demons.Add(d);
            return d;
        }

        /// <summary>Escaped demons still alive (candidates to return in later versions).</summary>
        public IEnumerable<DemonRecordData> EscapedDemons()
        {
            foreach (var d in Data.demons) if (d.alive && d.escaped) yield return d;
        }

        public WorldEventRecord Event(string id)
        {
            foreach (var e in Data.events) if (e.id == id) return e;
            var r = new WorldEventRecord { id = id };
            Data.events.Add(r);
            return r;
        }

        public StructureStateData Structure(string id)
        {
            foreach (var s in Data.structures) if (s.id == id) return s;
            var r = new StructureStateData { id = id };
            Data.structures.Add(r);
            return r;
        }

        public void DamageStructure(string id, string cause, double totalHours)
        {
            var s = Structure(id);
            s.damagedAt = totalHours;
            s.cause = cause;
        }

        public RecoveryStage StructureStage(string id, double totalHours)
        {
            foreach (var s in Data.structures)
                if (s.id == id) return StageAt(s.damagedAt, totalHours);
            return RecoveryStage.Intact;
        }

        public static RecoveryStage StageAt(double damagedAt, double now)
        {
            if (damagedAt < 0) return RecoveryStage.Intact;
            double since = now - damagedAt;
            if (since < 0) return RecoveryStage.Damaged;
            if (since < RepairStartsAfter) return RecoveryStage.Damaged;
            if (since < RepairedAfter) return RecoveryStage.Repairing;
            return RecoveryStage.Repaired;
        }

        public bool Discover(string area)
        {
            if (string.IsNullOrEmpty(area) || Data.discoveredAreas.Contains(area)) return false;
            Data.discoveredAreas.Add(area);
            return true;
        }

        public bool IsDiscovered(string area) => Data.discoveredAreas.Contains(area);
    }
}
