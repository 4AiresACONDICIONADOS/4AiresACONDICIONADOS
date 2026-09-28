// Breath of Eclipse — flash frame overlay: manga focus lines converging on the impact point plus a star burst.
// _Strength 0: bright element-colored lines (dark background flash). _Strength 1: black ink lines (silhouette flash).
Shader "BreathOfEclipse/UI/RadialBurst"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Center ("Center (viewport)", Vector) = (0.5, 0.5, 0, 0)
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _T ("Seed", Float) = 0
        _Strength ("Silhouette Mode", Range(0, 1)) = 0
        _LineCount ("Line Count", Range(40, 260)) = 150
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RadialBurst"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex UIVert
            #pragma fragment BurstFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Center;
                half4 _BaseColor;
                float _T;
                half _Strength;
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

            half4 BurstFrag(Varyings input) : SV_Target
            {
                float aspect = BoEAspectFromUV(input.uv);
                float2 p = (input.uv - _Center.xy) * float2(aspect, 1.0);
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float a = (angle / (2.0 * PI) + 0.5) * _LineCount;
                float cell = floor(a);
                float f = frac(a);
                float h = BoEHash21(float2(cell, _T));
                float width = 0.1 + 0.35 * h;
                // Focus lines: wedges that start at a random distance from the impact and reach the screen edge.
                float start = 0.16 + h * 0.22;
                half lineMask = smoothstep(width, width * 0.3, abs(f - 0.5)) * smoothstep(start, start + 0.12, r) * step(0.3, h);

                // Impact star: bright core plus sharp spikes.
                float spikes = pow(abs(cos(angle * 4.0 + _T)), 48.0) + pow(abs(cos(angle * 7.0 - _T * 1.7)), 90.0) * 0.6;
                half star = saturate(pow(saturate(1.0 - r * 7.0), 2.0) + spikes * saturate(1.0 - r * 3.2) * 1.2);

                half silhouette = saturate(_Strength);
                half3 lineColor = lerp(saturate(_BaseColor.rgb + 0.35h), half3(0, 0, 0), silhouette);
                half3 color = lerp(lineColor, half3(1, 1, 1), star);
                half alpha = saturate(max(lineMask * lerp(0.8h, 0.92h, silhouette), star)) * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
