// 조명기에서 내려오는 희미한 빛줄기(HeroSpotlight). 먼지에 비친 빛처럼 보이게 원뿔대 옆면에 빛을 더한다(가산).
// 위·아래 밝기를 따로 받는다 — 로비는 위가 밝아 쏟아지는 느낌, 판 위는 위로 갈수록 사라져 판을 가로지르지 않게.
// 메시는 반지름 1·높이 1의 원기둥이고(HeroSpotlight), 윗면 반지름은 셰이더가 _TopRatio로 좁힌다 — 비율을 바꿔도 메시를 다시 만들지 않으려고.
// 옆면을 비스듬히 볼수록(가장자리) 흐려서 기둥 윤곽이 칼로 자른 듯 보이지 않게 하고, 아래로 갈수록 옅게, 윗끝도 흐려 끊긴 자리가 안 보이게 한다.
Shader "Def/LightBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.95, 0.88, 1)
        _TopRatio ("Top Radius / Bottom Radius", Range(0, 1)) = 0.35
        _BottomFade ("Brightness at Bottom", Range(0, 1)) = 0.3
        _TopBrightness ("Brightness at Top", Range(0, 1)) = 1
        _EdgePower ("Edge Softness", Range(0.1, 8)) = 1.5
        _FloorY ("Floor Height (World)", Float) = 0
        _FloorFade ("Fade Height Above Floor (World)", Float) = 0.0001
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
            Name "LightBeam"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _TopRatio;
                half _BottomFade;
                half _TopBrightness;
                half _EdgePower;
                float _FloorY;
                float _FloorFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float height : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                // 높이 0(바닥) → 1(위)로 갈수록 반지름을 줄인다.
                p.xz *= lerp(1.0, _TopRatio, p.y);
                output.positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                // 옆면 법선은 바깥(가로) 방향으로 본다 — 기울기를 넣지 않아도 가장자리 흐림에는 충분하다.
                output.normalWS = TransformObjectToWorldDir(float3(input.positionOS.x, 0.0, input.positionOS.z));
                output.height = input.positionOS.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                half facing = abs(dot(normalize(input.normalWS), view));
                half edge = pow(facing, _EdgePower);
                half vertical = lerp(_BottomFade, _TopBrightness, input.height) * (1.0 - smoothstep(0.8, 1.0, input.height));
                // 바닥에 가까울수록 사라진다 — 바닥이 원뿔을 자른 자리가 선으로 남지 않게. 바닥의 빛은 웅덩이(LightPool)가 맡는다.
                half floorFade = smoothstep(0.0, 1.0, saturate((input.positionWS.y - _FloorY) / _FloorFade));
                return half4(_Color.rgb * (edge * vertical * floorFade), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
