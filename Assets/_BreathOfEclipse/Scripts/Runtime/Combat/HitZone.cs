using System;
using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Data;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// A lingering damage area that ticks independently of whoever created it (moon crescents that stay in the
    /// air, tornados, echoes). Pooled.
    /// </summary>
    public sealed class HitZone : MonoBehaviour, IPoolable
    {
        private const string PoolKey = "hit_zone";
        private float _radius;
        private float _delay;
        private int _remaining;
        private float _interval;
        private float _timer;
        private HitData _template;
        private HitSpec _spec;
        private Action<HitData, HitResult, HitSpec> _onHit;
        private readonly List<IDamageable> _buffer = new List<IDamageable>();
        private readonly HitRegistry _registry = new HitRegistry();

        public static HitZone Spawn(Vector3 center, float radius, float delay, int hits, float interval, HitData template, HitSpec spec,
            Action<HitData, HitResult, HitSpec> onHit)
        {
            var pool = PoolManager.Instance;
            if (pool == null) return null;
            var go = pool.Spawn(PoolKey, () =>
            {
                var g = new GameObject("HitZone");
                g.SetActive(false);
                g.AddComponent<HitZone>();
                return g;
            }, center, Quaternion.identity);
            var zone = go.GetComponent<HitZone>();
            zone._radius = radius;
            zone._delay = delay;
            zone._remaining = hits;
            zone._interval = Mathf.Max(0.02f, interval);
            zone._timer = delay;
            zone._template = template;
            zone._spec = spec;
            zone._onHit = onHit;
            zone._registry.Begin(hits, zone._interval * 0.8f);
            go.GetComponent<PooledObject>().Lifetime = delay + hits * zone._interval + 0.5f;
            return zone;
        }

        public void OnSpawned() { }

        public void OnReleased()
        {
            _onHit = null;
            _remaining = 0;
        }

        private void Update()
        {
            if (_remaining <= 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _interval;
            _remaining--;
            HitQuery.Sphere(transform.position, _radius, _template.AttackerTeam, _buffer);
            foreach (var target in _buffer)
            {
                if (!_registry.CanHit(target, Time.time)) continue;
                _registry.Register(target, Time.time);
                var hit = _template;
                hit.HitPoint = target.CenterPoint;
                Vector3 dir = target.CenterPoint - transform.position;
                dir.y = 0f;
                hit.Direction = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                var result = target.ReceiveHit(hit);
                if (result.Outcome != HitOutcome.Ignored) _onHit?.Invoke(hit, result, _spec);
            }
        }
    }
}
