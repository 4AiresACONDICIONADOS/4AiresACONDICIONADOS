// Breath of Eclipse — anime character shading for URP (imported characters: player, demons, boss).
// Base texture × color, two shadow bands (or a ramp texture), stepped rim, stylized specular, emission,
// face flattening, procedural demon markings (UV veins, driven by _MarkColor), inverted-hull outline that
// follows skinned normals, hit flash, dissolve and anime flash frames. Same property names as ToonLit so
// CharacterRig's MaterialPropertyBlocks drive both.
Shader "BreathOfEclipse/AnimeCharacterToon"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Detail (multiplied)", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.42, 0.4, 0.58, 1)
        _ShadeThreshold ("Shade Threshold", Range(0, 1)) = 0.42
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.035
        _Shade2Color ("Dark Shadow Band", Color) = (0.24, 0.2, 0.36, 1)
        _Shade2Threshold ("Dark Band Threshold", Range(0, 1)) = 0.16
        [NoScaleOffset] _RampMap ("Ramp (optional, x = half lambert)", 2D) = "white" {}
        [ToggleUI] _UseRamp ("Use Ramp", Float) = 0
        _FaceFlatten ("Face Flatten (softer shading)", Range(0, 1)) = 0
        [HDR] _RimColor ("Rim Color", Color) = (0.2, 0.23, 0.35, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimThreshold ("Rim Threshold", Range(0.05, 0.9)) = 0.26
        [HDR] _SpecColor ("Stylized Specular", Color) = (0, 0, 0, 1)
        _SpecSize ("Specular Size", Range(0, 1)) = 0.08
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [HDR] _MarkColor ("Demon Markings (emissive, black = off)", Color) = (0, 0, 0, 1)
        _MarkScale ("Markings Scale (UV)", Range(1, 40)) = 9
        _OutlineColor ("Outline Color", Color) = (0.03, 0.02, 0.05, 1)
        _OutlineWidth ("Outline Width (px at 1080p, 0 = off)", Range(0, 6)) = 1.4
        _OutlineRadial ("Outline Radial Blend (0 for skinned characters)", Range(0, 1)) = 0
        [ToggleUI] _IsCharacter ("Is Character (stays lit in flash frames)", Float) = 0
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        [HDR] _HitFlashColor ("Hit Flash Color", Color) = (1, 1, 1, 1)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        [HDR] _DissolveColor ("Dissolve Edge", Color) = (3, 0.4, 0.8, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _ShadeSoftness;
            half4 _Shade2Color;
            half _Shade2Threshold;
            half _UseRamp;
            half _FaceFlatten;
            half _RimThreshold;
            half4 _MarkColor;
            float _MarkScale;
            float _OutlineRadial;
            half4 _RimColor;
            half _RimPower;
            half4 _SpecColor;
            half _SpecSize;
            half4 _EmissionColor;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _IsCharacter;
            half _HitFlash;
            half4 _HitFlashColor;
            half _Dissolve;
            half4 _DissolveColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_RampMap);
        SAMPLER(sampler_RampMap);

        #include "BoECommon.hlsl"
        ENDHLSL

        // ------------------------------------------------------------------ lit color
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                half3 vertexLight : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                output.vertexLight = VertexLighting(pos.positionWS, nrm.normalWS);
                #endif
                return output;
            }

            half3 ToonAdditionalLight(Light light, float3 normalWS, half3 baseColor)
            {
                half ndl = saturate(dot(normalWS, light.direction));
                half atten = light.distanceAttenuation * light.shadowAttenuation;
                // Stepped band so lanterns and technique lights keep the cel look.
                half band = smoothstep(0.0, 0.12, ndl) * 0.8 + ndl * 0.2;
                return baseColor * light.color * atten * band;
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float dissolveEdge = BoEDissolve(input.positionOS, _Dissolve);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 baseColor = _BaseColor.rgb * detail;
                half3 shadeColor = _ShadeColor.rgb * detail;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);

                // Two-step ramp: half lambert thresholded, shadows stepped too.
                half ndl = dot(normalWS, mainLight.direction);
                half halfLambert = ndl * 0.5h + 0.5h;
                half shadowRamp = smoothstep(0.35h, 0.65h, mainLight.shadowAttenuation);
                // Faces: softer, mostly lit (anime faces carry little shading).
                half lit = lerp(halfLambert * lerp(0.6h, 1.0h, shadowRamp), 0.5h + halfLambert * 0.5h, _FaceFlatten);
                half lightRamp = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, lit);
                half darkRamp = smoothstep(_Shade2Threshold - _ShadeSoftness, _Shade2Threshold + _ShadeSoftness, lit);
                half ramp = lightRamp * lerp(1.0h, shadowRamp, 1.0h - _FaceFlatten * 0.7h) * ao.directAmbientOcclusion;

                half3 ambient = SampleSH(normalWS) * ao.indirectAmbientOcclusion;
                half3 lightColor = mainLight.color * mainLight.distanceAttenuation;
                half3 litSide = baseColor * (lightColor + ambient);
                half3 shadeSide = shadeColor * (lightColor * 0.3h + ambient);
                half3 darkSide = _Shade2Color.rgb * detail * (lightColor * 0.15h + ambient);
                half3 color = lerp(lerp(darkSide, shadeSide, darkRamp), litSide, ramp);
                if (_UseRamp > 0.5h)
                {
                    half3 rampColor = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(saturate(lit), 0.5)).rgb;
                    color = baseColor * rampColor * (lightColor + ambient);
                }

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    Light dirLight = GetAdditionalLight(dirIndex, input.positionWS, half4(1, 1, 1, 1));
                    color += ToonAdditionalLight(dirLight, normalWS, baseColor);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    color += ToonAdditionalLight(light, normalWS, baseColor);
                LIGHT_LOOP_END
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                color += baseColor * input.vertexLight;
                #endif

                // Rim (stepped) — brighter on the lit side, always a little present to separate silhouettes at night.
                half ndv = saturate(dot(normalWS, viewDirWS));
                half rimRaw = pow(1.0h - ndv, _RimPower);
                half rimStep = smoothstep(_RimThreshold - 0.04h, _RimThreshold + 0.04h, rimRaw);
                color += _RimColor.rgb * rimStep * (0.35h + 0.65h * lightRamp);

                // Stylized specular: a hard-edged highlight.
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half ndh = saturate(dot(normalWS, halfDir));
                half spec = smoothstep(1.0h - _SpecSize, 1.0h - _SpecSize + 0.012h, ndh) * ramp;
                color += _SpecColor.rgb * spec * lightColor;

                color += _EmissionColor.rgb;

                // Demon markings: thin glowing veins in UV space (stable on skinned meshes), in patches.
                if (_MarkColor.r + _MarkColor.g + _MarkColor.b > 0.001h)
                {
                    float2 muv = input.uv * _MarkScale;
                    float n = BoEFbm3(float3(muv, 0.37));
                    float vein = 1.0 - smoothstep(0.0, 0.035, abs(n - 0.5));
                    float patch = smoothstep(0.45, 0.62, BoEValueNoise3(float3(input.uv * 3.1, 5.3)));
                    color += _MarkColor.rgb * vein * patch;
                }
                color += _DissolveColor.rgb * dissolveEdge;
                color = lerp(color, _HitFlashColor.rgb, saturate(_HitFlash));
                color = MixFog(color, input.fogFactor);

                // Anime flash frames (1-3 frames): re-light the world for impact readability.
                if (_BoE_FlashFrame > 0.5)
                {
                    half isCharacter = step(0.5h, _IsCharacter);
                    half3 flashColor = _BoE_FlashColor.rgb;
                    if (_BoE_FlashMode < 0.5)
                    {
                        half3 characterColor = lerp(shadeColor, baseColor, lightRamp) * 1.35h + flashColor * rimStep * 2.0h + _EmissionColor.rgb;
                        half3 background = flashColor * 0.05h * (0.5h + 0.5h * lightRamp);
                        color = lerp(background, characterColor, isCharacter);
                    }
                    else
                    {
                        color = lerp(flashColor * (0.95h + 0.2h * lightRamp), half3(0, 0, 0), isCharacter);
                    }
                }
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ outline (inverted hull)
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionOS = input.positionOS.xyz;

                if (_OutlineWidth <= 0.0)
                {
                    // Degenerate triangle: nothing is rasterized.
                    output.positionCS = float4(0, 0, 0, 1);
                    return output;
                }

                // Skinned characters use their smooth normals (radial blend 0); props can close hard edges.
                float3 radial = input.positionOS.xyz;
                float radialLen = length(radial);
                float3 normalOS = radialLen > 1e-4 ? normalize(lerp(input.normalOS, radial / radialLen, _OutlineRadial)) : input.normalOS;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS);
                float2 dir = normalCS.xy;
                float dirLen = length(dir);
                dir = dirLen > 1e-5 ? dir / dirLen : float2(0, 0);

                // Width in 1080p pixels, thinner with distance and gone far away (distance-dependent outline).
                float dist = distance(positionWS, GetCameraPositionWS());
                float distanceScale = lerp(1.0, 0.35, saturate((dist - 4.0) / 56.0)) * (1.0 - saturate((dist - 70.0) / 30.0));
                float pixels = _OutlineWidth * distanceScale * (_ScreenParams.y / 1080.0);
                positionCS.xy += dir * pixels * 2.0 / _ScreenParams.xy * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                BoEDissolve(input.positionOS, _Dissolve);
                half3 color = _OutlineColor.rgb;
                color = lerp(color, _HitFlashColor.rgb * 0.5h, saturate(_HitFlash) * 0.5h);
                color = MixFog(color, input.fogFactor);
                if (_BoE_FlashFrame > 0.5)
                    color = (_BoE_FlashMode > 0.5 && _IsCharacter < 0.5) ? _BoE_FlashColor.rgb * 0.6h : half3(0, 0, 0);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                BoEDissolve(input.positionOS, _Dissolve);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth prepass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                BoEDissolve(input.positionOS, _Dissolve);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ depth + normals (SSAO)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                BoEDissolve(input.positionOS, _Dissolve);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
