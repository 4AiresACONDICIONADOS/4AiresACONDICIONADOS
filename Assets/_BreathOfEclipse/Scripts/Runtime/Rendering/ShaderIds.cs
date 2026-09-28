using UnityEngine;

namespace BreathOfEclipse.Rendering
{
    /// <summary>Shader names and cached property ids for the project's hand-written URP shaders.</summary>
    public static class ShaderIds
    {
        public const string ToonLit = "BreathOfEclipse/ToonLit";
        public const string VfxAdditive = "BreathOfEclipse/VFXAdditive";
        public const string VfxAlpha = "BreathOfEclipse/VFXAlphaBlend";
        public const string ElementRibbon = "BreathOfEclipse/ElementRibbon";
        public const string SwordTrail = "BreathOfEclipse/SwordTrail";
        public const string Ghost = "BreathOfEclipse/Ghost";
        public const string SkyDome = "BreathOfEclipse/SkyDome";
        public const string ToonWater = "BreathOfEclipse/ToonWater";
        public const string Distortion = "BreathOfEclipse/ScreenDistortion";
        public const string Telegraph = "BreathOfEclipse/Telegraph";
        public const string UISpeedLines = "BreathOfEclipse/UI/SpeedLines";
        public const string UIRadialBurst = "BreathOfEclipse/UI/RadialBurst";

        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int ShadeColor = Shader.PropertyToID("_ShadeColor");
        public static readonly int ShadeThreshold = Shader.PropertyToID("_ShadeThreshold");
        public static readonly int ShadeSoftness = Shader.PropertyToID("_ShadeSoftness");
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int SpecColor = Shader.PropertyToID("_SpecColor");
        public static readonly int SpecSize = Shader.PropertyToID("_SpecSize");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int IsCharacter = Shader.PropertyToID("_IsCharacter");
        public static readonly int HitFlash = Shader.PropertyToID("_HitFlash");
        public static readonly int HitFlashColor = Shader.PropertyToID("_HitFlashColor");
        public static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
        public static readonly int DissolveColor = Shader.PropertyToID("_DissolveColor");

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int TintColor = Shader.PropertyToID("_TintColor");
        public static readonly int ColorA = Shader.PropertyToID("_ColorA");
        public static readonly int ColorB = Shader.PropertyToID("_ColorB");
        public static readonly int NoiseTex = Shader.PropertyToID("_NoiseTex");
        public static readonly int Reveal = Shader.PropertyToID("_Reveal");
        public static readonly int Tail = Shader.PropertyToID("_Tail");
        public static readonly int Opacity = Shader.PropertyToID("_Opacity");
        public static readonly int ScrollSpeed = Shader.PropertyToID("_ScrollSpeed");
        public static readonly int GradientTex = Shader.PropertyToID("_GradientTex");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        public static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        public static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        public static readonly int Fill = Shader.PropertyToID("_Fill");
        public static readonly int Strength = Shader.PropertyToID("_Strength");
        public static readonly int Center = Shader.PropertyToID("_Center");
        public static readonly int Density = Shader.PropertyToID("_Density");
        public static readonly int Time01 = Shader.PropertyToID("_T");
        public static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");

        // Globals
        public static readonly int GlobalFlashFrame = Shader.PropertyToID("_BoE_FlashFrame");
        public static readonly int GlobalFlashMode = Shader.PropertyToID("_BoE_FlashMode");
        public static readonly int GlobalFlashColor = Shader.PropertyToID("_BoE_FlashColor");
        public static readonly int GlobalMoonDir = Shader.PropertyToID("_BoE_MoonDir");
    }
}
