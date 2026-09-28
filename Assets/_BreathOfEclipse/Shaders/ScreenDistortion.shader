// Breath of Eclipse — refraction bubble for shockwaves / heat haze: offsets the opaque scene color by noise and
// view-space normal, strongest at the silhouette rim. Requires "Opaque Texture" in the URP asset (enabled).
Shader "BreathOfEclipse/ScreenDistortion"
{
    Properties
    {
        [HDR] _BaseColor ("Rim Tint", Color) = (1, 1, 1, 1)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Opacity ("Strength", Range(0, 1)) = 1
        _Distortion ("Distortion Amount", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Distortion"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DistortVert
            #pragma fragment DistortFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _NoiseTex_ST;
                half _Opacity;
                half _Distortion;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DistortVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(pos.positionCS);
                return output;
            }

            half4 DistortFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), 2.5h);
                half2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * 2.0 + _Time.y * 0.5).rg - 0.5h;
                float3 normalVS = TransformWorldToViewDir(normalWS);
                half strength = rim * _Opacity;
                float2 offset = (noise * 1.2 + normalVS.xy * 0.8) * _Distortion * strength;
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 scene = SampleSceneColor(screenUV + offset);
                half3 color = scene + _BaseColor.rgb * rim * _Opacity * 0.25h;
                return half4(color, saturate(strength * 3.0h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
