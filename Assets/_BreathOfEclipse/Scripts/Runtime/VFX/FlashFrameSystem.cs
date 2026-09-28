using BreathOfEclipse.Core;
using BreathOfEclipse.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace BreathOfEclipse.VFX
{
    public enum FlashFrameStyle
    {
        /// <summary>Background goes dark, characters stay lit, radial lines + impact shape.</summary>
        DarkBackground = 0,
        /// <summary>Black silhouettes over a bright element-colored background.</summary>
        Silhouette = 1
    }

    /// <summary>
    /// Anime flash frames: for 1-3 rendered frames the world is re-lit (via global shader values read by the toon
    /// shaders) and an overlay draws speed lines converging on the impact. Reserved for criticals, finishers,
    /// parries, ultimates and special techniques. Can be disabled in settings.
    /// </summary>
    public sealed class FlashFrameSystem : MonoBehaviour
    {
        public static FlashFrameSystem Instance { get; private set; }

        private RawImage _burst;
        private Material _burstMaterial;
        private int _endFrame = -1;
        private float _cooldownUntil;

        public static void Create(Transform parent)
        {
            if (Instance != null) return;
            var go = new GameObject("FlashFrames", typeof(Canvas));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var sys = go.AddComponent<FlashFrameSystem>();
            var img = new GameObject("Burst", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(go.transform, false);
            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            sys._burst = img.GetComponent<RawImage>();
            sys._burst.raycastTarget = false;
            var shader = MaterialFactory.FindShader(ShaderIds.UIRadialBurst);
            sys._burstMaterial = new Material(shader) { name = "BoE_FlashBurst" };
            sys._burst.material = sys._burstMaterial;
            sys._burst.enabled = false;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ResetGlobals();
        }

        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat(ShaderIds.GlobalFlashFrame, 0f);
            Shader.SetGlobalFloat(ShaderIds.GlobalFlashMode, 0f);
        }

        /// <summary>Triggers a flash frame centered on a world point.</summary>
        public static void Trigger(Vector3 worldPoint, Color color, int frames = 2, FlashFrameStyle style = FlashFrameStyle.DarkBackground)
        {
            if (Instance == null || !SaveSystem.Settings.flashFramesEnabled) return;
            Instance.Play(worldPoint, color, frames, style);
        }

        private void Play(Vector3 worldPoint, Color color, int frames, FlashFrameStyle style)
        {
            // Never chain flash frames back to back: they must stay special.
            if (Time.unscaledTime < _cooldownUntil) return;
            _cooldownUntil = Time.unscaledTime + 0.25f;
            _endFrame = Time.frameCount + Mathf.Clamp(frames, 1, 3);
            Vector2 center = new Vector2(0.5f, 0.5f);
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(worldPoint);
                if (vp.z > 0f) center = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
            }
            float m = Mathf.Max(1f, color.maxColorComponent);
            Color ldr = new Color(color.r / m, color.g / m, color.b / m, 1f);
            _burstMaterial.SetVector(ShaderIds.Center, new Vector4(center.x, center.y, 0f, 0f));
            _burstMaterial.SetColor(ShaderIds.BaseColor, ldr);
            _burstMaterial.SetFloat(ShaderIds.Time01, Random.value * 100f);
            _burstMaterial.SetFloat(ShaderIds.Strength, style == FlashFrameStyle.Silhouette ? 1f : 0f);
            _burst.enabled = true;
            Shader.SetGlobalFloat(ShaderIds.GlobalFlashFrame, 1f);
            Shader.SetGlobalFloat(ShaderIds.GlobalFlashMode, style == FlashFrameStyle.Silhouette ? 1f : 0f);
            Shader.SetGlobalColor(ShaderIds.GlobalFlashColor, ldr);
        }

        private void Update()
        {
            // Runs on frame count (not time) so it works during hit stop: frames N..N+k-1 show the flash.
            if (_endFrame >= 0 && Time.frameCount >= _endFrame)
            {
                _endFrame = -1;
                EndFlash();
            }
        }

        private void EndFlash()
        {
            _burst.enabled = false;
            ResetGlobals();
        }
    }
}
