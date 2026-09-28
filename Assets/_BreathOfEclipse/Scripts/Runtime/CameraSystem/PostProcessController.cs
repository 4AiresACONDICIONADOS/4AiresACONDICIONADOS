using System.Collections.Generic;
using BreathOfEclipse.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BreathOfEclipse.CameraSystem
{
    /// <summary>
    /// Runtime URP post-processing: a global Volume built in code (HDR bloom, ACES, cool night grading, vignette,
    /// controlled motion blur, cinematic depth of field) plus short-lived combat pulses (saturation dips,
    /// chromatic aberration, lens distortion, bloom boosts) driven by techniques.
    /// </summary>
    public sealed class PostProcessController : MonoBehaviour
    {
        public static PostProcessController Instance { get; private set; }

        private struct Hold
        {
            public float Value;
            public float End;
        }

        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private ColorAdjustments _color;
        private Vignette _vignette;
        private ChromaticAberration _chromatic;
        private LensDistortion _lens;
        private MotionBlur _motionBlur;
        private DepthOfField _dof;
        private WhiteBalance _whiteBalance;
        private Tonemapping _tonemapping;

        private readonly Dictionary<string, Hold> _saturationHolds = new Dictionary<string, Hold>();
        private float _chromaticPulse, _lensPulse, _bloomPulse, _vignettePulse, _exposurePulse;
        private float _motionBlurPulse;
        private float _cinematicDof;
        private float _dofFocus = 5f;

        [Header("Base grading")]
        public float baseBloom = 0.85f;
        public float baseSaturation = 10f;
        public float baseContrast = 14f;
        public float baseExposure = 0.15f;
        public float baseVignette = 0.24f;
        public float baseTemperature = -10f;
        public Color colorFilter = new Color(0.94f, 0.97f, 1.05f);
        public float baseMotionBlur = 0.12f;

        private void Awake()
        {
            Instance = this;
            BuildVolume();
            SaveSystem.SettingsChanged += ApplySettings;
            ApplySettings(SaveSystem.Settings);
        }

        private void OnDestroy()
        {
            SaveSystem.SettingsChanged -= ApplySettings;
            if (Instance == this) Instance = null;
            if (_profile != null) Destroy(_profile);
        }

        private void BuildVolume()
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "BoE_RuntimePostProcess";
            _volume.sharedProfile = _profile;

            _tonemapping = _profile.Add<Tonemapping>(true);
            _tonemapping.mode.Override(TonemappingMode.ACES);

            _bloom = _profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.95f);
            _bloom.intensity.Override(baseBloom);
            _bloom.scatter.Override(0.72f);
            _bloom.tint.Override(new Color(0.95f, 0.97f, 1f));
            _bloom.highQualityFiltering.Override(true);

            _color = _profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(baseExposure);
            _color.contrast.Override(baseContrast);
            _color.saturation.Override(baseSaturation);
            _color.colorFilter.Override(colorFilter);
            _color.hueShift.Override(0f);

            _whiteBalance = _profile.Add<WhiteBalance>(true);
            _whiteBalance.temperature.Override(baseTemperature);
            _whiteBalance.tint.Override(4f);

            _vignette = _profile.Add<Vignette>(true);
            _vignette.intensity.Override(baseVignette);
            _vignette.smoothness.Override(0.42f);
            _vignette.color.Override(new Color(0.02f, 0.01f, 0.05f));

            _chromatic = _profile.Add<ChromaticAberration>(true);
            _chromatic.intensity.Override(0f);

            _lens = _profile.Add<LensDistortion>(true);
            _lens.intensity.Override(0f);
            _lens.scale.Override(1f);

            _motionBlur = _profile.Add<MotionBlur>(true);
            _motionBlur.mode.Override(MotionBlurMode.CameraOnly);
            _motionBlur.quality.Override(MotionBlurQuality.Medium);
            _motionBlur.intensity.Override(baseMotionBlur);
            _motionBlur.clamp.Override(0.04f);

            _dof = _profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(40f);
            _dof.gaussianEnd.Override(120f);
            _dof.gaussianMaxRadius.Override(1f);
            _dof.focusDistance.Override(5f);
            _dof.aperture.Override(3.2f);
            _dof.focalLength.Override(60f);
        }

        private bool _motionBlurEnabled = true;
        private GraphicsQuality _quality = GraphicsQuality.High;

        private void ApplySettings(GameSettings s)
        {
            _motionBlurEnabled = s.motionBlur;
            _quality = s.Quality;
            if (_bloom != null) _bloom.highQualityFiltering.Override(_quality >= GraphicsQuality.High);
            if (_motionBlur != null) _motionBlur.quality.Override(_quality >= GraphicsQuality.Ultra ? MotionBlurQuality.High : MotionBlurQuality.Medium);
        }

        // ------------------------------------------------------------------ requests

        /// <summary>Holds a saturation offset (e.g. -60 while Flash Breaker charges) until released or expired.</summary>
        public void HoldSaturation(string id, float value, float duration)
        {
            _saturationHolds[id] = new Hold { Value = value, End = Time.unscaledTime + duration };
        }

        public void ReleaseSaturation(string id) => _saturationHolds.Remove(id);

        public void PulseChromatic(float amount) => _chromaticPulse = Mathf.Max(_chromaticPulse, amount);
        public void PulseLensDistortion(float amount) => _lensPulse = Mathf.Abs(amount) > Mathf.Abs(_lensPulse) ? amount : _lensPulse;
        public void PulseBloom(float amount) => _bloomPulse = Mathf.Max(_bloomPulse, amount);
        public void PulseVignette(float amount) => _vignettePulse = Mathf.Max(_vignettePulse, amount);
        public void PulseExposure(float amount) => _exposurePulse = Mathf.Abs(amount) > Mathf.Abs(_exposurePulse) ? amount : _exposurePulse;
        public void PulseMotionBlur(float amount) => _motionBlurPulse = Mathf.Max(_motionBlurPulse, amount);

        /// <summary>Cinematic bokeh depth of field focused on a distance (ultimates). 0 = off.</summary>
        public void SetCinematicDof(float weight, float focusDistance)
        {
            _cinematicDof = Mathf.Clamp01(weight);
            _dofFocus = Mathf.Max(0.5f, focusDistance);
        }

        public void ResetTransient()
        {
            _saturationHolds.Clear();
            _chromaticPulse = _lensPulse = _bloomPulse = _vignettePulse = _exposurePulse = _motionBlurPulse = 0f;
            _cinematicDof = 0f;
        }

        private readonly List<string> _expired = new List<string>();

        private void Update()
        {
            if (_profile == null) return;
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            float satOffset = 0f;
            _expired.Clear();
            foreach (var kv in _saturationHolds)
            {
                if (now > kv.Value.End) _expired.Add(kv.Key);
                else satOffset = Mathf.Min(satOffset, kv.Value.Value);
            }
            foreach (var k in _expired) _saturationHolds.Remove(k);

            _chromaticPulse = Mathf.MoveTowards(_chromaticPulse, 0f, dt * 2.5f);
            _lensPulse = Mathf.MoveTowards(_lensPulse, 0f, dt * 2.2f);
            _bloomPulse = Mathf.MoveTowards(_bloomPulse, 0f, dt * 5f);
            _vignettePulse = Mathf.MoveTowards(_vignettePulse, 0f, dt * 1.5f);
            _exposurePulse = Mathf.MoveTowards(_exposurePulse, 0f, dt * 4f);
            _motionBlurPulse = Mathf.MoveTowards(_motionBlurPulse, 0f, dt * 2f);

            _color.saturation.value = Mathf.Clamp(baseSaturation + satOffset, -100f, 100f);
            _color.postExposure.value = baseExposure + _exposurePulse;
            _chromatic.intensity.value = Mathf.Clamp01(0.04f + _chromaticPulse);
            _lens.intensity.value = Mathf.Clamp(_lensPulse, -0.8f, 0.8f);
            _bloom.intensity.value = baseBloom + _bloomPulse;
            _vignette.intensity.value = Mathf.Clamp01(baseVignette + _vignettePulse);
            bool blur = _motionBlurEnabled && _quality >= GraphicsQuality.Medium;
            _motionBlur.intensity.value = blur ? Mathf.Clamp01(baseMotionBlur + _motionBlurPulse) : 0f;
            _motionBlur.active = blur;

            if (_cinematicDof > 0.01f && _quality >= GraphicsQuality.Medium)
            {
                _dof.mode.value = DepthOfFieldMode.Bokeh;
                _dof.focusDistance.value = _dofFocus;
                _dof.aperture.value = Mathf.Lerp(16f, 2.2f, _cinematicDof);
                _dof.active = true;
            }
            else
            {
                _dof.mode.value = DepthOfFieldMode.Gaussian;
                _dof.active = _quality >= GraphicsQuality.High;
            }
        }
    }
}
