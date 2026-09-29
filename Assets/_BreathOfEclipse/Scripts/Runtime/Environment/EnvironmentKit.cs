using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.Environment
{
    /// <summary>Keeps the sky dome centered on the camera.</summary>
    public sealed class SkyFollow : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position;
        }
    }

    /// <summary>
    /// Procedural set dressing for the prototype: night sky with a big moon, dramatic moonlight, fog, stylized
    /// trees, rocks, lanterns, an original shrine gate and temple ruins, a stream, destructible props and ambient
    /// particles. Everything is primitive-based and cheap.
    /// </summary>
    public static class EnvironmentKit
    {
        public static readonly Color MoonColor = new Color(0.72f, 0.8f, 1f);

        // ------------------------------------------------------------------ atmosphere

        /// <summary>Ambient, fog and shader globals for the night look (runtime only, not serialized with baked objects).</summary>
        public static void ApplyNightAtmosphere(Transform worldRoot, Vector3 moonDirection, float fogDensity = 0.016f)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.2f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.12f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.17f);
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.skybox = null;
            Shader.SetGlobalVector(ShaderIds.GlobalMoonDir, moonDirection.normalized);
            Shader.SetGlobalFloat(ShaderIds.GlobalFlashFrame, 0f);
            if (worldRoot != null)
            {
                var moon = worldRoot.Find("Moonlight");
                if (moon != null) RenderSettings.sun = moon.GetComponent<Light>();
            }
        }

        /// <summary>Moon key light + cool rim light (scene objects).</summary>
        public static Light CreateMoonLights(Transform parent, Vector3 moonDirection, float intensity = 1.6f)
        {
            var go = new GameObject("Moonlight");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(-moonDirection.normalized);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = MoonColor;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            RenderSettings.sun = light;

            var rim = new GameObject("RimLight");
            rim.transform.SetParent(parent, false);
            rim.transform.rotation = Quaternion.LookRotation(new Vector3(moonDirection.x, -0.3f, moonDirection.z).normalized);
            var rimLight = rim.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.45f, 0.35f, 0.8f);
            rimLight.intensity = 0.35f;
            rimLight.shadows = LightShadows.None;
            return light;
        }

        public static GameObject SkyDome(Transform parent, Vector3 moonDirection, float moonSize = 0.06f)
        {
            var mat = new Material(MaterialFactory.FindShader(ShaderIds.SkyDome)) { name = "BoE_Sky" };
            mat.SetColor("_TopColor", new Color(0.02f, 0.03f, 0.09f));
            mat.SetColor("_HorizonColor", new Color(0.12f, 0.13f, 0.28f));
            mat.SetColor("_MoonColor", new Color(1.6f, 1.7f, 2f));
            mat.SetVector("_MoonDir", moonDirection.normalized);
            mat.SetFloat("_MoonSize", moonSize);
            mat.SetTexture("_NoiseTex", ProceduralTextures.Noise);
            var go = ProceduralMeshes.CreatePart("SkyDome", ProceduralMeshes.SkySphere(), mat, parent, Vector3.zero, Quaternion.identity, Vector3.one * 800f);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<SkyFollow>();
            return go;
        }

        /// <summary>Soft volumetric-looking light shafts under the moon (fake volumetrics: additive cones).</summary>
        public static void LightShafts(Transform parent, Vector3 center, Vector3 moonDirection, int count, float radius)
        {
            var mat = MaterialFactory.Vfx(ProceduralTextures.Band, VfxBlend.Additive, 1f);
            var rng = new System.Random(7);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + new Vector3((float)(rng.NextDouble() * 2 - 1) * radius, 0f, (float)(rng.NextDouble() * 2 - 1) * radius);
                var go = ProceduralMeshes.CreatePart("Shaft" + i, ProceduralMeshes.OpenCylinder(0.8f, 2.5f, 16, 2), mat, parent, p,
                    Quaternion.FromToRotation(Vector3.up, moonDirection.normalized), new Vector3(1f, 26f, 1f));
                go.layer = Layers.VFX;
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ShaderIds.TintColor, new Color(0.25f, 0.3f, 0.5f, 0.12f));
                r.SetPropertyBlock(mpb);
            }
        }

        public static void AmbientParticles(Transform parent, Vector3 center, Vector3 size, bool fireflies, bool petals, bool mist)
        {
            var root = new GameObject("AmbientParticles").transform;
            root.SetParent(parent, false);
            root.position = center;
            if (fireflies)
            {
                ParticleBuilder.Create("Fireflies", root, MaterialFactory.Vfx(ProceduralTextures.Glow, VfxBlend.Additive), new Vector3(0f, 1.5f, 0f))
                    .Duration(5f, true).Rate(14f).Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, size).Life(4f, 7f).Speed(0.05f, 0.25f)
                    .Size(0.06f, 0.12f).Color(new Color(1.6f, 2f, 0.9f), new Color(0.9f, 1.8f, 1.6f)).Noise(0.6f, 0.4f, 0.3f).Fade(0.2f, 0.7f).MaxParticles(200)
                    .PlayOnAwake();
            }
            if (petals)
            {
                ParticleBuilder.Create("Petals", root, MaterialFactory.Vfx(ProceduralTextures.Petal, VfxBlend.AlphaBlend), new Vector3(0f, 8f, 0f))
                    .Duration(5f, true).Rate(8f).Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(size.x, 1f, size.z)).Life(8f, 12f).Speed(0.2f, 0.6f)
                    .Size(0.07f, 0.12f).Rotation(0f, 360f).Spin(-120f, 120f).Color(new Color(1f, 0.75f, 0.85f, 0.9f), new Color(0.95f, 0.85f, 1f, 0.9f))
                    .Gravity(0.02f).Noise(0.5f, 0.3f, 0.2f).Fade(0.1f, 0.85f).MaxParticles(150).PlayOnAwake();
            }
            if (mist)
            {
                ParticleBuilder.Create("GroundMist", root, MaterialFactory.Vfx(ProceduralTextures.Smoke, VfxBlend.AlphaBlend, 1f), new Vector3(0f, 0.4f, 0f))
                    .Duration(5f, true).Rate(5f).Shape(ParticleSystemShapeType.Box, 1f, 0f, 360f, new Vector3(size.x, 0.2f, size.z)).Life(10f, 14f).Speed(0.05f, 0.15f)
                    .Size(6f, 10f).Rotation(0f, 360f).Spin(-6f, 6f).Color(new Color(0.55f, 0.62f, 0.85f, 0.12f)).Fade(0.25f, 0.6f).MaxParticles(80).PlayOnAwake();
            }
            // Pre-simulate so the scene starts full.
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Simulate(8f, true, true);
                ps.Play(true);
            }
        }

        // ------------------------------------------------------------------ ground & water

        /// <summary>Grid ground with optional gentle hills (height function in world XZ). Includes a MeshCollider.</summary>
        public static GameObject Ground(Transform parent, string name, Vector2 size, float cell, System.Func<float, float, float> height, Color color, Texture detail, float tiling)
        {
            int nx = Mathf.Max(2, Mathf.RoundToInt(size.x / cell));
            int nz = Mathf.Max(2, Mathf.RoundToInt(size.y / cell));
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++)
            {
                for (int x = 0; x <= nx; x++)
                {
                    float wx = -size.x * 0.5f + x * size.x / nx;
                    float wz = -size.y * 0.5f + z * size.y / nz;
                    verts[z * (nx + 1) + x] = new Vector3(wx, height != null ? height(wx, wz) : 0f, wz);
                    uvs[z * (nx + 1) + x] = new Vector2(wx, wz) / 10f;
                }
            }
            int t = 0;
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    int a = z * (nx + 1) + x;
                    int b = a + nx + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            }
            var mesh = new Mesh { name = name, indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Toon(color, 0f, false, null, 0.55f, 0.12f, 0f, detail, tiling);
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        public static GameObject Water(Transform parent, Vector3 center, Vector2 size, float rotationY)
        {
            var mat = new Material(MaterialFactory.FindShader(ShaderIds.ToonWater)) { name = "BoE_Stream" };
            mat.SetColor("_ShallowColor", new Color(0.18f, 0.42f, 0.62f, 0.7f));
            mat.SetColor("_DeepColor", new Color(0.03f, 0.08f, 0.2f, 0.9f));
            mat.SetColor("_FoamColor", new Color(0.85f, 0.95f, 1f, 1f));
            mat.SetTexture("_NoiseTex", ProceduralTextures.Caustics);
            var go = ProceduralMeshes.CreatePart("Stream", ProceduralMeshes.GroundQuad(), mat, parent, center, Quaternion.Euler(0f, rotationY, 0f), new Vector3(size.x, 1f, size.y));
            go.layer = Layers.Water;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ props

        public static GameObject Tree(Transform parent, Vector3 position, float scale, int seed, Color leaves)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            root.transform.localScale = Vector3.one * scale;
            var bark = MaterialFactory.Toon(new Color(0.22f, 0.15f, 0.13f), 0.9f, false, null, 0.5f, 0.15f, 0f, ProceduralTextures.WoodDetail, 2f);
            var leaf = MaterialFactory.Toon(leaves, 0.8f, false, null, 0.45f, 0.35f);
            float h = 3.2f + (float)rng.NextDouble() * 1.6f;
            ProceduralMeshes.CreatePart("Trunk", PrimitiveType.Cylinder, bark, root.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3((float)rng.NextDouble() * 6f - 3f, 0f, (float)rng.NextDouble() * 6f - 3f), new Vector3(0.45f, h * 0.5f, 0.45f));
            int layers = 3;
            for (int i = 0; i < layers; i++)
            {
                float y = h * 0.75f + i * 1.0f;
                float r = 2.6f - i * 0.6f;
                var cone = ProceduralMeshes.CreatePart("Canopy" + i, ProceduralMeshes.Cone(9), leaf, root.transform, new Vector3(0f, y, 0f),
                    Quaternion.Euler(0f, i * 23f, 0f), new Vector3(r * 2f, 2.1f, r * 2f));
                cone.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            }
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.height = h;
            col.radius = 0.3f;
            return root;
        }

        /// <summary>
        /// Anime broadleaf tree: leaning trunk with two branches and a cluster of lumpy canopy blobs, darker below
        /// and lit on top (painted-foliage look). Cheap: ~7 low-poly parts, all static-batched.
        /// </summary>
        public static GameObject BroadleafTree(Transform parent, Vector3 position, float scale, int seed, Color leaves)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Broadleaf");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            root.transform.localScale = Vector3.one * scale;
            var bark = MaterialFactory.Toon(new Color(0.2f, 0.14f, 0.13f), 0.9f, false, null, 0.5f, 0.15f, 0f, ProceduralTextures.WoodDetail, 2f);
            var shade = MaterialFactory.Toon(leaves * 0.8f, 0.8f, false, null, 0.42f, 0.3f);
            var lit = MaterialFactory.Toon(Color.Lerp(leaves, new Color(0.55f, 0.75f, 0.85f), 0.25f), 0.8f, false, null, 0.45f, 0.45f);
            float h = 2.8f + (float)rng.NextDouble() * 1.4f;
            float lean = (float)rng.NextDouble() * 8f - 4f;
            ProceduralMeshes.CreatePart("Trunk", ProceduralMeshes.OpenCylinder(0.34f, 0.2f, 8, 3), bark, root.transform, Vector3.zero, new Vector3(lean, 0f, lean * 0.5f), new Vector3(1f, h, 1f));
            ProceduralMeshes.CreatePart("RootFlare", ProceduralMeshes.Cone(8), bark, root.transform, Vector3.zero, Vector3.zero, new Vector3(1.1f, 0.5f, 1.1f));
            for (int i = 0; i < 2; i++)
            {
                float a = i * 150f + (float)rng.NextDouble() * 40f;
                ProceduralMeshes.CreatePart("Branch" + i, ProceduralMeshes.OpenCylinder(0.12f, 0.06f, 6, 1), bark, root.transform, new Vector3(0f, h * 0.7f, 0f),
                    new Vector3(-50f, a, 0f), new Vector3(1f, 1.4f, 1f));
            }
            int blobs = 4 + rng.Next(3);
            for (int i = 0; i < blobs; i++)
            {
                float a = i / (float)blobs * Mathf.PI * 2f + (float)rng.NextDouble();
                float r = i == 0 ? 0f : 0.9f + (float)rng.NextDouble() * 0.6f;
                float y = h + 0.4f + (i == 0 ? 0.9f : (float)rng.NextDouble() * 0.7f);
                float size = i == 0 ? 2.6f : 1.7f + (float)rng.NextDouble() * 0.8f;
                bool top = i == 0 || y > h + 0.9f;
                var blob = ProceduralMeshes.CreatePart("Canopy" + i, ProceduralMeshes.Blob(rng.Next(4)), top ? lit : shade, root.transform,
                    new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(size, size * 0.8f, size));
                blob.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            }
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.height = h;
            col.radius = 0.28f;
            return root;
        }

        /// <summary>Fern / grass clump: a few flattened blades fanning out (undergrowth, no collider).</summary>
        public static GameObject Undergrowth(Transform parent, Vector3 position, int seed, Color color, float size = 1f)
        {
            var rng = new System.Random(seed);
            var root = new GameObject("Undergrowth");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var mat = MaterialFactory.Toon(color, 0f, false, null, 0.5f, 0.3f);
            int blades = 4 + rng.Next(3);
            var blade = ProceduralMeshes.Cone(4, 0.35f);
            for (int i = 0; i < blades; i++)
            {
                float a = i / (float)blades * 360f + (float)rng.NextDouble() * 30f;
                float h = (0.45f + (float)rng.NextDouble() * 0.45f) * size;
                var part = ProceduralMeshes.CreatePart("Blade" + i, blade, mat, root.transform, Vector3.zero,
                    new Vector3(25f + (float)rng.NextDouble() * 30f, a, 0f), new Vector3(0.22f * size, h, 0.05f * size));
                part.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return root;
        }

        /// <summary>Low ground mist: a few large soft horizontal sheets hugging the ground (fake volumetric).</summary>
        public static void GroundMist(Transform parent, Vector3 center, Vector2 area, int count, int seed, Color color)
        {
            var rng = new System.Random(seed);
            var mat = MaterialFactory.Vfx(ProceduralTextures.Smoke, VfxBlend.AlphaBlend, 1.5f);
            for (int i = 0; i < count; i++)
            {
                var pos = center + new Vector3(((float)rng.NextDouble() - 0.5f) * area.x, 0.25f + (float)rng.NextDouble() * 0.6f, ((float)rng.NextDouble() - 0.5f) * area.y);
                float size = 9f + (float)rng.NextDouble() * 8f;
                var sheet = ProceduralMeshes.CreatePart("Mist" + i, ProceduralMeshes.GroundQuad(), mat, parent, pos, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(size, 1f, size));
                var r = sheet.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ShaderIds.TintColor, color);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Static-batches the plain scenery under <paramref name="root"/> (no scripts, rigidbodies or particles on the
        /// object or its parents): hundreds of trees, rocks and plants render in a few batches.
        /// </summary>
        public static void BatchStatic(Transform root)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (IsPlainScenery(r.transform, root)) list.Add(r.gameObject);
            }
            if (list.Count > 0) StaticBatchingUtility.Combine(list.ToArray(), root.gameObject);
        }

        private static bool IsPlainScenery(Transform t, Transform root)
        {
            for (var c = t; c != null; c = c.parent)
            {
                if (c.GetComponent<MonoBehaviour>() != null || c.GetComponent<Rigidbody>() != null || c.GetComponent<ParticleSystem>() != null) return false;
                if (c == root) break;
            }
            // Mist sheets use a per-renderer tint (property block): keep them out of the batch.
            if (t.GetComponent<MeshRenderer>().HasPropertyBlock()) return false;
            var mf = t.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable;
        }

        public static GameObject Rock(Transform parent, Vector3 position, Vector3 scale, int seed, bool collider = true)
        {
            var rng = new System.Random(seed);
            var mat = MaterialFactory.Toon(new Color(0.3f, 0.31f, 0.36f), 0.8f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
            var go = ProceduralMeshes.CreatePart("Rock", PrimitiveType.Sphere, mat, parent, position,
                new Vector3((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f), scale);
            if (collider) go.AddComponent<SphereCollider>();
            return go;
        }

        public static GameObject StoneLantern(Transform parent, Vector3 position, bool lit)
        {
            var root = new GameObject("StoneLantern");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var stone = MaterialFactory.Toon(new Color(0.45f, 0.46f, 0.5f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 1f);
            var glow = MaterialFactory.Toon(new Color(1f, 0.75f, 0.4f), 0f, false, new Color(3f, 1.8f, 0.7f));
            ProceduralMeshes.CreatePart("Base", PrimitiveType.Cylinder, stone, root.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.6f, 0.15f, 0.6f));
            ProceduralMeshes.CreatePart("Post", PrimitiveType.Cylinder, stone, root.transform, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(0.22f, 0.45f, 0.22f));
            ProceduralMeshes.CreatePart("Box", PrimitiveType.Cube, stone, root.transform, new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(0.55f, 0.4f, 0.55f));
            if (lit) ProceduralMeshes.CreatePart("Fire", PrimitiveType.Cube, glow, root.transform, new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(0.58f, 0.2f, 0.3f));
            ProceduralMeshes.CreatePart("Roof", ProceduralMeshes.Cone(4), stone, root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0f, 45f, 0f), new Vector3(1.1f, 0.45f, 1.1f));
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(0.6f, 1.6f, 0.6f);
            if (lit)
            {
                var l = new GameObject("Light").AddComponent<Light>();
                l.transform.SetParent(root.transform, false);
                l.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.65f, 0.35f);
                l.range = 7f;
                l.intensity = 2.2f;
                l.shadows = LightShadows.None;
            }
            return root;
        }

        /// <summary>Original "moon gate": two pillars, double crossbeam and a round moon plaque.</summary>
        public static GameObject MoonGate(Transform parent, Vector3 position, float yaw, float scale = 1f)
        {
            var root = new GameObject("MoonGate");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            root.transform.localScale = Vector3.one * scale;
            var red = MaterialFactory.Toon(new Color(0.55f, 0.1f, 0.12f), 1.2f, false, null, 0.5f, 0.3f, 0f, ProceduralTextures.WoodDetail, 1f);
            var dark = MaterialFactory.Toon(new Color(0.08f, 0.07f, 0.09f), 1.2f);
            var plaque = MaterialFactory.Toon(new Color(0.85f, 0.85f, 0.95f), 1f, false, new Color(0.4f, 0.45f, 0.8f));
            ProceduralMeshes.CreatePart("PillarL", PrimitiveType.Cylinder, red, root.transform, new Vector3(-2.2f, 2.4f, 0f), Vector3.zero, new Vector3(0.45f, 2.4f, 0.45f));
            ProceduralMeshes.CreatePart("PillarR", PrimitiveType.Cylinder, red, root.transform, new Vector3(2.2f, 2.4f, 0f), Vector3.zero, new Vector3(0.45f, 2.4f, 0.45f));
            ProceduralMeshes.CreatePart("BeamLow", PrimitiveType.Cube, red, root.transform, new Vector3(0f, 3.9f, 0f), Vector3.zero, new Vector3(5.2f, 0.3f, 0.35f));
            ProceduralMeshes.CreatePart("BeamTop", PrimitiveType.Cube, dark, root.transform, new Vector3(0f, 4.8f, 0f), Vector3.zero, new Vector3(6.4f, 0.4f, 0.55f));
            ProceduralMeshes.CreatePart("BeamCapL", PrimitiveType.Cube, dark, root.transform, new Vector3(-3.1f, 4.95f, 0f), new Vector3(0f, 0f, 10f), new Vector3(0.8f, 0.3f, 0.55f));
            ProceduralMeshes.CreatePart("BeamCapR", PrimitiveType.Cube, dark, root.transform, new Vector3(3.1f, 4.95f, 0f), new Vector3(0f, 0f, -10f), new Vector3(0.8f, 0.3f, 0.55f));
            ProceduralMeshes.CreatePart("Moon", PrimitiveType.Cylinder, plaque, root.transform, new Vector3(0f, 4.35f, 0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.7f, 0.06f, 0.7f));
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                var c = root.AddComponent<CapsuleCollider>();
                c.center = new Vector3(x, 2.4f, 0f);
                c.height = 4.8f;
                c.radius = 0.25f;
            }
            return root;
        }

        /// <summary>Abandoned temple: raised stone platform, broken pillars, collapsed roof and stairs.</summary>
        public static GameObject TempleRuins(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("TempleRuins");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var stone = MaterialFactory.Toon(new Color(0.36f, 0.37f, 0.42f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.StoneDetail, 2f);
            var wood = MaterialFactory.Toon(new Color(0.3f, 0.18f, 0.14f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
            var roof = MaterialFactory.Toon(new Color(0.12f, 0.13f, 0.18f), 1.2f);
            var t = root.transform;

            var platform = ProceduralMeshes.CreatePart("Platform", PrimitiveType.Cube, stone, t, new Vector3(0f, 0.4f, 6f), Vector3.zero, new Vector3(12f, 0.8f, 9f));
            platform.AddComponent<BoxCollider>();
            for (int i = 0; i < 3; i++)
            {
                var step = ProceduralMeshes.CreatePart("Step" + i, PrimitiveType.Cube, stone, t, new Vector3(0f, 0.13f + i * 0.13f, 0.9f + i * 0.35f), Vector3.zero, new Vector3(5f, 0.27f + i * 0.26f, 0.35f));
                step.AddComponent<BoxCollider>();
            }
            float[] px = { -5f, -1.8f, 1.8f, 5f };
            for (int i = 0; i < 4; i++)
            {
                for (int row = 0; row < 2; row++)
                {
                    bool broken = (i + row) % 3 == 1;
                    float h = broken ? 1.6f : 3.6f;
                    var p = ProceduralMeshes.CreatePart($"Pillar{i}_{row}", PrimitiveType.Cylinder, wood, t, new Vector3(px[i], 0.8f + h * 0.5f, 3f + row * 6f),
                        broken ? new Vector3(0f, 0f, 6f) : Vector3.zero, new Vector3(0.5f, h * 0.5f, 0.5f));
                    p.AddComponent<CapsuleCollider>();
                }
            }
            var roofL = ProceduralMeshes.CreatePart("RoofL", PrimitiveType.Cube, roof, t, new Vector3(-3f, 5f, 6f), new Vector3(0f, 0f, 22f), new Vector3(7.5f, 0.35f, 10f));
            roofL.AddComponent<BoxCollider>();
            var roofBroken = ProceduralMeshes.CreatePart("RoofBroken", PrimitiveType.Cube, roof, t, new Vector3(4.5f, 2.2f, 8.5f), new Vector3(12f, 20f, -35f), new Vector3(5f, 0.35f, 5f));
            roofBroken.AddComponent<BoxCollider>();
            var altar = ProceduralMeshes.CreatePart("Altar", PrimitiveType.Cube, stone, t, new Vector3(0f, 1.2f, 9.5f), Vector3.zero, new Vector3(2.4f, 0.8f, 1.2f));
            altar.AddComponent<BoxCollider>();
            return root;
        }

        public static GameObject Bridge(Transform parent, Vector3 position, float yaw, float length)
        {
            var root = new GameObject("Bridge");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var wood = MaterialFactory.Toon(new Color(0.42f, 0.26f, 0.16f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
            var red = MaterialFactory.Toon(new Color(0.6f, 0.12f, 0.12f), 1f);
            var deck = ProceduralMeshes.CreatePart("Deck", PrimitiveType.Cube, wood, root.transform, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(3f, 0.2f, length));
            deck.AddComponent<BoxCollider>();
            for (int s = -1; s <= 1; s += 2)
            {
                ProceduralMeshes.CreatePart("Rail", PrimitiveType.Cube, red, root.transform, new Vector3(1.45f * s, 1.1f, 0f), Vector3.zero, new Vector3(0.1f, 0.1f, length));
                for (int i = 0; i <= 4; i++)
                {
                    float z = -length * 0.5f + i * length / 4f;
                    ProceduralMeshes.CreatePart("Post", PrimitiveType.Cube, red, root.transform, new Vector3(1.45f * s, 0.75f, z), Vector3.zero, new Vector3(0.14f, 0.8f, 0.14f));
                }
            }
            return root;
        }

        // ------------------------------------------------------------------ destructibles

        public static Destructible Crate(Transform parent, Vector3 position, float size = 0.9f)
        {
            var mat = MaterialFactory.Toon(new Color(0.55f, 0.38f, 0.22f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
            var go = ProceduralMeshes.CreatePart("Crate", PrimitiveType.Cube, mat, parent, position + Vector3.up * size * 0.5f, new Vector3(0f, Random.Range(0f, 360f), 0f), Vector3.one * size);
            go.layer = Layers.Destructible;
            go.AddComponent<BoxCollider>();
            var d = go.AddComponent<Destructible>();
            d.Health = 22f;
            d.DebrisColor = new Color(0.55f, 0.38f, 0.22f);
            d.DebrisSize = size * 0.25f;
            return d;
        }

        public static Destructible Vase(Transform parent, Vector3 position)
        {
            var mat = MaterialFactory.Toon(new Color(0.25f, 0.35f, 0.55f), 1f, false, null, 0.5f, 0.35f, 0.4f);
            var root = new GameObject("Vase");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.layer = Layers.Destructible;
            ProceduralMeshes.CreatePart("Body", PrimitiveType.Sphere, mat, root.transform, new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(0.6f, 0.7f, 0.6f)).layer = Layers.Destructible;
            ProceduralMeshes.CreatePart("Neck", PrimitiveType.Cylinder, mat, root.transform, new Vector3(0f, 0.85f, 0f), Vector3.zero, new Vector3(0.25f, 0.12f, 0.25f)).layer = Layers.Destructible;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.height = 0.9f;
            col.radius = 0.3f;
            var d = root.AddComponent<Destructible>();
            d.Health = 6f;
            d.DebrisColor = new Color(0.25f, 0.35f, 0.55f);
            d.DebrisSize = 0.14f;
            d.DebrisCount = 10;
            d.BreakSfx = "block";
            d.BreakVfx = "impact_slash";
            return d;
        }

        public static Destructible SmallShrine(Transform parent, Vector3 position, float yaw)
        {
            var wood = MaterialFactory.Toon(new Color(0.45f, 0.28f, 0.18f), 1f, false, null, 0.5f, 0.2f, 0f, ProceduralTextures.WoodDetail, 1f);
            var roofMat = MaterialFactory.Toon(new Color(0.15f, 0.15f, 0.2f), 1f);
            var root = new GameObject("SmallShrine");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            root.layer = Layers.Destructible;
            ProceduralMeshes.CreatePart("Box", PrimitiveType.Cube, wood, root.transform, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(0.9f, 0.8f, 0.7f)).layer = Layers.Destructible;
            ProceduralMeshes.CreatePart("Legs", PrimitiveType.Cube, wood, root.transform, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.7f, 0.2f, 0.5f)).layer = Layers.Destructible;
            ProceduralMeshes.CreatePart("Roof", ProceduralMeshes.Cone(4), roofMat, root.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0f, 45f, 0f), new Vector3(1.6f, 0.5f, 1.3f)).layer = Layers.Destructible;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(0.9f, 1.2f, 0.7f);
            var d = root.AddComponent<Destructible>();
            d.Health = 40f;
            d.BreakOnlyWithHeavy = true;
            d.DebrisCount = 14;
            d.DebrisColor = new Color(0.45f, 0.28f, 0.18f);
            return d;
        }
    }
}
