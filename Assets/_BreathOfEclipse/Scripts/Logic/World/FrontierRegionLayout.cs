using System;
using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    public enum AmbienceZone
    {
        Village = 0,
        Fields = 1,
        Forest = 2,
        River = 3,
        Danger = 4,
        Cave = 5
    }

    public enum SectorBiome
    {
        Village = 0,
        Fields = 1,
        Woods = 2,
        Riverside = 3,
        Forest = 4,
        DeepForest = 5,
        Ruins = 6,
        Mountain = 7,
        Cursed = 8
    }

    public sealed class SectorDef
    {
        public string Id;
        public string Name;
        public WorldRect Bounds;
        /// <summary>0 safe … 3 cursed ground.</summary>
        public int Danger;
        public SectorBiome Biome;
        public AmbienceZone Ambience;
        public bool SafeZone;
        /// <summary>Trees per 100 m² of free ground.</summary>
        public float TreeDensity;
    }

    public enum PlaceKind
    {
        House = 0,
        Inn = 1,
        Shop = 2,
        Smithy = 3,
        HunterPost = 4,
        Watchtower = 5,
        Shrine = 6,
        Well = 7,
        Farmhouse = 8,
        Barn = 9,
        Cabin = 10,
        Camp = 11,
        Ruins = 12,
        Cave = 13,
        HiddenShrine = 14,
        Lookout = 15,
        GiantTree = 16,
        CursedGate = 17,
        Fort = 18,
        Field = 19,
        Paddy = 20,
        Pond = 21,
        Dock = 22,
        LumberYard = 23,
        Bridge = 24,
        Ford = 25,
        Waterfall = 26,
        RiverShrine = 27,
        Spot = 28
    }

    /// <summary>A named place of the region (buildings, landmarks, work spots).</summary>
    public sealed class PlaceDef
    {
        public string Id;
        public string Name;
        public PlaceKind Kind;
        public float X, Z;
        /// <summary>Facing of the door / front (degrees, 0 = +Z north, 90 = +X east).</summary>
        public float Yaw;
        public float Width, Depth;
        public string Sector;
        /// <summary>Navigation node where NPCs stand to use the place (door, work spot).</summary>
        public string Node;
    }

    public sealed class RoadDef
    {
        public string Id;
        public PathKind Kind;
        /// <summary>Half width painted / flattened (m).</summary>
        public float HalfWidth;
        public float[] Points; // x0, z0, x1, z1, …
    }

    public sealed class NpcDef
    {
        public string Id;
        public string Name;
        public NpcRole Role;
        public bool Female;
        public bool Child;
        public bool Elder;
        public string Home;
        public string Work;
        public string Social;
        public int LookSeed;
        /// <summary>Hunters: a rare one shows a breathing technique.</summary>
        public string BreathingTeaser;
    }

    public enum WorldEventKind
    {
        CaravanAttack = 0,
        FamilyPursued = 1,
        WoundedHunter = 2,
        HunterDuel = 3,
        VillageAttack = 4,
        RareDemon = 5,
        LostChild = 6,
        ExceptionalPresence = 7
    }

    public sealed class EventSpot
    {
        public WorldEventKind Kind;
        public float X, Z;
        public string Sector;
        /// <summary>Where participants try to go (village gate, road) / come from.</summary>
        public string Destination;
    }

    public sealed class DiscoverySpot
    {
        public string Id;
        public string Name;
        public float X, Z, Radius;
        public string Sector;
        /// <summary>Secret places never appear on the map until visited.</summary>
        public bool Secret;
    }

    /// <summary>
    /// THE ASAGIRI FRONTIER — the first open region: a frontier village, rice terraces and a lumber camp to the
    /// south, the Shirase river with its waterfall and bridge, the forest road with a traveller camp, old ruins,
    /// the mountain path to a cave, the deep forest under a giant tree, and the Ash Hollow — cursed ground in the
    /// north-east. 480 × 480 m in 12 sectors. Pure data + an analytic height function (engine independent, tested).
    /// </summary>
    public static class FrontierRegionLayout
    {
        public const string RegionName = "Asagiri Frontier";
        public const float HalfSize = 240f;
        public const float WaterLevel = -0.8f;
        public const float RiverBed = -2.5f;
        public const float RiverHalfWidth = 6.5f;
        public const float RiverBank = 12f;
        public const float VillageX = 0f, VillageZ = -165f, VillageFenceRadius = 62f;
        public const float WaterfallX = -211f;
        public const float CliffTop = 13f;
        public static readonly WorldRect Bounds = new WorldRect(-HalfSize, -HalfSize, HalfSize, HalfSize);

        public static readonly List<SectorDef> Sectors = new List<SectorDef>();
        public static readonly List<PlaceDef> Places = new List<PlaceDef>();
        public static readonly List<RoadDef> Roads = new List<RoadDef>();
        public static readonly List<NpcDef> Npcs = new List<NpcDef>();
        public static readonly List<EventSpot> EventSpots = new List<EventSpot>();
        public static readonly List<DiscoverySpot> Discoveries = new List<DiscoverySpot>();
        /// <summary>Demon hideouts (x, z) per sector: lairs, thickets, the cave.</summary>
        public static readonly Dictionary<string, List<float[]>> Hideouts = new Dictionary<string, List<float[]>>();
        /// <summary>Village fence sections (structure ids) with their arc (start / end degrees around the village).</summary>
        public static readonly List<(string id, float from, float to)> FenceSections = new List<(string, float, float)>();

        static FrontierRegionLayout()
        {
            BuildSectors();
            BuildRoads();
            BuildPlaces();
            BuildNpcs();
            BuildSpots();
        }

        public static SectorDef Sector(string id)
        {
            foreach (var s in Sectors) if (s.Id == id) return s;
            return null;
        }

        public static SectorDef SectorAt(float x, float z)
        {
            foreach (var s in Sectors) if (s.Bounds.Contains(x, z)) return s;
            return null;
        }

        public static PlaceDef Place(string id)
        {
            foreach (var p in Places) if (p.Id == id) return p;
            return null;
        }

        // ------------------------------------------------------------------ sectors

        private static void AddSector(string id, string name, float x0, float z0, float x1, float z1, int danger, SectorBiome biome, AmbienceZone amb, float trees, bool safe = false)
        {
            Sectors.Add(new SectorDef { Id = id, Name = name, Bounds = new WorldRect(x0, z0, x1, z1), Danger = danger, Biome = biome, Ambience = amb, TreeDensity = trees, SafeZone = safe });
        }

        private static void BuildSectors()
        {
            const float W0 = -240f, W1 = -80f, E0 = 80f, E1 = 240f, S0 = -240f, S1 = -80f, R1 = -20f, M1 = 100f, N1 = 240f;
            AddSector("farmland", "Rice Terraces", W0, S0, W1, S1, 1, SectorBiome.Fields, AmbienceZone.Fields, 0.12f);
            AddSector("village", "Asagiri Village", W1, S0, E0, S1, 0, SectorBiome.Village, AmbienceZone.Village, 0.05f, true);
            AddSector("woodcutters", "Woodcutters' Clearing", E0, S0, E1, S1, 1, SectorBiome.Woods, AmbienceZone.Forest, 0.55f);
            AddSector("river_west", "Shirase Falls", W0, S1, W1, R1, 1, SectorBiome.Riverside, AmbienceZone.River, 0.3f);
            AddSector("river_crossing", "Shirase Bridge", W1, S1, E0, R1, 1, SectorBiome.Riverside, AmbienceZone.River, 0.15f);
            AddSector("river_east", "Riverside Shrine", E0, S1, E1, R1, 1, SectorBiome.Riverside, AmbienceZone.River, 0.3f);
            AddSector("forest_west", "Western Woods", W0, R1, W1, M1, 2, SectorBiome.Forest, AmbienceZone.Forest, 1f);
            AddSector("forest_road", "Forest Road", W1, R1, E0, M1, 1, SectorBiome.Forest, AmbienceZone.Forest, 0.75f);
            AddSector("old_ruins", "Old Ruins", E0, R1, E1, M1, 2, SectorBiome.Ruins, AmbienceZone.Forest, 0.7f);
            AddSector("mountain_path", "Mountain Path", W0, M1, W1, N1, 2, SectorBiome.Mountain, AmbienceZone.Forest, 0.45f);
            AddSector("deep_forest", "Deep Forest", W1, M1, E0, N1, 2, SectorBiome.DeepForest, AmbienceZone.Forest, 1.15f);
            AddSector("danger_zone", "Ash Hollow", E0, M1, E1, N1, 3, SectorBiome.Cursed, AmbienceZone.Danger, 0.8f);
        }

        // ------------------------------------------------------------------ roads

        private static void Road(string id, PathKind kind, float halfWidth, params float[] pts) =>
            Roads.Add(new RoadDef { Id = id, Kind = kind, HalfWidth = halfWidth, Points = pts });

        private static void BuildRoads()
        {
            // Main road: south end of the village → plaza → north gate → bridge → forest road → crossroads → giant tree.
            Road("main_south", PathKind.Road, 3.2f, 0, -201.5f, 0, -190, 0, -178, 0, -160, 0, -142, 0, -120, 0, -100, 1, -82, 0, -66);
            Road("bridge", PathKind.Bridge, 2f, 0, -66, 0, -48, 0, -30);
            Road("main_north", PathKind.Road, 3f, 0, -30, 3, -12, 0, 6, -4, 20, -6, 35, -2, 48, 2, 60, 2, 72, 0, 85);
            Road("north_trail", PathKind.Trail, 1.6f, 0, 85, 1, 105, -2, 125, -3, 145, 0, 158);
            Road("deep_trail", PathKind.Forest, 1.3f, 0, 158, 18, 178, 45, 190, 75, 195, 108, 190, 140, 178, 165, 166, 178, 160);
            // Village lanes and gates.
            // The plaza lanes pass between the inn / smithy and the houses to the north (never through a building).
            Road("lane_plaza_w", PathKind.Village, 2f, 0, -158.5f, -14, -158.5f, -30, -158.5f, -40, -160, -62, -165);
            Road("lane_plaza_e", PathKind.Village, 2f, 0, -158.5f, 14, -158.5f, 30, -158.5f, 40, -160, 62, -165);
            Road("lane_w", PathKind.Village, 1.6f, -40, -132, -40, -150, -40, -165, -40, -182, -40, -200);
            Road("lane_e", PathKind.Village, 1.6f, 40, -132, 40, -150, 40, -165, 40, -182, 40, -200);
            Road("lane_n", PathKind.Village, 1.6f, -40, -132, -22, -122, 0, -120, 22, -122, 40, -132);
            Road("lane_s", PathKind.Village, 1.6f, -40, -200, -20, -204, 0, -200, 20, -204, 40, -200);
            // Farmland.
            Road("farm_road", PathKind.Road, 2.4f, -62, -165, -85, -164, -105, -158, -125, -146, -138, -132);
            Road("farm_paths", PathKind.Trail, 1.2f, -105, -158, -110, -175, -128, -180, -150, -176, -172, -168, -182, -152);
            // Woodcutters and the pond.
            Road("wood_road", PathKind.Road, 2.4f, 62, -165, 88, -162, 112, -156, 135, -150, 150, -146, 160, -134);
            Road("pond_path", PathKind.Trail, 1.2f, 135, -150, 145, -170, 158, -186);
            Road("ford_path", PathKind.Trail, 1.3f, 150, -146, 170, -125, 184, -100, 190, -80);
            Road("ford", PathKind.Bridge, 1f, 190, -80, 190, -66, 190, -54, 190, -42, 190, -30);
            Road("ruins_south", PathKind.Trail, 1.3f, 190, -30, 186, -8, 178, 15, 168, 35, 160, 45);
            // River banks.
            Road("bank_west", PathKind.Trail, 1.3f, 0, -30, -20, -32, -45, -34, -75, -36, -110, -36, -145, -34, -175, -32, -198, -30);
            Road("bank_east", PathKind.Trail, 1.3f, 0, -30, 25, -28, 55, -27, 90, -26, 112, -25, 128, -24);
            Road("fish_path", PathKind.Trail, 1f, 0, -82, -12, -74, -22, -70);
            // Forest trails.
            Road("west_trail", PathKind.Trail, 1.4f, -4, 20, -25, 26, -50, 30, -80, 35, -105, 46, -122, 56);
            Road("falls_trail", PathKind.Trail, 1.2f, -198, -30, -190, -8, -172, 10, -150, 28, -132, 44, -122, 56);
            Road("mountain_trail", PathKind.Trail, 1.3f, -122, 56, -140, 76, -152, 98, -160, 120, -164, 145, -170, 168, -178, 188);
            Road("lookout_trail", PathKind.Trail, 1f, -160, 120, -142, 134, -126, 146);
            Road("east_trail", PathKind.Trail, 1.4f, 2, 60, 25, 63, 50, 61, 80, 56, 105, 50, 130, 47, 160, 45);
            Road("danger_trail", PathKind.Danger, 1.3f, 160, 45, 166, 70, 171, 94, 173, 110, 176, 135, 178, 160, 186, 186);
            Road("camp_path", PathKind.Trail, 1.2f, -2, 48, 12, 44, 22, 42);
        }

        // ------------------------------------------------------------------ places

        private static PlaceDef AddPlace(string id, string name, PlaceKind kind, float x, float z, float yaw = 0f, float w = 0f, float d = 0f)
        {
            var p = new PlaceDef { Id = id, Name = name, Kind = kind, X = x, Z = z, Yaw = yaw, Width = w, Depth = d };
            var s = SectorAt(x, z);
            p.Sector = s != null ? s.Id : null;
            p.Node = id;
            Places.Add(p);
            return p;
        }

        /// <summary>Door / use point of a place (in front of it).</summary>
        public static void FrontPoint(PlaceDef p, float extra, out float x, out float z)
        {
            double a = p.Yaw * Math.PI / 180.0;
            float reach = p.Depth * 0.5f + extra;
            x = p.X + (float)Math.Sin(a) * reach;
            z = p.Z + (float)Math.Cos(a) * reach;
        }

        private static void BuildPlaces()
        {
            // Asagiri village: two rows of houses facing the main road, a plaza with the well, inn, shop, smithy,
            // hunters' post and the bell tower by the north gate, the village shrine at the south end.
            AddPlace("house_a", "Haru's house", PlaceKind.House, -28, -141, 90, 8, 6.5f);
            AddPlace("inn", "Kasumi Inn", PlaceKind.Inn, -30, -168, 90, 12, 9);
            AddPlace("house_b", "Fisher's house", PlaceKind.House, -28, -192, 90, 8, 6.5f);
            AddPlace("shop", "Ohara's store", PlaceKind.Shop, 27, -145, -90, 9, 7);
            AddPlace("smithy", "Tetsuo's smithy", PlaceKind.Smithy, 27, -170, -90, 9, 7);
            AddPlace("house_c", "Daichi's house", PlaceKind.House, 28, -194, -90, 8, 6.5f);
            AddPlace("house_d", "Genta's house", PlaceKind.House, -53, -149, 90, 8, 6.5f);
            AddPlace("house_e", "Mitsu's house", PlaceKind.House, -54, -183, 90, 8, 6.5f);
            AddPlace("house_f", "Ryo's house", PlaceKind.House, 53, -152, -90, 8, 6.5f);
            AddPlace("house_g", "Kanta's house", PlaceKind.House, 54, -184, -90, 8, 6.5f);
            AddPlace("village_shrine", "Village shrine", PlaceKind.Shrine, 0, -207, 0, 6, 5);
            AddPlace("hunter_post", "Hunters' post", PlaceKind.HunterPost, 17, -109, -90, 7, 6);
            AddPlace("watchtower", "Bell tower", PlaceKind.Watchtower, -14, -108, 90, 4, 4);
            AddPlace("well", "Well", PlaceKind.Well, -7, -166, 0, 2, 2);
            AddPlace("stall", "Market stall", PlaceKind.Spot, 18, -145, -90, 4, 2);
            AddPlace("forge", "Forge", PlaceKind.Spot, 18.5f, -171, -90, 2, 2);
            AddPlace("plaza", "Plaza", PlaceKind.Spot, 6, -154, 0);
            AddPlace("plaza_bench", "Plaza bench", PlaceKind.Spot, -8, -152, 180);
            AddPlace("laundry", "Laundry lines", PlaceKind.Spot, -35, -142, 0);
            AddPlace("play_yard", "Play yard", PlaceKind.Spot, 8, -186, 0);
            AddPlace("gate_n", "North gate", PlaceKind.Spot, 0, -102, 0);
            AddPlace("gate_e", "East gate", PlaceKind.Spot, 60, -165, 90);
            AddPlace("gate_w", "West gate", PlaceKind.Spot, -60, -165, -90);
            AddPlace("garden", "Kitchen garden", PlaceKind.Field, -44, -118, 0, 12, 8);
            // Farmland.
            AddPlace("farmhouse", "Hana's farmhouse", PlaceKind.Farmhouse, -141, -121, 180, 10, 7);
            AddPlace("barn", "Barn", PlaceKind.Barn, -162, -118, 180, 9, 7);
            AddPlace("paddy_1", "Rice paddy", PlaceKind.Paddy, -112, -194, 0, 26, 20);
            AddPlace("paddy_2", "Rice paddy", PlaceKind.Paddy, -146, -196, 0, 28, 20);
            AddPlace("paddy_3", "Rice paddy", PlaceKind.Paddy, -186, -186, 0, 24, 22);
            AddPlace("field_veg", "Vegetable rows", PlaceKind.Field, -88, -136, 0, 18, 12);
            AddPlace("field_far", "Barley field", PlaceKind.Field, -195, -140, 0, 22, 26);
            // Woodcutters' clearing and the pond.
            AddPlace("lumber_yard", "Lumber yard", PlaceKind.LumberYard, 150, -152, 0, 14, 10);
            AddPlace("cabin", "Woodcutters' cabin", PlaceKind.Cabin, 163, -124, 180, 8, 6);
            AddPlace("chop_1", "Felling spot", PlaceKind.Spot, 132, -168, 0);
            AddPlace("chop_2", "Felling spot", PlaceKind.Spot, 176, -160, 0);
            AddPlace("pond", "Fish pond", PlaceKind.Pond, 172, -202, 0, 32, 28);
            AddPlace("dock", "Pond dock", PlaceKind.Dock, 158, -187, 137, 3, 7);
            // River.
            AddPlace("bridge", "Shirase Bridge", PlaceKind.Bridge, 0, -48, 0, 4, 36);
            AddPlace("ford", "Stepping stones", PlaceKind.Ford, 190, -55, 0, 3, 40);
            AddPlace("waterfall", "Shirase Falls", PlaceKind.Waterfall, WaterfallX, RiverZ(WaterfallX), 90);
            AddPlace("fish_spot", "Fishing spot", PlaceKind.Spot, -22, -70, 0);
            AddPlace("river_shrine", "Riverside shrine", PlaceKind.RiverShrine, 134, -24, 180, 4, 3);
            // Forest road.
            AddPlace("camp", "Traveller camp", PlaceKind.Camp, 24, 42, -90, 10, 8);
            AddPlace("crossroads", "Crossroads", PlaceKind.Spot, 0, 85, 0);
            // Ruins / mountain / deep forest / danger.
            AddPlace("ruins", "Old Ruins", PlaceKind.Ruins, 166, 52, 200, 14, 12);
            AddPlace("cave", "Cave of Echoes", PlaceKind.Cave, -184, 203, 160, 10, 10);
            AddPlace("lookout", "Hawk Lookout", PlaceKind.Lookout, -126, 148, 0);
            AddPlace("giant_tree", "Elder Camphor", PlaceKind.GiantTree, 0, 166, 0);
            AddPlace("hidden_shrine", "Hidden stone guardians", PlaceKind.HiddenShrine, -46, 200, 160, 6, 5);
            AddPlace("ash_gate", "Broken gate", PlaceKind.CursedGate, 173, 110, 0, 6, 2);
            AddPlace("ash_fort", "Ruined palisade", PlaceKind.Fort, 192, 196, 210, 26, 20);
        }

        // ------------------------------------------------------------------ people

        private static void Npc(string id, string name, NpcRole role, string home, string work, string social, int seed, bool female = false, bool child = false, bool elder = false, string teaser = null)
        {
            Npcs.Add(new NpcDef { Id = id, Name = name, Role = role, Home = home, Work = work, Social = social, LookSeed = seed, Female = female, Child = child, Elder = elder, BreathingTeaser = teaser });
        }

        private static void BuildNpcs()
        {
            Npc("genta", "Genta", NpcRole.Farmer, "house_d", "field_veg", "plaza", 11);
            Npc("mitsu", "Mitsu", NpcRole.Farmer, "house_e", "paddy_1", "well", 12, true);
            Npc("hana", "Hana", NpcRole.Farmer, "farmhouse", "paddy_2", "farmhouse", 13, true);
            Npc("ohara", "Ohara", NpcRole.Merchant, "shop", "stall", "plaza", 14, true);
            Npc("tetsuo", "Tetsuo", NpcRole.Smith, "smithy", "forge", "inn", 15);
            Npc("sayo", "Sayo", NpcRole.Villager, "inn", "inn", "plaza", 16, true);
            Npc("kanta", "Kanta", NpcRole.Lumberjack, "house_g", "chop_1", "inn", 17);
            Npc("ryo", "Ryo", NpcRole.Lumberjack, "house_f", "chop_2", "plaza", 18);
            Npc("isamu", "Isamu", NpcRole.Fisher, "house_b", "fish_spot", "plaza", 19);
            Npc("nao", "Nao", NpcRole.Fisher, "cabin", "dock", "lumber_yard", 20, true);
            Npc("haru", "Haru", NpcRole.Parent, "house_a", "laundry", "well", 21, true);
            Npc("kei", "Kei", NpcRole.Child, "house_a", "play_yard", "plaza", 22, false, true);
            Npc("mio", "Mio", NpcRole.Child, "house_e", "play_yard", "plaza", 23, true, true);
            Npc("daichi", "Daichi", NpcRole.Parent, "house_c", "garden", "plaza_bench", 24);
            Npc("shion", "Elder Shion", NpcRole.Priest, "village_shrine", "village_shrine", "plaza", 25, true, false, true);
            Npc("toku", "Old Toku", NpcRole.Villager, "house_b", "plaza_bench", "well", 26, false, false, true);
            Npc("jubei", "Jubei", NpcRole.Guard, "hunter_post", "gate_n", "watchtower", 27);
            Npc("masa", "Masa", NpcRole.Guard, "house_f", "gate_e", "plaza", 28);
            Npc("yoshi", "Pilgrim Yoshi", NpcRole.Traveler, "camp", "plaza", "camp", 29);
            Npc("ume", "Peddler Ume", NpcRole.Traveler, "camp", "stall", "camp", 30, true);
            Npc("kaede", "Kaede", NpcRole.Hunter, "hunter_post", "crossroads", "inn", 31, true);
            Npc("rokuro", "Rokuro", NpcRole.Hunter, "hunter_post", "west_patrol", "hunter_post", 32);
            Npc("sora", "Sora", NpcRole.Hunter, "camp", "deep_patrol", "camp", 33, false, false, false, "moonlight");
            Npc("goro", "Wanderer Goro", NpcRole.Camper, "river_shrine", "river_shrine", "camp", 34, false, false, true);
        }

        // ------------------------------------------------------------------ spots

        private static void Spot(WorldEventKind kind, float x, float z, string destination)
        {
            var s = SectorAt(x, z);
            EventSpots.Add(new EventSpot { Kind = kind, X = x, Z = z, Sector = s != null ? s.Id : null, Destination = destination });
        }

        private static void Discovery(string id, string name, float x, float z, float r, bool secret = false)
        {
            var s = SectorAt(x, z);
            Discoveries.Add(new DiscoverySpot { Id = id, Name = name, X = x, Z = z, Radius = r, Secret = secret, Sector = s != null ? s.Id : null });
        }

        private static void Hideout(string sector, float x, float z)
        {
            if (!Hideouts.TryGetValue(sector, out var list)) Hideouts[sector] = list = new List<float[]>();
            list.Add(new[] { x, z });
        }

        private static void BuildSpots()
        {
            Spot(WorldEventKind.CaravanAttack, -3, 26, "gate_n");
            Spot(WorldEventKind.CaravanAttack, 2, 66, "gate_n");
            Spot(WorldEventKind.FamilyPursued, -62, 31, "gate_n");
            Spot(WorldEventKind.FamilyPursued, -160, 22, "gate_n");
            Spot(WorldEventKind.WoundedHunter, -118, 64, "hunter_post");
            Spot(WorldEventKind.WoundedHunter, 140, 52, "hunter_post");
            Spot(WorldEventKind.WoundedHunter, 30, 184, "hunter_post");
            Spot(WorldEventKind.HunterDuel, -95, 42, null);
            Spot(WorldEventKind.HunterDuel, 112, 52, null);
            Spot(WorldEventKind.HunterDuel, 2, 122, null);
            Spot(WorldEventKind.VillageAttack, -22, -106, "plaza");
            Spot(WorldEventKind.VillageAttack, 58, -140, "plaza");
            Spot(WorldEventKind.RareDemon, -128, 150, null);
            Spot(WorldEventKind.RareDemon, 70, 198, null);
            Spot(WorldEventKind.RareDemon, -205, -18, null);
            Spot(WorldEventKind.LostChild, -92, 14, "gate_n");
            Spot(WorldEventKind.LostChild, -136, 74, "gate_n");
            Spot(WorldEventKind.ExceptionalPresence, 0, -98, "plaza");

            Discovery("waterfall", "Shirase Falls", -200, -44, 26);
            Discovery("ruins", "Old Ruins", 166, 52, 26);
            Discovery("cave", "Cave of Echoes", -184, 203, 18);
            Discovery("lookout", "Hawk Lookout", -126, 148, 12);
            Discovery("giant_tree", "Elder Camphor", 0, 166, 24);
            Discovery("river_shrine", "Riverside shrine", 134, -24, 12);
            Discovery("ash_hollow", "Ash Hollow", 173, 112, 16);
            Discovery("hidden_shrine", "Hidden stone guardians", -46, 200, 9, true);
            Discovery("camp", "Traveller camp", 24, 42, 14);

            Hideout("forest_west", -150, 62);
            Hideout("forest_west", -98, 82);
            Hideout("forest_west", -205, 40);
            Hideout("forest_road", 42, 90);
            Hideout("forest_road", -42, 72);
            Hideout("old_ruins", 172, 62);
            Hideout("old_ruins", 210, 20);
            Hideout("mountain_path", -188, 214);
            Hideout("mountain_path", -110, 200);
            Hideout("deep_forest", -30, 192);
            Hideout("deep_forest", 42, 150);
            Hideout("deep_forest", 60, 205);
            Hideout("danger_zone", 178, 172);
            Hideout("danger_zone", 200, 196);
            Hideout("danger_zone", 146, 186);
            Hideout("danger_zone", 214, 140);
            Hideout("farmland", -214, -118);
            Hideout("farmland", -205, -215);
            Hideout("woodcutters", 212, -128);
            Hideout("woodcutters", 210, -215);
            Hideout("river_west", -150, -66);
            Hideout("river_east", 225, -30);
            Hideout("river_crossing", 60, -72);

            FenceSections.Add(("fence_ne", 10f, 80f));
            FenceSections.Add(("fence_e", 100f, 170f));
            FenceSections.Add(("fence_s", 190f, 260f));
            FenceSections.Add(("fence_w", 280f, 350f));
        }

        // ------------------------------------------------------------------ terrain

        /// <summary>River centre line (z) at a given x: meanders across the middle of the region.</summary>
        public static float RiverZ(float x) => -50f + 9f * (float)Math.Sin(x * 0.018f) + 4f * (float)Math.Sin(x * 0.047f + 1f);

        public static float RiverDistance(float x, float z) => Math.Abs(z - RiverZ(x));

        /// <summary>Inside the river channel (below the water line), excluding the waterfall cliff top.</summary>
        public static bool IsWater(float x, float z) => x > WaterfallX - 1f && RiverDistance(x, z) < RiverHalfWidth + 0.5f
                                                        || InPond(x, z) || InPaddy(x, z);

        public static bool InPond(float x, float z)
        {
            float dx = (x - 172f) / 16f, dz = (z + 202f) / 14f;
            return dx * dx + dz * dz < 1f;
        }

        public static bool InPaddy(float x, float z)
        {
            foreach (var p in Places)
            {
                if (p.Kind != PlaceKind.Paddy) continue;
                if (Math.Abs(x - p.X) < p.Width * 0.5f - 0.6f && Math.Abs(z - p.Z) < p.Depth * 0.5f - 0.6f) return true;
            }
            return false;
        }

        /// <summary>
        /// Terrain height (m). Rolling hills, steep mountains along the border, the western cliff with the waterfall,
        /// the river channel, flattened village / fields / camp / ruins, raised lookout and shrine mounds, the sunken
        /// Ash Hollow, and smoothed road beds.
        /// </summary>
        public static float Height(float x, float z)
        {
            float low = 2.4f * Fbm(x * 0.011f, z * 0.011f, 3) + 0.5f;
            float detail = 0.8f * (Fbm(x * 0.05f + 17f, z * 0.05f + 3f, 2) - 0.5f);
            float north = Smooth((z - 30f) / 200f) * 6f * Fbm(x * 0.02f + 5f, z * 0.02f + 11f, 2);
            float h = low + north;

            // Hills and plateaus.
            h += Bump(x, z, -170f, 175f, 75f) * 8f;   // mountain path plateau
            h += Bump(x, z, -126f, 148f, 36f) * 6f;   // hawk lookout
            h += Bump(x, z, 134f, -22f, 12f) * 2.2f;  // riverside shrine mound
            h -= Bump(x, z, 182f, 175f, 70f) * 2.5f;  // Ash Hollow
            h += Bump(x, z, 220f, 70f, 40f) * 5f;
            h += Bump(x, z, -90f, 110f, 35f) * 3f;

            // Roads drop the small bumps (walkable, readable paths) but keep the shape of the hills.
            float road = RoadMask(x, z);
            h += detail * (1f - road * 0.85f);

            // Flattened places (after the roads: the village plateau wins over its lanes).
            h = Flatten(h, x, z, VillageX, VillageZ, 70f, 90f, 0.6f);
            h = Flatten(h, x, z, -150f, -170f, 70f, 92f, 0.35f);   // terraces
            h = Flatten(h, x, z, 150f, -150f, 26f, 40f, 0.9f);     // lumber yard
            h = Flatten(h, x, z, 24f, 42f, 12f, 20f, Height0(24f, 42f));
            h = Flatten(h, x, z, 166f, 52f, 18f, 28f, Height0(166f, 52f));
            h = Flatten(h, x, z, 0f, 166f, 16f, 24f, Height0(0f, 166f));

            // Mountains on the border.
            float edge = Math.Min(HalfSize - Math.Abs(x), HalfSize - Math.Abs(z));
            h += Smooth((26f - edge) / 22f) * (30f + 8f * Fbm(x * 0.03f, z * 0.03f, 2));

            // Western cliff: the river falls from it.
            float cliff = Smooth((WaterfallX + 2f - x) / 5f);
            float cliffBand = 1f - Smooth((Math.Abs(z + 45f) - 70f) / 20f);
            h = Math.Max(h, h * (1f - cliff * cliffBand) + CliffTop * cliff * cliffBand);

            // Pond.
            float pd = (float)Math.Sqrt(Sq((x - 172f) / 16f) + Sq((z + 202f) / 14f));
            if (pd < 1.6f) h = Lerp(-1.6f, h, Smooth((pd - 0.7f) / 0.9f));

            // River channel (east of the falls; on the cliff top a shallow stream bed).
            float rd = RiverDistance(x, z);
            if (rd < RiverBank)
            {
                float bed = x > WaterfallX ? RiverBed : CliffTop - 1.4f;
                float k = Smooth((rd - RiverHalfWidth) / (RiverBank - RiverHalfWidth));
                if (x > WaterfallX) h = Lerp(bed, h, k);
                else h = Lerp(bed, h, k * 0.6f + 0.4f * Smooth((rd - 2.5f) / 3f));
            }
            // Plunge pool below the falls.
            float pool = (float)Math.Sqrt(Sq(x - (WaterfallX + 9f)) + Sq(z - RiverZ(WaterfallX + 9f)));
            if (pool < 16f) h = Math.Min(h, Lerp(RiverBed - 0.5f, h, Smooth((pool - 7f) / 9f)));
            return h;
        }

        /// <summary>Height without places / roads (used to pick plateau levels).</summary>
        private static float Height0(float x, float z) => 2.4f * Fbm(x * 0.011f, z * 0.011f, 3) + 0.5f + Smooth((z - 30f) / 200f) * 6f * Fbm(x * 0.02f + 5f, z * 0.02f + 11f, 2);

        // Road segments bucketed in 16 m cells: height / road queries only test the few nearby segments.
        private const float SegCell = 16f;
        private const int SegCells = (int)(2 * HalfSize / SegCell) + 1;
        private static List<int>[] _segGrid;
        private static readonly List<(int road, int seg)> SegRefs = new List<(int, int)>();

        private static void BuildSegmentGrid()
        {
            _segGrid = new List<int>[SegCells * SegCells];
            for (int ri = 0; ri < Roads.Count; ri++)
            {
                var p = Roads[ri].Points;
                float reach = Roads[ri].HalfWidth + 3.5f;
                for (int i = 2; i < p.Length; i += 2)
                {
                    int refIndex = SegRefs.Count;
                    SegRefs.Add((ri, i));
                    float minX = Math.Min(p[i - 2], p[i]) - reach, maxX = Math.Max(p[i - 2], p[i]) + reach;
                    float minZ = Math.Min(p[i - 1], p[i + 1]) - reach, maxZ = Math.Max(p[i - 1], p[i + 1]) + reach;
                    for (int cz = CellOf(minZ); cz <= CellOf(maxZ); cz++)
                    for (int cx = CellOf(minX); cx <= CellOf(maxX); cx++)
                    {
                        int c = cz * SegCells + cx;
                        if (_segGrid[c] == null) _segGrid[c] = new List<int>();
                        _segGrid[c].Add(refIndex);
                    }
                }
            }
        }

        private static int CellOf(float v)
        {
            int c = (int)Math.Floor((v + HalfSize) / SegCell);
            return c < 0 ? 0 : c >= SegCells ? SegCells - 1 : c;
        }

        /// <summary>0..1: how much a point lies on a road (1 on the centre line, fading at the shoulders).</summary>
        public static float RoadMask(float x, float z)
        {
            if (_segGrid == null) BuildSegmentGrid();
            var cell = _segGrid[CellOf(z) * SegCells + CellOf(x)];
            if (cell == null) return 0f;
            float best = 0f;
            foreach (int refIndex in cell)
            {
                var (ri, i) = SegRefs[refIndex];
                var r = Roads[ri];
                if (r.Kind == PathKind.Bridge) continue;
                var p = r.Points;
                float d = SegmentDistance(x, z, p[i - 2], p[i - 1], p[i], p[i + 1]);
                float m = 1f - Smooth((d - r.HalfWidth) / 3f);
                if (m > best) best = m;
            }
            return best;
        }

        /// <summary>Distance to the nearest road within ~4 m of its shoulder (else <paramref name="max"/>) and its kind.</summary>
        public static float NearestRoad(float x, float z, float max, out PathKind kind, out float halfWidth)
        {
            if (_segGrid == null) BuildSegmentGrid();
            kind = PathKind.Forest;
            halfWidth = 0f;
            float best = max;
            var cell = _segGrid[CellOf(z) * SegCells + CellOf(x)];
            if (cell == null) return best;
            foreach (int refIndex in cell)
            {
                var (ri, i) = SegRefs[refIndex];
                var r = Roads[ri];
                var p = r.Points;
                float d = SegmentDistance(x, z, p[i - 2], p[i - 1], p[i], p[i + 1]);
                if (d < best)
                {
                    best = d;
                    kind = r.Kind;
                    halfWidth = r.HalfWidth;
                }
            }
            return best;
        }

        public static float DistanceToPolyline(float[] p, float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 2; i < p.Length; i += 2)
            {
                float d = SegmentDistance(x, z, p[i - 2], p[i - 1], p[i], p[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        public static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float vx = bx - ax, vz = bz - az;
            float len2 = vx * vx + vz * vz;
            float t = len2 > 1e-6f ? ((px - ax) * vx + (pz - az) * vz) / len2 : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float dx = px - (ax + vx * t), dz = pz - (az + vz * t);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Approximate ground slope in degrees (finite differences).</summary>
        public static float Slope(float x, float z)
        {
            const float e = 1f;
            float dx = (Height(x + e, z) - Height(x - e, z)) / (2f * e);
            float dz = (Height(x, z + e) - Height(x, z - e)) / (2f * e);
            return (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
        }

        /// <summary>Height an NPC walks at: the bridge deck over the river, otherwise the ground.</summary>
        public static float WalkHeight(float x, float z)
        {
            if (Math.Abs(x) < 2.6f && z > -66f && z < -30f) return BridgeDeck(z);
            return Math.Max(Height(x, z), IsWater(x, z) && Math.Abs(x - 190f) < 1.5f ? WaterLevel + 0.35f : Height(x, z));
        }

        /// <summary>Bridge deck height along its length (arched).</summary>
        public static float BridgeDeck(float z)
        {
            float t = (z + 66f) / 36f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float ends = Lerp(Height0Clamp(-66f), Height0Clamp(-30f), t);
            return ends + (float)Math.Sin(t * Math.PI) * 1.4f;
        }

        private static float Height0Clamp(float z) => Math.Max(0.2f, Height(0f, z));

        // ------------------------------------------------------------------ noise

        private static float Flatten(float h, float x, float z, float cx, float cz, float inner, float outer, float level)
        {
            float d = (float)Math.Sqrt(Sq(x - cx) + Sq(z - cz));
            if (d >= outer) return h;
            float k = Smooth((d - inner) / Math.Max(1f, outer - inner));
            return Lerp(level + (h - level) * 0.12f, h, k);
        }

        private static float Bump(float x, float z, float cx, float cz, float r)
        {
            float d2 = (Sq(x - cx) + Sq(z - cz)) / (r * r);
            return d2 >= 1f ? 0f : Sq(1f - d2);
        }

        public static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Sq(float v) => v * v;

        /// <summary>Smooth value noise in [0, 1] (engine independent so layout tests match the game).</summary>
        public static float Noise(float x, float z)
        {
            int xi = (int)Math.Floor(x), zi = (int)Math.Floor(z);
            float xf = x - xi, zf = z - zi;
            float u = xf * xf * (3f - 2f * xf), v = zf * zf * (3f - 2f * zf);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), d = Hash(xi + 1, zi + 1);
            return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, z) * amp;
                norm += amp;
                x = x * 2.03f + 7.1f;
                z = z * 2.03f + 3.3f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>FNV-1a: deterministic across sessions and platforms (string.GetHashCode is not guaranteed to be).</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (s != null) foreach (char ch in s) h = (h ^ ch) * 16777619u;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        public static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        // ------------------------------------------------------------------ navigation

        /// <summary>
        /// Builds the navigation graph: nodes every ~12 m along every road / trail (merged where roads meet), a node
        /// at each place's door / use point linked to the closest road node, and patrol / hideout nodes linked through
        /// the forest. NPCs, events and demons all travel on it.
        /// </summary>
        public static NavGraph BuildNavGraph()
        {
            var g = new NavGraph();
            int serial = 0;
            NavGraph.Node NodeAt(float x, float z, string id = null)
            {
                // Merge with an existing node within 2.5 m (road junctions).
                foreach (var n in g.Nodes)
                    if (Sq(n.X - x) + Sq(n.Z - z) < 6.25f && (id == null || n.Id.StartsWith("r"))) return n;
                var s = SectorAt(x, z);
                return g.AddNode(id ?? $"r{serial++}", x, WalkHeight(x, z), z, s != null ? s.Id : null);
            }

            foreach (var r in Roads)
            {
                NavGraph.Node prev = null;
                var p = r.Points;
                for (int i = 0; i < p.Length; i += 2)
                {
                    if (i == 0)
                    {
                        prev = NodeAt(p[0], p[1]);
                        continue;
                    }
                    float ax = p[i - 2], az = p[i - 1], bx = p[i], bz = p[i + 1];
                    float len = (float)Math.Sqrt(Sq(bx - ax) + Sq(bz - az));
                    int steps = Math.Max(1, (int)Math.Ceiling(len / 12f));
                    for (int s = 1; s <= steps; s++)
                    {
                        float t = s / (float)steps;
                        var n = NodeAt(ax + (bx - ax) * t, az + (bz - az) * t);
                        if (n != prev) g.Link(prev.Index, n.Index, r.Kind);
                        prev = n;
                    }
                }
            }

            foreach (var place in Places)
            {
                float fx, fz;
                // Open spots: the node is the spot itself; spots with a footprint (stall, forge) are used from the front.
                if ((place.Kind == PlaceKind.Spot && place.Width <= 0f) || place.Kind == PlaceKind.Dock) { fx = place.X; fz = place.Z; }
                else FrontPoint(place, place.Kind == PlaceKind.Paddy || place.Kind == PlaceKind.Field || place.Kind == PlaceKind.Pond ? -place.Depth * 0.5f : 1.2f, out fx, out fz);
                if (place.Kind == PlaceKind.Pond) { fx = 158f; fz = -186f; }
                if (place.Kind == PlaceKind.Bridge || place.Kind == PlaceKind.Ford || place.Kind == PlaceKind.Waterfall) continue;
                var node = g.AddNode(place.Id, fx, WalkHeight(fx, fz), fz, place.Sector);
                LinkToNearestRoad(g, node, place.Kind == PlaceKind.Cave || place.Kind == PlaceKind.HiddenShrine ? PathKind.Forest : PathKind.Village);
            }

            // Patrol spots for hunters and wandering points for demons (forest links).
            AddLinked(g, "west_patrol", -128, 40, PathKind.Trail);
            AddLinked(g, "deep_patrol", 20, 130, PathKind.Trail);
            AddLinked(g, "east_patrol", 120, 30, PathKind.Trail);
            foreach (var kv in Hideouts)
            {
                int i = 0;
                foreach (var h in kv.Value) AddLinked(g, $"lair_{kv.Key}_{i++}", h[0], h[1], PathKind.Forest);
            }
            foreach (var spot in EventSpots)
                AddLinked(g, $"ev_{spot.Kind}_{spot.X:0}_{spot.Z:0}", spot.X, spot.Z, PathKind.Trail);
            return g;
        }

        private static void AddLinked(NavGraph g, string id, float x, float z, PathKind kind)
        {
            var s = SectorAt(x, z);
            var n = g.AddNode(id, x, WalkHeight(x, z), z, s != null ? s.Id : null);
            LinkToNearestRoad(g, n, kind);
        }

        private static void LinkToNearestRoad(NavGraph g, NavGraph.Node node, PathKind kind)
        {
            NavGraph.Node best = null;
            float bestD = float.MaxValue;
            foreach (var n in g.Nodes)
            {
                if (n == node || n.Edges.Count == 0) continue;
                // Never link across the river or through a building / stall / forge.
                if (CrossesWater(n.X, n.Z, node.X, node.Z)) continue;
                if (CrossesSolid(n.X, n.Z, node.X, node.Z)) continue;
                float d = Sq(n.X - node.X) + Sq(n.Z - node.Z);
                if (d < bestD)
                {
                    bestD = d;
                    best = n;
                }
            }
            if (best != null) g.Link(node.Index, best.Index, kind);
        }

        /// <summary>True when a straight walk between two points passes through a building or a solid prop footprint.</summary>
        public static bool CrossesSolid(float ax, float az, float bx, float bz)
        {
            float len = (float)Math.Sqrt(Sq(bx - ax) + Sq(bz - az));
            int steps = Math.Max(2, (int)(len / 0.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                if (NpcPlaces.Blocked(ax + (bx - ax) * t, az + (bz - az) * t, 0.15f)) return true;
            }
            return false;
        }

        /// <summary>True when a straight walk between two points goes through river / pond water (bridge and ford excluded).</summary>
        public static bool CrossesWater(float ax, float az, float bx, float bz)
        {
            float len = (float)Math.Sqrt(Sq(bx - ax) + Sq(bz - az));
            int steps = Math.Max(2, (int)(len / 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = ax + (bx - ax) * t, z = az + (bz - az) * t;
                if (Math.Abs(x) < 2.6f && z > -66f && z < -30f) continue;      // bridge
                if (Math.Abs(x - 190f) < 1.6f && z > -80f && z < -30f) continue; // stepping stones
                if (IsWater(x, z) && !InPaddy(x, z)) return true;
            }
            return false;
        }
    }
}
