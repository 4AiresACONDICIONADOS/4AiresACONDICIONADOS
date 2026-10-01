using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Terrain of the Asagiri frontier: the analytic height function sampled once on a 2 m grid for the whole region
    /// (meshes, NPC ground height, normals without seams between sectors), a painted ground colour map (grass by
    /// biome, packed-earth roads by kind, plaza, furrows, river sand, rock on slopes, ash in the hollow) and chunk
    /// meshes per sector at two resolutions (detail with collider / far view with skirts).
    /// </summary>
    public static class RegionTerrain
    {
        public const float Cell = 2f;
        public const int GridSize = (int)(2 * L.HalfSize / Cell) + 1;
        public const int ColorMapSize = 1024;

        private static float[] _h;
        private static Texture2D _colorMap;
        private static Material _material;

        public static bool Initialized => _h != null;
        public static Texture2D ColorMap => _colorMap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _h = null;
            _colorMap = null;
            _material = null;
        }

        /// <summary>Samples the height grid and paints the colour map (rows in parallel; once, behind the loading fade).</summary>
        public static void Initialize()
        {
            if (_h != null) return;
            _h = new float[GridSize * GridSize];
            for (int j = 0; j < GridSize; j++)
            {
                float z = -L.HalfSize + j * Cell;
                for (int i = 0; i < GridSize; i++) _h[j * GridSize + i] = L.Height(-L.HalfSize + i * Cell, z);
            }
            _colorMap = PaintColorMap();
        }

        private static float GridAt(int i, int j)
        {
            i = i < 0 ? 0 : i >= GridSize ? GridSize - 1 : i;
            j = j < 0 ? 0 : j >= GridSize ? GridSize - 1 : j;
            return _h[j * GridSize + i];
        }

        /// <summary>Ground height (bilinear on the 2 m grid, matches the terrain mesh).</summary>
        public static float SampleHeight(float x, float z)
        {
            if (_h == null) return L.Height(x, z);
            float fx = (x + L.HalfSize) / Cell, fz = (z + L.HalfSize) / Cell;
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            float u = fx - i, v = fz - j;
            float a = GridAt(i, j), b = GridAt(i + 1, j), c = GridAt(i, j + 1), d = GridAt(i + 1, j + 1);
            // The mesh splits each cell along the same diagonal as BuildChunk.
            return u + v <= 1f ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1f - u) + (b - d) * (1f - v);
        }

        /// <summary>Where feet go: bridge deck and stepping stones over water, otherwise the ground.</summary>
        public static float WalkHeight(float x, float z)
        {
            if (Mathf.Abs(x) < 2.6f && z > -66f && z < -30f) return L.BridgeDeck(z);
            float h = SampleHeight(x, z);
            if (Mathf.Abs(x - 190f) < 1.6f && z > -80f && z < -30f) return Mathf.Max(h, L.WaterLevel + 0.35f);
            return h;
        }

        public static Vector3 Normal(float x, float z)
        {
            float dx = SampleHeight(x + Cell, z) - SampleHeight(x - Cell, z);
            float dz = SampleHeight(x, z + Cell) - SampleHeight(x, z - Cell);
            return new Vector3(-dx, 2f * Cell, -dz).normalized;
        }

        public static Vector3 OnGround(float x, float z) => new Vector3(x, SampleHeight(x, z), z);

        public static Material Material
        {
            get
            {
                if (_material != null) return _material;
                Initialize();
                _material = new Material(MaterialFactory.FindShader(ShaderIds.ToonLit)) { name = "BoE_FrontierGround" };
                _material.SetColor(ShaderIds.BaseColor, Color.white);
                _material.SetTexture(ShaderIds.BaseMap, _colorMap);
                _material.SetColor(ShaderIds.ShadeColor, new Color(0.48f, 0.46f, 0.62f));
                _material.SetFloat(ShaderIds.ShadeThreshold, 0.42f);
                _material.SetFloat(ShaderIds.ShadeSoftness, 0.05f);
                _material.SetColor(ShaderIds.RimColor, new Color(0.05f, 0.06f, 0.1f));
                _material.SetFloat(ShaderIds.RimPower, 4f);
                _material.SetColor(ShaderIds.SpecColor, Color.black);
                _material.SetFloat(ShaderIds.OutlineWidth, 0f);
                _material.SetFloat(ShaderIds.IsCharacter, 0f);
                _material.enableInstancing = false;
                return _material;
            }
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>
        /// Terrain mesh for a rectangle. <paramref name="step"/> = grid cells per quad (1 = 2 m detail, 5 = 10 m far view).
        /// Far meshes get a 3 m skirt so no gap shows next to a detailed neighbour.
        /// </summary>
        public static Mesh BuildChunk(WorldRect r, int step, bool skirt, string name)
        {
            Initialize();
            int i0 = Mathf.RoundToInt((r.MinX + L.HalfSize) / Cell), i1 = Mathf.RoundToInt((r.MaxX + L.HalfSize) / Cell);
            int j0 = Mathf.RoundToInt((r.MinZ + L.HalfSize) / Cell), j1 = Mathf.RoundToInt((r.MaxZ + L.HalfSize) / Cell);
            int nx = Mathf.Max(1, (i1 - i0) / step), nz = Mathf.Max(1, (j1 - j0) / step);
            int vx = nx + 1, vz = nz + 1;
            int skirtVerts = skirt ? 2 * (vx + vz) * 2 : 0;
            var verts = new Vector3[vx * vz + skirtVerts];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            for (int b = 0; b < vz; b++)
            {
                for (int a = 0; a < vx; a++)
                {
                    int gi = Mathf.Min(i0 + a * step, i1), gj = Mathf.Min(j0 + b * step, j1);
                    float x = -L.HalfSize + gi * Cell, z = -L.HalfSize + gj * Cell;
                    int k = b * vx + a;
                    verts[k] = new Vector3(x, GridAt(gi, gj), z);
                    float dx = GridAt(gi + 1, gj) - GridAt(gi - 1, gj);
                    float dz = GridAt(gi, gj + 1) - GridAt(gi, gj - 1);
                    normals[k] = new Vector3(-dx, 2f * Cell, -dz).normalized;
                    uvs[k] = new Vector2((x + L.HalfSize) / (2f * L.HalfSize), (z + L.HalfSize) / (2f * L.HalfSize));
                }
            }
            var tris = new System.Collections.Generic.List<int>(nx * nz * 6 + skirtVerts * 3);
            for (int b = 0; b < nz; b++)
            {
                for (int a = 0; a < nx; a++)
                {
                    int p = b * vx + a;
                    int q = p + vx;
                    tris.Add(p); tris.Add(q); tris.Add(p + 1);
                    tris.Add(p + 1); tris.Add(q); tris.Add(q + 1);
                }
            }
            if (skirt)
            {
                int s = vx * vz;
                void Edge(int count, System.Func<int, int> index, bool flip)
                {
                    for (int e = 0; e < count; e++)
                    {
                        int top = index(e);
                        verts[s + e * 2] = verts[top];
                        verts[s + e * 2 + 1] = verts[top] - Vector3.up * 3f;
                        normals[s + e * 2] = normals[s + e * 2 + 1] = normals[top];
                        uvs[s + e * 2] = uvs[s + e * 2 + 1] = uvs[top];
                    }
                    for (int e = 0; e < count - 1; e++)
                    {
                        int a0 = s + e * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                        if (flip) { tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1); }
                        else { tris.Add(a0); tris.Add(a1); tris.Add(b0); tris.Add(a1); tris.Add(b1); tris.Add(b0); }
                    }
                    s += count * 2;
                }
                Edge(vx, a => a, true);                         // south
                Edge(vx, a => (vz - 1) * vx + a, false);        // north
                Edge(vz, b => b * vx, false);                   // west
                Edge(vz, b => b * vx + vx - 1, true);           // east
            }
            var mesh = new Mesh { name = name };
            if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ colour map

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        private static Texture2D PaintColorMap()
        {
            int n = ColorMapSize;
            var px = new Color32[n * n];
            Color grassLight = Hex(0x7FA662), grassVillage = Hex(0x86A964), grassForest = Hex(0x4E7650), grassDeep = Hex(0x3A5E4A);
            Color grassMountain = Hex(0x6E8A63), ash = Hex(0x5E5560), ashRed = Hex(0x6B4A50);
            Color dirtRoad = Hex(0xB89D74), dirtTrail = Hex(0x9C8466), dirtVillage = Hex(0xC2A983), dirtDanger = Hex(0x6E4E48);
            Color rock = Hex(0x8A8A90), sand = Hex(0xC9BC97), mud = Hex(0x6C5A44), furrowA = Hex(0x7C6448), furrowB = Hex(0x93A55E);
            float scale = 2f * L.HalfSize / n;
            var fields = new System.Collections.Generic.List<PlaceDef>();
            foreach (var p in L.Places)
                if (p.Kind == PlaceKind.Field || p.Kind == PlaceKind.Paddy) fields.Add(p);
            L.NearestRoad(0f, 0f, 1f, out _, out _); // builds the (lazy) road grid before the worker threads read it
            // Rows are independent and everything below is pure math on immutable data: paint them in parallel.
            System.Threading.Tasks.Parallel.For(0, n, j =>
            {
                float z = -L.HalfSize + (j + 0.5f) * scale;
                for (int i = 0; i < n; i++)
                {
                    float x = -L.HalfSize + (i + 0.5f) * scale;
                    float h = SampleHeight(x, z);
                    // Biome blend: lighter grass in the south, deep green in the north, ash in the hollow.
                    float north = Mathf.Clamp01((z + 30f) / 180f);
                    Color c = Color.Lerp(grassLight, grassForest, Mathf.SmoothStep(0f, 1f, north));
                    c = Color.Lerp(c, grassDeep, Mathf.Clamp01((z - 110f) / 80f) * Mathf.Clamp01(1f - Mathf.Abs(x) / 120f));
                    float village = Mathf.Clamp01(1f - (new Vector2(x - L.VillageX, z - L.VillageZ).magnitude - 55f) / 30f);
                    c = Color.Lerp(c, grassVillage, village);
                    c = Color.Lerp(c, grassMountain, Mathf.Clamp01((h - 7f) / 8f));
                    float hollow = Mathf.Clamp01(1f - (new Vector2(x - 182f, z - 172f).magnitude - 45f) / 35f);
                    if (hollow > 0f) c = Color.Lerp(c, Color.Lerp(ash, ashRed, L.Noise(x * 0.08f, z * 0.08f)), hollow);

                    // Fields: furrows on dry fields, mud in paddies.
                    foreach (var p in fields)
                    {
                        if (Mathf.Abs(x - p.X) > p.Width * 0.5f || Mathf.Abs(z - p.Z) > p.Depth * 0.5f) continue;
                        c = p.Kind == PlaceKind.Paddy ? mud : (Mathf.Repeat(x * 0.9f, 1f) < 0.5f ? furrowA : furrowB);
                    }

                    // River sand and rock on steep ground.
                    float rd = L.RiverDistance(x, z);
                    if (x > L.WaterfallX - 4f && rd < L.RiverBank) c = Color.Lerp(sand, c, Mathf.SmoothStep(0f, 1f, (rd - L.RiverHalfWidth) / (L.RiverBank - L.RiverHalfWidth)));
                    float dhx = SampleHeight(x + 1f, z) - SampleHeight(x - 1f, z), dhz = SampleHeight(x, z + 1f) - SampleHeight(x, z - 1f);
                    float slope = Mathf.Sqrt(dhx * dhx + dhz * dhz) * 0.5f;
                    c = Color.Lerp(c, rock, Mathf.SmoothStep(0f, 1f, (slope - 0.55f) / 0.4f));

                    // Roads by kind (the visual language: wide pale road, narrow darker trail, red-brown danger path).
                    float d = L.NearestRoad(x, z, 8f, out var kind, out float half);
                    if (d < half + 1.2f && kind != PathKind.Bridge)
                    {
                        Color road = kind == PathKind.Road ? dirtRoad : kind == PathKind.Village ? dirtVillage : kind == PathKind.Danger ? dirtDanger : dirtTrail;
                        float edge = 1f - Mathf.SmoothStep(0f, 1f, (d - half + 0.6f) / 1.8f);
                        // Wheel ruts on the main road.
                        if (kind == PathKind.Road && Mathf.Abs(Mathf.Abs(d) - half * 0.45f) < 0.25f) road *= 0.88f;
                        c = Color.Lerp(c, road, edge * (kind == PathKind.Trail || kind == PathKind.Forest ? 0.8f : 1f));
                    }
                    // Plaza: packed earth around the well.
                    float plaza = new Vector2(x - 0f, z + 158f).magnitude;
                    if (plaza < 16f) c = Color.Lerp(dirtVillage, c, Mathf.SmoothStep(0f, 1f, (plaza - 12f) / 4f));

                    // Painted detail: two noise octaves and tiny speckles (grass tufts / pebbles).
                    float detail = 0.86f + 0.16f * L.Noise(x * 0.7f, z * 0.7f) + 0.08f * L.Noise(x * 3.1f, z * 3.1f);
                    if (L.Hash(i, j) > 0.985f) detail *= 1.15f;
                    c *= detail;
                    px[j * n + i] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), 255);
                }
            });
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
            {
                name = "BoE_FrontierColorMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
