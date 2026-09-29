// Breath of Eclipse — TIDAL BREATH water, drawn like anime but in 3D (not realistic water).
// Used by WaterRibbonRenderer (flat curved ribbons: arcs, crescents, rings, spirals) and WaterSerpentRenderer
// (tube body + head). UV.x runs tail (0) -> head (1), UV.y across the ribbon (or around the tube).
// Look: posterized deep blue -> cyan bands, hard white foam on edges and crests, sharp stylized highlight
// streaks, bright rim, emissive core. Motion: scrolling flow noise, vertex waves, head reveal, dissolve that
// eats the water from the tail with a foam rim. Soft intersection with geometry and a near-camera fade.
Shader "BreathOfEclipse/TidalWaterAnime"
{
    Properties
    {
        [HDR] _DeepColor ("Deep Color", Color) = (0.03, 0.2, 0.62, 1)
        [HDR] _MidColor ("Mid (Cyan) Color", Color) = (0.25, 0.85, 1.5, 1)
        [HDR] _FoamColor ("Foam Color", Color) = (2.2, 2.4, 2.6, 1)
        [HDR] _HighlightColor ("Highlight Color", Color) = (3, 3.4, 4, 1)
        _NoiseTex ("Flow Noise", 2D) = "gray" {}
        _FlowSpeed ("Flow Speed", Float) = 1.6
        _FlowScale ("Flow Scale (xy)", Vector) = (3, 1, 0, 0)
        _Bands ("Color Bands", Range(2, 6)) = 3
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _FoamEdge ("Edge Foam Width", Range(0, 0.6)) = 0.18
        _CrestFoam ("Crest Foam", Range(0, 1)) = 0.45
        _Emission ("Emission", Range(0, 3)) = 1
        _Reveal ("Head Reveal (0-1)", Range(0, 1.2)) = 1.2
        _Tail ("Tail Cut (0-1)", Range(0, 1)) = 0
        _Dissolve ("Dissolve (0-1)", Range(0, 1)) = 0
        _DissolveEdge ("Dissolve Foam Edge", Range(0.01, 0.3)) = 0.08
        _DepthFade ("Depth Fade (m)", Range(0, 2)) = 0.35
        _WaveAmp ("Vertex Wave Amplitude", Float) = 0.05
        _WaveFreq ("Vertex Wave Frequency", Float) = 3
        _WaveSpeed ("Vertex Wave Speed", Float) = 6
        [Toggle] _TubeMode ("Tube (edges from fresnel)", Float) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        _T ("Age (set by script)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "TidalWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _MidColor;
                half4 _FoamColor;
                half4 _HighlightColor;
                float4 _NoiseTex_ST;
                float _FlowSpeed;
                float4 _FlowScale;
                half _Bands;
                half _FresnelPower;
                half _FoamEdge;
                half _CrestFoam;
                half _Emission;
                half _Reveal;
                half _Tail;
                half _Dissolve;
                half _DissolveEdge;
                half _DepthFade;
                float _WaveAmp;
                float _WaveFreq;
                float _WaveSpeed;
                half _TubeMode;
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
                float4 screenPos : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings WaterVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Travelling swell along the length; stronger toward the head, none at the tail tip.
                float swell = sin(input.uv.x * _WaveFreq * 6.2831853 - _T * _WaveSpeed);
                float3 posOS = input.positionOS.xyz + input.normalOS * (swell * _WaveAmp * saturate(input.uv.x * 2.0));

                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(pos.positionCS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 WaterFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half ndv = saturate(abs(dot(normalWS, viewDirWS)));
                half fresnel = pow(1.0h - ndv, _FresnelPower);

                // Flow: two scrolling noise octaves, the second faster and skewed.
                float2 flowUV = float2(input.uv.x * _FlowScale.x - _T * _FlowSpeed, input.uv.y * _FlowScale.y + input.uv.x * 0.35);
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowUV * 2.3 + float2(_T * 0.9, 0.17)).r;
                half flow = n1 * 0.6h + n2 * 0.4h;

                // Anime banding: facing + flow, posterized into a few flat tones.
                half bands = max(2.0h, _Bands);
                half shade = saturate(ndv * 0.75h + (flow - 0.5h) * 0.55h + 0.2h);
                half band = saturate(floor(shade * bands) / (bands - 1.0h));
                half3 color = lerp(_DeepColor.rgb, _MidColor.rgb, band);

                // Sharp highlight streaks (ink-like), only on faces toward the camera.
                half highlight = smoothstep(0.74h, 0.79h, flow) * smoothstep(0.45h, 0.6h, ndv);
                color = lerp(color, _HighlightColor.rgb, highlight * 0.85h);

                // Edge foam (ribbon borders, or the tube silhouette) and crest foam streaks along the length.
                half edge = _TubeMode > 0.5h ? (1.0h - ndv) : abs(input.uv.y * 2.0h - 1.0h);
                half foamEdge = smoothstep(1.0h - _FoamEdge - 0.04h, 1.0h - _FoamEdge + 0.02h, edge + (flow - 0.5h) * 0.3h);
                half crestLine = 1.0h - abs(frac(input.uv.y * 2.0h + n1 * 0.4h) * 2.0h - 1.0h);
                half crest = smoothstep(0.86h, 0.93h, crestLine) * smoothstep(0.55h, 0.7h, n2) * _CrestFoam;
                half foam = saturate(max(foamEdge, crest));
                color = lerp(color, _FoamColor.rgb, foam);

                // Rim glow and emissive core.
                color += _MidColor.rgb * fresnel * 0.6h;
                color *= _Emission;

                half alpha = lerp(0.74h, 1.0h, foam);
                alpha = max(alpha, fresnel * 0.9h);

                // Growth: the head reveals along UV.x; the tail can be cut away.
                alpha *= 1.0h - smoothstep(_Reveal - 0.03h, _Reveal, input.uv.x);
                alpha *= smoothstep(_Tail, _Tail + 0.12h, input.uv.x);

                // Dissolve from the tail with a bright foam rim at the boundary.
                if (_Dissolve > 0.001h)
                {
                    half d = flow * 0.7h + input.uv.x * 0.3h;
                    half threshold = _Dissolve * 1.05h;
                    half visible = smoothstep(threshold, threshold + _DissolveEdge, d);
                    half rim = visible * (1.0h - smoothstep(threshold + _DissolveEdge, threshold + _DissolveEdge * 2.5h, d));
                    color = lerp(color, _FoamColor.rgb, rim);
                    alpha *= visible;
                }

                float fragEye = input.screenPos.w;
                if (_DepthFade > 0.001h)
                {
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    alpha *= saturate((sceneEye - fragEye) / _DepthFade);
                }
                // Near-camera fade: the water never becomes a wall in front of the lens (first person).
                alpha *= saturate((fragEye - 0.3) / 0.9);
                alpha = saturate(alpha * _Opacity);

                if (abs(_SrcBlend - 4.0) < 0.5)
                    color *= alpha;
                bool additive = abs(_DstBlend - 1.0) < 0.5;
                color = additive ? MixFogColor(color, half3(0, 0, 0), input.fogFactor) : MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
