using System;
using System.Collections.Generic;

namespace BreathOfEclipse.World
{
    /// <summary>Kind of ground a link crosses (NPCs prefer roads; demons prefer cover).</summary>
    public enum PathKind
    {
        Road = 0,
        Trail = 1,
        Forest = 2,
        Danger = 3,
        Bridge = 4,
        Village = 5
    }

    /// <summary>
    /// Navigation graph of the region: named nodes on roads, trails, village spots and forest cover, linked by
    /// walkable edges. A* with per-kind cost (so villagers keep to the road and only demons cut through the woods).
    /// NPC routines, events and offscreen simulation all travel on it; local steering handles the last metres.
    /// </summary>
    public sealed class NavGraph
    {
        public sealed class Node
        {
            public int Index;
            public string Id;
            public float X, Y, Z;
            public string Sector;
            public readonly List<Edge> Edges = new List<Edge>();
        }

        public struct Edge
        {
            public int To;
            public float Length;
            public PathKind Kind;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, int> _byId = new Dictionary<string, int>();

        public IReadOnlyList<Node> Nodes => _nodes;
        public int Count => _nodes.Count;

        public Node AddNode(string id, float x, float y, float z, string sector = null)
        {
            if (id != null && _byId.TryGetValue(id, out int existing)) return _nodes[existing];
            var n = new Node { Index = _nodes.Count, Id = id ?? $"n{_nodes.Count}", X = x, Y = y, Z = z, Sector = sector };
            _nodes.Add(n);
            _byId[n.Id] = n.Index;
            return n;
        }

        public Node Get(string id) => id != null && _byId.TryGetValue(id, out int i) ? _nodes[i] : null;
        public Node Get(int index) => index >= 0 && index < _nodes.Count ? _nodes[index] : null;

        public void Link(string a, string b, PathKind kind = PathKind.Road)
        {
            var na = Get(a);
            var nb = Get(b);
            if (na == null || nb == null || na == nb) return;
            Link(na.Index, nb.Index, kind);
        }

        public void Link(int a, int b, PathKind kind = PathKind.Road)
        {
            var na = _nodes[a];
            var nb = _nodes[b];
            foreach (var e in na.Edges) if (e.To == b) return;
            float len = Distance(na, nb);
            na.Edges.Add(new Edge { To = b, Length = len, Kind = kind });
            nb.Edges.Add(new Edge { To = a, Length = len, Kind = kind });
        }

        /// <summary>Links consecutive ids as a path (road / trail polyline).</summary>
        public void Chain(PathKind kind, params string[] ids)
        {
            for (int i = 1; i < ids.Length; i++) Link(ids[i - 1], ids[i], kind);
        }

        public static float Distance(Node a, Node b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Closest node to a point on the ground plane (optionally only nodes that have links).</summary>
        public Node Nearest(float x, float z, bool linkedOnly = true)
        {
            Node best = null;
            float bestD = float.MaxValue;
            foreach (var n in _nodes)
            {
                if (linkedOnly && n.Edges.Count == 0) continue;
                float dx = n.X - x, dz = n.Z - z;
                float d = dx * dx + dz * dz;
                if (d < bestD)
                {
                    bestD = d;
                    best = n;
                }
            }
            return best;
        }

        /// <summary>Cost multiplier per path kind for a traveller profile.</summary>
        public static float Cost(PathKind kind, bool demon)
        {
            if (demon)
            {
                switch (kind)
                {
                    case PathKind.Forest: return 0.8f;
                    case PathKind.Danger: return 0.7f;
                    case PathKind.Village: return 3f;
                    case PathKind.Road: return 1.3f;
                    default: return 1f;
                }
            }
            switch (kind)
            {
                case PathKind.Road:
                case PathKind.Village:
                case PathKind.Bridge: return 1f;
                case PathKind.Trail: return 1.4f;
                case PathKind.Forest: return 2.5f;
                case PathKind.Danger: return 6f;
                default: return 1f;
            }
        }

        /// <summary>A* from node <paramref name="from"/> to <paramref name="to"/>. Returns node indices (empty if unreachable).</summary>
        public List<int> FindPath(int from, int to, bool demon = false, List<int> result = null)
        {
            result = result ?? new List<int>();
            result.Clear();
            if (from < 0 || to < 0 || from >= _nodes.Count || to >= _nodes.Count) return result;
            if (from == to)
            {
                result.Add(from);
                return result;
            }
            int n = _nodes.Count;
            var g = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                g[i] = float.MaxValue;
                came[i] = -1;
            }
            var open = new List<int> { from };
            g[from] = 0f;
            var target = _nodes[to];
            while (open.Count > 0)
            {
                int bestI = 0;
                float bestF = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int c = open[i];
                    float f = g[c] + Distance(_nodes[c], target);
                    if (f < bestF)
                    {
                        bestF = f;
                        bestI = i;
                    }
                }
                int cur = open[bestI];
                open.RemoveAt(bestI);
                if (cur == to) break;
                if (closed[cur]) continue;
                closed[cur] = true;
                foreach (var e in _nodes[cur].Edges)
                {
                    if (closed[e.To]) continue;
                    float cost = g[cur] + e.Length * Cost(e.Kind, demon);
                    if (cost >= g[e.To]) continue;
                    g[e.To] = cost;
                    came[e.To] = cur;
                    if (!open.Contains(e.To)) open.Add(e.To);
                }
            }
            if (came[to] < 0) return result;
            for (int c = to; c >= 0; c = came[c])
            {
                result.Add(c);
                if (c == from) break;
            }
            result.Reverse();
            return result;
        }

        public List<int> FindPath(string from, string to, bool demon = false)
        {
            var a = Get(from);
            var b = Get(to);
            return a == null || b == null ? new List<int>() : FindPath(a.Index, b.Index, demon);
        }

        /// <summary>Length of a path in metres.</summary>
        public float PathLength(List<int> path)
        {
            float len = 0f;
            for (int i = 1; i < path.Count; i++) len += Distance(_nodes[path[i - 1]], _nodes[path[i]]);
            return len;
        }

        /// <summary>Position along a path after travelling <paramref name="distance"/> metres (offscreen simulation).</summary>
        public void PointAlong(List<int> path, float distance, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            if (path == null || path.Count == 0) return;
            var a = _nodes[path[0]];
            x = a.X;
            y = a.Y;
            z = a.Z;
            for (int i = 1; i < path.Count; i++)
            {
                var p = _nodes[path[i - 1]];
                var q = _nodes[path[i]];
                float seg = Distance(p, q);
                if (distance <= seg && seg > 1e-4f)
                {
                    float t = distance / seg;
                    x = p.X + (q.X - p.X) * t;
                    y = p.Y + (q.Y - p.Y) * t;
                    z = p.Z + (q.Z - p.Z) * t;
                    return;
                }
                distance -= seg;
                x = q.X;
                y = q.Y;
                z = q.Z;
            }
        }

        /// <summary>Nodes not reachable from <paramref name="start"/> (data validation).</summary>
        public List<Node> Unreachable(string start)
        {
            var result = new List<Node>();
            var s = Get(start);
            if (s == null) return result;
            var seen = new bool[_nodes.Count];
            var stack = new Stack<int>();
            stack.Push(s.Index);
            seen[s.Index] = true;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                foreach (var e in _nodes[c].Edges)
                {
                    if (seen[e.To]) continue;
                    seen[e.To] = true;
                    stack.Push(e.To);
                }
            }
            foreach (var n in _nodes) if (!seen[n.Index]) result.Add(n);
            return result;
        }
    }
}
