using System;
using System.Collections.Generic;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Animated tube along a parametric path: water serpents and dragons, fire arcs, wind spirals.
    /// The head travels along the path (reveal), then the tail catches up (dissipate). Undulation and radius
    /// profile give each element its own silhouette.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RibbonTube : MonoBehaviour, IVfxPart
    {
        /// <summary>Local-space path, u in [0,1].</summary>
        public Func<float, Vector3> Path;
        public AnimationCurve RadiusProfile = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public float Radius = 0.35f;
        public int Segments = 48;
        public int RadialSegments = 10;
        public float GrowTime = 0.3f;
        public float HoldTime = 0.2f;
        public float FadeTime = 0.4f;
        public float StartDelay;
        /// <summary>Undulation amplitude perpendicular to the path (serpent motion).</summary>
        public float Wobble;
        public float WobbleFrequency = 3f;
        public float WobbleSpeed = 8f;
        public float Opacity = 1f;
        /// <summary>Optional object kept at the head (serpent head, dragon head).</summary>
        public Transform Head;
        public bool UseUnscaledTime;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _age;
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<float> _radii = new List<float>();

        private void Awake()
        {
            _mesh = new Mesh { name = "RibbonTube" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mpb = new MaterialPropertyBlock();
        }

        public void Restart()
        {
            _age = -StartDelay;
            if (_mesh != null) _mesh.Clear();
            if (Head != null) Head.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate()
        {
            if (Path == null) return;
            _age += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_age < 0f)
            {
                _renderer.enabled = false;
                return;
            }
            float head = GrowTime <= 0f ? 1f : Mathf.Clamp01(_age / GrowTime);
            head = 1f - (1f - head) * (1f - head);
            float tail = FadeTime <= 0f ? 0f : Mathf.Clamp01((_age - GrowTime - HoldTime) / FadeTime);
            tail = tail * tail;
            if (tail >= head - 0.01f)
            {
                _renderer.enabled = false;
                if (Head != null) Head.gameObject.SetActive(false);
                return;
            }
            _renderer.enabled = true;

            _points.Clear();
            _radii.Clear();
            int n = Mathf.Max(4, Segments);
            float time = _age * WobbleSpeed;
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n;
                float u = Mathf.Lerp(tail, head, k);
                Vector3 p = Path(u);
                if (Wobble > 0f)
                {
                    Vector3 next = Path(Mathf.Min(1f, u + 0.01f));
                    Vector3 tangent = (next - p).sqrMagnitude > 1e-6f ? (next - p).normalized : Vector3.forward;
                    Vector3 side = Vector3.Cross(tangent, Vector3.up);
                    if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                    side.Normalize();
                    Vector3 up = Vector3.Cross(side, tangent);
                    float w = Mathf.Sin(u * WobbleFrequency * Mathf.PI * 2f - time) * Wobble * Mathf.Sin(k * Mathf.PI);
                    p += side * w + up * (w * 0.4f);
                }
                _points.Add(p);
                // Radius along the visible part: taper at both ends plus the designer profile over u.
                float taper = Mathf.Clamp01(Mathf.Min(k * 5f, (1f - k) * 3f + 0.25f));
                _radii.Add(Radius * RadiusProfile.Evaluate(u) * taper);
            }
            ProceduralMeshes.BuildTube(_mesh, _points, _radii, RadialSegments, Vector3.up);

            float fade = Mathf.Clamp01(1f - tail * 1.2f) * Opacity;
            _mpb.Clear();
            _mpb.SetFloat(ShaderIds.Opacity, fade);
            _mpb.SetFloat(ShaderIds.Time01, _age);
            _renderer.SetPropertyBlock(_mpb);

            if (Head != null)
            {
                bool show = head < 0.999f || tail < 0.05f;
                Head.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 hp = _points[_points.Count - 1];
                    Vector3 prev = _points[Mathf.Max(0, _points.Count - 3)];
                    Head.localPosition = hp;
                    if ((hp - prev).sqrMagnitude > 1e-6f) Head.localRotation = Quaternion.LookRotation(hp - prev, Vector3.up);
                }
            }
        }
    }

    /// <summary>Mesh that scales / spins / fades over its life (shockwave rings, crescent slashes, glows, domes).</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ExpandingMesh : MonoBehaviour, IVfxPart
    {
        public Vector3 StartScale = Vector3.one * 0.2f;
        public Vector3 EndScale = Vector3.one * 3f;
        public AnimationCurve ScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve AlphaCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.08f, 1f), new Keyframe(1f, 0f));
        public float Duration = 0.5f;
        public float Delay;
        public Vector3 SpinDegreesPerSecond;
        public Vector3 Drift;
        public Color Tint = Color.white;
        public string ColorProperty = "_TintColor";
        public bool UseUnscaledTime;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _age;
        private Vector3 _basePos;
        private Quaternion _baseRot;
        private int _colorId;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mpb = new MaterialPropertyBlock();
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _colorId = Shader.PropertyToID(ColorProperty);
        }

        public void Restart()
        {
            _age = -Delay;
            transform.localPosition = _basePos;
            transform.localRotation = _baseRot;
            transform.localScale = StartScale;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            _age += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_age < 0f || _age > Duration)
            {
                _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;
            float t = Mathf.Clamp01(_age / Duration);
            transform.localScale = Vector3.LerpUnclamped(StartScale, EndScale, ScaleCurve.Evaluate(t));
            transform.localRotation = _baseRot * Quaternion.Euler(SpinDegreesPerSecond * _age);
            transform.localPosition = _basePos + Drift * _age;
            // Color carries rgb only; the fade goes through _Opacity so shaders never apply alpha twice.
            Color c = Tint;
            float alpha = c.a * AlphaCurve.Evaluate(t);
            c.a = 1f;
            _mpb.Clear();
            _mpb.SetColor(_colorId, c);
            _mpb.SetFloat(ShaderIds.Opacity, alpha);
            _renderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Jagged lightning between two local points, re-randomized a few times per second, with branches.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LightningBolt : MonoBehaviour, IVfxPart
    {
        public Vector3 From;
        public Vector3 To = Vector3.forward * 4f;
        public int Points = 14;
        public float Jaggedness = 0.35f;
        public float Width = 0.12f;
        public float Duration = 0.35f;
        public float Delay;
        public float Flicker = 0.035f;
        public Color Tint = Color.white;
        public int Branches = 2;
        /// <summary>When set, endpoints follow these transforms (world space).</summary>
        public Transform FromTarget, ToTarget;

        private LineRenderer _line;
        private LineRenderer[] _branchLines;
        private float _age;
        private float _nextFlicker;
        private readonly Vector3[] _buffer = new Vector3[32];

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.textureMode = LineTextureMode.Stretch;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCapVertices = 2;
            _branchLines = new LineRenderer[Branches];
            for (int i = 0; i < Branches; i++)
            {
                var go = new GameObject("Branch" + i);
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.sharedMaterial = _line.sharedMaterial;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                _branchLines[i] = lr;
            }
        }

        public void SetMaterial(Material m)
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.sharedMaterial = m;
            if (_branchLines == null) return;
            foreach (var b in _branchLines) if (b != null) b.sharedMaterial = m;
        }

        public void Restart()
        {
            _age = -Delay;
            _nextFlicker = 0f;
            _line.enabled = false;
            foreach (var b in _branchLines) b.enabled = false;
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            bool visible = _age >= 0f && _age <= Duration;
            _line.enabled = visible;
            if (!visible)
            {
                foreach (var b in _branchLines) b.enabled = false;
                return;
            }
            float t = _age / Duration;
            float intensity = t < 0.1f ? 1f : Mathf.Clamp01(1f - (t - 0.1f) / 0.9f);
            Color c = Tint;
            c.a = intensity;
            _line.startColor = c;
            _line.endColor = c;
            float w = Width * (0.4f + 0.6f * intensity);
            _line.startWidth = w;
            _line.endWidth = w * 0.5f;

            if (Time.time >= _nextFlicker || Time.deltaTime <= 0f)
            {
                _nextFlicker = Time.time + Flicker;
                Vector3 a = FromTarget != null ? transform.InverseTransformPoint(FromTarget.position) : From;
                Vector3 b = ToTarget != null ? transform.InverseTransformPoint(ToTarget.position) : To;
                int n = Mathf.Clamp(Points, 2, _buffer.Length);
                Jag(_line, a, b, n, Jaggedness);
                for (int i = 0; i < _branchLines.Length; i++)
                {
                    var lr = _branchLines[i];
                    lr.enabled = UnityEngine.Random.value > 0.3f;
                    if (!lr.enabled) continue;
                    float k = UnityEngine.Random.Range(0.2f, 0.8f);
                    Vector3 start = _line.GetPosition(Mathf.Clamp(Mathf.RoundToInt(k * (n - 1)), 0, n - 1));
                    Vector3 dir = (b - a);
                    Vector3 end = start + Quaternion.Euler(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-60f, 60f), 0f) * dir * UnityEngine.Random.Range(0.15f, 0.35f);
                    Jag(lr, start, end, 6, Jaggedness * 0.6f);
                    lr.startWidth = w * 0.5f;
                    lr.endWidth = 0f;
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }
        }

        private void Jag(LineRenderer lr, Vector3 a, Vector3 b, int n, float jag)
        {
            lr.positionCount = n;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            Vector3 perpA = Vector3.Cross(dir, Vector3.up);
            if (perpA.sqrMagnitude < 1e-4f) perpA = Vector3.Cross(dir, Vector3.right);
            perpA.Normalize();
            Vector3 perpB = Vector3.Cross(dir.normalized, perpA);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                Vector3 p = a + dir * k;
                if (i > 0 && i < n - 1)
                {
                    float amp = jag * len * 0.12f * Mathf.Sin(k * Mathf.PI);
                    p += perpA * UnityEngine.Random.Range(-amp, amp) + perpB * UnityEngine.Random.Range(-amp, amp);
                }
                _buffer[i] = p;
            }
            lr.SetPositions(_buffer);
            lr.positionCount = n;
        }
    }

    /// <summary>Point light with an intensity envelope (impact flashes, charge glows).</summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlashLight : MonoBehaviour, IVfxPart
    {
        public float Peak = 6f;
        public float Duration = 0.25f;
        public float Delay;
        public AnimationCurve Curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(1f, 0f));
        private Light _light;
        private float _age;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _light.shadows = LightShadows.None;
        }

        public void Restart()
        {
            _age = -Delay;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            bool on = _age >= 0f && _age <= Duration;
            _light.enabled = on;
            if (on) _light.intensity = Peak * Curve.Evaluate(_age / Duration);
        }
    }

    /// <summary>Makes a pooled effect follow a transform (auras, charge effects on the sword).</summary>
    public sealed class FollowTarget : MonoBehaviour, IVfxPart
    {
        public Transform Target;
        public Vector3 Offset;
        public bool FollowRotation;
        public Quaternion RotationOffset = Quaternion.identity;

        public void Restart() { }

        private void LateUpdate()
        {
            if (Target == null) return;
            transform.position = Target.TransformPoint(Offset);
            if (FollowRotation) transform.rotation = Target.rotation * RotationOffset;
        }
    }

    /// <summary>Ground decal quad fading out (cracks, scorch marks, frost).</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GroundDecal : MonoBehaviour, IVfxPart
    {
        public float Duration = 4f;
        public float FadeIn = 0.05f;
        public Color Tint = Color.white;
        public float GrowTime = 0.12f;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _age;
        private Vector3 _scale;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mpb = new MaterialPropertyBlock();
            _scale = transform.localScale;
        }

        public void Restart()
        {
            _age = 0f;
            transform.localScale = _scale * 0.3f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float g = GrowTime <= 0f ? 1f : Mathf.Clamp01(_age / GrowTime);
            transform.localScale = _scale * Mathf.Lerp(0.3f, 1f, 1f - (1f - g) * (1f - g));
            float a = Mathf.Clamp01(_age / Mathf.Max(0.001f, FadeIn)) * Mathf.Clamp01((Duration - _age) / (Duration * 0.4f));
            Color c = Tint;
            c.a *= a;
            _mpb.Clear();
            _mpb.SetColor(ShaderIds.TintColor, c);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
