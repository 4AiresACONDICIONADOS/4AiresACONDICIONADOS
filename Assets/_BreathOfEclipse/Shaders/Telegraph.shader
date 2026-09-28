// Breath of Eclipse — enemy attack telegraph on a ground quad: outer ring + a disc that fills toward the
// moment of impact (_Fill 0 -> 1), with a bright leading edge and a warning pulse near the end.
Shader "BreathOfEclipse/Telegraph"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (2, 0.15, 0.1, 1)
        _Fill ("Fill", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Telegraph"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TelegraphVert
            #pragma fragment TelegraphFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Fill;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings TelegraphVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 TelegraphFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 p = input.uv * 2.0 - 1.0;
                half d = length(p);
                half aa = fwidth(d) * 1.5h;
                half inside = 1.0h - smoothstep(1.0h - aa, 1.0h, d);
                half ring = smoothstep(0.9h - aa, 0.9h, d) * inside;
                half fill = saturate(_Fill);
                half fillDisc = (1.0h - smoothstep(fill - aa, fill, d)) * inside;
                half front = smoothstep(fill - 0.06h, fill, d) * (1.0h - smoothstep(fill, fill + aa, d)) * inside * step(0.01h, fill);
                half pulse = 1.0h + 0.35h * sin(_Time.y * 28.0) * smoothstep(0.75h, 1.0h, fill);
                half alpha = (ring * 0.9h + fillDisc * 0.28h + front * 0.9h + inside * 0.06h) * _Opacity * pulse;
                half3 color = _BaseColor.rgb * (1.0h + ring + front);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
