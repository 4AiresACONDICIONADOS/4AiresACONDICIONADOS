// Breath of Eclipse — anime speed lines overlay (full-screen UI image). Radial streaks at the screen edges that
// flicker on "twos" like hand-drawn animation; _Intensity pulls them toward the center.
Shader "BreathOfEclipse/UI/SpeedLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0
        _T ("Time (set by script)", Float) = 0
        _LineCount ("Line Count", Range(20, 200)) = 90
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpeedLines"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex UIVert
            #pragma fragment SpeedLinesFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _Intensity;
                float _T;
                float _LineCount;
            CBUFFER_END

            #include "BoECommon.hlsl"

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
            };

            Varyings UIVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 SpeedLinesFrag(Varyings input) : SV_Target
            {
                half intensity = saturate(_Intensity);
                float aspect = BoEAspectFromUV(input.uv);
                float2 p = (input.uv - 0.5) * float2(aspect, 1.0);
                float r = length(p);
                float angle = atan2(p.y, p.x) / (2.0 * PI) + 0.5;
                float a = angle * _LineCount;
                float cell = floor(a);
                float f = frac(a);
                // New random pattern 12 times per second (animation on twos at 24 fps).
                float h = BoEHash21(float2(cell, floor(_T * 12.0)));
                float width = 0.12 + 0.28 * h;
                half lineMask = smoothstep(width, width * 0.4, abs(f - 0.5)) * step(0.42, h);
                float inner = lerp(0.8, 0.32, intensity) + h * 0.18;
                half radial = smoothstep(inner, inner + 0.22, r);
                half alpha = lineMask * radial * intensity * input.color.a * _BaseColor.a;
                return half4(_BaseColor.rgb * input.color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
