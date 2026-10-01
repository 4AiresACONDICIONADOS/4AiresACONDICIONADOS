using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;
using M = BreathOfEclipse.World.RegionMats;

namespace BreathOfEclipse.World
{
    public enum TreeKind
    {
        Broadleaf = 0,
        Conifer = 1,
        Bamboo = 2,
        RedMaple = 3,
        Dead = 4,
        Sakura = 5
    }

    /// <summary>One tree placement (same list for the detailed sector and its far-view canopy).</summary>
    public struct TreePlacement
    {
        public Vector3 Position;
        public float Scale;
        public int Seed;
        public TreeKind Kind;
        public int Palette;
    }

    /// <summary>
    /// Vegetation of the frontier, merged into <see cref="MeshBatcher"/>s: anime broadleaves, conifers, bamboo
    /// groves, sakura near the village, red maples and dead trees in the Ash Hollow, ferns, grass tufts, flowers,
    /// reeds and rocks. Placement is deterministic per sector and avoids roads, water, places and steep cliffs.
    /// </summary>
    public static class RegionVegetation
    {
        private static readonly Color[] ForestLeaves =
        {
            new Color(0.17f, 0.34f, 0.27f), new Color(0.22f, 0.4f, 0.26f), new Color(0.28f, 0.45f, 0.3f), new Color(0.18f, 0.3f, 0.33f)
        };
        private static readonly Color[] FieldLeaves = { new Color(0.32f, 0.52f, 0.3f), new Color(0.38f, 0.58f, 0.32f), new Color(0.27f, 0.48f, 0.3f) };
        private static readonly Color[] DeepLeaves = { new Color(0.12f, 0.25f, 0.24f), new Color(0.16f, 0.28f, 0.27f), new Color(0.2f, 0.22f, 0.3f) };
        private static readonly Color[] CursedLeaves = { new Color(0.45f, 0.08f, 0.1f), new Color(0.32f, 0.07f, 0.12f), new Color(0.22f, 0.12f, 0.16f) };

        public static Color LeafColor(SectorBiome biome, int palette)
        {
            switch (biome)
            {
                case SectorBiome.Village:
                case SectorBiome.Fields:
                case SectorBiome.Riverside: return FieldLeaves[palette % FieldLeaves.Length];
                case SectorBiome.DeepForest:
                case SectorBiome.Mountain: return DeepLeaves[palette % DeepLeaves.Length];
                case SectorBiome.Cursed: return CursedLeaves[palette % CursedLeaves.Length];
                default: return ForestLeaves[palette % ForestLeaves.Length];
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Whether vegetation may grow at (x, z): not on roads, water, places, fields, cliffs or the plaza.</summary>
        public static bool Free(float x, float z, float clearance)
        {
            if (Mathf.Abs(x) > L.HalfSize - 4f || Mathf.Abs(z) > L.HalfSize - 4f) return false;
            if (L.NearestRoad(x, z, 8f, out _, out float half) < half + 1.5f + clearance) return false;
            if (x > L.WaterfallX - 6f && L.RiverDistance(x, z) < L.RiverBank - 3f + clearance) return false;
            if (L.InPond(x, z) || (x - 172f) * (x - 172f) / 400f + (z + 202f) * (z + 202f) / 330f < 1f) return false;
            foreach (var p in L.Places)
            {
                float r = Mathf.Max(p.Width, p.Depth) * 0.75f + 3f + clearance;
                if (p.Kind == PlaceKind.Field || p.Kind == PlaceKind.Paddy)
                {
                    if (Mathf.Abs(x - p.X) < p.Width * 0.5f + 2f && Mathf.Abs(z - p.Z) < p.Depth * 0.5f + 2f) return false;
                    continue;
                }
                if (p.Kind == PlaceKind.Spot) r = 4f + clearance;
                if (p.Kind == PlaceKind.GiantTree) r = 14f;
                if (p.Kind == PlaceKind.Camp || p.Kind == PlaceKind.Ruins) r = 16f;
                if ((x - p.X) * (x - p.X) + (z - p.Z) * (z - p.Z) < r * r) return false;
            }
            foreach (var s in L.EventSpots)
                if ((x - s.X) * (x - s.X) + (z - s.Z) * (z - s.Z) < 64f) return false;
            float village = new Vector2(x - L.VillageX, z - L.VillageZ).magnitude;
            if (village < L.VillageFenceRadius + 2f && village > L.VillageFenceRadius - 4f) return false; // fence line
            return true;
        }

        /// <summary>Deterministic tree placements of a sector.</summary>
        public static List<TreePlacement> Trees(SectorDef s)
        {
            var list = new List<TreePlacement>();
            if (s.TreeDensity <= 0f) return list;
            var rng = new System.Random(L.StableHash(s.Id));
            float spacing = 10f / Mathf.Sqrt(s.TreeDensity);
            var b = s.Bounds;
            for (float z = b.MinZ + spacing * 0.5f; z < b.MaxZ; z += spacing)
            for (float x = b.MinX + spacing * 0.5f; x < b.MaxX; x += spacing)
            {
                float jx = x + (float)(rng.NextDouble() - 0.5) * spacing * 0.9f;
                float jz = z + (float)(rng.NextDouble() - 0.5) * spacing * 0.9f;
                int seed = rng.Next();
                double roll = rng.NextDouble();
                if (!b.Contains(jx, jz) || !Free(jx, jz, 0f)) continue;
                float h = RegionTerrain.SampleHeight(jx, jz);
                if (L.Slope(jx, jz) > 38f) continue;
                // Village grounds: only a few trees inside the fence.
                float village = new Vector2(jx - L.VillageX, jz - L.VillageZ).magnitude;
                if (village < L.VillageFenceRadius && roll < 0.8) continue;
                var kind = PickKind(s, jx, jz, h, roll, village);
                list.Add(new TreePlacement
                {
                    Position = new Vector3(jx, h - 0.15f, jz),
                    Scale = 0.9f + (float)rng.NextDouble() * 0.8f + (s.Biome == SectorBiome.DeepForest ? 0.35f : 0f),
                    Seed = seed,
                    Kind = kind,
                    Palette = rng.Next(4)
                });
            }
            return list;
        }

        private static TreeKind PickKind(SectorDef s, float x, float z, float h, double roll, float villageDistance)
        {
            switch (s.Biome)
            {
                case SectorBiome.Cursed:
                    float hollow = new Vector2(x - 182f, z - 172f).magnitude;
                    if (hollow < 55f) return roll < 0.55 ? TreeKind.Dead : TreeKind.RedMaple;
                    return roll < 0.4 ? TreeKind.RedMaple : TreeKind.Conifer;
                case SectorBiome.Mountain:
                    return roll < 0.7 ? TreeKind.Conifer : TreeKind.Broadleaf;
                case SectorBiome.Village:
                case SectorBiome.Fields:
                    if (villageDistance < 95f && roll < 0.35) return TreeKind.Sakura;
                    return roll < 0.8 ? TreeKind.Broadleaf : TreeKind.Bamboo;
                case SectorBiome.Riverside:
                    return roll < 0.35 ? TreeKind.Bamboo : roll < 0.85 ? TreeKind.Broadleaf : TreeKind.Conifer;
                case SectorBiome.DeepForest:
                    return roll < 0.35 ? TreeKind.Conifer : TreeKind.Broadleaf;
                default:
                    if (Mathf.Abs(x) < 30f && roll < 0.2) return TreeKind.Bamboo;
                    return roll < 0.6 ? TreeKind.Broadleaf : TreeKind.Conifer;
            }
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>Adds a detailed tree to the batcher; returns its trunk radius / height for a collider.</summary>
        public static void Tree(MeshBatcher batch, TreePlacement t, SectorBiome biome, out float colliderRadius, out float colliderHeight)
        {
            var rng = new System.Random(t.Seed);
            var rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float s = t.Scale;
            Color leaves = LeafColor(biome, t.Palette);
            colliderRadius = 0.3f * s;
            colliderHeight = 3f * s;
            switch (t.Kind)
            {
                case TreeKind.Conifer:
                {
                    float h = (3.2f + (float)rng.NextDouble() * 1.6f) * s;
                    batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Cylinder), t.Position + Vector3.up * h * 0.5f, rot, new Vector3(0.45f * s, h * 0.5f, 0.45f * s), M.Bark);
                    var leaf = M.Leaves(leaves);
                    for (int i = 0; i < 3; i++)
                    {
                        float r = (2.6f - i * 0.6f) * s;
                        batch.Add(ProceduralMeshes.Cone(9), t.Position + Vector3.up * (h * 0.75f + i * 1.0f * s), rot * Quaternion.Euler(0f, i * 23f, 0f), new Vector3(r * 2f, 2.1f * s, r * 2f), leaf);
                    }
                    colliderHeight = h;
                    break;
                }
                case TreeKind.Bamboo:
                {
                    var stem = M.Plant(new Color(0.45f, 0.62f, 0.3f));
                    var leaf = M.Leaves(new Color(0.36f, 0.56f, 0.3f));
                    int count = 5 + rng.Next(5);
                    for (int i = 0; i < count; i++)
                    {
                        var off = new Vector3((float)rng.NextDouble() * 2.4f - 1.2f, 0f, (float)rng.NextDouble() * 2.4f - 1.2f);
                        float h = (6f + (float)rng.NextDouble() * 4f) * s;
                        var lean = Quaternion.Euler((float)rng.NextDouble() * 8f - 4f, 0f, (float)rng.NextDouble() * 8f - 4f);
                        batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Cylinder), t.Position + off + lean * Vector3.up * h * 0.5f, lean, new Vector3(0.12f, h * 0.5f, 0.12f), stem);
                        batch.Add(ProceduralMeshes.Blob(rng.Next(4), 6, 4, 0.25f), t.Position + off + lean * Vector3.up * h, lean, new Vector3(1.4f, 1.8f, 1.4f) * s, leaf);
                    }
                    colliderRadius = 0.8f;
                    colliderHeight = 6f * s;
                    break;
                }
                case TreeKind.Dead:
                {
                    var dead = MaterialFactory.Toon(new Color(0.12f, 0.1f, 0.12f), 0.9f, false, null, 0.5f, 0.3f);
                    float h = (3.5f + (float)rng.NextDouble() * 2f) * s;
                    var lean = Quaternion.Euler((float)rng.NextDouble() * 10f - 5f, 0f, (float)rng.NextDouble() * 10f - 5f);
                    batch.Add(ProceduralMeshes.OpenCylinder(0.35f, 0.1f, 7, 3, 1.2f), t.Position, lean * rot, new Vector3(1f, h, 1f) * s, dead);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 90f + (float)rng.NextDouble() * 40f;
                        batch.Add(ProceduralMeshes.Cone(5, 0.6f), t.Position + lean * Vector3.up * h * (0.55f + i * 0.1f), rot * Quaternion.Euler(-55f - (float)rng.NextDouble() * 20f, a, 0f),
                            new Vector3(0.18f, 1.8f, 0.18f) * s, dead);
                    }
                    colliderHeight = h;
                    break;
                }
                default:
                {
                    // Broadleaf / sakura / red maple: leaning trunk, branches, lumpy canopy (lit on top, shaded below).
                    Color baseLeaf = t.Kind == TreeKind.Sakura ? new Color(0.98f, 0.72f, 0.8f) : t.Kind == TreeKind.RedMaple ? CursedLeaves[t.Palette % CursedLeaves.Length] * 1.2f : leaves;
                    var shade = M.Leaves(baseLeaf * 0.8f);
                    var lit = M.Leaves(Color.Lerp(baseLeaf, new Color(0.62f, 0.78f, 0.82f), t.Kind == TreeKind.Sakura ? 0.1f : 0.22f));
                    float h = (2.8f + (float)rng.NextDouble() * 1.4f) * s;
                    var lean = Quaternion.Euler((float)rng.NextDouble() * 8f - 4f, 0f, (float)rng.NextDouble() * 6f - 3f);
                    batch.Add(ProceduralMeshes.OpenCylinder(0.34f, 0.2f, 8, 3), t.Position, rot * lean, new Vector3(s, h, s), M.Bark);
                    batch.Add(ProceduralMeshes.Cone(8), t.Position, rot, new Vector3(1.1f * s, 0.5f * s, 1.1f * s), M.Bark);
                    int blobs = 4 + rng.Next(3);
                    for (int i = 0; i < blobs; i++)
                    {
                        float a = i / (float)blobs * Mathf.PI * 2f + (float)rng.NextDouble();
                        float r = (i == 0 ? 0f : 0.9f + (float)rng.NextDouble() * 0.6f) * s;
                        float y = h + (0.4f + (i == 0 ? 0.9f : (float)rng.NextDouble() * 0.7f)) * s;
                        float size = (i == 0 ? 2.6f : 1.7f + (float)rng.NextDouble() * 0.8f) * s;
                        bool top = i == 0 || y > h + 0.9f * s;
                        batch.Add(ProceduralMeshes.Blob(rng.Next(4)), t.Position + rot * new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r),
                            Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(size, size * 0.8f, size), top ? lit : shade);
                    }
                    colliderRadius = 0.28f * s;
                    colliderHeight = h;
                    break;
                }
            }
        }

        /// <summary>Far-view stand-in: one low blob per tree on a stick (shares the placement list).</summary>
        public static void FarTree(MeshBatcher batch, TreePlacement t, SectorBiome biome)
        {
            float s = t.Scale;
            Color leaves = t.Kind == TreeKind.Sakura ? new Color(0.95f, 0.72f, 0.8f) : t.Kind == TreeKind.Dead ? new Color(0.12f, 0.1f, 0.12f) : t.Kind == TreeKind.RedMaple ? CursedLeaves[0] : LeafColor(biome, t.Palette);
            var mat = MaterialFactory.Toon(leaves * 0.9f, 0f, false, null, 0.45f, 0.3f);
            float h = t.Kind == TreeKind.Conifer ? 4.2f * s : t.Kind == TreeKind.Bamboo ? 7f * s : 3.6f * s;
            if (t.Kind == TreeKind.Conifer)
                batch.Add(ProceduralMeshes.Cone(6), t.Position + Vector3.up * h * 0.55f, Quaternion.identity, new Vector3(4.2f * s, 3.8f * s, 4.2f * s), mat, false);
            else
                batch.Add(RegionMeshes.LowBlob(t.Seed & 3), t.Position + Vector3.up * h, Quaternion.identity, new Vector3(3.8f, 3f, 3.8f) * s, mat, false);
        }

        public static void Fern(MeshBatcher batch, Vector3 pos, System.Random rng, Color color, float size)
        {
            var mat = M.Plant(color);
            int blades = 4 + rng.Next(3);
            var blade = ProceduralMeshes.Cone(4, 0.35f);
            for (int i = 0; i < blades; i++)
            {
                float a = i / (float)blades * 360f + (float)rng.NextDouble() * 30f;
                float h = (0.45f + (float)rng.NextDouble() * 0.45f) * size;
                batch.Add(blade, pos, Quaternion.Euler(25f + (float)rng.NextDouble() * 30f, a, 0f), new Vector3(0.22f * size, h, 0.05f * size), mat, false);
            }
        }

        public static void GrassTuft(MeshBatcher batch, Vector3 pos, System.Random rng, Material mat)
        {
            var blade = ProceduralMeshes.Cone(3, 0.25f);
            for (int i = 0; i < 3; i++)
                batch.Add(blade, pos, Quaternion.Euler((float)rng.NextDouble() * 25f, (float)rng.NextDouble() * 360f, 0f), new Vector3(0.09f, 0.3f + (float)rng.NextDouble() * 0.25f, 0.03f), mat, false);
        }

        public static void Flowers(MeshBatcher batch, Vector3 pos, System.Random rng, Color color)
        {
            var mat = MaterialFactory.Toon(color, 0f, false, color * 0.15f, 0.6f, 0.3f);
            var stem = M.Plant(new Color(0.3f, 0.5f, 0.28f));
            for (int i = 0; i < 4; i++)
            {
                var p = pos + new Vector3((float)rng.NextDouble() * 0.8f - 0.4f, 0f, (float)rng.NextDouble() * 0.8f - 0.4f);
                float h = 0.25f + (float)rng.NextDouble() * 0.2f;
                batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Cylinder), p + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(0.02f, h * 0.5f, 0.02f), stem, false);
                batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Sphere), p + Vector3.up * h, Quaternion.identity, new Vector3(0.12f, 0.06f, 0.12f), mat, false);
            }
        }

        public static void Reeds(MeshBatcher batch, Vector3 pos, System.Random rng)
        {
            var mat = M.Plant(new Color(0.48f, 0.55f, 0.3f));
            var head = M.Plant(new Color(0.42f, 0.3f, 0.2f));
            for (int i = 0; i < 6; i++)
            {
                var p = pos + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f);
                float h = 1f + (float)rng.NextDouble() * 0.8f;
                var lean = Quaternion.Euler((float)rng.NextDouble() * 12f - 6f, 0f, (float)rng.NextDouble() * 12f - 6f);
                batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Cylinder), p + lean * Vector3.up * h * 0.5f, lean, new Vector3(0.025f, h * 0.5f, 0.025f), mat, false);
                if (i % 2 == 0) batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Capsule), p + lean * Vector3.up * h, lean, new Vector3(0.06f, 0.12f, 0.06f), head, false);
            }
        }

        public static void Rock(MeshBatcher batch, Vector3 pos, Vector3 scale, System.Random rng, Material mat = null)
        {
            batch.Add(ProceduralMeshes.Primitive(PrimitiveType.Sphere), pos, Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f), scale, mat ?? M.Rock);
        }

        /// <summary>Rows of plants over a field (vegetables) or tufts in a paddy (rice).</summary>
        public static void Crops(MeshBatcher batch, PlaceDef field, System.Random rng)
        {
            bool rice = field.Kind == PlaceKind.Paddy;
            var mat = M.Plant(rice ? new Color(0.45f, 0.7f, 0.32f) : new Color(0.32f, 0.55f, 0.25f));
            var alt = M.Plant(new Color(0.75f, 0.68f, 0.3f));
            float hw = field.Width * 0.5f - 1f, hd = field.Depth * 0.5f - 1f;
            float row = rice ? 0.9f : 1.1f, step = rice ? 0.9f : 0.7f;
            for (float x = -hw; x <= hw; x += row)
            for (float z = -hd; z <= hd; z += step)
            {
                float px = field.X + x + (float)rng.NextDouble() * 0.15f, pz = field.Z + z;
                float y = rice ? L.WaterLevel + 1.3f : RegionTerrain.SampleHeight(px, pz);
                if (rice) y = RegionTerrain.SampleHeight(px, pz) + 0.05f;
                if (rice) GrassTuft(batch, new Vector3(px, y, pz), rng, mat);
                else batch.Add(ProceduralMeshes.Blob(rng.Next(4), 6, 4, 0.25f), new Vector3(px, y + 0.15f, pz), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                    new Vector3(0.45f, 0.35f, 0.45f), field.Id == "field_far" ? alt : mat, false);
            }
        }
    }
}
