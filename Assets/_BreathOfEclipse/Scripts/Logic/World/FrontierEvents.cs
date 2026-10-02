using System;
using System.Collections.Generic;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// The dynamic events of the Asagiri Frontier (data) and what they leave in the world's memory (facts, damaged
    /// structures, NPC records). The runtime stages them physically near the player and resolves them logically
    /// elsewhere; both paths end in <see cref="ApplyOutcome"/>.
    /// </summary>
    public static class FrontierEvents
    {
        public const string CaravanAttack = "caravan_attack";
        public const string FamilyPursued = "family_pursued";
        public const string WoundedHunter = "wounded_hunter";
        public const string HunterDuel = "hunter_duel";
        public const string VillageAttack = "village_attack";
        public const string RareDemon = "rare_demon";
        public const string Discovery = "discovery";
        public const string LostChild = "lost_child";
        public const string ExceptionalPresence = "exceptional_presence";

        public static readonly List<WorldEventDef> Defs = new List<WorldEventDef>
        {
            new WorldEventDef { Id = CaravanAttack, Title = "Caravan under attack", Sector = "forest_road", WindowStart = 17.5f, WindowEnd = 23.5f, ChancePerCheck = 0.05f, SoftDeadline = 0.6f, HardDeadline = 2.5f, CooldownHours = 30f, BaseSuccessChance = 0.35f, Variants = 2 },
            new WorldEventDef { Id = FamilyPursued, Title = "Family pursued", Sector = "forest_west", WindowStart = 17f, WindowEnd = 21f, ChancePerCheck = 0.05f, SoftDeadline = 0.45f, HardDeadline = 1.5f, CooldownHours = 26f, BaseSuccessChance = 0.45f, Variants = 2, MinDay = 1 },
            new WorldEventDef { Id = WoundedHunter, Title = "Wounded hunter", Sector = "forest_west", WindowStart = 4.5f, WindowEnd = 9f, ChancePerCheck = 0.06f, SoftDeadline = 2.5f, HardDeadline = 4f, CooldownHours = 36f, BaseSuccessChance = 0.5f, Variants = 3, MinDay = 2 },
            new WorldEventDef { Id = HunterDuel, Title = "Hunter against a demon", Sector = "forest_road", NightOnly = true, ChancePerCheck = 0.05f, SoftDeadline = 0.4f, HardDeadline = 1.2f, CooldownHours = 16f, BaseSuccessChance = 0.6f, Variants = 3 },
            new WorldEventDef { Id = VillageAttack, Title = "Night attack on the village", Sector = "village", WindowStart = 21f, WindowEnd = 3f, NightOnly = true, ChancePerCheck = 0.025f, SoftDeadline = 0.5f, HardDeadline = 1.5f, CooldownHours = 72f, BaseSuccessChance = 0.55f, Variants = 2, MinDay = 2 },
            new WorldEventDef { Id = RareDemon, Title = "Rare demon sighted", Sector = "deep_forest", NightOnly = true, ChancePerCheck = 0.02f, SoftDeadline = 0.8f, HardDeadline = 1.6f, CooldownHours = 48f, BaseSuccessChance = 0f, Variants = 3, MinDay = 2 },
            new WorldEventDef { Id = Discovery, Title = "Something left behind", Sector = "forest_road", WindowStart = 8f, WindowEnd = 16f, ChancePerCheck = 0.03f, SoftDeadline = 10f, HardDeadline = 22f, CooldownHours = 40f, BaseSuccessChance = 0f, Variants = 3 },
            new WorldEventDef { Id = LostChild, Title = "Lost child", Sector = "forest_west", WindowStart = 13f, WindowEnd = 16.5f, ChancePerCheck = 0.04f, SoftDeadline = 3f, HardDeadline = 5f, CooldownHours = 72f, BaseSuccessChance = 0.75f, Variants = 2, MinDay = 2 },
            new WorldEventDef { Id = ExceptionalPresence, Title = "Exceptional demonic presence", Sector = "village", ManualOnly = true, SoftDeadline = 1.5f, HardDeadline = 2f, CooldownHours = 24f, BaseSuccessChance = 1f },
        };

        public static WorldEventDef Def(string id)
        {
            foreach (var d in Defs) if (d.Id == id) return d;
            return null;
        }

        public static WorldEventKind Kind(string id)
        {
            switch (id)
            {
                case CaravanAttack: return WorldEventKind.CaravanAttack;
                case FamilyPursued: return WorldEventKind.FamilyPursued;
                case WoundedHunter: return WorldEventKind.WoundedHunter;
                case HunterDuel: return WorldEventKind.HunterDuel;
                case VillageAttack: return WorldEventKind.VillageAttack;
                case RareDemon: return WorldEventKind.RareDemon;
                case LostChild: return WorldEventKind.LostChild;
                case ExceptionalPresence: return WorldEventKind.ExceptionalPresence;
                default: return WorldEventKind.CaravanAttack;
            }
        }

        /// <summary>The staging spot of an event run (by its variant). Discovery uses its own caches.</summary>
        public static EventSpot Spot(string id, int variant)
        {
            if (id == Discovery) return null;
            var kind = Kind(id);
            var spots = new List<EventSpot>();
            foreach (var s in L.EventSpots) if (s.Kind == kind) spots.Add(s);
            if (spots.Count == 0) return null;
            return spots[((variant % spots.Count) + spots.Count) % spots.Count];
        }

        /// <summary>Places a discovery cache can appear (off the roads, never on the map until found).</summary>
        public static readonly float[][] DiscoveryCaches =
        {
            new[] { -58f, 54f }, new[] { 96f, 70f }, new[] { -150f, 104f }
        };

        /// <summary>
        /// Chance an unattended run ends well: hunters on duty help at night, the village fights back.
        /// </summary>
        public static float SuccessChance(WorldEventDef def, float hour, int huntersOnDuty)
        {
            float c = def.BaseSuccessChance;
            if (def.Id == CaravanAttack || def.Id == FamilyPursued || def.Id == HunterDuel) c += 0.12f * huntersOnDuty;
            if (def.Id == VillageAttack) c += 0.1f * huntersOnDuty;
            return c < 0f ? 0f : c > 0.95f ? 0.95f : c;
        }

        /// <summary>
        /// Writes what an event leaves behind. <paramref name="playerInvolved"/>: the player was there (gratitude facts).
        /// Returns a short summary for logs / the World Lab.
        /// </summary>
        public static string ApplyOutcome(WorldStateDatabase db, WorldEventDef def, WorldEventRecord r, bool success, bool playerInvolved, double now)
        {
            db.AddFact("Event_" + def.Id + (success ? "_Success" : "_Failure"), 1, now);
            var spot = Spot(def.Id, r.variant);
            switch (def.Id)
            {
                case CaravanAttack:
                    if (success)
                    {
                        db.SetFact(playerInvolved ? "Saved_Caravan_001" : "Caravan_Arrived", 1, now);
                        return playerInvolved ? "caravan saved by the player" : "caravan made it (hunters)";
                    }
                    db.SetFact("Lost_Caravan_001", 1, now);
                    if (spot != null) db.DamageStructure($"cart_{spot.X:0}_{spot.Z:0}", def.Id, now);
                    return "caravan lost: wreck left on the road";
                case FamilyPursued:
                    if (success)
                    {
                        db.SetFact(playerInvolved ? "Saved_Family_001" : "Family_Reached_Village", 1, now);
                        return "family reached the village";
                    }
                    // No melodrama: they scattered into the woods; the hunters bring them back later.
                    db.SetFact("Family_Missing_001", 1, now);
                    return "family scattered (missing until found)";
                case WoundedHunter:
                    if (success && playerInvolved)
                    {
                        db.SetFact("Hunter_Saved", 1, now);
                        return "hunter helped back on their feet";
                    }
                    db.SetFact(success ? "Hunter_Walked_Back" : "Hunter_Recovering", 1, now);
                    return success ? "hunter limped home" : "hunter carried back, recovering";
                case HunterDuel:
                    db.AddFact(success ? "Hunter_Duel_Won" : "Hunter_Duel_Lost", 1, now);
                    return success ? "hunter won the duel" : "hunter hurt in the duel";
                case VillageAttack:
                    db.SetFact("Village_Attacked", 1, now);
                    db.SetSectorFlag("village", "attacked", true);
                    // The side of the attack loses a stretch of fence (repaired over the next days).
                    string fence = spot != null && spot.X > 0f ? "fence_e" : "fence_ne";
                    if (!success) db.DamageStructure(fence, def.Id, now);
                    else if (!playerInvolved) db.DamageStructure(fence, def.Id + "_light", now);
                    if (success && playerInvolved) db.SetFact("Defended_Village", 1, now);
                    return success ? "attack repelled" : "attack: fence broken, villagers shaken";
                case RareDemon:
                    db.SetFact(success ? "Rare_Demon_Slain" : "Rare_Demon_Seen", 1, now);
                    return success ? "rare demon slain" : "rare demon seen and gone";
                case Discovery:
                    if (success) db.AddFact("Found_Caches", 1, now);
                    return success ? "cache found" : "cache left untouched";
                case LostChild:
                    if (success && playerInvolved)
                    {
                        db.SetFact("Found_Child_001", 1, now);
                        return "child brought home by the player";
                    }
                    db.SetFact(success ? "Child_Found_By_Guards" : "Child_Night_Search", 1, now);
                    return success ? "guards found the child" : "night search (found at dawn)";
                case ExceptionalPresence:
                    db.AddFact("Exceptional_Presence", 1, now);
                    db.SetSectorFlag("village", "presence_survived", true);
                    return "the presence faded";
            }
            return success ? "success" : "failure";
        }
    }
}
