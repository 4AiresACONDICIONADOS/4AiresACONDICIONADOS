using System;
using BreathOfEclipse.Core;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Pooled projectile (air crescents, fire wheels, lunar waves, demon fire orbs). Sweeps a sphere every frame,
    /// optionally pierces and homes, and hands every hit to the owner through a callback.
    /// </summary>
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        private const string PoolKey = "projectile";

        private Vector3 _direction;
        private float _speed;
        private float _lifetime;
        private float _age;
        private float _radius;
        private bool _pierce;
        private Transform _homing;
        private Team _team;
        private HitData _template;
        private Action<HitData, HitResult> _onHit;
        private string _impactVfx;
        private Element _element;
        private VFXInstance _visual;
        private readonly HitRegistry _registry = new HitRegistry();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static Projectile Spawn(string vfxId, Vector3 position, Vector3 direction, float speed, float lifetime, float radius, bool pierce,
            Transform homing, Team team, HitData template, Action<HitData, HitResult> onHit, string impactVfx, Element element)
        {
            var pool = PoolManager.Instance;
            if (pool == null) return null;
            var go = pool.Spawn(PoolKey, () =>
            {
                var g = new GameObject("Projectile");
                g.SetActive(false);
                g.AddComponent<Projectile>();
                return g;
            }, position, Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.forward));
            var p = go.GetComponent<Projectile>();
            p._direction = direction.normalized;
            p._speed = speed;
            p._lifetime = lifetime;
            p._radius = radius;
            p._pierce = pierce;
            p._homing = homing;
            p._team = team;
            p._template = template;
            p._onHit = onHit;
            p._impactVfx = impactVfx;
            p._element = element;
            p._age = 0f;
            p._registry.Begin(1, 1f);
            if (!string.IsNullOrEmpty(vfxId))
                p._visual = VFXLibrary.Spawn(vfxId, position, go.transform.rotation, 1f, element, go.transform, lifetime + 0.4f);
            return p;
        }

        public void OnSpawned() { }

        public void OnReleased()
        {
            _onHit = null;
            _homing = null;
            if (_visual != null)
            {
                _visual.StopEmitting();
                _visual = null;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age >= _lifetime)
            {
                Release();
                return;
            }

            if (_homing != null)
            {
                Vector3 to = _homing.position - transform.position;
                if (to.sqrMagnitude > 0.1f) _direction = Vector3.RotateTowards(_direction, to.normalized, 3f * dt, 0f).normalized;
            }

            Vector3 start = transform.position;
            float dist = _speed * dt;
            int n = Physics.SphereCastNonAlloc(start, _radius, _direction, Hits, dist, Layers.HittableMask | Layers.EnvironmentMask, QueryTriggerInteraction.Collide);
            bool stop = false;
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                int layer = col.gameObject.layer;
                if (((1 << layer) & Layers.EnvironmentMask) != 0)
                {
                    // Low obstacles (ground) never stop grounded waves; walls do.
                    if (Vector3.Dot(Hits[i].normal, Vector3.up) < 0.6f && !_pierce) stop = true;
                    continue;
                }
                var target = HitQuery.Resolve(col);
                if (target == null || !target.IsAlive || (target.Team == _team && _team != Team.Neutral)) continue;
                if (!_registry.CanHit(target, Time.time)) continue;
                _registry.Register(target, Time.time);
                var hit = _template;
                hit.HitPoint = Hits[i].point == Vector3.zero ? target.CenterPoint : Hits[i].point;
                Vector3 dir = _direction;
                dir.y = 0f;
                hit.Direction = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
                hit.AttackInstanceId = _registry.AttackInstanceId;
                var result = target.ReceiveHit(hit);
                if (result.Outcome != HitOutcome.Ignored) _onHit?.Invoke(hit, result);
                if (!string.IsNullOrEmpty(_impactVfx)) VFXLibrary.Spawn(_impactVfx, hit.HitPoint, transform.rotation, 1f, _element);
                if (!_pierce) stop = true;
            }

            transform.position = start + _direction * dist;
            transform.rotation = Quaternion.LookRotation(_direction);
            if (stop) Release();
        }

        private void Release()
        {
            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Release();
            else Destroy(gameObject);
        }
    }
}
