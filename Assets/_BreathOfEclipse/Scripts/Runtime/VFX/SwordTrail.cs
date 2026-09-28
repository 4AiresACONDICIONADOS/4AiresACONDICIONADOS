using System.Collections.Generic;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Custom mesh trail following a blade (not a Unity TrailRenderer ribbon): samples the blade base/tip every
    /// frame, smooths fast arcs with Catmull-Rom subdivision and renders with the SwordTrail shader
    /// (gradient, noise, dissolve, emission, width curve, scrolling texture). Two instances are combined:
    /// the physical sword trail and a wider elemental arc.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public sealed class SwordTrail : MonoBehaviour
    {
        private struct Sample
        {
            public Vector3 Base;
            public Vector3 Tip;
            public float Time;
        }

        public Transform BladeBase;
        public Transform BladeTip;
        public float Lifetime = 0.16f;
        /// <summary>Fraction of the blade where the trail starts (0 = guard).</summary>
        public float StartFraction = 0.15f;
        /// <summary>Trail length relative to the blade (&gt;1 extends past the tip: elemental arcs).</summary>
        public float LengthScale = 1f;
        public AnimationCurve WidthOverAge = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);
        public int Subdivisions = 4;
        public float MinSampleDistance = 0.01f;
        public bool Emitting;
        public float Intensity = 1f;

        private readonly List<Sample> _samples = new List<Sample>(64);
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Transform _meshRoot;
        private readonly List<Vector3> _verts = new List<Vector3>(512);
        private readonly List<Vector2> _uvs = new List<Vector2>(512);
        private readonly List<Color> _colors = new List<Color>(512);
        private readonly List<int> _tris = new List<int>(1024);

        public static SwordTrail Create(string name, Transform owner, Transform bladeBase, Transform bladeTip, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(owner, false);
            var trail = go.AddComponent<SwordTrail>();
            trail.BladeBase = bladeBase;
            trail.BladeTip = bladeTip;
            trail.Setup(material);
            return trail;
        }

        private void Setup(Material material)
        {
            // Vertices are world space, so the mesh lives on an unparented object at the origin.
            var meshGo = new GameObject(name + "_Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            meshGo.layer = Layers.VFX;
            _meshRoot = meshGo.transform;
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            meshGo.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = meshGo.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mpb = new MaterialPropertyBlock();
        }

        public void SetMaterial(Material material)
        {
            if (_renderer != null) _renderer.sharedMaterial = material;
        }

        public void Clear()
        {
            _samples.Clear();
            if (_mesh != null) _mesh.Clear();
        }

        private void OnDestroy()
        {
            if (_meshRoot != null) Destroy(_meshRoot.gameObject);
            if (_mesh != null) Destroy(_mesh);
        }

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnEnable()
        {
            if (_renderer != null) _renderer.enabled = true;
        }

        private void LateUpdate()
        {
            if (BladeBase == null || BladeTip == null || _mesh == null) return;
            if (Time.deltaTime <= 0f) return; // frozen during hit stop
            float now = Time.time;

            if (Emitting)
            {
                Vector3 b = BladeBase.position;
                Vector3 t = BladeTip.position;
                Vector3 dir = t - b;
                var s = new Sample { Base = b + dir * StartFraction, Tip = b + dir * LengthScale, Time = now };
                if (_samples.Count == 0 || (_samples[_samples.Count - 1].Tip - s.Tip).sqrMagnitude > MinSampleDistance * MinSampleDistance)
                    _samples.Add(s);
                else _samples[_samples.Count - 1] = s;
            }

            while (_samples.Count > 0 && now - _samples[0].Time > Lifetime) _samples.RemoveAt(0);
            if (_samples.Count > 48) _samples.RemoveRange(0, _samples.Count - 48);

            BuildMesh(now);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private void BuildMesh(float now)
        {
            _verts.Clear();
            _uvs.Clear();
            _colors.Clear();
            _tris.Clear();
            int n = _samples.Count;
            if (n < 2)
            {
                _mesh.Clear();
                return;
            }

            int sub = Mathf.Max(1, Subdivisions);
            for (int i = 0; i < n - 1; i++)
            {
                var s0 = _samples[Mathf.Max(0, i - 1)];
                var s1 = _samples[i];
                var s2 = _samples[i + 1];
                var s3 = _samples[Mathf.Min(n - 1, i + 2)];
                int steps = i == n - 2 ? sub + 1 : sub;
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)sub;
                    Vector3 b = CatmullRom(s0.Base, s1.Base, s2.Base, s3.Base, t);
                    Vector3 tip = CatmullRom(s0.Tip, s1.Tip, s2.Tip, s3.Tip, t);
                    float time = Mathf.Lerp(s1.Time, s2.Time, t);
                    float age = Mathf.Clamp01((now - time) / Lifetime);
                    float w = WidthOverAge.Evaluate(age);
                    Vector3 mid = (b + tip) * 0.5f;
                    Vector3 half = (tip - b) * 0.5f * w;
                    _verts.Add(mid - half);
                    _verts.Add(mid + half);
                    _uvs.Add(new Vector2(age, 0f));
                    _uvs.Add(new Vector2(age, 1f));
                    var c = new Color(1f, 1f, 1f, (1f - age) * Intensity);
                    _colors.Add(c);
                    _colors.Add(c);
                }
            }

            int rows = _verts.Count / 2;
            for (int r = 0; r < rows - 1; r++)
            {
                int a = r * 2;
                _tris.Add(a); _tris.Add(a + 1); _tris.Add(a + 2);
                _tris.Add(a + 2); _tris.Add(a + 1); _tris.Add(a + 3);
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
            _mpb.Clear();
            _mpb.SetFloat(ShaderIds.Intensity, Intensity);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
