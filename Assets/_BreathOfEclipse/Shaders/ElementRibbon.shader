// Breath of Eclipse — element ribbons / tubes: water serpents, fire arcs, wind spirals, moon crescents.
// UV.x runs tail (0) -> head (1) along the tube, UV.y around it. Scrolling noise gives the flowing body,
// fresnel keeps a bright rim, and the tail breaks up as the effect dissipates.
Shader "BreathOfEclipse/ElementRibbon"
{
    Properties
    {
        [HDR] _ColorA ("Core Color", Color) = (0.4, 0.8, 1.6, 1)
        [HDR] _ColorB ("Edge Color", Color) = (1.5, 2.2, 3, 1)
        _NoiseTex ("Flow Noise", 2D) = "gray" {}
        _ScrollSpeed ("Scroll Speed", Float) = 1.5
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2
        _Opacity ("Opacity", Range(0, 1)) = 1
        _T ("Age (set by script)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Ribbon"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RibbonVert
            #pragma fragment RibbonFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                float4 _NoiseTex_ST;
                float _ScrollSpeed;
                half _FresnelPower;
                half _Opacity;
                float _T;
                half _SrcBlend;
                half _DstBlend;
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
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RibbonVert(Attributes input)
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
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 RibbonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), _FresnelPower);

                float2 flowUV = float2(input.uv.x * 3.0 - _T * _ScrollSpeed, input.uv.y + input.uv.x * 0.5);
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUV * 2.1 + float2(_T * 0.7, 0.3)).r;
                half flow = n1 * 0.65h + n2 * 0.35h;

                half3 color = lerp(_ColorA.rgb, _ColorB.rgb, saturate(fresnel * 1.2h + (flow - 0.5h) * 0.6h));
                half streak = smoothstep(0.62h, 0.8h, flow);
                color += _ColorB.rgb * streak * 0.6h;
                color *= 1.0h + smoothstep(0.85h, 1.0h, input.uv.x) * 0.6h;

                half tailFade = smoothstep(0.0h, 0.25h, input.uv.x);
                half ragged = smoothstep(0.0h, 0.3h, flow + input.uv.x * 0.5h);
                half alpha = saturate((0.35h + fresnel * 0.9h + streak * 0.4h) * tailFade * ragged) * _Opacity;

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
