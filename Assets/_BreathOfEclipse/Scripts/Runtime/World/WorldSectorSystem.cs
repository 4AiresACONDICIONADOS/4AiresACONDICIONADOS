using System;
using System.Collections;
using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// Sector streaming for the frontier: every sector always has a cheap far view (10 m terrain with skirts + tree
    /// silhouettes); sectors near the player load their detailed content over several frames (one at a time, closest
    /// first) and unload when far (hysteresis in <see cref="SectorStreamingModel"/>). The persistent root (player,
    /// managers, time, audio, UI) never belongs to a sector, so nothing is duplicated when sectors come and go.
    /// </summary>
    public sealed class WorldSectorSystem : MonoBehaviour
    {
        public sealed class Entry
        {
            public SectorDef Def;
            public SectorStreamingModel.Sector Model;
            public GameObject Far;
            public SectorInstance Instance;
            public Coroutine Loading;
            public float LoadedAt;
        }

        public static WorldSectorSystem Instance { get; private set; }

        public SectorStreamingModel Model { get; } = new SectorStreamingModel();
        public IReadOnlyList<Entry> Entries => _entries;
        /// <summary>Sector the player stands in (null outside).</summary>
        public SectorDef Current { get; private set; }
        public bool ShowBounds { get; set; }

        /// <summary>A sector finished loading its detailed content.</summary>
        public event Action<SectorDef, SectorInstance> SectorLoaded;
        /// <summary>A sector is about to be destroyed.</summary>
        public event Action<SectorDef, SectorInstance> SectorUnloading;
        /// <summary>The player crossed into another sector (previous, next).</summary>
        public event Action<SectorDef, SectorDef> SectorChanged;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<SectorStreamingModel.Sector> _toLoad = new List<SectorStreamingModel.Sector>();
        private readonly List<SectorStreamingModel.Sector> _toUnload = new List<SectorStreamingModel.Sector>();
        private Transform _sectorsRoot, _farRoot;
        private float _nextPlan;
        private bool _busy;
        private Func<Vector3> _focus;
        private Func<WorldStateDatabase> _world;
        private Func<double> _now;
        private readonly List<LineRenderer> _bounds = new List<LineRenderer>();

        public static WorldSectorSystem Create(Transform parent, Func<Vector3> focus, Func<WorldStateDatabase> world, Func<double> now)
        {
            var go = new GameObject("WorldSectorSystem");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<WorldSectorSystem>();
            s._focus = focus;
            s._world = world;
            s._now = now;
            s.Init();
            return s;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Init()
        {
            _sectorsRoot = new GameObject("Sectors").transform;
            _sectorsRoot.SetParent(transform, false);
            _farRoot = new GameObject("FarView").transform;
            _farRoot.SetParent(transform, false);
            foreach (var def in L.Sectors)
            {
                var e = new Entry { Def = def, Model = Model.Add(def.Id, def.Bounds) };
                e.Far = BuildFar(def);
                _entries.Add(e);
            }
        }

        /// <summary>Far view of a sector: 10 m terrain with skirts and one blob per tree.</summary>
        private GameObject BuildFar(SectorDef def)
        {
            var go = new GameObject("Far_" + def.Id);
            go.transform.SetParent(_farRoot, false);
            var mesh = RegionTerrain.BuildChunk(def.Bounds, 5, true, "FarTerrain_" + def.Id);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RegionTerrain.Material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var batch = new MeshBatcher { CellSize = 400f };
            foreach (var t in RegionVegetation.Trees(def)) RegionVegetation.FarTree(batch, t, def.Biome);
            batch.Build(go.transform, "FarTrees");
            return go;
        }

        public Entry Find(string id)
        {
            foreach (var e in _entries) if (e.Def.Id == id) return e;
            return null;
        }

        public bool IsLoaded(string id)
        {
            var e = Find(id);
            return e != null && e.Model.State == SectorLoadState.Loaded;
        }

        public SectorInstance Loaded(string id)
        {
            var e = Find(id);
            return e != null && e.Model.State == SectorLoadState.Loaded ? e.Instance : null;
        }

        public bool IsLoadedAt(Vector3 p)
        {
            var s = Model.At(p.x, p.z);
            return s != null && s.State == SectorLoadState.Loaded;
        }

        public int LoadedCount
        {
            get
            {
                int n = 0;
                foreach (var e in _entries) if (e.Model.State == SectorLoadState.Loaded) n++;
                return n;
            }
        }

        /// <summary>Builds the sector(s) around a point synchronously (scene start / teleports) so there is ground.</summary>
        public void LoadAround(Vector3 p, bool includeNeighbours)
        {
            Model.Plan(p.x, p.z, _toLoad, _toUnload);
            foreach (var s in new List<SectorStreamingModel.Sector>(_toLoad))
            {
                bool inside = s.Bounds.Distance(p.x, p.z) <= 0f;
                if (inside || includeNeighbours) LoadImmediate(Find(s.Id));
            }
            Current = L.SectorAt(p.x, p.z);
        }

        private void LoadImmediate(Entry e)
        {
            if (e == null || e.Model.State == SectorLoadState.Loaded) return;
            if (e.Loading != null)
            {
                StopCoroutine(e.Loading);
                e.Loading = null;
                DestroyInstance(e, true);
                _busy = false;
            }
            e.Instance = new SectorInstance();
            var it = RegionSectorBuilder.Build(e.Def, _sectorsRoot, _world?.Invoke(), _now != null ? _now() : 0, e.Instance, true);
            while (it.MoveNext()) { }
            Finish(e);
        }

        private void Finish(Entry e)
        {
            e.Model.State = SectorLoadState.Loaded;
            e.LoadedAt = Time.time;
            e.Loading = null;
            if (e.Far != null) e.Far.SetActive(false);
            SectorLoaded?.Invoke(e.Def, e.Instance);
        }

        private IEnumerator LoadRoutine(Entry e)
        {
            _busy = true;
            e.Model.State = SectorLoadState.Loading;
            e.Instance = new SectorInstance();
            yield return RegionSectorBuilder.Build(e.Def, _sectorsRoot, _world?.Invoke(), _now != null ? _now() : 0, e.Instance, false);
            _busy = false;
            if (e.Model.State != SectorLoadState.Loading) yield break;
            Finish(e);
        }

        private void Unload(Entry e)
        {
            bool wasLoading = false;
            if (e.Loading != null)
            {
                StopCoroutine(e.Loading);
                e.Loading = null;
                _busy = false;
                wasLoading = true;
            }
            if (e.Model.State == SectorLoadState.Loaded) SectorUnloading?.Invoke(e.Def, e.Instance);
            DestroyInstance(e, wasLoading);
            e.Model.State = SectorLoadState.Unloaded;
            if (e.Far != null) e.Far.SetActive(true);
        }

        private void DestroyInstance(Entry e, bool midLoad)
        {
            if (e.Instance == null) return;
            if (e.Instance.Root != null) Destroy(e.Instance.Root.gameObject);
            // A collider bake may still be reading the terrain mesh on a worker thread when a load is cancelled.
            e.Instance.ReleaseMeshes(midLoad ? 3f : 0f);
            e.Instance = null;
        }

        private void Update()
        {
            if (_focus == null) return;
            Vector3 p = _focus();
            var now = L.SectorAt(p.x, p.z);
            if (now != Current)
            {
                var prev = Current;
                Current = now;
                SectorChanged?.Invoke(prev, now);
            }
            if (Time.unscaledTime >= _nextPlan)
            {
                _nextPlan = Time.unscaledTime + 0.25f;
                Model.Plan(p.x, p.z, _toLoad, _toUnload);
                foreach (var s in _toUnload) Unload(Find(s.Id));
                if (!_busy && _toLoad.Count > 0)
                {
                    var e = Find(_toLoad[0].Id);
                    // The ground under the player can never wait.
                    if (e.Def.Bounds.Distance(p.x, p.z) <= 0f) LoadImmediate(e);
                    else e.Loading = StartCoroutine(LoadRoutine(e));
                }
            }
            UpdateBounds();
        }

        /// <summary>Re-applies world state to every loaded sector (fence repaired, wreck cleared…).</summary>
        public void RefreshStates()
        {
            var world = _world?.Invoke();
            double now = _now != null ? _now() : 0;
            foreach (var e in _entries)
                if (e.Model.State == SectorLoadState.Loaded) RegionSectorBuilder.RefreshState(e.Instance, world, now);
        }

        // ------------------------------------------------------------------ debug

        /// <summary>Debug: keep a sector loaded regardless of distance (or release it).</summary>
        public void Pin(string id, bool pinned)
        {
            var e = Find(id);
            if (e == null) return;
            e.Model.Pinned = pinned;
            e.Model.Suppressed = false;
        }

        /// <summary>Debug: force a sector out until the player walks into it.</summary>
        public void Suppress(string id)
        {
            var e = Find(id);
            if (e == null) return;
            e.Model.Pinned = false;
            e.Model.Suppressed = true;
            _nextPlan = 0f;
        }

        private void UpdateBounds()
        {
            if (!ShowBounds)
            {
                if (_bounds.Count > 0)
                {
                    foreach (var lr in _bounds) if (lr != null) Destroy(lr.gameObject);
                    _bounds.Clear();
                }
                return;
            }
            if (_bounds.Count == 0)
            {
                var mat = MaterialFactory.Vfx(ProceduralTextures.Band, VfxBlend.Additive, 0f);
                foreach (var e in _entries)
                {
                    var go = new GameObject("Bounds_" + e.Def.Id);
                    go.transform.SetParent(transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = mat;
                    lr.widthMultiplier = 0.6f;
                    lr.loop = true;
                    var b = e.Def.Bounds;
                    var pts = new List<Vector3>();
                    void Edge(float x0, float z0, float x1, float z1)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            float t = i / 16f;
                            float x = Mathf.Lerp(x0, x1, t) , z = Mathf.Lerp(z0, z1, t);
                            pts.Add(new Vector3(x, RegionTerrain.SampleHeight(x, z) + 1.5f, z));
                        }
                    }
                    Edge(b.MinX + 0.5f, b.MinZ + 0.5f, b.MaxX - 0.5f, b.MinZ + 0.5f);
                    Edge(b.MaxX - 0.5f, b.MinZ + 0.5f, b.MaxX - 0.5f, b.MaxZ - 0.5f);
                    Edge(b.MaxX - 0.5f, b.MaxZ - 0.5f, b.MinX + 0.5f, b.MaxZ - 0.5f);
                    Edge(b.MinX + 0.5f, b.MaxZ - 0.5f, b.MinX + 0.5f, b.MinZ + 0.5f);
                    lr.positionCount = pts.Count;
                    lr.SetPositions(pts.ToArray());
                    _bounds.Add(lr);
                }
            }
            for (int i = 0; i < _entries.Count && i < _bounds.Count; i++)
            {
                var st = _entries[i].Model.State;
                Color c = st == SectorLoadState.Loaded ? new Color(0.2f, 1.6f, 0.4f) : st == SectorLoadState.Loading ? new Color(1.6f, 1.4f, 0.2f) : new Color(1.4f, 0.2f, 0.2f);
                _bounds[i].startColor = _bounds[i].endColor = c;
            }
        }
    }
}
