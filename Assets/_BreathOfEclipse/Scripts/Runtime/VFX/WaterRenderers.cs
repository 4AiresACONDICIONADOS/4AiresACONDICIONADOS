using System;
using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Smooth water paths for <see cref="WaterRibbonRenderer"/> and <see cref="WaterSerpentRenderer"/>.
    /// Local space of the effect: character at the origin, +Z forward, +Y up. Every path is re-parameterised by
    /// arc length so the water travels at an even speed.
    /// </summary>
    public static class WaterSpline
    {
        private static Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>Catmull-Rom curve through every control point.</summary>
        public static Func<float, Vector3> CatmullRom(params Vector3[] points)
        {
            int n = points.Length;
            if (n == 0) return u => Vector3.zero;
            if (n == 1) return u => points[0];
            var pts = (Vector3[])points.Clone();
            Vector3 Raw(float u)
            {
                float f = Mathf.Clamp01(u) * (n - 1);
                int i = Mathf.Min(n - 2, (int)f);
                return CatmullRomPoint(pts[Mathf.Max(0, i - 1)], pts[i], pts[i + 1], pts[Mathf.Min(n - 1, i + 2)], f - i);
            }
            return EvenSpeed(Raw);
        }

        /// <summary>Arc-length re-parameterisation (u = fraction of the path's length).</summary>
        public static Func<float, Vector3> EvenSpeed(Func<float, Vector3> path, int samples = 64)
        {
            var dist = new float[samples + 1];
            Vector3 prev = path(0f);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 p = path(i / (float)samples);
                dist[i] = dist[i - 1] + Vector3.Distance(prev, p);
                prev = p;
            }
            float total = dist[samples];
            if (total < 1e-5f) return path;
            return u =>
            {
                float target = Mathf.Clamp01(u) * total;
                int lo = 0, hi = samples;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (dist[mid] < target) lo = mid;
                    else hi = mid;
                }
                float span = dist[hi] - dist[lo];
                float k = span > 1e-6f ? (target - dist[lo]) / span : 0f;
                return path((lo + k) / samples);
            };
        }

        /// <summary>Horizontal sword arc around the swordsman (degrees from +Z toward +X), tilted around +Z for diagonals.</summary>
        public static Func<float, Vector3> SlashArc(float radius, float startDeg, float endDeg, float height, float tiltDeg = 0f, float forward = 0f)
        {
            var tilt = Quaternion.AngleAxis(tiltDeg, Vector3.forward);
            return EvenSpeed(u =>
            {
                float a = Mathf.Lerp(startDeg, endDeg, u) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                return tilt * p + new Vector3(0f, height, forward);
            });
        }

        /// <summary>Wide crescent in front: a large, thick arc swept from one side to the other.</summary>
        public static Func<float, Vector3> Crescent(float radius, float sweepDeg, float height, float forward, float tiltDeg = 0f) =>
            SlashArc(radius, -sweepDeg * 0.5f, sweepDeg * 0.5f, height, tiltDeg, forward - radius * 0.35f);

        /// <summary>Forward wave: a flow travelling along +Z that snakes left and right.</summary>
        public static Func<float, Vector3> Wave(float length, float amplitude, float waves, float height, float phase = 0f) =>
            EvenSpeed(u => new Vector3(Mathf.Sin(u * waves * Mathf.PI * 2f + phase) * amplitude, height + Mathf.Sin(u * waves * Mathf.PI * 4f) * amplitude * 0.15f, u * length));

        /// <summary>Forward spiral (drill): a helix around +Z that tightens toward the tip.</summary>
        public static Func<float, Vector3> Spiral(float length, float radius, float turns, float height, float phase = 0f) =>
            EvenSpeed(u =>
            {
                float a = u * turns * Mathf.PI * 2f + phase;
                float r = radius * (1f - 0.55f * u);
                return new Vector3(Mathf.Cos(a) * r, height + Mathf.Sin(a) * r, u * length);
            });

        /// <summary>Circular current around the swordsman, optionally rising.</summary>
        public static Func<float, Vector3> Ring(float radius, float height, float turns = 1f, float rise = 0f, float startDeg = 0f) =>
            EvenSpeed(u =>
            {
                float a = (startDeg + u * turns * 360f) * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(a) * radius, height + rise * u, Mathf.Cos(a) * radius);
            });

        /// <summary>Breaking wave: rises forward and curls over its own lip.</summary>
        public static Func<float, Vector3> Curl(float height, float radius, float forward, float side = 0f) =>
            CatmullRom(
                new Vector3(side, 0.1f, 0.2f),
                new Vector3(side, height * 0.55f, forward * 0.45f),
                new Vector3(side, height, forward * 0.8f),
                new Vector3(side, height + radius * 0.2f, forward + radius * 0.45f),
                new Vector3(side, height - radius * 0.55f, forward + radius * 0.8f),
                new Vector3(side, height - radius * 1.1f, forward + radius * 0.35f));

        /// <summary>Waterfall: climbs above the swordsman and falls in front of him.</summary>
        public static Func<float, Vector3> Cascade(float top, float forward, float side = 0f) =>
            CatmullRom(
                new Vector3(side, top * 0.35f, 0.1f),
                new Vector3(side, top, forward * 0.3f),
                new Vector3(side, top * 0.92f, forward * 0.68f),
                new Vector3(side, top * 0.45f, forward * 0.95f),
                new Vector3(side, 0.05f, forward));
    }

    /// <summary>
    /// Anime water ribbon: a curved sheet (not a trail) along a spline, with width profile, camber (volume),
    /// twist, curling lip and travelling undulation. Grows from tail to head, holds, then dissolves in the
    /// TidalWaterAnime shader with a foam rim. Used for slash arcs, crescents, rings, spirals, curls and cascades.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterRibbonRenderer : MonoBehaviour, IVfxPart
    {
        public Func<float, Vector3> Path;
        public float Width = 0.6f;
        public AnimationCurve WidthProfile = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.35f, 1f), new Keyframe(0.85f, 0.85f), new Keyframe(1f, 0.2f));
        /// <summary>Camber: the sheet bulges along its normal (gives water volume instead of a flat card).</summary>
        public float Thickness = 0.25f;
        /// <summary>Roll of the sheet over the whole length (degrees).</summary>
        public float Twist;
        /// <summary>Extra roll that grows toward the head: a curling lip (degrees).</summary>
        public float Curl;
        public float WaveHeight;
        public float WaveFrequency = 2f;
        public float WaveSpeed = 6f;
        /// <summary>The sheet's width lies along tangent × PlaneNormal (up = flat horizontal arcs, right = vertical sheets).</summary>
        public Vector3 PlaneNormal = Vector3.up;
        public int Segments = 40;
        public int CrossSegments = 4;
        public float GrowTime = 0.18f;
        public float HoldTime = 0.25f;
        public float FadeTime = 0.35f;
        public float StartDelay;
        public float Opacity = 1f;
        public bool UseUnscaledTime;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _age;
        private Vector3[] _verts;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int[] _tris;
        private bool _topology;

        private void Awake()
        {
            _mesh = new Mesh { name = "WaterRibbon" };
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
            if (_renderer != null) _renderer.enabled = false;
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
            float grow = GrowTime <= 0f ? 1f : Mathf.Clamp01(_age / GrowTime);
            float head = 1f - (1f - grow) * (1f - grow);
            float fade = FadeTime <= 0f ? (_age > GrowTime + HoldTime ? 1f : 0f) : Mathf.Clamp01((_age - GrowTime - HoldTime) / FadeTime);
            float tail = fade * 0.45f;
            if (fade >= 1f || tail >= head - 0.01f)
            {
                _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;
            Build(tail, head);

            _mpb.Clear();
            _mpb.SetFloat(ShaderIds.Opacity, Opacity);
            _mpb.SetFloat(ShaderIds.Time01, _age);
            _mpb.SetFloat(ShaderIds.Dissolve, fade);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void Build(float tail, float head)
        {
            int segs = Mathf.Max(4, Segments);
            int cross = Mathf.Max(1, CrossSegments);
            int stride = cross + 1;
            int count = (segs + 1) * stride;
            if (_verts == null || _verts.Length != count)
            {
                _verts = new Vector3[count];
                _normals = new Vector3[count];
                _uvs = new Vector2[count];
                _tris = new int[segs * cross * 6];
                int t = 0;
                for (int i = 0; i < segs; i++)
                for (int c = 0; c < cross; c++)
                {
                    int a = i * stride + c, b = a + stride;
                    _tris[t++] = a; _tris[t++] = a + 1; _tris[t++] = b;
                    _tris[t++] = a + 1; _tris[t++] = b + 1; _tris[t++] = b;
                }
                _topology = false;
            }

            Vector3 plane = PlaneNormal.sqrMagnitude > 1e-6f ? PlaneNormal.normalized : Vector3.up;
            for (int i = 0; i <= segs; i++)
            {
                float k = i / (float)segs;
                float u = Mathf.Lerp(tail, head, k);
                Vector3 p = Path(u);
                Vector3 tangent = Path(Mathf.Min(1f, u + 0.01f)) - Path(Mathf.Max(0f, u - 0.01f));
                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector3.forward;
                Vector3 w = Vector3.Cross(tangent, plane);
                if (w.sqrMagnitude < 1e-4f) w = Vector3.Cross(tangent, Vector3.right);
                w.Normalize();
                float roll = Twist * u + Curl * u * u;
                if (Mathf.Abs(roll) > 0.01f) w = Quaternion.AngleAxis(roll, tangent) * w;
                Vector3 n = Vector3.Cross(w, tangent).normalized;
                if (WaveHeight > 0f)
                    p += n * (Mathf.Sin(u * WaveFrequency * Mathf.PI * 2f - _age * WaveSpeed) * WaveHeight * Mathf.Sin(k * Mathf.PI));

                // Pointed tips: the tail thins out, the leading edge stays sharp.
                float taper = Mathf.Clamp01(Mathf.Min(k * 6f, (1f - k) * 4f + 0.15f));
                float half = Width * 0.5f * WidthProfile.Evaluate(u) * taper;
                for (int c = 0; c <= cross; c++)
                {
                    float s = c / (float)cross * 2f - 1f;
                    float bulge = Thickness * (1f - s * s) * half;
                    int idx = i * stride + c;
                    _verts[idx] = p + w * (s * half) + n * bulge;
                    _normals[idx] = (n - w * (2f * s * Thickness)).normalized;
                    _uvs[idx] = new Vector2(k, c / (float)cross);
                }
            }

            if (!_topology) _mesh.Clear();
            _mesh.vertices = _verts;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            if (!_topology)
            {
                _mesh.triangles = _tris;
                _topology = true;
            }
            _mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// The TIDAL BREATH water serpent: spline body (tapered tube with twisting internal flow and a slithering
    /// motion), a foam crest along its back, and a stylised head (skull, snout, hinged jaw, horns, crest and
    /// cheek fins, glowing eyes) riding the growth front. Grows, holds on screen, then dissolves from the tail.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterSerpentRenderer : MonoBehaviour, IVfxPart
    {
        public Func<float, Vector3> Path;
        public float Radius = 0.5f;
        public AnimationCurve RadiusProfile = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.45f, 0.8f), new Keyframe(0.85f, 1.15f), new Keyframe(1f, 0.95f));
        public int Segments = 56;
        public int RadialSegments = 12;
        /// <summary>Degrees the flow spirals around the body from tail to head.</summary>
        public float Twist = 540f;
        public float Slither = 0.18f;
        public float SlitherFrequency = 2f;
        public float SlitherSpeed = 10f;
        public float GrowTime = 0.26f;
        public float HoldTime = 0.6f;
        public float FadeTime = 0.55f;
        public float StartDelay;
        public float Opacity = 1f;
        public bool UseUnscaledTime;

        private Mesh _mesh, _crestMesh;
        private MeshRenderer _renderer, _crestRenderer;
        private MeshRenderer[] _headRenderers = new MeshRenderer[0];
        private MaterialPropertyBlock _mpb;
        private Transform _head, _jaw;
        private Vector3 _headScale = Vector3.one;
        private float _age;
        private Vector3[] _points, _ups;
        private float[] _radii;
        private Vector3[] _verts, _normals, _crestVerts, _crestNormals;
        private Vector2[] _uvs, _crestUvs;
        private bool _topology;

        private void Awake()
        {
            _mesh = new Mesh { name = "WaterSerpent" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>Creates the crest and head (call once after AddComponent). <paramref name="headSize"/> is relative to the body radius.</summary>
        public void Setup(Material crest, Material head, Material eyes, float headSize, bool detailed)
        {
            if (detailed && crest != null)
            {
                var go = new GameObject("Crest", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                _crestMesh = new Mesh { name = "SerpentCrest" };
                _crestMesh.MarkDynamic();
                go.GetComponent<MeshFilter>().sharedMesh = _crestMesh;
                _crestRenderer = go.GetComponent<MeshRenderer>();
                _crestRenderer.sharedMaterial = crest;
                _crestRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _crestRenderer.receiveShadows = false;
            }
            _head = BuildHead(transform, head, crest != null ? crest : head, eyes, detailed, out _jaw);
            _headScale = Vector3.one * (Radius * 2f * headSize);
            _head.localScale = _headScale;
            _headRenderers = _head.GetComponentsInChildren<MeshRenderer>(true);
            _head.gameObject.SetActive(false);
        }

        public void Restart()
        {
            _age = -StartDelay;
            if (_renderer != null) _renderer.enabled = false;
            if (_crestRenderer != null) _crestRenderer.enabled = false;
            if (_head != null) _head.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_crestMesh != null) Destroy(_crestMesh);
        }

        private void LateUpdate()
        {
            if (Path == null) return;
            _age += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_age < 0f)
            {
                SetVisible(false);
                return;
            }
            float grow = GrowTime <= 0f ? 1f : Mathf.Clamp01(_age / GrowTime);
            float head = 1f - (1f - grow) * (1f - grow) * (1f - grow);
            float fade = FadeTime <= 0f ? 0f : Mathf.Clamp01((_age - GrowTime - HoldTime) / FadeTime);
            float tail = fade * 0.35f;
            if (fade >= 1f || tail >= head - 0.01f)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);
            BuildBody(tail, head);

            _mpb.Clear();
            _mpb.SetFloat(ShaderIds.Opacity, Opacity);
            _mpb.SetFloat(ShaderIds.Time01, _age);
            _mpb.SetFloat(ShaderIds.Dissolve, fade);
            _renderer.SetPropertyBlock(_mpb);
            if (_crestRenderer != null) _crestRenderer.SetPropertyBlock(_mpb);
            foreach (var r in _headRenderers) r.SetPropertyBlock(_mpb);

            if (_head != null)
            {
                int last = _points.Length - 1;
                Vector3 forward = _points[last] - _points[Mathf.Max(0, last - 2)];
                _head.localPosition = _points[last];
                if (forward.sqrMagnitude > 1e-6f) _head.localRotation = Quaternion.LookRotation(forward, _ups[last]);
                // The head shrinks into foam as the body dissolves.
                _head.localScale = _headScale * Mathf.Lerp(1f, 0.35f, Mathf.SmoothStep(0f, 1f, fade * 1.4f));
                if (_jaw != null)
                {
                    // Roar while striking, then a slow breath.
                    float open = grow < 1f ? 26f + 10f * Mathf.Sin(_age * 30f) : 14f + 6f * Mathf.Sin(_age * 5f);
                    _jaw.localRotation = Quaternion.Euler(open, 0f, 0f);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
            if (_crestRenderer != null) _crestRenderer.enabled = visible;
            if (_head != null && _head.gameObject.activeSelf != visible) _head.gameObject.SetActive(visible);
        }

        private void BuildBody(float tail, float head)
        {
            int n = Mathf.Max(6, Segments) + 1;
            int ring = Mathf.Max(4, RadialSegments) + 1;
            if (_points == null || _points.Length != n || _verts.Length != n * ring)
            {
                _points = new Vector3[n];
                _ups = new Vector3[n];
                _radii = new float[n];
                _verts = new Vector3[n * ring];
                _normals = new Vector3[_verts.Length];
                _uvs = new Vector2[_verts.Length];
                _crestVerts = new Vector3[n * 2];
                _crestNormals = new Vector3[n * 2];
                _crestUvs = new Vector2[n * 2];
                _topology = false;
            }

            float time = _age * SlitherSpeed;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                float u = Mathf.Lerp(tail, head, k);
                _points[i] = Path(u);
                // Taper: thin tail, full body, the neck stays thick where the head joins.
                float taper = Mathf.Clamp01(k * 4f);
                _radii[i] = Radius * RadiusProfile.Evaluate(u) * taper;
            }

            // Parallel-transport frames seeded with world up; slither displaces the body sideways.
            Vector3 prevUp = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = i < n - 1 ? _points[i + 1] - _points[i] : _points[i] - _points[i - 1];
                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector3.forward;
                Vector3 up = Vector3.ProjectOnPlane(prevUp, tangent);
                if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.right, tangent);
                up.Normalize();
                prevUp = up;
                _ups[i] = up;
                if (Slither > 0f)
                {
                    float k = i / (float)(n - 1);
                    Vector3 side = Vector3.Cross(up, tangent);
                    _points[i] += side * (Mathf.Sin(k * SlitherFrequency * Mathf.PI * 2f - time) * Slither * Mathf.Sin(k * Mathf.PI));
                }
            }

            int rs = ring - 1;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                Vector3 tangent = i < n - 1 ? _points[i + 1] - _points[i] : _points[i] - _points[i - 1];
                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector3.forward;
                Vector3 up = _ups[i];
                Vector3 side = Vector3.Cross(up, tangent).normalized;
                float twist = Twist / 360f * k;
                for (int s = 0; s < ring; s++)
                {
                    float a = s / (float)rs * Mathf.PI * 2f;
                    // Slightly flattened cross-section reads as a creature, not a pipe.
                    Vector3 dir = up * Mathf.Cos(a) + side * (Mathf.Sin(a) * 1.12f);
                    int idx = i * ring + s;
                    _verts[idx] = _points[i] + dir * _radii[i];
                    _normals[idx] = dir.normalized;
                    _uvs[idx] = new Vector2(k, s / (float)rs + twist);
                }
                // Dorsal crest: a ragged foam fin standing on the back.
                float fin = _radii[i] * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 7f - _age * 4f))) * Mathf.Clamp01(k * 3f) * (1f - Mathf.SmoothStep(0.88f, 1f, k));
                Vector3 baseP = _points[i] + up * (_radii[i] * 0.85f);
                _crestVerts[i * 2] = baseP;
                _crestVerts[i * 2 + 1] = baseP + (up - tangent * 0.35f).normalized * fin;
                _crestNormals[i * 2] = side;
                _crestNormals[i * 2 + 1] = side;
                _crestUvs[i * 2] = new Vector2(k, 0.5f);
                _crestUvs[i * 2 + 1] = new Vector2(k, 1f);
            }

            if (!_topology)
            {
                _mesh.Clear();
                _mesh.vertices = _verts;
                var tris = new int[(n - 1) * rs * 6];
                int t = 0;
                for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < rs; s++)
                {
                    int a = i * ring + s, b = a + ring;
                    tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                    tris[t++] = a + 1; tris[t++] = b + 1; tris[t++] = b;
                }
                _mesh.normals = _normals;
                _mesh.uv = _uvs;
                _mesh.triangles = tris;
                if (_crestMesh != null)
                {
                    _crestMesh.Clear();
                    _crestMesh.vertices = _crestVerts;
                    var ct = new int[(n - 1) * 6];
                    int c = 0;
                    for (int i = 0; i < n - 1; i++)
                    {
                        int a = i * 2, b = a + 2;
                        ct[c++] = a; ct[c++] = a + 1; ct[c++] = b;
                        ct[c++] = a + 1; ct[c++] = b + 1; ct[c++] = b;
                    }
                    _crestMesh.normals = _crestNormals;
                    _crestMesh.uv = _crestUvs;
                    _crestMesh.triangles = ct;
                }
                _topology = true;
            }
            else
            {
                _mesh.vertices = _verts;
                _mesh.normals = _normals;
                _mesh.uv = _uvs;
                if (_crestMesh != null)
                {
                    _crestMesh.vertices = _crestVerts;
                    _crestMesh.normals = _crestNormals;
                    _crestMesh.uv = _crestUvs;
                }
            }
            _mesh.RecalculateBounds();
            if (_crestMesh != null) _crestMesh.RecalculateBounds();
        }

        /// <summary>
        /// Stylised water-dragon head at unit scale (pivot at the neck, facing +Z): skull, long snout, hinged open jaw,
        /// swept horns, dorsal crest blades, cheek fins and two glowing eyes. Reads as a serpent in silhouette.
        /// </summary>
        private static Transform BuildHead(Transform parent, Material body, Material foam, Material eyes, bool detailed, out Transform jaw)
        {
            var head = new GameObject("SerpentHead").transform;
            head.SetParent(parent, false);
            head.gameObject.layer = parent.gameObject.layer;
            var sphere = ProceduralMeshes.Primitive(PrimitiveType.Sphere);
            var cone = ProceduralMeshes.Cone(8, 0.25f);
            var blade = ProceduralMeshes.Cone(4, 0.4f);

            Part(head, "Skull", sphere, body, new Vector3(0f, 0.06f, 0.3f), Quaternion.identity, new Vector3(0.78f, 0.6f, 1.05f));
            Part(head, "Snout", sphere, body, new Vector3(0f, 0.02f, 0.92f), Quaternion.identity, new Vector3(0.46f, 0.34f, 0.95f));
            Part(head, "Brow", sphere, foam, new Vector3(0f, 0.24f, 0.6f), Quaternion.Euler(-12f, 0f, 0f), new Vector3(0.62f, 0.14f, 0.55f));

            var hinge = new GameObject("JawHinge").transform;
            hinge.SetParent(head, false);
            hinge.localPosition = new Vector3(0f, -0.1f, 0.3f);
            hinge.gameObject.layer = head.gameObject.layer;
            Part(hinge, "Jaw", sphere, body, new Vector3(0f, -0.06f, 0.48f), Quaternion.identity, new Vector3(0.42f, 0.18f, 0.9f));
            jaw = hinge;

            for (int s = -1; s <= 1; s += 2)
            {
                // Swept horns backward and up.
                Part(head, "Horn", cone, foam, new Vector3(0.24f * s, 0.3f, 0.18f),
                    Quaternion.FromToRotation(Vector3.up, new Vector3(0.35f * s, 0.55f, -1f).normalized), new Vector3(0.13f, 0.95f, 0.13f));
                Part(head, "Eye", sphere, eyes, new Vector3(0.24f * s, 0.2f, 0.66f), Quaternion.identity, Vector3.one * 0.13f);
                if (detailed)
                    Part(head, "CheekFin", blade, foam, new Vector3(0.36f * s, -0.02f, 0.2f),
                        Quaternion.FromToRotation(Vector3.up, new Vector3(1f * s, 0.15f, -0.8f).normalized), new Vector3(0.06f, 0.6f, 0.26f));
            }
            if (detailed)
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = 0.2f - i * 0.3f;
                    float size = 0.75f - i * 0.18f;
                    Part(head, "CrestBlade", blade, foam, new Vector3(0f, 0.34f - i * 0.04f, z),
                        Quaternion.FromToRotation(Vector3.up, new Vector3(0f, 1f, -0.9f).normalized), new Vector3(0.05f, size, 0.3f));
                }
            }
            return head;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = ProceduralMeshes.CreatePart(name, mesh, material, parent, pos, rot, scale);
            go.layer = parent.gameObject.layer;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    /// <summary>
    /// Stylised anime foam: white foam ribbons riding crests and edges, brush-like flecks on impacts and a foam ring
    /// on the ground. Few pieces, big shapes — never a flood of round particles.
    /// </summary>
    public static class AnimeFoam
    {
        /// <summary>A thin foam ribbon that follows a water path slightly above it, a beat later.</summary>
        public static WaterRibbonRenderer Crest(VfxBuild b, string name, Func<float, Vector3> path, Vector3 offset, float width, float delay, float grow, float hold, float fade, Vector3 planeNormal)
        {
            if (!VFXQuality.Secondary) return null;
            var r = b.WaterRibbon(name, MaterialFactory.TidalWater(MaterialFactory.WaterLook.Foam, false), u => path(u) + offset, width, grow, hold, fade);
            r.StartDelay = delay;
            r.Thickness = 0.1f;
            r.PlaneNormal = planeNormal;
            r.WidthProfile = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(0.8f, 0.7f), new Keyframe(1f, 0f));
            return r;
        }

        /// <summary>Brush-like foam flecks (stretched, drag-slowed, short-lived).</summary>
        public static void Flecks(VfxBuild b, string name, Vector3 pos, int count, float radius, float speed, float delay = 0f)
        {
            b.Particles(name, b.Additive(ProceduralTextures.Streak), pos).Burst(VFXQuality.Count(count)).Delay(delay)
                .Shape(ParticleSystemShapeType.Sphere, radius).Life(0.25f, 0.5f).Speed(speed * 0.5f, speed).Size(0.12f, 0.26f)
                .Color(new Color(1.6f, 1.8f, 2f, 0.9f)).Drag(0.35f).Gravity(0.8f).Stretch(0.8f, 0.04f).Fade(0.01f, 0.45f);
        }

        /// <summary>Foam ring spreading on the ground plus flecks: every water impact.</summary>
        public static void Impact(VfxBuild b, string name, Vector3 pos, float radius, float delay = 0f)
        {
            var ring = b.WaterRibbon(name + "Ring", MaterialFactory.TidalWater(MaterialFactory.WaterLook.Foam, false), WaterSpline.Ring(radius, 0.06f, 1f, 0f, 0f), radius * 0.35f, 0.18f, 0.1f, 0.35f);
            ring.transform.localPosition = pos;
            ring.StartDelay = delay;
            ring.Thickness = 0.05f;
            ring.WidthProfile = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            if (VFXQuality.Secondary) Flecks(b, name + "Flecks", pos + Vector3.up * 0.3f, 14, radius * 0.4f, 6f, delay);
        }
    }
}
