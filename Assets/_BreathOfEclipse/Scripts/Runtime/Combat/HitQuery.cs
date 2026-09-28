using System.Collections.Generic;
using BreathOfEclipse.Core;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Remembers which targets an attack instance already hit, so one swing never hits the same target twice by
    /// accident (colliders overlapping across frames). Multi-hit techniques allow repeats after an interval.
    /// </summary>
    public sealed class HitRegistry
    {
        private readonly Dictionary<IDamageable, int> _counts = new Dictionary<IDamageable, int>();
        private readonly Dictionary<IDamageable, float> _lastTime = new Dictionary<IDamageable, float>();

        public int AttackInstanceId { get; private set; }
        public int MaxHitsPerTarget { get; set; } = 1;
        public float RepeatInterval { get; set; } = 0.1f;

        private static int _nextId = 1;

        public void Begin(int maxHitsPerTarget = 1, float repeatInterval = 0.1f)
        {
            _counts.Clear();
            _lastTime.Clear();
            AttackInstanceId = _nextId++;
            MaxHitsPerTarget = Mathf.Max(1, maxHitsPerTarget);
            RepeatInterval = repeatInterval;
        }

        public bool CanHit(IDamageable target, float now)
        {
            if (!_counts.TryGetValue(target, out int count)) return true;
            if (count >= MaxHitsPerTarget) return false;
            return now - _lastTime[target] >= RepeatInterval;
        }

        public void Register(IDamageable target, float now)
        {
            _counts.TryGetValue(target, out int count);
            _counts[target] = count + 1;
            _lastTime[target] = now;
        }

        public int TotalTargets => _counts.Count;
    }

    /// <summary>Physics queries shared by sword sweeps, techniques, projectiles and enemy attacks.</summary>
    public static class HitQuery
    {
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly RaycastHit[] Casts = new RaycastHit[32];
        private static readonly HashSet<IDamageable> Seen = new HashSet<IDamageable>();

        /// <summary>Resolves the IDamageable owning a collider (colliders live on the character root).</summary>
        public static IDamageable Resolve(Collider c)
        {
            if (c == null) return null;
            return c.GetComponentInParent<IDamageable>();
        }

        private static bool Valid(IDamageable d, Team attackerTeam)
        {
            if (d == null || !d.IsAlive) return false;
            if (d.Team == Team.Neutral) return true;
            return d.Team != attackerTeam;
        }

        /// <summary>Targets inside a sphere.</summary>
        public static void Sphere(Vector3 center, float radius, Team attackerTeam, List<IDamageable> results, int mask = -1)
        {
            results.Clear();
            Seen.Clear();
            if (mask == -1) mask = Layers.HittableMask;
            HitboxDebug.Sphere(center, radius);
            int n = Physics.OverlapSphereNonAlloc(center, radius, Overlaps, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var d = Resolve(Overlaps[i]);
                if (Valid(d, attackerTeam) && Seen.Add(d)) results.Add(d);
            }
        }

        /// <summary>Targets inside a capsule.</summary>
        public static void Capsule(Vector3 a, Vector3 b, float radius, Team attackerTeam, List<IDamageable> results)
        {
            results.Clear();
            Seen.Clear();
            HitboxDebug.Capsule(a, b, radius);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, Overlaps, Layers.HittableMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var d = Resolve(Overlaps[i]);
                if (Valid(d, attackerTeam) && Seen.Add(d)) results.Add(d);
            }
        }

        /// <summary>Targets inside a horizontal cone (sphere filtered by angle).</summary>
        public static void Cone(Vector3 origin, Vector3 forward, float range, float angleDegrees, Team attackerTeam, List<IDamageable> results)
        {
            Sphere(origin, range, attackerTeam, results);
            if (angleDegrees >= 359f) return;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();
            float half = angleDegrees * 0.5f;
            for (int i = results.Count - 1; i >= 0; i--)
            {
                Vector3 to = results[i].CenterPoint - origin;
                to.y = 0f;
                // Targets overlapping the attacker always count.
                if (to.sqrMagnitude < 0.36f) continue;
                if (Vector3.Angle(forward, to) > half) results.RemoveAt(i);
            }
        }

        /// <summary>
        /// Sweeps spheres from the previous to the current blade segment. Catches fast swings that would tunnel
        /// through targets between frames.
        /// </summary>
        public static void BladeSweep(Vector3 prevBase, Vector3 prevTip, Vector3 curBase, Vector3 curTip, float radius, int samples,
            Team attackerTeam, List<IDamageable> results, List<Vector3> points)
        {
            results.Clear();
            points.Clear();
            Seen.Clear();
            samples = Mathf.Max(2, samples);
            if (HitboxDebug.Enabled)
            {
                HitboxDebug.Capsule(curBase, curTip, radius);
                HitboxDebug.Segment(prevTip, curTip, HitboxDebug.SweepColor);
                HitboxDebug.Segment(prevBase, curBase, HitboxDebug.SweepColor);
            }
            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)(samples - 1);
                Vector3 from = Vector3.Lerp(prevBase, prevTip, t);
                Vector3 to = Vector3.Lerp(curBase, curTip, t);
                Vector3 delta = to - from;
                float dist = delta.magnitude;
                int n;
                if (dist < 0.001f)
                {
                    n = Physics.OverlapSphereNonAlloc(to, radius, Overlaps, Layers.HittableMask, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < n; i++)
                    {
                        var d = Resolve(Overlaps[i]);
                        if (Valid(d, attackerTeam) && Seen.Add(d))
                        {
                            results.Add(d);
                            points.Add(Overlaps[i].ClosestPoint(to));
                        }
                    }
                    continue;
                }
                // Overlap at the start (targets already inside the blade) + cast along the motion.
                n = Physics.OverlapSphereNonAlloc(from, radius, Overlaps, Layers.HittableMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    var d = Resolve(Overlaps[i]);
                    if (Valid(d, attackerTeam) && Seen.Add(d))
                    {
                        results.Add(d);
                        points.Add(Overlaps[i].ClosestPoint(from));
                    }
                }
                n = Physics.SphereCastNonAlloc(from, radius, delta / dist, Casts, dist, Layers.HittableMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    var d = Resolve(Casts[i].collider);
                    if (Valid(d, attackerTeam) && Seen.Add(d))
                    {
                        results.Add(d);
                        points.Add(Casts[i].point == Vector3.zero ? to : Casts[i].point);
                    }
                }
            }
        }

        /// <summary>Is there static geometry between two points?</summary>
        public static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.01f) return false;
            return Physics.Raycast(from, d / dist, dist, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Ground height below a point (for decals and ground effects).</summary>
        public static bool GroundPoint(Vector3 from, out Vector3 point, out Vector3 normal, float maxDistance = 6f)
        {
            if (Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out var hit, maxDistance + 0.5f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }
            point = new Vector3(from.x, from.y, from.z);
            normal = Vector3.up;
            return false;
        }
    }
}
