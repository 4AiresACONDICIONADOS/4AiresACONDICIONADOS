using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreathOfEclipse.Combat
{
    /// <summary>
    /// Draws recorded hit queries (red/yellow) and character hurtboxes (green) as a line mesh.
    /// Works in builds (no Gizmos) and with URP. Added by the debug menu.
    /// </summary>
    public sealed class HitboxDebugRenderer : MonoBehaviour
    {
        private static readonly Color HurtColor = new Color(0.3f, 1f, 0.45f, 1f);
        private const int CircleSegments = 20;

        private Mesh _mesh;
        private Material _material;
        private readonly List<Vector3> _verts = new List<Vector3>(4096);
        private readonly List<Color> _colors = new List<Color>(4096);
        private readonly List<int> _indices = new List<int>(4096);
        private Damageable[] _damageables = new Damageable[0];
        private float _nextScan;

        private void Awake()
        {
            _mesh = new Mesh { name = "HitboxDebugLines", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _material = MaterialFactory.Vfx(Texture2D.whiteTexture, VfxBlend.Additive, 0f);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        private void LateUpdate()
        {
            if (!HitboxDebug.Enabled) return;
            HitboxDebug.Prune();
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                _damageables = FindObjectsByType<Damageable>(FindObjectsSortMode.None);
            }

            _verts.Clear();
            _colors.Clear();
            _indices.Clear();

            foreach (var d in _damageables)
            {
                if (d == null || !d.isActiveAndEnabled || !d.IsAlive) continue;
                AddCollider(d.GetComponent<Collider>(), HurtColor);
            }
            float now = Time.unscaledTime;
            var shapes = HitboxDebug.Recent;
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                float fade = 1f - Mathf.Clamp01((now - s.Time) / HitboxDebug.Lifetime);
                Color c = s.Color * fade;
                switch (s.Kind)
                {
                    case HitboxDebug.ShapeKind.Sphere: AddSphere(s.A, s.Radius, c); break;
                    case HitboxDebug.ShapeKind.Capsule: AddCapsule(s.A, s.B, s.Radius, c); break;
                    default: AddLine(s.A, s.B, c); break;
                }
            }

            if (_verts.Count == 0) return;
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            var rp = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
        }

        private void AddCollider(Collider c, Color color)
        {
            switch (c)
            {
                case CharacterController cc:
                {
                    Vector3 center = cc.transform.TransformPoint(cc.center);
                    float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                    AddCapsule(center - Vector3.up * half, center + Vector3.up * half, cc.radius, color);
                    break;
                }
                case CapsuleCollider cap:
                {
                    Vector3 center = cap.transform.TransformPoint(cap.center);
                    Vector3 axis = cap.direction == 0 ? cap.transform.right : cap.direction == 1 ? cap.transform.up : cap.transform.forward;
                    float half = Mathf.Max(0f, cap.height * 0.5f - cap.radius);
                    AddCapsule(center - axis * half, center + axis * half, cap.radius, color);
                    break;
                }
                case SphereCollider sph:
                    AddSphere(sph.transform.TransformPoint(sph.center), sph.radius * sph.transform.lossyScale.x, color);
                    break;
                case null:
                    break;
                default:
                    AddBox(c.bounds, color);
                    break;
            }
        }

        private void AddLine(Vector3 a, Vector3 b, Color c)
        {
            _indices.Add(_verts.Count);
            _verts.Add(a);
            _colors.Add(c);
            _indices.Add(_verts.Count);
            _verts.Add(b);
            _colors.Add(c);
        }

        private void AddCircle(Vector3 center, Vector3 u, Vector3 v, float radius, Color c)
        {
            Vector3 prev = center + u * radius;
            for (int i = 1; i <= CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                Vector3 p = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                AddLine(prev, p, c);
                prev = p;
            }
        }

        private void AddSphere(Vector3 center, float radius, Color c)
        {
            AddCircle(center, Vector3.right, Vector3.forward, radius, c);
            AddCircle(center, Vector3.right, Vector3.up, radius, c);
            AddCircle(center, Vector3.forward, Vector3.up, radius, c);
        }

        private void AddCapsule(Vector3 a, Vector3 b, float radius, Color c)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 0.0001f)
            {
                AddSphere(a, radius, c);
                return;
            }
            Vector3 n = axis.normalized;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(n, u);
            AddCircle(a, u, v, radius, c);
            AddCircle(b, u, v, radius, c);
            AddCircle(a, u, n, radius, c);
            AddCircle(b, u, n, radius, c);
            AddCircle(a, v, n, radius, c);
            AddCircle(b, v, n, radius, c);
            AddLine(a + u * radius, b + u * radius, c);
            AddLine(a - u * radius, b - u * radius, c);
            AddLine(a + v * radius, b + v * radius, c);
            AddLine(a - v * radius, b - v * radius, c);
        }

        private void AddBox(Bounds b, Color c)
        {
            Vector3 min = b.min, max = b.max;
            Vector3 p000 = min, p111 = max;
            Vector3 p100 = new Vector3(max.x, min.y, min.z), p010 = new Vector3(min.x, max.y, min.z), p001 = new Vector3(min.x, min.y, max.z);
            Vector3 p110 = new Vector3(max.x, max.y, min.z), p101 = new Vector3(max.x, min.y, max.z), p011 = new Vector3(min.x, max.y, max.z);
            AddLine(p000, p100, c); AddLine(p000, p010, c); AddLine(p000, p001, c);
            AddLine(p111, p011, c); AddLine(p111, p101, c); AddLine(p111, p110, c);
            AddLine(p100, p110, c); AddLine(p100, p101, c); AddLine(p010, p110, c);
            AddLine(p010, p011, c); AddLine(p001, p101, c); AddLine(p001, p011, c);
        }
    }
}
