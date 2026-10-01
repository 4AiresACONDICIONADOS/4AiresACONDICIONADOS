using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Merges many small meshes (trees, plants, rocks, crops, fence posts) into one mesh per material and 40 m cell:
    /// a forest sector renders in a few draw calls, builds in milliseconds (no StaticBatchingUtility hitch) and still
    /// culls by cell.
    /// </summary>
    public sealed class MeshBatcher
    {
        private sealed class Bucket
        {
            public Material Material;
            public int Cell;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            public bool Shadows;
        }

        private readonly Dictionary<(Material, int), Bucket> _buckets = new Dictionary<(Material, int), Bucket>();
        private readonly Dictionary<Mesh, (Vector3[] v, Vector3[] n, Vector2[] uv, int[] t)> _meshData = new Dictionary<Mesh, (Vector3[], Vector3[], Vector2[], int[])>();
        public float CellSize = 40f;
        public int Count { get; private set; }
        /// <summary>Receives every mesh <see cref="Build"/> creates, so streamed content can free them on unload.</summary>
        public List<Mesh> Sink;

        private int CellOf(Vector3 p) => Mathf.FloorToInt((p.x + 1000f) / CellSize) * 1000 + Mathf.FloorToInt((p.z + 1000f) / CellSize);

        public void Add(Mesh mesh, Matrix4x4 m, Material material, bool castShadows = true)
        {
            if (mesh == null || material == null) return;
            if (!_meshData.TryGetValue(mesh, out var data))
            {
                data = (mesh.vertices, mesh.normals, mesh.uv, mesh.triangles);
                _meshData[mesh] = data;
            }
            var key = (material, CellOf(m.GetColumn(3)));
            if (!_buckets.TryGetValue(key, out var b))
            {
                b = new Bucket { Material = material, Cell = key.Item2, Shadows = castShadows };
                _buckets[key] = b;
            }
            int baseIndex = b.V.Count;
            var nm = m.inverse.transpose;
            bool hasN = data.n != null && data.n.Length == data.v.Length;
            bool hasUV = data.uv != null && data.uv.Length == data.v.Length;
            for (int i = 0; i < data.v.Length; i++)
            {
                b.V.Add(m.MultiplyPoint3x4(data.v[i]));
                b.N.Add(hasN ? nm.MultiplyVector(data.n[i]).normalized : Vector3.up);
                b.UV.Add(hasUV ? data.uv[i] : Vector2.zero);
            }
            bool flip = m.determinant < 0f;
            for (int i = 0; i < data.t.Length; i += 3)
            {
                b.T.Add(baseIndex + data.t[i]);
                b.T.Add(baseIndex + (flip ? data.t[i + 2] : data.t[i + 1]));
                b.T.Add(baseIndex + (flip ? data.t[i + 1] : data.t[i + 2]));
            }
            Count++;
        }

        public void Add(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool castShadows = true) =>
            Add(mesh, Matrix4x4.TRS(position, rotation, scale), material, castShadows);

        /// <summary>Creates the merged renderers under <paramref name="parent"/> and clears the batcher.</summary>
        public List<GameObject> Build(Transform parent, string name, int layer = 0)
        {
            var result = new List<GameObject>();
            foreach (var b in _buckets.Values)
            {
                if (b.V.Count == 0) continue;
                var mesh = new Mesh { name = name };
                if (b.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(b.V);
                mesh.SetNormals(b.N);
                mesh.SetUVs(0, b.UV);
                mesh.SetTriangles(b.T, 0);
                mesh.RecalculateBounds();
                Sink?.Add(mesh);
                var go = new GameObject($"{name}_{b.Material.name}");
                go.layer = layer;
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = b.Material;
                r.shadowCastingMode = b.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                result.Add(go);
            }
            _buckets.Clear();
            Count = 0;
            return result;
        }
    }

    /// <summary>Shapes for the region's buildings (gable roofs, open boxes) and cheap low-poly blobs.</summary>
    public static class RegionMeshes
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        /// <summary>
        /// Gable roof: two sloped planes with thickness along X (ridge along X), the gable triangles closed. Unit size
        /// 1 × 1 × 1 (width x, height y, depth z) centred on the eaves line; scale it per building. Slightly curved eaves.
        /// </summary>
        public static Mesh Gable()
        {
            if (Cache.TryGetValue("gable", out var m)) return m;
            var v = new List<Vector3>();
            var t = new List<int>();
            const int seg = 6;
            const float thick = 0.06f;
            // Each slope: from eave (z = ±0.5, y = 0) to ridge (z = 0, y = 1), slightly concave (anime temple roof).
            for (int side = -1; side <= 1; side += 2)
            {
                int start = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float k = i / (float)seg;
                    float z = side * 0.5f * (1f - k);
                    float y = Mathf.Pow(k, 1.25f) - 0.06f * Mathf.Sin(k * Mathf.PI);
                    v.Add(new Vector3(-0.5f, y + thick, z));
                    v.Add(new Vector3(0.5f, y + thick, z));
                    v.Add(new Vector3(-0.5f, y, z));
                    v.Add(new Vector3(0.5f, y, z));
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = start + i * 4, b = a + 4;
                    if (side < 0)
                    {
                        t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });         // top
                        t.AddRange(new[] { a + 2, a + 3, b + 2, a + 3, b + 3, b + 2 }); // bottom
                    }
                    else
                    {
                        t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                        t.AddRange(new[] { a + 2, b + 2, a + 3, a + 3, b + 2, b + 3 });
                    }
                }
            }
            // Gable ends (triangles under the slopes).
            int g = v.Count;
            v.Add(new Vector3(-0.48f, 0f, -0.5f)); v.Add(new Vector3(-0.48f, 0f, 0.5f)); v.Add(new Vector3(-0.48f, 0.98f, 0f));
            v.Add(new Vector3(0.48f, 0f, -0.5f)); v.Add(new Vector3(0.48f, 0f, 0.5f)); v.Add(new Vector3(0.48f, 0.98f, 0f));
            t.AddRange(new[] { g, g + 1, g + 2, g + 3, g + 5, g + 4 });
            m = new Mesh { name = "GableRoof" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            Cache["gable"] = m;
            return m;
        }

        /// <summary>Low-poly lumpy blob (far canopies, bushes): an octahedron subdivided once and jittered.</summary>
        public static Mesh LowBlob(int variant)
        {
            string key = "lowblob" + variant;
            if (Cache.TryGetValue(key, out var m)) return m;
            m = ProceduralMeshes.Blob(variant, 6, 4, 0.2f);
            Cache[key] = m;
            return m;
        }

        /// <summary>Flat quad lying on XZ (unit), double sided.</summary>
        public static Mesh FlatQuad()
        {
            if (Cache.TryGetValue("flatquad", out var m)) return m;
            m = new Mesh { name = "FlatQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
                                 new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.down, Vector3.down, Vector3.down, Vector3.down };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6 };
            m.RecalculateBounds();
            Cache["flatquad"] = m;
            return m;
        }

        /// <summary>Vertical quad facing +Z (unit), double sided (cloth, banners, windows).</summary>
        public static Mesh Panel()
        {
            if (Cache.TryGetValue("panel", out var m)) return m;
            m = new Mesh { name = "Panel" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
                                 new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 1, 2, 1, 3, 2, 4, 6, 5, 5, 6, 7 };
            m.RecalculateBounds();
            Cache["panel"] = m;
            return m;
        }

        /// <summary>A long ribbon following a polyline on the ground / water (river surface), with world-space UVs.</summary>
        public static Mesh Ribbon(IList<Vector3> centre, IList<float> halfWidths, string name)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float along = 0f;
            for (int i = 0; i < centre.Count; i++)
            {
                Vector3 dir = i < centre.Count - 1 ? centre[i + 1] - centre[i] : centre[i] - centre[i - 1];
                dir.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                if (i > 0) along += Vector3.Distance(centre[i], centre[i - 1]);
                v.Add(centre[i] - side * halfWidths[i]);
                v.Add(centre[i] + side * halfWidths[i]);
                uv.Add(new Vector2(0f, along));
                uv.Add(new Vector2(1f, along));
                if (i > 0)
                {
                    int a = (i - 1) * 2;
                    t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Shoji / window paper texture: pale paper with a dark wooden lattice.</summary>
        public static Texture2D Shoji => ProceduralTextures.Generate("shoji", 64, 64, (u, v) =>
        {
            float gx = Mathf.Repeat(u * 3f, 1f), gy = Mathf.Repeat(v * 4f, 1f);
            bool frame = gx < 0.08f || gy < 0.08f || u < 0.05f || u > 0.95f || v < 0.04f || v > 0.96f;
            return frame ? new Color(0.25f, 0.18f, 0.14f) : new Color(0.97f, 0.94f, 0.86f);
        }, TextureWrapMode.Repeat);

        /// <summary>Roof tile rows (multiplied on the roof colour).</summary>
        public static Texture2D RoofTiles => ProceduralTextures.Generate("roof_tiles", 64, 64, (u, v) =>
        {
            float row = Mathf.Repeat(v * 8f, 1f);
            float col = Mathf.Repeat(u * 10f + (Mathf.Floor(v * 8f) % 2f) * 0.5f, 1f);
            float shade = 0.82f + 0.18f * Mathf.SmoothStep(0f, 0.6f, row) - (col < 0.06f ? 0.12f : 0f);
            return new Color(shade, shade, shade);
        }, TextureWrapMode.Repeat);

        /// <summary>Plaster wall with a faint stain gradient near the ground.</summary>
        public static Texture2D Plaster => ProceduralTextures.Generate("plaster", 64, 64, (u, v) =>
        {
            float n = 0.92f + 0.08f * ProceduralTextures.ValueNoise(u * 8f, v * 8f, 8, 31);
            n *= Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 0.25f, v));
            return new Color(n, n, n);
        }, TextureWrapMode.Repeat);
    }
}
