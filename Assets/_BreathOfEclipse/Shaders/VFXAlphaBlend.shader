// Breath of Eclipse — alpha-blended particles (smoke, dust, debris, petals) and ground decals (cracks, scorch).
Shader "BreathOfEclipse/VFXAlphaBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _SoftFade ("Soft Particles Distance (0 = off)", Range(0, 3)) = 0.4
        _Opacity ("Opacity", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            // Decals lie on the ground: bias toward the camera to avoid z-fighting.
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VfxVert
            #pragma fragment VfxFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include "BoEVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
