using System;
using System.Collections.Generic;
using L = BreathOfEclipse.World.FrontierRegionLayout;

namespace BreathOfEclipse.World
{
    /// <summary>
    /// A walkable route for an NPC: its own position → the nav graph (roads, lanes, trails validated to never cross
    /// water or cliffs) → its standing slot at the destination. Sampled by distance, so the same route drives a
    /// visible villager (every frame) and an off-screen one (coarse ticks / time skips).
    /// </summary>
    public sealed class NpcRoute
    {
        private readonly List<float> _x = new List<float>(), _z = new List<float>(), _cum = new List<float>();

        public float Length { get; private set; }
        public float Travelled { get; set; }
        public bool Done => Travelled >= Length - 0.01f;
        public int PointCount => _x.Count;
        /// <summary>Nav node index the route ends on (the destination place's node).</summary>
        public int EndNode { get; private set; } = -1;
        /// <summary>Nodes passed, in order (for re-planning from the last one).</summary>
        public readonly List<int> Nodes = new List<int>();
        private readonly List<int> _nodeAtPoint = new List<int>();

        public void AddPoint(float x, float z, int node = -1)
        {
            if (_x.Count > 0)
            {
                float dx = x - _x[_x.Count - 1], dz = z - _z[_z.Count - 1];
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 0.05f) return;
                Length += d;
            }
            _x.Add(x);
            _z.Add(z);
            _cum.Add(Length);
            _nodeAtPoint.Add(node);
        }

        /// <summary>Position and heading (degrees, 0 = +Z) at a distance along the route.</summary>
        public void Sample(float distance, out float x, out float z, out float heading)
        {
            heading = 0f;
            if (_x.Count == 0)
            {
                x = z = 0f;
                return;
            }
            if (_x.Count == 1 || distance <= 0f)
            {
                x = _x[0];
                z = _z[0];
                if (_x.Count > 1) heading = Heading(0);
                return;
            }
            if (distance >= Length)
            {
                int last = _x.Count - 1;
                x = _x[last];
                z = _z[last];
                heading = Heading(last - 1);
                return;
            }
            int lo = 0, hi = _cum.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_cum[mid] <= distance) lo = mid;
                else hi = mid;
            }
            float seg = _cum[hi] - _cum[lo];
            float t = seg > 1e-5f ? (distance - _cum[lo]) / seg : 0f;
            x = _x[lo] + (_x[hi] - _x[lo]) * t;
            z = _z[lo] + (_z[hi] - _z[lo]) * t;
            heading = Heading(lo);
        }

        private float Heading(int i)
        {
            float dx = _x[i + 1] - _x[i], dz = _z[i + 1] - _z[i];
            return (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
        }

        /// <summary>Last nav node reached at <paramref name="distance"/> (-1 before the first).</summary>
        public int LastNodeAt(float distance)
        {
            int node = -1;
            for (int i = 0; i < _cum.Count && _cum[i] <= distance + 0.01f; i++)
                if (_nodeAtPoint[i] >= 0) node = _nodeAtPoint[i];
            return node;
        }

        public void PointAt(int i, out float x, out float z)
        {
            x = _x[i];
            z = _z[i];
        }

        /// <summary>
        /// Plans from (<paramref name="fromX"/>, <paramref name="fromZ"/>) — standing near nav node
        /// <paramref name="fromNode"/> — to the slot (<paramref name="toX"/>, <paramref name="toZ"/>) of node
        /// <paramref name="toNode"/>. Null when the graph has no path.
        /// </summary>
        public static NpcRoute Plan(NavGraph g, int fromNode, float fromX, float fromZ, int toNode, float toX, float toZ)
        {
            var route = new NpcRoute();
            route.AddPoint(fromX, fromZ);
            if (fromNode == toNode || fromNode < 0 || toNode < 0)
            {
                if (fromNode >= 0 && fromNode == toNode)
                {
                    // Same place: walk via its node only if the slot is on the other side of it.
                    var n = g.Get(fromNode);
                    float direct = Dist(fromX, fromZ, toX, toZ);
                    if (direct > 6f) route.AddPoint(n.X, n.Z, n.Index);
                }
                route.AddPoint(toX, toZ, toNode);
                route.EndNode = toNode;
                return route;
            }
            var path = g.FindPath(fromNode, toNode);
            if (path == null || path.Count == 0) return null;
            for (int i = 0; i < path.Count; i++)
            {
                var n = g.Get(path[i]);
                route.AddPoint(n.X, n.Z, n.Index);
                route.Nodes.Add(n.Index);
            }
            route.AddPoint(toX, toZ, toNode);
            route.EndNode = toNode;
            return route;
        }

        private static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }

    /// <summary>Where NPCs stand and which way they face at a place (deterministic per NPC, clear of buildings and water).</summary>
    public static class NpcPlaces
    {
        /// <summary>Inside a building / solid prop footprint (+ margin) — NPCs never stand or walk there.</summary>
        public static bool Blocked(float x, float z, float margin = 0.6f)
        {
            foreach (var p in L.Places)
            {
                if (!Solid(p)) continue;
                double a = -p.Yaw * Math.PI / 180.0;
                float dx = x - p.X, dz = z - p.Z;
                // into the place's local frame (Width along local x, Depth along local z)
                float lx = (float)(dx * Math.Cos(a) + dz * Math.Sin(a));
                float lz = (float)(-dx * Math.Sin(a) + dz * Math.Cos(a));
                if (Math.Abs(lx) < p.Width * 0.5f + margin && Math.Abs(lz) < p.Depth * 0.5f + margin) return true;
            }
            return false;
        }

        public static bool Solid(PlaceDef p)
        {
            switch (p.Kind)
            {
                case PlaceKind.House:
                case PlaceKind.Inn:
                case PlaceKind.Shop:
                case PlaceKind.Smithy:
                case PlaceKind.HunterPost:
                case PlaceKind.Watchtower:
                case PlaceKind.Shrine:
                case PlaceKind.Well:
                case PlaceKind.Farmhouse:
                case PlaceKind.Barn:
                case PlaceKind.Cabin:
                case PlaceKind.RiverShrine:
                    return true;
                case PlaceKind.Spot:
                    return p.Width > 0f;
                default:
                    return false;
            }
        }

        public static bool Standable(float x, float z)
        {
            if (Blocked(x, z)) return false;
            if (L.IsWater(x, z) && !L.InPaddy(x, z)) return false;
            return Math.Abs(x) < L.HalfSize - 8f && Math.Abs(z) < L.HalfSize - 8f;
        }

        /// <summary>
        /// The slot an NPC uses at <paramref name="place"/> for <paramref name="activity"/>: position and facing
        /// (degrees). Different NPCs (seeds) get different slots so nobody stacks.
        /// </summary>
        public static void Slot(PlaceDef place, NpcActivity activity, int seed, out float x, out float z, out float yaw)
        {
            var rng = new Random(seed * 92821 + L.StableHash(place.Id));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            double py = place.Yaw * Math.PI / 180.0;
            float fx = (float)Math.Sin(py), fz = (float)Math.Cos(py);   // place front
            float rx = fz, rz = -fx;                                   // place right

            switch (place.Kind)
            {
                case PlaceKind.Field:
                case PlaceKind.Paddy:
                    for (int i = 0; i < 10; i++)
                    {
                        x = place.X + R(-0.5f, 0.5f) * Math.Max(0f, place.Width - 3f);
                        z = place.Z + R(-0.5f, 0.5f) * Math.Max(0f, place.Depth - 3f);
                        yaw = R(0f, 360f);
                        if (!Blocked(x, z)) return;
                    }
                    break;
                case PlaceKind.Dock:
                    // along the boards, facing the water
                    float along = R(1.5f, place.Depth * 0.5f + 1.5f);
                    x = place.X + fx * along + rx * R(-0.4f, 0.4f);
                    z = place.Z + fz * along + rz * R(-0.4f, 0.4f);
                    yaw = place.Yaw;
                    return;
                case PlaceKind.Camp:
                    {
                        // around the campfire
                        float a = R(0f, (float)Math.PI * 2f);
                        float r = R(2.2f, 3f);
                        x = place.X + (float)Math.Sin(a) * r;
                        z = place.Z + (float)Math.Cos(a) * r;
                        yaw = (float)(Math.Atan2(place.X - x, place.Z - z) * 180.0 / Math.PI);
                        if (Standable(x, z)) return;
                        break;
                    }
            }

            bool building = Solid(place) && place.Kind != PlaceKind.Spot;
            bool footprintSpot = place.Kind == PlaceKind.Spot && place.Width > 0f;
            if (building || footprintSpot)
            {
                // In front of the door / counter / forge.
                L.FrontPoint(place, 1.2f, out float bx, out float bz);
                for (int i = 0; i < 10; i++)
                {
                    float side = R(-1f, 1f) * Math.Min(2.2f, place.Width * 0.35f + 0.5f);
                    float fwd = R(0f, 1.6f) + i * 0.3f;
                    x = bx + rx * side + fx * fwd;
                    z = bz + rz * side + fz * fwd;
                    bool facingIn = activity == NpcActivity.Pray || activity == NpcActivity.Smith || activity == NpcActivity.Work;
                    yaw = facingIn ? place.Yaw + 180f : activity == NpcActivity.Trade ? place.Yaw : place.Yaw + R(-40f, 40f);
                    if (Standable(x, z)) return;
                }
                x = bx;
                z = bz;
                yaw = place.Yaw;
                return;
            }

            // Open spot: a ring around it, facing its centre (people talking face each other).
            for (int i = 0; i < 12; i++)
            {
                float a = R(0f, (float)Math.PI * 2f);
                float r = R(1.4f, 3.4f) + i * 0.15f;
                x = place.X + (float)Math.Sin(a) * r;
                z = place.Z + (float)Math.Cos(a) * r;
                bool lookOut = activity == NpcActivity.Guard || activity == NpcActivity.Fish;
                yaw = lookOut ? place.Yaw + R(-25f, 25f) : (float)(Math.Atan2(place.X - x, place.Z - z) * 180.0 / Math.PI);
                if (Standable(x, z)) return;
            }
            x = place.X;
            z = place.Z;
            yaw = place.Yaw;
        }
    }
}
