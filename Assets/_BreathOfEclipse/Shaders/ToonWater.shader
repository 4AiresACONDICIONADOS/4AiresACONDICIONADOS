// Breath of Eclipse — stylized stream: depth-tinted color, stepped caustic highlights, anime foam lines at the
// shore (depth intersection), moon glint and sky fresnel. World-space UVs so any quad size tiles correctly.
Shader "BreathOfEclipse/ToonWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.18, 0.42, 0.62, 0.7)
        _DeepColor ("Deep", Color) = (0.03, 0.08, 0.2, 0.9)
        _FoamColor ("Foam", Color) = (0.85, 0.95, 1, 1)
        _NoiseTex ("Caustics / Noise", 2D) = "gray" {}
        _DepthRange ("Depth Range", Range(0.1, 5)) = 1.5
        _FoamDistance ("Foam Distance", Range(0, 1)) = 0.35
        _FlowSpeed ("Flow Speed", Vector) = (0.05, 0.02, -0.03, 0.04)
        _MoonGlint ("Moon Glint", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float4 _NoiseTex_ST;
                float _DepthRange;
                float _FoamDistance;
                float4 _FlowSpeed;
                half _MoonGlint;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            #include "BoECommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings WaterVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.screenPos = ComputeScreenPos(pos.positionCS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 WaterFrag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthDiff = max(0.0, sceneEye - input.screenPos.w);
                half deep = saturate(depthDiff / _DepthRange);

                float2 worldUV = input.positionWS.xz;
                float t = _Time.y;
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, worldUV * 0.15 + _FlowSpeed.xy * t).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, worldUV * 0.23 + _FlowSpeed.zw * t).r;

                half4 color = lerp(_ShallowColor, _DeepColor, deep);

                // Stepped caustic ripples.
                half ripple = n1 * n2;
                half rippleLine = smoothstep(0.33h, 0.37h, ripple) * (1.0h - smoothstep(0.42h, 0.46h, ripple));
                Light mainLight = GetMainLight();
                color.rgb += mainLight.color * rippleLine * 0.18h;

                // Sky fresnel.
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(viewDirWS.y), 4.0h);
                color.rgb = lerp(color.rgb, unity_FogColor.rgb * 1.3h, fresnel * 0.5h);
                color.a = saturate(color.a + fresnel * 0.2h);

                // Moon glint on a noise-perturbed normal.
                float3 normalWS = normalize(float3((n1 - 0.5) * 0.35, 1.0, (n2 - 0.5) * 0.35));
                float3 moonDir = normalize(_BoE_MoonDir.xyz + float3(0, 1e-4, 0));
                float3 reflected = reflect(-viewDirWS, normalWS);
                half glint = smoothstep(0.985h, 0.995h, dot(reflected, moonDir));
                color.rgb += mainLight.color * glint * _MoonGlint;
                color.a = saturate(color.a + glint * 0.5h);

                // Anime foam line where the water meets geometry.
                half foamEdge = _FoamDistance * (0.8h + (n1 - 0.5h) * 0.6h);
                half foam = 1.0h - smoothstep(foamEdge * 0.85h, foamEdge, depthDiff);
                color.rgb = lerp(color.rgb, _FoamColor.rgb, foam);
                color.a = max(color.a, foam * _FoamColor.a);

                color.rgb = MixFog(color.rgb, input.fogFactor);
                if (_BoE_FlashFrame > 0.5)
                    color.rgb = _BoE_FlashMode > 0.5 ? _BoE_FlashColor.rgb : _BoE_FlashColor.rgb * 0.05;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
