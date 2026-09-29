using System;
using System.Collections.Generic;
using BreathOfEclipse.Combat;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>Context passed to a VFX recipe while it builds an effect instance.</summary>
    public sealed class VfxBuild
    {
        public Transform Root;
        public Element Element;
        public ElementPalette Pal;
        /// <summary>Seconds before the instance returns to the pool.</summary>
        public float Lifetime = 2f;

        public Material Additive(Texture tex) => MaterialFactory.Vfx(tex, VfxBlend.Additive);
        public Material Alpha(Texture tex) => MaterialFactory.Vfx(tex, VfxBlend.AlphaBlend);

        public ParticleBuilder Particles(string name, Material material, Vector3 localPos = default, Vector3 localEuler = default)
            => ParticleBuilder.Create(name, Root, material, localPos, localEuler);

        public RibbonTube Tube(string name, Material material, Func<float, Vector3> path, float radius, float grow, float hold, float fade)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = Layers.VFX;
            go.transform.SetParent(Root, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var tube = go.AddComponent<RibbonTube>();
            tube.Path = path;
            tube.Radius = radius;
            tube.GrowTime = grow;
            tube.HoldTime = hold;
            tube.FadeTime = fade;
            return tube;
        }

        /// <summary>Anime water sheet along a path (TidalWaterAnime): arcs, crescents, rings, spirals, curls, cascades.</summary>
        public WaterRibbonRenderer WaterRibbon(string name, Material material, Func<float, Vector3> path, float width, float grow, float hold, float fade)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = Layers.VFX;
            go.transform.SetParent(Root, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var r = go.AddComponent<WaterRibbonRenderer>();
            r.Path = path;
            r.Width = width;
            r.GrowTime = grow;
            r.HoldTime = hold;
            r.FadeTime = fade;
            // Low quality: fewer segments, same silhouette.
            if (!VFXQuality.Secondary) r.Segments = 24;
            return r;
        }

        /// <summary>Water serpent / dragon (body, foam crest, stylised head). Detail follows <see cref="VFXQuality"/>.</summary>
        public WaterSerpentRenderer WaterSerpent(string name, Func<float, Vector3> path, float radius, float grow, float hold, float fade, float headSize = 1.35f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = Layers.VFX;
            go.transform.SetParent(Root, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, true);
            var s = go.AddComponent<WaterSerpentRenderer>();
            s.Path = path;
            s.Radius = radius;
            s.GrowTime = grow;
            s.HoldTime = hold;
            s.FadeTime = fade;
            bool full = VFXQuality.Full;
            if (!VFXQuality.Secondary)
            {
                s.Segments = 32;
                s.RadialSegments = 8;
            }
            s.Setup(full || VFXQuality.Secondary ? MaterialFactory.TidalWater(MaterialFactory.WaterLook.Foam, false) : null,
                MaterialFactory.TidalWater(MaterialFactory.WaterLook.Body, true),
                MaterialFactory.TidalWater(MaterialFactory.WaterLook.Edge, true, VfxBlend.Additive), headSize, full);
            return s;
        }

        public ExpandingMesh Shape(string name, Mesh mesh, Material material, Vector3 localPos, Vector3 localEuler, Vector3 startScale, Vector3 endScale, float duration, Color tint)
        {
            var go = ProceduralMeshes.CreatePart(name, mesh, material, Root, localPos, Quaternion.Euler(localEuler), startScale);
            go.layer = Layers.VFX;
            var e = go.AddComponent<ExpandingMesh>();
            e.StartScale = startScale;
            e.EndScale = endScale;
            e.Duration = duration;
            e.Tint = tint;
            return e;
        }

        public LightningBolt Bolt(string name, Vector3 from, Vector3 to, float width, float duration, Color tint, float delay = 0f, int branches = 2)
        {
            var go = new GameObject(name);
            go.layer = Layers.VFX;
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = MaterialFactory.Vfx(ProceduralTextures.Streak, VfxBlend.Additive, 0f);
            var bolt = go.AddComponent<LightningBolt>();
            bolt.Branches = branches;
            bolt.From = from;
            bolt.To = to;
            bolt.Width = width;
            bolt.Duration = duration;
            bolt.Tint = tint;
            bolt.Delay = delay;
            bolt.SetMaterial(lr.sharedMaterial);
            return bolt;
        }

        public FlashLight Light(string name, Vector3 localPos, Color color, float range, float peak, float duration, float delay = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            float m = Mathf.Max(1f, color.maxColorComponent);
            l.color = new Color(color.r / m, color.g / m, color.b / m);
            l.range = range;
            l.intensity = 0f;
            var f = go.AddComponent<FlashLight>();
            f.Peak = peak;
            f.Duration = duration;
            f.Delay = delay;
            return f;
        }

        public GroundDecal Decal(string name, Texture tex, Color tint, float size, float duration, Vector3 localPos = default)
        {
            var go = ProceduralMeshes.CreatePart(name, ProceduralMeshes.GroundQuad(), MaterialFactory.Decal(tex, Color.white), Root,
                localPos + Vector3.up * 0.03f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), new Vector3(size, 1f, size));
            go.layer = Layers.VFX;
            var d = go.AddComponent<GroundDecal>();
            d.Duration = duration;
            d.Tint = tint;
            return d;
        }
    }

    /// <summary>
    /// Registry of procedural effect recipes + pooled spawning. An artist can override any id with a prefab
    /// through a <see cref="VFXData"/> in the GameDatabase.
    /// </summary>
    public static class VFXLibrary
    {
        private struct Recipe
        {
            public Action<VfxBuild> Build;
        }

        private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>(StringComparer.Ordinal);
        private static bool _registered;

        public static GameDatabase Database;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Recipes.Clear();
            _registered = false;
            Database = null;
        }

        public static void Register(string id, Action<VfxBuild> build) => Recipes[id] = new Recipe { Build = build };

        public static bool Has(string id)
        {
            EnsureRegistered();
            return !string.IsNullOrEmpty(id) && Recipes.ContainsKey(id);
        }

        public static IEnumerable<string> Ids
        {
            get
            {
                EnsureRegistered();
                return Recipes.Keys;
            }
        }

        private static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            CommonVFX.Register();
            WaterVFX.Register();
            ThunderVFX.Register();
            FireVFX.Register();
            WindVFX.Register();
            MoonVFX.Register();
        }

        /// <summary>Spawns a pooled effect. Returns null for unknown ids (logged once).</summary>
        public static VFXInstance Spawn(string id, Vector3 position, Quaternion rotation, float scale = 1f, Element element = Element.None,
            Transform follow = null, float lifetime = 0f)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureRegistered();
            var pool = PoolManager.Instance;
            if (pool == null) return null;

            var overrideData = Database != null ? Database.FindVfxOverride(id) : null;
            string key;
            Func<GameObject> factory;
            if (overrideData != null)
            {
                key = "vfxp:" + id;
                factory = () => BuildFromPrefab(overrideData);
            }
            else
            {
                if (!Recipes.TryGetValue(id, out var recipe))
                {
                    WarnOnce(id);
                    return null;
                }
                key = "vfx:" + id + ":" + (int)element;
                factory = () => BuildFromRecipe(id, recipe, element);
            }

            var go = pool.Spawn(key, factory, position, rotation);
            if (go == null) return null;
            DevTelemetry.ReportVfx(id, position);
            go.transform.localScale = Vector3.one * scale * (overrideData != null ? overrideData.scale : 1f);
            var inst = go.GetComponent<VFXInstance>();
            if (lifetime > 0f) inst.SetLifetime(lifetime);
            var followComp = go.GetComponent<FollowTarget>();
            if (follow != null)
            {
                if (followComp == null) followComp = go.AddComponent<FollowTarget>();
                followComp.Target = follow;
                followComp.Offset = follow.InverseTransformPoint(position);
                followComp.FollowRotation = true;
                followComp.RotationOffset = Quaternion.Inverse(follow.rotation) * rotation;
                followComp.enabled = true;
            }
            else if (followComp != null)
            {
                followComp.Target = null;
                followComp.enabled = false;
            }
            return inst;
        }

        /// <summary>
        /// Spawns an effect authored along local +Z (0..1) stretched between two points (flash-step lines, chain lightning).
        /// </summary>
        public static VFXInstance SpawnBetween(string id, Vector3 from, Vector3 to, Element element = Element.None, float thickness = 1f, float lifetime = 0f)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.01f) return null;
            var inst = Spawn(id, from, Quaternion.LookRotation(d / length, Vector3.up), 1f, element, null, lifetime);
            if (inst != null) inst.transform.localScale = new Vector3(thickness, thickness, length);
            return inst;
        }

        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static void WarnOnce(string id)
        {
            if (Warned.Add(id)) Debug.LogWarning($"[VFXLibrary] Unknown VFX id '{id}'.");
        }

        private static GameObject BuildFromRecipe(string id, Recipe recipe, Element element)
        {
            var go = new GameObject("VFX_" + id);
            go.SetActive(false);
            go.layer = Layers.VFX;
            var build = new VfxBuild { Root = go.transform, Element = element, Pal = ElementPalette.Get(element) };
            recipe.Build(build);
            var inst = go.AddComponent<VFXInstance>();
            inst.Id = id;
            inst.DefaultLifetime = build.Lifetime;
            go.AddComponent<PooledObject>().Lifetime = build.Lifetime;
            inst.Cache();
            return go;
        }

        private static GameObject BuildFromPrefab(VFXData data)
        {
            var go = UnityEngine.Object.Instantiate(data.prefab);
            go.SetActive(false);
            var inst = go.GetComponent<VFXInstance>();
            if (inst == null) inst = go.AddComponent<VFXInstance>();
            inst.Id = data.vfxId;
            inst.DefaultLifetime = data.lifetime;
            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null) pooled = go.AddComponent<PooledObject>();
            pooled.Lifetime = data.lifetime;
            inst.Cache();
            return go;
        }
    }
}
