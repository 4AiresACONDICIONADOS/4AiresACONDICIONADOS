using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BreathOfEclipse.Core;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Rendering;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;
using M = BreathOfEclipse.World.RegionMats;

namespace BreathOfEclipse.World
{
    /// <summary>What a loaded sector exposes to the world systems (shop shutters, forge, campfire, state props).</summary>
    public sealed class SectorInstance
    {
        public SectorDef Def;
        public Transform Root;
        /// <summary>Props that depend on world state (damaged fence, wrecked cart): rebuilt by <see cref="RegionSectorBuilder.RefreshState"/>.</summary>
        public Transform StateRoot;
        /// <summary>Event actors and aftermath placed by the event director.</summary>
        public Transform DynamicRoot;
        public readonly Dictionary<string, Component> Handles = new Dictionary<string, Component>();
        /// <summary>Meshes generated for this sector (terrain, merged batches): destroyed on unload so streaming never leaks.</summary>
        public readonly List<Mesh> OwnedMeshes = new List<Mesh>();
        public readonly List<Mesh> StateMeshes = new List<Mesh>();

        public T Get<T>(string key) where T : Component => Handles.TryGetValue(key, out var c) ? c as T : null;

        /// <summary>Frees the generated meshes (delay: a collider bake may still be reading the terrain on a worker thread).</summary>
        public void ReleaseMeshes(float delay)
        {
            foreach (var m in OwnedMeshes) if (m != null) Object.Destroy(m, delay);
            foreach (var m in StateMeshes) if (m != null) Object.Destroy(m, delay);
            OwnedMeshes.Clear();
            StateMeshes.Clear();
        }
    }

    /// <summary>
    /// Builds the detailed content of a sector over several frames: terrain with collider (cooked on a worker
    /// thread), its buildings and set pieces, merged vegetation with trunk colliders, water, and the props that depend
    /// on the world's memory. Unloading destroys it; everything is rebuilt deterministically from the layout + state.
    /// </summary>
    public static class RegionSectorBuilder
    {
        public static IEnumerator Build(SectorDef s, Transform parent, WorldStateDatabase world, double now, SectorInstance inst, bool immediate)
        {
            var root = new GameObject("Sector_" + s.Id).transform;
            root.SetParent(parent, false);
            inst.Def = s;
            inst.Root = root;
            inst.StateRoot = new GameObject("State").transform;
            inst.StateRoot.SetParent(root, false);
            inst.DynamicRoot = new GameObject("Dynamic").transform;
            inst.DynamicRoot.SetParent(root, false);

            // 1. Terrain + collider (cooking off the main thread).
            var mesh = RegionTerrain.BuildChunk(s.Bounds, 1, false, "Terrain_" + s.Id);
            inst.OwnedMeshes.Add(mesh);
            var ground = new GameObject("Terrain");
            ground.transform.SetParent(root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = ground.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RegionTerrain.Material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var meshId = mesh.GetEntityId();
            if (immediate) Physics.BakeMesh(meshId, false);
            else
            {
                var bake = Task.Run(() => Physics.BakeMesh(meshId, false));
                while (!bake.IsCompleted) yield return null;
            }
            if (ground == null) yield break;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            if (!immediate) yield return null;

            // 2. Places and set pieces.
            var batch = new MeshBatcher { Sink = inst.OwnedMeshes };
            var ctx = new PropContext(batch, root);
            foreach (var p in L.Places)
            {
                if (p.Sector != s.Id) continue;
                BuildPlace(ctx, p, inst);
            }
            BuildSectorDressing(ctx, s, inst);
            batch.Build(root, "Props");
            if (!immediate) yield return null;

            // 3. Vegetation (merged) and trunk colliders.
            var trees = RegionVegetation.Trees(s);
            var colliders = new GameObject("TreeColliders").transform;
            colliders.SetParent(root, false);
            int n = 0;
            foreach (var t in trees)
            {
                RegionVegetation.Tree(batch, t, s.Biome, out float cr, out float ch);
                var col = new GameObject("Trunk");
                col.transform.SetParent(colliders, false);
                col.transform.position = t.Position + Vector3.up * ch * 0.5f;
                var cap = col.AddComponent<CapsuleCollider>();
                cap.radius = cr;
                cap.height = ch;
                if (!immediate && ++n % 80 == 0) yield return null;
            }
            batch.Build(root, "Trees");
            if (!immediate) yield return null;
            Undergrowth(batch, s, root);
            batch.Build(root, "Plants");
            if (!immediate) yield return null;

            // 4. State-dependent props.
            RefreshState(inst, world, now);
        }

        // ------------------------------------------------------------------ places

        private static Vector3 Ground(float x, float z) => RegionTerrain.OnGround(x, z);

        private static void BuildPlace(PropContext c, PlaceDef p, SectorInstance inst)
        {
            int seed = L.StableHash(p.Id);
            float y = RegionTerrain.SampleHeight(p.X, p.Z);
            c.PushWorld(new Vector3(p.X, y, p.Z), p.Yaw);
            switch (p.Kind)
            {
                case PlaceKind.House: RegionProps.House(c, p.Width, p.Depth, seed); break;
                case PlaceKind.Cabin: RegionProps.House(c, p.Width, p.Depth, seed, true); break;
                case PlaceKind.Farmhouse: RegionProps.House(c, p.Width, p.Depth, seed, true); break;
                case PlaceKind.Inn: RegionProps.Inn(c, p.Width, p.Depth, seed); break;
                case PlaceKind.Shop: inst.Handles["shop_shutters"] = RegionProps.Shop(c, p.Width, p.Depth, seed); break;
                case PlaceKind.Smithy: inst.Handles["forge_light"] = RegionProps.Smithy(c, p.Width, p.Depth); break;
                case PlaceKind.HunterPost: RegionProps.HunterPost(c, p.Width, p.Depth, seed, new Color(0.9f, 0.9f, 1f)); break;
                case PlaceKind.Shrine: RegionProps.Shrine(c, false); break;
                case PlaceKind.RiverShrine: RegionProps.Shrine(c, true); break;
                case PlaceKind.Well: RegionProps.Well(c); break;
                case PlaceKind.Barn: RegionProps.Barn(c, p.Width, p.Depth); break;
                case PlaceKind.Dock: RegionProps.Dock(c, p.Depth); break;
                case PlaceKind.Cave: RegionProps.Cave(c, seed); break;
                case PlaceKind.HiddenShrine: RegionProps.HiddenShrine(c, seed); break;
                case PlaceKind.Lookout: RegionProps.Lookout(c); break;
                case PlaceKind.CursedGate: RegionProps.CursedGate(c, seed); break;
                case PlaceKind.Fort: RegionProps.Fort(c, p.Width, p.Depth, seed); break;
                case PlaceKind.Ruins:
                    c.Pop();
                    EnvironmentKit.TempleRuins(inst.Root, new Vector3(p.X, y, p.Z), p.Yaw);
                    c.PushWorld(new Vector3(p.X, y, p.Z), p.Yaw);
                    RuinsDressing(c, seed);
                    break;
                case PlaceKind.Camp:
                {
                    RegionProps.Campfire(c, Vector3.zero, out var light, out var fire);
                    var campfire = light.gameObject.AddComponent<Campfire>();
                    campfire.Light = light;
                    campfire.Fire = fire;
                    campfire.SetLit(false);
                    inst.Handles["campfire"] = campfire;
                    RegionProps.Tent(c, new Vector3(-3.5f, 0f, 2.5f), 30f, new Color(0.62f, 0.55f, 0.42f));
                    RegionProps.Tent(c, new Vector3(3.2f, 0f, 3f), -25f, new Color(0.35f, 0.42f, 0.5f));
                    RegionProps.Cart(c, new Vector3(4f, 0f, -3f), 70f, false, seed);
                    for (int i = 0; i < 3; i++) c.Box(M.Wood, new Vector3(-1.6f + i * 1.6f, 0.2f, -1.8f), new Vector3(1.2f, 0.4f, 0.35f), new Vector3(0f, i * 25f, 0f));
                    break;
                }
                case PlaceKind.LumberYard:
                    RegionProps.LogPile(c, new Vector3(-3f, 0f, 0f), 0f, 3);
                    RegionProps.LogPile(c, new Vector3(3f, 0f, 1f), 10f, 2);
                    RegionProps.Stump(c, new Vector3(0f, 0f, -3.5f), true);
                    c.Box(M.Wood, new Vector3(1f, 0.6f, 3f), new Vector3(2.4f, 0.12f, 0.5f));
                    foreach (float sx in new[] { 0f, 2f }) c.Box(M.Wood, new Vector3(sx, 0.3f, 3f), new Vector3(0.6f, 0.6f, 0.1f), new Vector3(0f, 0f, 25f));
                    RegionProps.Cart(c, new Vector3(-6f, 0f, -4f), 120f, false, seed + 3);
                    break;
                case PlaceKind.Paddy:
                case PlaceKind.Field:
                    c.Pop();
                    Field(c, p);
                    c.PushWorld(Vector3.zero);
                    break;
                case PlaceKind.Pond:
                {
                    c.Pop();
                    var water = ProceduralMeshes.CreatePart("Pond", ProceduralMeshes.Primitive(PrimitiveType.Cylinder),
                        RegionProps.WaterMaterial("BoE_Pond", new Color(0.2f, 0.45f, 0.55f, 0.75f), new Color(0.04f, 0.12f, 0.2f, 0.92f)),
                        inst.Root, new Vector3(p.X, L.WaterLevel + 0.3f, p.Z), Quaternion.identity, new Vector3(p.Width * 1.05f, 0.01f, p.Depth * 1.05f));
                    water.layer = Layers.Water;
                    var rng = new System.Random(seed);
                    for (int i = 0; i < 18; i++)
                    {
                        float a = i / 18f * Mathf.PI * 2f;
                        float x = p.X + Mathf.Cos(a) * 16.5f, z = p.Z + Mathf.Sin(a) * 14.5f;
                        RegionVegetation.Reeds(c.Batch, Ground(x, z), rng);
                    }
                    c.PushWorld(Vector3.zero);
                    break;
                }
                case PlaceKind.Spot:
                    SpotDressing(c, p, inst);
                    break;
            }
            c.Pop();
        }

        private static void SpotDressing(PropContext c, PlaceDef p, SectorInstance inst)
        {
            switch (p.Id)
            {
                case "stall":
                    RegionProps.Stall(c);
                    break;
                case "plaza_bench":
                    c.Box(M.Wood, new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.1f, 0.6f));
                    foreach (float x in new[] { -0.9f, 0.9f }) c.Box(M.DarkWood, new Vector3(x, 0.22f, 0f), new Vector3(0.12f, 0.45f, 0.5f));
                    break;
                case "laundry":
                    c.Pop();
                    RegionProps.LaundryLine(c, Ground(-40f, -128f), Ground(-31f, -133f), 5);
                    c.PushWorld(Vector3.zero);
                    break;
                case "play_yard":
                    for (int i = 0; i < 3; i++) c.Cyl(M.Wood, new Vector3(-1.5f + i * 1.5f, 0.3f + i * 0.15f, 0f), 0.25f, 0.6f + i * 0.3f);
                    break;
                case "forge":
                case "gate_n":
                case "gate_e":
                case "gate_w":
                    break;
            }
        }

        private static void Field(PropContext c, PlaceDef p)
        {
            var rng = new System.Random(L.StableHash(p.Id));
            if (p.Kind == PlaceKind.Paddy)
            {
                float y = RegionTerrain.SampleHeight(p.X, p.Z) + 0.12f;
                var water = ProceduralMeshes.CreatePart("Paddy", ProceduralMeshes.GroundQuad(),
                    RegionProps.WaterMaterial("BoE_Paddy", new Color(0.45f, 0.6f, 0.55f, 0.6f), new Color(0.2f, 0.3f, 0.25f, 0.8f)), c.Root,
                    new Vector3(p.X, y, p.Z), Quaternion.identity, new Vector3(p.Width - 0.8f, 1f, p.Depth - 0.8f));
                water.layer = Layers.Water;
                // Dikes around the paddy.
                c.PushWorld(new Vector3(p.X, y - 0.1f, p.Z));
                c.Box(M.Plant(new Color(0.42f, 0.5f, 0.3f)), new Vector3(0f, 0.1f, p.Depth * 0.5f), new Vector3(p.Width, 0.35f, 0.7f));
                c.Box(M.Plant(new Color(0.42f, 0.5f, 0.3f)), new Vector3(0f, 0.1f, -p.Depth * 0.5f), new Vector3(p.Width, 0.35f, 0.7f));
                c.Box(M.Plant(new Color(0.42f, 0.5f, 0.3f)), new Vector3(p.Width * 0.5f, 0.1f, 0f), new Vector3(0.7f, 0.35f, p.Depth));
                c.Box(M.Plant(new Color(0.42f, 0.5f, 0.3f)), new Vector3(-p.Width * 0.5f, 0.1f, 0f), new Vector3(0.7f, 0.35f, p.Depth));
                c.Pop();
            }
            RegionVegetation.Crops(c.Batch, p, rng);
            RegionProps.Scarecrow(c, Ground(p.X + p.Width * 0.3f, p.Z + p.Depth * 0.2f), (float)rng.NextDouble() * 360f,
                new Color(0.3f + (float)rng.NextDouble() * 0.4f, 0.3f, 0.5f));
        }

        private static void RuinsDressing(PropContext c, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 22f - 11f, 0f, (float)rng.NextDouble() * 22f - 11f);
                if (p.magnitude < 6f) continue;
                c.Box(M.DarkStone, p + new Vector3(0f, 0.4f, 0f), new Vector3(1.2f + (float)rng.NextDouble(), 0.8f + (float)rng.NextDouble() * 1.5f, 0.5f),
                    new Vector3((float)rng.NextDouble() * 15f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 10f));
            }
            // A toppled guardian statue overgrown with moss.
            c.Ball(M.Stone, new Vector3(-7f, 0.6f, -6f), new Vector3(1.4f, 1.1f, 2.2f), new Vector3(0f, 30f, 80f));
            c.Ball(M.Stone, new Vector3(-8.2f, 0.5f, -4.8f), new Vector3(0.9f, 0.9f, 0.9f));
            RegionProps.StoneLantern(c, new Vector3(6f, 0f, -8f), false);
            RegionProps.OldGear(c, new Vector3(4f, 0f, 6f), seed);
        }

        private static void BuildSectorDressing(PropContext c, SectorDef s, SectorInstance inst)
        {
            var rng = new System.Random(L.StableHash(s.Id) ^ 0x5151);
            switch (s.Id)
            {
                case "village":
                    // Lanterns along the main road and the lanes, haystacks by the gardens, a cart at the inn.
                    for (float z = -205f; z <= -104f; z += 14f)
                    {
                        RegionProps.PostLantern(c, Ground(-4.2f, z));
                        RegionProps.PostLantern(c, Ground(4.2f, z + 7f));
                    }
                    foreach (var z in new[] { -150f, -182f })
                    {
                        RegionProps.PostLantern(c, Ground(-42.5f, z));
                        RegionProps.PostLantern(c, Ground(42.5f, z));
                    }
                    RegionProps.Cart(c, Ground(-19f, -176f), 15f, false, 3);
                    RegionProps.Haystack(c, Ground(-47f, -114f), 1f);
                    RegionProps.Haystack(c, Ground(-51f, -122f), 0.8f);
                    for (int i = 0; i < 6; i++)
                        c.Cyl(M.Wood, Ground(-5f + i * 0.8f, -116f) + Vector3.up * 0.45f, 0.32f, 0.9f);
                    break;
                case "farmland":
                    for (int i = 0; i < 5; i++) RegionProps.Haystack(c, Ground(-200f + i * 7f, -112f + (i % 2) * 4f), 0.8f + (float)rng.NextDouble() * 0.5f);
                    RegionProps.Cart(c, Ground(-120f, -133f), 200f, false, 7);
                    RegionProps.PostLantern(c, Ground(-136f, -127f));
                    break;
                case "woodcutters":
                    for (int i = 0; i < 14; i++)
                    {
                        float x = 120f + (float)rng.NextDouble() * 70f, z = -185f + (float)rng.NextDouble() * 60f;
                        if (!RegionVegetation.Free(x, z, 0f)) continue;
                        RegionProps.Stump(c, Ground(x, z), i % 5 == 0);
                    }
                    RegionProps.LogPile(c, Ground(176f, -148f), 80f, 2);
                    break;
                case "river_crossing":
                    RegionProps.Bridge(c);
                    break;
                case "river_east":
                    RegionProps.SteppingStones(c);
                    break;
                case "forest_road":
                    // Signpost at the crossroads: arrows to the village, the ruins, the mountain path, the deep forest.
                    c.PushWorld(Ground(3f, 88f), 0f);
                    c.Box(M.DarkWood, new Vector3(0f, 1.2f, 0f), new Vector3(0.14f, 2.4f, 0.14f));
                    c.Box(M.Wood, new Vector3(0.6f, 2f, 0f), new Vector3(1.2f, 0.25f, 0.06f), new Vector3(0f, 0f, 0f));
                    c.Box(M.Wood, new Vector3(0f, 1.6f, 0.6f), new Vector3(0.06f, 0.25f, 1.2f), new Vector3(0f, 0f, 0f));
                    c.Box(M.Wood, new Vector3(-0.6f, 1.25f, 0f), new Vector3(1.2f, 0.25f, 0.06f), new Vector3(0f, 0f, 0f));
                    c.Pop();
                    RegionProps.StoneLantern(c, Ground(-4.5f, 0f), true);
                    RegionProps.StoneLantern(c, Ground(4.5f, 70f), true);
                    break;
                case "danger_zone":
                    // Claw-scored trunks, scattered gear and red lamps warn travellers away.
                    for (int i = 0; i < 6; i++)
                    {
                        float x = 150f + (float)rng.NextDouble() * 70f, z = 120f + (float)rng.NextDouble() * 90f;
                        if (!RegionVegetation.Free(x, z, 0f)) continue;
                        RegionProps.OldGear(c, Ground(x, z), rng.Next());
                    }
                    break;
                case "mountain_path":
                    for (int i = 0; i < 18; i++)
                    {
                        float x = -230f + (float)rng.NextDouble() * 140f, z = 110f + (float)rng.NextDouble() * 120f;
                        if (!RegionVegetation.Free(x, z, 1f)) continue;
                        var sc = new Vector3(1.5f + (float)rng.NextDouble() * 3f, 1f + (float)rng.NextDouble() * 2.5f, 1.5f + (float)rng.NextDouble() * 3f);
                        var pos = Ground(x, z);
                        RegionVegetation.Rock(c.Batch, pos, sc, rng);
                        c.PushWorld(pos);
                        c.Collider(Vector3.zero, sc * 0.8f);
                        c.Pop();
                    }
                    break;
            }
        }

        private static void Undergrowth(MeshBatcher batch, SectorDef s, Transform root)
        {
            var rng = new System.Random(L.StableHash(s.Id) ^ 0x77);
            var b = s.Bounds;
            float area = b.Width * b.Depth;
            bool lush = s.Biome == SectorBiome.Forest || s.Biome == SectorBiome.DeepForest || s.Biome == SectorBiome.Woods || s.Biome == SectorBiome.Ruins;
            int ferns = (int)(area / (lush ? 45f : 160f));
            Color fernA = s.Biome == SectorBiome.Cursed ? new Color(0.25f, 0.15f, 0.18f) : new Color(0.16f, 0.32f, 0.22f);
            Color fernB = s.Biome == SectorBiome.Cursed ? new Color(0.3f, 0.1f, 0.12f) : new Color(0.22f, 0.36f, 0.24f);
            for (int i = 0; i < ferns; i++)
            {
                float x = b.MinX + (float)rng.NextDouble() * b.Width, z = b.MinZ + (float)rng.NextDouble() * b.Depth;
                if (!RegionVegetation.Free(x, z, -1f)) continue;
                RegionVegetation.Fern(batch, Ground(x, z), rng, rng.NextDouble() < 0.5 ? fernA : fernB, 0.8f + (float)rng.NextDouble() * 0.7f);
            }
            var grass = M.Plant(s.Biome == SectorBiome.Cursed ? new Color(0.35f, 0.28f, 0.28f) : new Color(0.42f, 0.6f, 0.32f));
            int tufts = (int)(area / (lush ? 60f : 25f));
            for (int i = 0; i < tufts; i++)
            {
                float x = b.MinX + (float)rng.NextDouble() * b.Width, z = b.MinZ + (float)rng.NextDouble() * b.Depth;
                if (L.NearestRoad(x, z, 4f, out _, out float half) < half + 0.4f || L.IsWater(x, z)) continue;
                RegionVegetation.GrassTuft(batch, Ground(x, z), rng, grass);
            }
            if (s.Biome != SectorBiome.Cursed)
            {
                var colors = new[] { new Color(0.95f, 0.85f, 0.35f), new Color(0.85f, 0.45f, 0.6f), new Color(0.6f, 0.65f, 0.95f), new Color(0.95f, 0.95f, 0.95f) };
                int flowers = (int)(area / 400f);
                for (int i = 0; i < flowers; i++)
                {
                    float x = b.MinX + (float)rng.NextDouble() * b.Width, z = b.MinZ + (float)rng.NextDouble() * b.Depth;
                    if (!RegionVegetation.Free(x, z, -1f)) continue;
                    RegionVegetation.Flowers(batch, Ground(x, z), rng, colors[rng.Next(colors.Length)]);
                }
            }
            // Reeds along the river banks.
            if (s.Biome == SectorBiome.Riverside)
            {
                for (float x = b.MinX + 2f; x < b.MaxX; x += 3.5f + (float)rng.NextDouble() * 3f)
                {
                    if (x < L.WaterfallX + 6f || Mathf.Abs(x) < 6f || Mathf.Abs(x - 190f) < 4f) continue;
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    float z = L.RiverZ(x) + side * (L.RiverHalfWidth + 1f + (float)rng.NextDouble() * 1.5f);
                    if (!b.Contains(x, z)) continue;
                    RegionVegetation.Reeds(batch, Ground(x, z), rng);
                }
            }
            // Scattered rocks (with colliders for the big ones).
            int rocks = (int)(area / (s.Biome == SectorBiome.Mountain ? 300f : 900f));
            var col = new GameObject("RockColliders").transform;
            col.SetParent(root, false);
            for (int i = 0; i < rocks; i++)
            {
                float x = b.MinX + (float)rng.NextDouble() * b.Width, z = b.MinZ + (float)rng.NextDouble() * b.Depth;
                if (!RegionVegetation.Free(x, z, 0f)) continue;
                var sc = new Vector3(0.6f + (float)rng.NextDouble() * 1.8f, 0.4f + (float)rng.NextDouble() * 1.2f, 0.6f + (float)rng.NextDouble() * 1.8f);
                var pos = Ground(x, z);
                RegionVegetation.Rock(batch, pos, sc, rng);
                if (sc.y < 0.7f) continue;
                var go = new GameObject("Rock");
                go.transform.SetParent(col, false);
                go.transform.position = pos;
                go.AddComponent<SphereCollider>().radius = Mathf.Min(sc.x, sc.z) * 0.45f;
            }
        }

        // ------------------------------------------------------------------ state

        /// <summary>Rebuilds the state props of a loaded sector (fence damage / repair stages, …).</summary>
        public static void RefreshState(SectorInstance inst, WorldStateDatabase world, double now)
        {
            if (inst == null || inst.StateRoot == null) return;
            for (int i = inst.StateRoot.childCount - 1; i >= 0; i--) Object.Destroy(inst.StateRoot.GetChild(i).gameObject);
            foreach (var m in inst.StateMeshes) if (m != null) Object.Destroy(m);
            inst.StateMeshes.Clear();
            var batch = new MeshBatcher { Sink = inst.StateMeshes };
            var c = new PropContext(batch, inst.StateRoot);
            if (inst.Def.Id == "village")
            {
                foreach (var (id, from, to) in L.FenceSections)
                    RegionProps.Fence(c, from, to, world != null ? world.StructureStage(id, now) : RecoveryStage.Intact, L.StableHash(id));
            }
            // Wreck left by a lost caravan stays on the road until the villagers clear it (recovery stages).
            if (world != null)
            {
                foreach (var spot in L.EventSpots)
                {
                    if (spot.Kind != WorldEventKind.CaravanAttack || spot.Sector != inst.Def.Id) continue;
                    string id = $"cart_{spot.X:0}_{spot.Z:0}";
                    var stage = world.StructureStage(id, now);
                    if (stage == RecoveryStage.Damaged || stage == RecoveryStage.Repairing)
                        RegionProps.Cart(c, RegionTerrain.OnGround(spot.X + 3f, spot.Z), 20f, true, L.StableHash(id));
                }
            }
            batch.Build(inst.StateRoot, "StateProps");
        }
    }

    /// <summary>Campfire that travellers light at dusk and put out in the morning.</summary>
    public sealed class Campfire : MonoBehaviour
    {
        public NightLight Light;
        public ParticleSystem Fire;
        private bool _lit = true;

        public void SetLit(bool lit)
        {
            if (lit == _lit) return;
            _lit = lit;
            if (Light != null) Light.Enabled = lit ? 1f : 0f;
            if (Fire == null) return;
            if (lit) Fire.Play(true);
            else Fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
