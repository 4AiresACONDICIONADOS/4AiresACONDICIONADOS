// Breath of Eclipse — procedural night sky on a camera-following sphere: gradient, stars, a large moon with
// craters and halo, moonlit wisps of cloud, horizon blended into the scene fog. Reacts to flash frames.
Shader "BreathOfEclipse/SkyDome"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.02, 0.03, 0.09, 1)
        _HorizonColor ("Horizon", Color) = (0.12, 0.13, 0.28, 1)
        [HDR] _MoonColor ("Moon", Color) = (1.6, 1.7, 2, 1)
        _MoonDir ("Moon Direction", Vector) = (0.2, 0.42, 1, 0)
        _MoonSize ("Moon Angular Radius (rad)", Range(0.01, 0.4)) = 0.08
        _NoiseTex ("Noise", 2D) = "gray" {}
        _StarDensity ("Star Density", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkyVert
            #pragma fragment SkyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _MoonColor;
                float4 _MoonDir;
                float _MoonSize;
                float4 _NoiseTex_ST;
                half _StarDensity;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            #include "BoECommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS : TEXCOORD0;
            };

            Varyings SkyVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.viewDirWS = positionWS - GetCameraPositionWS();
                return output;
            }

            half Stars(float3 dir)
            {
                // Stars on a spherical grid; each cell may hold one star with its own size and twinkle.
                float2 sph = float2(atan2(dir.x, dir.z), asin(clamp(dir.y, -1.0, 1.0)));
                float2 grid = sph * float2(95.0, 95.0);
                float2 cell = floor(grid);
                float h = BoEHash21(cell);
                if (h < 1.0 - 0.03 * _StarDensity * 2.0)
                    return 0.0;
                float2 offset = float2(BoEHash21(cell + 17.3), BoEHash21(cell + 41.7)) * 0.6 + 0.2;
                float d = length(frac(grid) - offset);
                float size = lerp(0.05, 0.14, BoEHash21(cell + 3.1));
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 4.0) + h * 40.0);
                return smoothstep(size, 0.0, d) * twinkle;
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDirWS);
                float h = dir.y;
                float3 moonDir = normalize(_MoonDir.xyz);
                float md = dot(dir, moonDir);

                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(-0.05, 0.55, h));
                color = lerp(color, _HorizonColor.rgb * 0.55, smoothstep(0.0, -0.25, h));
                // Moonlit atmosphere around the moon.
                color += _MoonColor.rgb * (0.05 * pow(saturate(md), 10.0) + 0.02 * pow(saturate(md), 2.0));

                // Stars, hidden near the horizon and around the moon.
                half starMask = smoothstep(0.05, 0.3, h) * (1.0 - smoothstep(0.9, 0.99, md));
                color += Stars(dir) * starMask * half3(0.9, 0.95, 1.1) * 1.4;

                // Moon disc with craters and a soft halo.
                float angle = acos(clamp(md, -1.0, 1.0));
                half disc = smoothstep(_MoonSize, _MoonSize * 0.965, angle);
                float3 t1 = normalize(cross(moonDir, abs(moonDir.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0)));
                float3 t2 = cross(t1, moonDir);
                float3 onPlane = dir - moonDir * md;
                float2 moonUV = float2(dot(onPlane, t1), dot(onPlane, t2)) / max(_MoonSize, 1e-3);
                half crater = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, moonUV * 0.45 + 0.5).r;
                half limb = saturate(1.0 - dot(moonUV, moonUV) * 0.35);
                half3 moonColor = _MoonColor.rgb * (0.78 + 0.3 * crater) * (0.85 + 0.15 * limb);
                half halo = exp(-angle / (_MoonSize * 2.5)) * 0.45 + exp(-angle / (_MoonSize * 9.0)) * 0.12;
                color = lerp(color, moonColor, disc) + _MoonColor.rgb * halo * (1.0 - disc) * 0.35;

                // Thin moonlit clouds near the horizon.
                float2 cloudUV = dir.xz / max(h + 0.25, 0.08) * 0.08 + _Time.y * 0.002;
                half cloud = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, cloudUV).r * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, cloudUV * 2.3 + 0.1).r;
                cloud = smoothstep(0.22, 0.55, cloud) * smoothstep(-0.02, 0.25, h) * (1.0 - smoothstep(0.5, 0.9, h));
                half3 cloudLit = lerp(_HorizonColor.rgb * 0.8, _MoonColor.rgb * 0.35, pow(saturate(md * 0.5 + 0.5), 6.0));
                color = lerp(color, cloudLit, cloud * 0.6);

                // Blend the horizon into the scene fog so distant geometry melts into the sky.
                color = lerp(color, unity_FogColor.rgb, (1.0 - smoothstep(0.0, 0.22, abs(h))) * 0.65);

                if (_BoE_FlashFrame > 0.5)
                    color = _BoE_FlashMode > 0.5 ? _BoE_FlashColor.rgb : _BoE_FlashColor.rgb * 0.04;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
