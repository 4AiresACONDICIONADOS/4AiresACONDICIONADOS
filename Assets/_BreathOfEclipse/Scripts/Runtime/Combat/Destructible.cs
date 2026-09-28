using System.Collections.Generic;
using BreathOfEclipse.Audio;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using BreathOfEclipse.VFX;
using UnityEngine;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Breakable prop (crates, vases, small lanterns). Light hits wobble it; heavy attacks and techniques
    /// (CanBreakObjects) or enough damage shatter it into pooled rigidbody debris.
    /// </summary>
    public sealed class Destructible : MonoBehaviour, IDamageable
    {
        public float Health = 30f;
        public bool BreakOnlyWithHeavy;
        public Color DebrisColor = new Color(0.45f, 0.3f, 0.18f);
        public string BreakVfx = "debris_wood";
        public string BreakSfx = "crate_break";
        public int DebrisCount = 8;
        public float DebrisSize = 0.18f;

        private float _wobble;
        private Vector3 _wobbleAxis;
        private Quaternion _baseRotation;
        private bool _broken;

        public Team Team => Team.Neutral;
        public bool IsAlive => !_broken;
        public Transform Transform => transform;
        public Vector3 CenterPoint => transform.position + Vector3.up * 0.4f;

        private void Awake() => _baseRotation = transform.rotation;

        public HitResult ReceiveHit(HitData hit)
        {
            if (_broken) return HitResult.Ignored;
            float damage = hit.BaseDamage * Mathf.Max(0.1f, hit.Multiplier);
            bool heavy = hit.CanBreakObjects || hit.Category == DamageCategory.Heavy || hit.Category == DamageCategory.Skill ||
                         hit.Category == DamageCategory.Ultimate || hit.Category == DamageCategory.Finisher;
            if (!BreakOnlyWithHeavy || heavy) Health -= damage;
            _wobble = 1f;
            _wobbleAxis = Vector3.Cross(Vector3.up, hit.Direction.sqrMagnitude > 0.01f ? hit.Direction : Vector3.forward);
            var result = new HitResult { Outcome = HitOutcome.Hit, Damage = Mathf.RoundToInt(damage), Target = gameObject, HitPoint = hit.HitPoint };
            if (Health <= 0f) Break(hit.Direction);
            return result;
        }

        public void Break(Vector3 direction)
        {
            if (_broken) return;
            _broken = true;
            VFXLibrary.Spawn(BreakVfx, transform.position + Vector3.up * 0.3f, Quaternion.identity);
            Sfx.Play(BreakSfx, transform.position);
            DebrisPiece.Burst(transform.position + Vector3.up * 0.4f, direction, DebrisCount, DebrisSize, DebrisColor);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_wobble <= 0f) return;
            _wobble = Mathf.MoveTowards(_wobble, 0f, Time.deltaTime * 3f);
            float angle = Mathf.Sin(_wobble * 25f) * 9f * _wobble;
            transform.rotation = Quaternion.AngleAxis(angle, _wobbleAxis.sqrMagnitude > 0.001f ? _wobbleAxis : Vector3.right) * _baseRotation;
        }
    }

    public static class DestructibleUtility
    {
        private static readonly List<IDamageable> Buffer = new List<IDamageable>();

        /// <summary>Breaks every destructible in a radius (ground slams, ultimates).</summary>
        public static void BreakInRadius(Vector3 center, float radius, GameObject source)
        {
            HitQuery.Sphere(center, radius, Team.Player, Buffer, Layers.Mask(Layers.Destructible));
            foreach (var d in Buffer)
            {
                if (d is Destructible destructible)
                {
                    Vector3 dir = destructible.transform.position - center;
                    dir.y = 0f;
                    destructible.Break(dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward);
                }
            }
        }
    }

    /// <summary>Pooled physics debris that fades away (temporary: never modifies the scene permanently).</summary>
    public sealed class DebrisPiece : MonoBehaviour, IPoolable
    {
        private const string PoolKey = "debris_piece";
        private Rigidbody _body;
        private float _age;
        private Vector3 _scale;

        public static void Burst(Vector3 center, Vector3 direction, int count, float size, Color color)
        {
            var pool = PoolManager.Instance;
            if (pool == null) return;
            var mat = MaterialFactory.Toon(color, 0.6f);
            for (int i = 0; i < count; i++)
            {
                var go = pool.Spawn(PoolKey, Create, center + Random.insideUnitSphere * 0.3f, Random.rotation);
                var piece = go.GetComponent<DebrisPiece>();
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                float s = size * Random.Range(0.6f, 1.4f);
                piece._scale = new Vector3(s, s * Random.Range(0.3f, 1f), s * Random.Range(0.8f, 2f));
                go.transform.localScale = piece._scale;
                Vector3 v = (direction.normalized * 3f + Random.insideUnitSphere * 3f + Vector3.up * Random.Range(2f, 5f));
                piece._body.linearVelocity = v;
                piece._body.angularVelocity = Random.insideUnitSphere * 12f;
                go.GetComponent<PooledObject>().Lifetime = 3.2f;
            }
        }

        private static GameObject Create()
        {
            var go = new GameObject("Debris", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Rigidbody));
            go.SetActive(false);
            go.layer = Layers.Debris;
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.Primitive(PrimitiveType.Cube);
            var rb = go.GetComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            var piece = go.AddComponent<DebrisPiece>();
            piece._body = rb;
            return go;
        }

        public void OnSpawned() => _age = 0f;

        public void OnReleased()
        {
            if (_body == null) return;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age > 2.4f) transform.localScale = _scale * Mathf.Clamp01(1f - (_age - 2.4f) / 0.8f);
        }
    }
}
