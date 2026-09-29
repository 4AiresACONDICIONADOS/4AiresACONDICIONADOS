// Compile-check ONLY. Minimal signatures mirrored from URP/Core RP 17.3 source so the game code can be
// type-checked outside the Unity Editor. Never copied into Assets/.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public virtual bool overrideState { get; set; } }
    public class VolumeParameter<T> : VolumeParameter
    {
        public virtual T value { get; set; }
        public void Override(T x) { overrideState = true; value = x; }
        public VolumeParameter() { }
        public VolumeParameter(T value, bool overrideState) { this.value = value; this.overrideState = overrideState; }
    }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float value, bool overrideState = false) : base(value, overrideState) { } }
    public class MinFloatParameter : FloatParameter { public float min; public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { this.min = min; } }
    public class ClampedFloatParameter : FloatParameter { public float min, max; public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { this.min = min; this.max = max; } }
    public class ClampedIntParameter : VolumeParameter<int> { public ClampedIntParameter(int value, int min, int max, bool overrideState = false) : base(value, overrideState) { } }
    public class ColorParameter : VolumeParameter<Color> { public ColorParameter(Color value, bool overrideState = false) : base(value, overrideState) { } public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false) : base(value, overrideState) { } }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool value, bool overrideState = false) : base(value, overrideState) { } }
    public class Vector2Parameter : VolumeParameter<Vector2> { public Vector2Parameter(Vector2 value, bool overrideState = false) : base(value, overrideState) { } }
    public class TextureParameter : VolumeParameter<Texture> { public TextureParameter(Texture value, bool overrideState = false) : base(value, overrideState) { } }

    public class VolumeComponent : ScriptableObject { public bool active = true; }
    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public void Remove<T>() where T : VolumeComponent { }
        public bool Has<T>() where T : VolumeComponent => false;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
    }
    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float blendDistance = 0f;
        public float weight = 1f;
        public VolumeProfile sharedProfile = null;
        public VolumeProfile profile { get; set; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public enum AntialiasingQuality { Low, Medium, High }
    public enum CameraRenderType { Base, Overlay }
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderShadows { get; set; }
        public bool requiresDepthTexture { get; set; }
        public bool requiresColorTexture { get; set; }
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; }
        public bool stopNaN { get; set; }
        public bool dithering { get; set; }
        public CameraRenderType renderType { get; set; }
    }
    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null;
    }
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public bool supportsCameraDepthTexture { get; set; }
        public bool supportsCameraOpaqueTexture { get; set; }
        public bool supportsHDR { get; set; }
        public int msaaSampleCount { get; set; }
        public float renderScale { get; set; }
        public int mainLightShadowmapResolution { get; }
        public float shadowDistance { get; set; }
        public int shadowCascadeCount { get; set; }
        protected override RenderPipeline CreatePipeline() => null;
    }

    public enum BloomFilterMode { Gaussian, Dual, Kawase }
    public sealed class BloomFilterModeParameter : VolumeParameter<BloomFilterMode> { public BloomFilterModeParameter(BloomFilterMode v, bool o = false) : base(v, o) { } }
    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
        public BoolParameter highQualityFiltering = new BoolParameter(false);
        public BloomFilterModeParameter filter = new BloomFilterModeParameter(BloomFilterMode.Gaussian);
        public ClampedIntParameter maxIterations = new ClampedIntParameter(6, 2, 8);
        public TextureParameter dirtTexture = new TextureParameter(null);
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);
    }
    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure = new FloatParameter(0f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);
        public ColorParameter colorFilter = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);
    }
    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color = new ColorParameter(Color.black, false, false, true);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
        public BoolParameter rounded = new BoolParameter(false);
    }
    public enum DepthOfFieldMode { Off, Gaussian, Bokeh }
    public sealed class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode> { public DepthOfFieldModeParameter(DepthOfFieldMode v, bool o = false) : base(v, o) { } }
    public sealed class DepthOfField : VolumeComponent
    {
        public DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.Off);
        public MinFloatParameter gaussianStart = new MinFloatParameter(10f, 0f);
        public MinFloatParameter gaussianEnd = new MinFloatParameter(30f, 0f);
        public ClampedFloatParameter gaussianMaxRadius = new ClampedFloatParameter(1f, 0.5f, 1.5f);
        public BoolParameter highQualitySampling = new BoolParameter(false);
        public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0.1f);
        public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, 1f, 32f);
        public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 1f, 300f);
    }
    public enum MotionBlurMode { CameraOnly, CameraAndObjects }
    public enum MotionBlurQuality { Low, Medium, High }
    public sealed class MotionBlurModeParameter : VolumeParameter<MotionBlurMode> { public MotionBlurModeParameter(MotionBlurMode v, bool o = false) : base(v, o) { } }
    public sealed class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality> { public MotionBlurQualityParameter(MotionBlurQuality v, bool o = false) : base(v, o) { } }
    public sealed class MotionBlur : VolumeComponent
    {
        public MotionBlurModeParameter mode = new MotionBlurModeParameter(MotionBlurMode.CameraOnly);
        public MotionBlurQualityParameter quality = new MotionBlurQualityParameter(MotionBlurQuality.Low);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter clamp = new ClampedFloatParameter(0.05f, 0f, 0.2f);
    }
    public sealed class ChromaticAberration : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); }
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { public TonemappingModeParameter(TonemappingMode v, bool o = false) : base(v, o) { } }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None); }
    public sealed class LensDistortion : VolumeComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter xMultiplier = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter yMultiplier = new ClampedFloatParameter(1f, 0f, 1f);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter scale = new ClampedFloatParameter(1f, 0.01f, 5f);
    }
    public sealed class WhiteBalance : VolumeComponent
    {
        public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100, 100f);
        public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100, 100f);
    }
    public sealed class SplitToning : VolumeComponent
    {
        public ColorParameter shadows = new ColorParameter(Color.grey);
        public ColorParameter highlights = new ColorParameter(Color.grey);
        public ClampedFloatParameter balance = new ClampedFloatParameter(0f, -100f, 100f);
    }
}
