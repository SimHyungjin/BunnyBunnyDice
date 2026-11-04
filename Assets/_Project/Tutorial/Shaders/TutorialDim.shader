// 튜토리얼 덮개: 화면을 어둡게 덮고 둥근 사각형 구멍 둘을 뚫는다(UI_TutorialDim).
// 구멍은 영역의 로컬 좌표(UV × _Size, 왼쪽 아래 원점)로 받는다 — 가운데 xy + 절반 크기 zw, 절반 크기 0이면 없음.
// UI 기본 셰이더의 스텐실·클립 속성을 그대로 둔다(마스크 안에 넣어도 깨지지 않게).
Shader "Def/UI/TutorialDim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HoleA ("Hole A", Vector) = (0,0,0,0)
        _HoleB ("Hole B", Vector) = (0,0,0,0)
        _Size ("Size", Vector) = (1,1,0,0)
        _Corner ("Corner", Float) = 0.5
        _Soft ("Soft", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
            };

            fixed4 _Color;
            float4 _HoleA;
            float4 _HoleB;
            float4 _Size;
            float _Corner;
            float _Soft;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // 구멍 안 = 1, 밖 = 0(가장자리는 _Soft 폭으로 흐림). 둥근 사각형 거리 함수.
            float HoleMask(float2 p, float4 hole)
            {
                if (hole.z <= 0 || hole.w <= 0) return 0;
                float radius = _Corner * min(hole.z, hole.w);
                float2 d = abs(p - hole.xy) - (hole.zw - radius);
                float dist = length(max(d, 0)) + min(max(d.x, d.y), 0) - radius;
                return 1 - smoothstep(-_Soft, 0, dist);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * _Size.xy;
                float hole = max(HoleMask(p, _HoleA), HoleMask(p, _HoleB));
                fixed4 col = i.color;
                col.a *= 1 - hole;
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
