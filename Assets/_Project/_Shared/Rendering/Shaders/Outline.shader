// 게임 물체(주사위·칩 등)의 툰 아웃라인 — 뒤집은 껍데기(inverted hull).
// 물체 머티리얼 뒤에 두 번째 머티리얼로 붙어 같은 메시를 한 번 더 그린다: 뒷면만 그리고(Cull Front) 법선 방향으로 화면에서 부풀린다.
// 깊이 검사를 하므로 가려진 부분의 선은 보이지 않고, 칩 위 주사위처럼 붙은 두 물체 사이에도 선이 생긴다.
// 굵기는 화면 높이 비율이다(해상도·화면비가 달라도 같은 굵기 — 픽셀 값 금지 규칙).
// 각진 모델은 면마다 법선이 달라 모서리에서 껍데기가 갈라지므로, 같은 위치 정점의 법선을 평균 낸 부드러운 법선을 UV3에 넣어 쓴다(OutlineMesh).
// UV3가 비어 있으면(0) 메시 법선을 쓴다 — 칩처럼 법선이 이미 이어진 메시.
// 바닥 쪽으로 부풀린 선은 펠트보다 깊이가 뒤라 가려지므로, 깊이만 카메라 쪽으로 조금 당긴다(_OutlineDepthBias, 월드 단위) — 바닥에 닿는 테두리도 그려진다.
// 화면 위치는 그대로라 선 모양은 바뀌지 않는다. 물체 두께보다 작아야 자기 앞면을 뚫고 나오지 않는다.
Shader "Def/Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (0.05, 0.03, 0.07, 1)
        _OutlineWidth ("Width (screen height ratio)", Float) = 0.0025
        _OutlineDepthBias ("Depth Bias (world)", Float) = 0.04
        // 알파로 깎은 재질(레이스 치맛단 등) 위에 붙을 때 그 모양대로 선도 깎는다. OutlineMesh가 서브메시마다 MaterialPropertyBlock으로 넣는다.
        // 기본값(흰 텍스처, 0)이면 아무것도 깎지 않는다 — 주사위·칩은 그대로.
        _ClipMap ("Clip Texture (alpha)", 2D) = "white" {}
        _ClipCutoff ("Clip Cutoff", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Outline"
            // 물체의 UniversalForward 패스와 따로 그려지도록 기본 언릿 태그를 쓴다.
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // SRP Batcher가 묶을 수 있게 머티리얼 값은 이 버퍼에만 둔다.
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineDepthBias;
                float4 _ClipMap_ST;
                float _ClipCutoff;
            CBUFFER_END

            TEXTURE2D(_ClipMap);
            SAMPLER(sampler_ClipMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = dot(input.smoothNormalOS, input.smoothNormalOS) > 1e-6 ? input.smoothNormalOS : input.normalOS;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float4 biasedCS = TransformWorldToHClip(positionWS + normalize(GetCameraPositionWS() - positionWS) * _OutlineDepthBias);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float length2 = dot(normalCS, normalCS);
                normalCS = length2 > 1e-10 ? normalCS * rsqrt(length2) : float2(0.0, 0.0);

                // NDC 높이는 2이므로 비율 × 2. w를 곱해 원근과 무관하게 화면에서 같은 굵기, 가로는 화면비로 맞춘다.
                float2 offset = normalCS * (_OutlineWidth * 2.0 * positionCS.w);
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                positionCS.xy += offset;
                positionCS.z = biasedCS.z / biasedCS.w * positionCS.w;
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _ClipMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 컷오프가 0이면 알파가 0이어도 남는다(clip은 음수만 버린다).
                clip(SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, input.uv).a - _ClipCutoff);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
