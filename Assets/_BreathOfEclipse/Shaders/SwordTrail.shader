// Breath of Eclipse — sword trail. UV.x = age (0 new -> 1 old), UV.y = blade base (0) -> tip (1).
// Gradient over age, scrolling noise, dissolve as the trail ages, bright emissive tip line.
Shader "BreathOfEclipse/SwordTrail"
{
    Properties
    {
        _GradientTex ("Gradient (age)", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Intensity ("Intensity", Range(0, 4)) = 1
        _NoiseScroll ("Noise Scroll", Float) = 1.5
        _Dissolve ("Dissolve Strength", Range(0, 2)) = 1.1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Trail"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TrailVert
            #pragma fragment TrailFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GradientTex_ST;
                float4 _NoiseTex_ST;
                half _Intensity;
                float _NoiseScroll;
                half _Dissolve;
                half _SrcBlend;
                half _DstBlend;
            CBUFFER_END

            TEXTURE2D(_GradientTex);
            SAMPLER(sampler_GradientTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings TrailVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 TrailFrag(Varyings input) : SV_Target
            {
                half age = saturate(input.uv.x);
                half along = saturate(input.uv.y);
                half4 gradient = SAMPLE_TEXTURE2D(_GradientTex, sampler_GradientTex, float2(age, 0.5));
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(age * 2.0 - _Time.y * _NoiseScroll, along * 0.8)).r;

                half threshold = age * _Dissolve;
                half dissolve = smoothstep(threshold - 0.05h, threshold + 0.05h, noise + 0.25h);
                half edge = smoothstep(0.55h, 1.0h, along);
                half baseFade = smoothstep(0.0h, 0.25h, along);

                half3 color = gradient.rgb * (0.6h + edge * 1.6h) * max(_Intensity, 0.0h);
                color += gradient.rgb * smoothstep(0.9h, 1.0h, along) * 1.5h * (1.0h - age);
                half alpha = saturate(gradient.a * input.color.a * baseFade * dissolve * (0.5h + edge * 0.5h));

                if (abs(_SrcBlend - 4.0) < 0.5)
                    color *= alpha;
                bool additive = abs(_DstBlend - 1.0) < 0.5;
                color = additive ? MixFogColor(color, half3(0, 0, 0), input.fogFactor) : MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
