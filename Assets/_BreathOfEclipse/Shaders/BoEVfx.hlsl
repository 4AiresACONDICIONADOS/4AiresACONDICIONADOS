#ifndef BOE_VFX_INCLUDED
#define BOE_VFX_INCLUDED

// Shared particle / sprite shading for VFXAdditive and VFXAlphaBlend.
// Vertex color x texture x tint, soft particles against the depth texture, fog that fades additive
// effects to black (so they never glow through fog as fog-colored cards).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _TintColor;
    half _SoftFade;
    half _Opacity;
    half _SrcBlend;
    half _DstBlend;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

struct VfxAttributes
{
    float4 positionOS : POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VfxVaryings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float4 screenPos : TEXCOORD1;
    half fogFactor : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

VfxVaryings VfxVert(VfxAttributes input)
{
    VfxVaryings output = (VfxVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.color = input.color;
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.screenPos = ComputeScreenPos(output.positionCS);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

half4 VfxFrag(VfxVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    half4 color = tex * input.color * _TintColor;
    color.a = saturate(color.a * _Opacity);

    if (_SoftFade > 0.001)
    {
        float2 screenUV = input.screenPos.xy / input.screenPos.w;
        float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
        float fragEye = input.screenPos.w;
        color.a *= saturate((sceneEye - fragEye) / _SoftFade);
    }

    // BlendMode.One == 1 as destination factor means additive.
    bool additive = abs(_DstBlend - 1.0) < 0.5;
    // BlendMode.OneMinusDstColor == 4 (soft additive) ignores source alpha: premultiply here.
    if (abs(_SrcBlend - 4.0) < 0.5)
        color.rgb *= color.a;

    if (additive)
        color.rgb = MixFogColor(color.rgb, half3(0, 0, 0), input.fogFactor);
    else
        color.rgb = MixFog(color.rgb, input.fogFactor);
    return color;
}

#endif
