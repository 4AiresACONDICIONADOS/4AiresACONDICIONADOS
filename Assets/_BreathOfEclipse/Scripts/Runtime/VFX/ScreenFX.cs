using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace BreathOfEclipse.VFX
{
    /// <summary>
    /// Screen-space anime overlays: radial speed lines (dashes, techniques, ultimates) and full-screen color
    /// washes (element tints, white flashes). Lives on a persistent overlay canvas.
    /// </summary>
    public sealed class ScreenFX : MonoBehaviour
    {
        public static ScreenFX Instance { get; private set; }

        private RawImage _lines;
        private Material _linesMaterial;
        private Image _wash;
        private float _linesIntensity, _linesTarget, _linesUntil;
        private Color _washColor;
        private float _washAlpha, _washDecay;

        public static void Create(Transform parent)
        {
            if (Instance != null) return;
            var go = new GameObject("ScreenFX", typeof(Canvas));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var fx = go.AddComponent<ScreenFX>();

            var lines = new GameObject("SpeedLines", typeof(RectTransform), typeof(RawImage));
            lines.transform.SetParent(go.transform, false);
            Stretch(lines.GetComponent<RectTransform>());
            fx._lines = lines.GetComponent<RawImage>();
            fx._lines.raycastTarget = false;
            fx._linesMaterial = new Material(MaterialFactory.FindShader(ShaderIds.UISpeedLines)) { name = "BoE_SpeedLines" };
            fx._lines.material = fx._linesMaterial;
            fx._lines.enabled = false;

            var wash = new GameObject("Wash", typeof(RectTransform), typeof(Image));
            wash.transform.SetParent(go.transform, false);
            Stretch(wash.GetComponent<RectTransform>());
            fx._wash = wash.GetComponent<Image>();
            fx._wash.raycastTarget = false;
            fx._wash.color = Color.clear;
            fx._wash.enabled = false;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows anime speed lines for a duration (intensity 0..1).</summary>
        public static void SpeedLines(float intensity, float duration, Color? color = null)
        {
            if (Instance == null) return;
            Instance._linesTarget = Mathf.Max(Instance._linesTarget, Mathf.Clamp01(intensity));
            Instance._linesUntil = Mathf.Max(Instance._linesUntil, Time.unscaledTime + duration);
            Color c = color ?? Color.white;
            float m = Mathf.Max(1f, c.maxColorComponent);
            Instance._linesMaterial.SetColor(ShaderIds.BaseColor, new Color(c.r / m, c.g / m, c.b / m, 1f));
        }

        /// <summary>Full-screen color flash that decays (white impact flash, element tint).</summary>
        public static void Wash(Color color, float alpha, float decayPerSecond = 3f)
        {
            if (Instance == null) return;
            float m = Mathf.Max(1f, color.maxColorComponent);
            Instance._washColor = new Color(color.r / m, color.g / m, color.b / m, 1f);
            Instance._washAlpha = Mathf.Max(Instance._washAlpha, Mathf.Clamp01(alpha));
            Instance._washDecay = decayPerSecond;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            if (now > _linesUntil) _linesTarget = 0f;
            _linesIntensity = Mathf.MoveTowards(_linesIntensity, _linesTarget, dt * (_linesTarget > _linesIntensity ? 12f : 3f));
            bool show = _linesIntensity > 0.01f;
            _lines.enabled = show;
            if (show)
            {
                _linesMaterial.SetFloat(ShaderIds.Intensity, _linesIntensity);
                _linesMaterial.SetFloat(ShaderIds.Time01, now);
            }

            if (_washAlpha > 0f)
            {
                _washAlpha = Mathf.MoveTowards(_washAlpha, 0f, dt * _washDecay);
                _wash.enabled = _washAlpha > 0.005f;
                var c = _washColor;
                c.a = _washAlpha;
                _wash.color = c;
            }
            else if (_wash.enabled) _wash.enabled = false;
        }
    }
}
