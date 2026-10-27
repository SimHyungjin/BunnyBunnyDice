// 바닥에 떨어진 스포트라이트 웅덩이(로비 — LobbyLights). 실제 조명으로 바닥을 밝히면 같은 빛을 받는 캐릭터가 하얗게 날아가므로,
// 캐릭터 밝기는 조명이, 바닥 웅덩이는 이 판이 따로 맡는다.
// 사각 판 위에 가운데가 밝고 가장자리로 갈수록 사라지는 원을 더한다(가산 — 아래 카펫 무늬가 그대로 비친다).
// 바닥과 같은 높이에 깔리므로 깊이를 조금 당겨(Offset) 바닥과 깜빡이며 다투지 않게 한다.
Shader "Def/LightPool"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.95, 0.88, 1)
        _Softness ("Softness (0 = hard edge, 1 = all fade)", Range(0.01, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "LightPool"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 판 가운데 0 → 가장자리 1. 1 - Softness 안쪽은 꽉 차고, 그 밖은 부드럽게 사라진다.
                half r = length(input.uv * 2.0 - 1.0);
                // 제곱으로 한 번 더 눌러 가장자리 꼬리를 길게 — 선형에 가까우면 밝은 바닥 위에서 원판처럼 테두리가 읽힌다.
                half fade = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                fade *= fade;
                return half4(_Color.rgb * fade, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
