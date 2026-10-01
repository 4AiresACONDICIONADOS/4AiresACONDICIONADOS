using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Environment;
using BreathOfEclipse.Rendering;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;
using M = BreathOfEclipse.World.RegionMats;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// What is always loaded: the river and the waterfall, the landmarks you navigate by (the Elder Camphor, the
    /// bell tower, the mountains around the region), and the world border. Sector far views (low-res terrain and tree
    /// silhouettes) are owned by <see cref="WorldSectorSystem"/>.
    /// </summary>
    public static class RegionBase
    {
        public static void Build(Transform root)
        {
            var batch = new MeshBatcher { CellSize = 200f };
            var c = new PropContext(batch, root);

            // River surface following the channel, from the plunge pool to the eastern gorge.
            var centre = new List<Vector3>();
            var widths = new List<float>();
            for (float x = L.WaterfallX - 1f; x <= L.HalfSize + 10f; x += 4f)
            {
                centre.Add(new Vector3(x, L.WaterLevel, L.RiverZ(x)));
                widths.Add(L.RiverHalfWidth + 2.5f + (x < L.WaterfallX + 18f ? 5f : 0f));
            }
            var river = new GameObject("River");
            river.transform.SetParent(root, false);
            river.layer = Layers.Water;
            river.AddComponent<MeshFilter>().sharedMesh = RegionMeshes.Ribbon(centre, widths, "River");
            var rr = river.AddComponent<MeshRenderer>();
            rr.sharedMaterial = RegionProps.WaterMaterial("BoE_Shirase", new Color(0.18f, 0.45f, 0.62f, 0.72f), new Color(0.03f, 0.1f, 0.22f, 0.92f));
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Stream on the cliff top feeding the falls.
            var top = new List<Vector3>();
            var topW = new List<float>();
            for (float x = -L.HalfSize - 5f; x <= L.WaterfallX - 2f; x += 3f)
            {
                top.Add(new Vector3(x, L.CliffTop - 0.9f, L.RiverZ(x)));
                topW.Add(3.2f);
            }
            var stream = new GameObject("CliffStream");
            stream.transform.SetParent(root, false);
            stream.layer = Layers.Water;
            stream.AddComponent<MeshFilter>().sharedMesh = RegionMeshes.Ribbon(top, topW, "CliffStream");
            var sr = stream.AddComponent<MeshRenderer>();
            sr.sharedMaterial = rr.sharedMaterial;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            RegionProps.Waterfall(root);

            // Landmarks.
            var tree = L.Place("giant_tree");
            c.PushWorld(RegionTerrain.OnGround(tree.X, tree.Z), 20f);
            RegionProps.GiantTree(c, 99);
            c.Pop();
            var tower = L.Place("watchtower");
            c.PushWorld(RegionTerrain.OnGround(tower.X, tower.Z), tower.Yaw);
            RegionProps.Watchtower(c);
            c.Pop();

            // Distant mountains beyond the border: the tall one in the north-west reads from everywhere.
            Mountain(c, new Vector3(-150f, -6f, 420f), 260f, 190f, true);
            Mountain(c, new Vector3(120f, -6f, 470f), 320f, 150f, true);
            Mountain(c, new Vector3(-420f, -6f, 60f), 260f, 120f, false);
            Mountain(c, new Vector3(-400f, -6f, -260f), 240f, 95f, false);
            Mountain(c, new Vector3(430f, -6f, 150f), 280f, 110f, false);
            Mountain(c, new Vector3(420f, -6f, -250f), 260f, 85f, false);
            Mountain(c, new Vector3(0f, -6f, -440f), 340f, 70f, false);
            batch.Build(root, "Landmarks");

            // World border: steep mountains already contain the region; these walls catch the river gorge and corners.
            const float e = L.HalfSize - 6f;
            Wall(root, new Vector3(0f, 20f, e), new Vector3(2f * L.HalfSize, 60f, 2f));
            Wall(root, new Vector3(0f, 20f, -e), new Vector3(2f * L.HalfSize, 60f, 2f));
            Wall(root, new Vector3(e, 20f, 0f), new Vector3(2f, 60f, 2f * L.HalfSize));
            Wall(root, new Vector3(-e, 20f, 0f), new Vector3(2f, 60f, 2f * L.HalfSize));
        }

        private static void Mountain(PropContext c, Vector3 basePos, float radius, float height, bool snow)
        {
            var rng = new System.Random((int)(basePos.x * 13 + basePos.z));
            for (int i = 0; i < 4; i++)
            {
                var off = new Vector3((float)rng.NextDouble() * radius * 0.5f - radius * 0.25f, 0f, (float)rng.NextDouble() * radius * 0.5f - radius * 0.25f);
                float h = height * (i == 0 ? 1f : 0.55f + (float)rng.NextDouble() * 0.3f);
                float r = radius * (i == 0 ? 1f : 0.5f + (float)rng.NextDouble() * 0.3f);
                c.Cone(M.Mountain, basePos + off, new Vector3(r * 2f, h, r * 2f), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), 0f, 9, false);
                if (snow && i == 0) c.Cone(M.Snow, basePos + off + Vector3.up * h * 0.72f, new Vector3(r * 0.58f, h * 0.29f, r * 0.58f), Vector3.zero, 0f, 9, false);
            }
        }

        private static void Wall(Transform root, Vector3 center, Vector3 size)
        {
            var go = new GameObject("WorldBorder");
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.layer = Layers.IgnoreRaycast;
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
