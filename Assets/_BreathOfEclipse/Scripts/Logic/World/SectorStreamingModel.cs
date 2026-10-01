using System;
using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    /// <summary>Axis-aligned rectangle on the ground plane (x / z, metres).</summary>
    [Serializable]
    public struct WorldRect
    {
        public float MinX, MinZ, MaxX, MaxZ;

        public WorldRect(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MaxX = Math.Max(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;

        public bool Contains(float x, float z) => x >= MinX && x < MaxX && z >= MinZ && z < MaxZ;

        /// <summary>Distance from a point to the rectangle (0 inside).</summary>
        public float Distance(float x, float z)
        {
            float dx = x < MinX ? MinX - x : x > MaxX ? x - MaxX : 0f;
            float dz = z < MinZ ? MinZ - z : z > MaxZ ? z - MaxZ : 0f;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Overlaps(WorldRect o) => MinX < o.MaxX && o.MinX < MaxX && MinZ < o.MaxZ && o.MinZ < MaxZ;
    }

    public enum SectorLoadState
    {
        Unloaded = 0,
        Loading = 1,
        Loaded = 2,
        Unloading = 3
    }

    /// <summary>
    /// Decides which sectors should be loaded from the player's position: load inside <see cref="LoadDistance"/> of a
    /// sector, unload only beyond <see cref="UnloadDistance"/> (hysteresis: walking along a border never thrashes).
    /// The sector the player stands in is always kept. Engine independent.
    /// </summary>
    public sealed class SectorStreamingModel
    {
        public sealed class Sector
        {
            public string Id;
            public WorldRect Bounds;
            public SectorLoadState State;
            /// <summary>Pinned sectors stay loaded (debug "Load Sector", scripted scenes).</summary>
            public bool Pinned;
            /// <summary>Forced unloaded by debug until the player walks in.</summary>
            public bool Suppressed;
        }

        public float LoadDistance { get; set; } = 70f;
        public float UnloadDistance { get; set; } = 110f;

        private readonly List<Sector> _sectors = new List<Sector>();
        public IReadOnlyList<Sector> Sectors => _sectors;

        public Sector Add(string id, WorldRect bounds)
        {
            var s = new Sector { Id = id, Bounds = bounds };
            _sectors.Add(s);
            return s;
        }

        public Sector Find(string id)
        {
            foreach (var s in _sectors) if (s.Id == id) return s;
            return null;
        }

        /// <summary>The sector containing a point (null outside the region).</summary>
        public Sector At(float x, float z)
        {
            foreach (var s in _sectors) if (s.Bounds.Contains(x, z)) return s;
            return null;
        }

        /// <summary>Whether a sector should be loaded for a player at (x, z), given its current state.</summary>
        public bool ShouldBeLoaded(Sector s, float x, float z)
        {
            float d = s.Bounds.Distance(x, z);
            if (d <= 0f) return true;
            if (s.Pinned) return true;
            if (s.Suppressed) return false;
            bool loaded = s.State == SectorLoadState.Loaded || s.State == SectorLoadState.Loading;
            return loaded ? d <= UnloadDistance : d <= LoadDistance;
        }

        /// <summary>Sectors to start loading / unloading now (in order: closest loads first).</summary>
        public void Plan(float x, float z, List<Sector> toLoad, List<Sector> toUnload)
        {
            toLoad.Clear();
            toUnload.Clear();
            foreach (var s in _sectors)
            {
                if (s.Suppressed && s.Bounds.Distance(x, z) <= 0f) s.Suppressed = false;
                bool want = ShouldBeLoaded(s, x, z);
                if (want && (s.State == SectorLoadState.Unloaded || s.State == SectorLoadState.Unloading)) toLoad.Add(s);
                else if (!want && (s.State == SectorLoadState.Loaded || s.State == SectorLoadState.Loading)) toUnload.Add(s);
            }
            toLoad.Sort((a, b) => a.Bounds.Distance(x, z).CompareTo(b.Bounds.Distance(x, z)));
        }
    }
}
