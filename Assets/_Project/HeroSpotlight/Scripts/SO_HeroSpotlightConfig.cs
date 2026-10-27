using System;
using UnityEngine;

/// <summary>
/// 영웅 스포트라이트 한 벌의 모습. 로비와 게임에 한 벌씩 두고, 뛰어드는 동안 둘을 섞는다(HeroSpotlight).
/// 빛이 나오는 곳(SourceOffset)은 두 벌이 같다 — 같은 조명기가 밝기·폭만 바꾸는 것이라 뛰어드는 동안 빛줄기가 끊기지 않는다.
/// 크기는 지금 보이는 영웅 키(월드)의 배수다 — 로비(크게)와 판 위(작게)에서 같은 비율로 따라간다.
/// </summary>
[Serializable]
public struct HeroSpotlightLook
{
    [Header("키 라이트 (조명기에서 몸을 비추는 실제 조명)")]
    [Tooltip("영웅에 닿는 밝기(거리와 상관없이 같게 맞춘다). 0이면 끈다.")]
    [Min(0f)] public float KeyLightBrightness;
    [Tooltip("영웅 자리에서 빛 원의 반지름(영웅 키 배수). 판 위에서는 좁게 — 넓으면 둘레 주사위 색이 바래 보인다(색 = 뜻).")]
    [Min(0.05f)] public float KeyLightRadius;

    [Header("림 라이트 (뒤에서 윤곽)")]
    [Tooltip("자리(영웅 키 배수): x = 카메라 오른쪽, y = 위, z = 카메라 쪽(음수면 영웅 뒤).")]
    public Vector3 RimLightOffset;
    [Tooltip("0이면 끈다.")]
    [Min(0f)] public float RimLightBrightness;
    [Range(1f, 179f)] public float RimLightAngle;

    [Header("방")]
    [Tooltip("방(해·주변광) 밝기 배율. 낮을수록 영웅만 조명을 받는 무대처럼 보인다. 판이 보여야 하는 게임 중에는 1 가까이.")]
    [Range(0f, 1f)] public float RoomDim;

    [Header("발밑 빛 웅덩이")]
    [Tooltip("반지름(영웅 키 배수). 빛이 비스듬히 오는 만큼 조명기 반대쪽으로 길어진다(PoolMaxStretch까지).")]
    [Min(0f)] public float PoolRadius;
    [Tooltip("가운데 밝기(키 라이트 색에 곱한다). 바닥에 더해지는 빛이라 1 근처면 충분하다.")]
    [Min(0f)] public float PoolBrightness;

    [Header("빛줄기 (조명기에서 웅덩이까지 비스듬히 내려오는 희미한 빛)")]
    [Tooltip("밝기(키 라이트 색에 곱한다). 몸 위에도 더해지므로 아주 작게. 0이면 끈다.")]
    [Min(0f)] public float BeamBrightness;
    [Tooltip("바닥 쪽 끝의 밝기 비율.")]
    [Range(0f, 1f)] public float BeamBottom;
    [Tooltip("조명기 쪽 끝의 밝기 비율. 판 위에서는 0 가까이 — 위로 갈수록 사라져 판을 가로지르는 줄이 보이지 않게.")]
    [Range(0f, 1f)] public float BeamTop;

    public static HeroSpotlightLook Lerp(HeroSpotlightLook a, HeroSpotlightLook b, float t)
    {
        return new HeroSpotlightLook
        {
            KeyLightBrightness = Mathf.Lerp(a.KeyLightBrightness, b.KeyLightBrightness, t),
            KeyLightRadius = Mathf.Lerp(a.KeyLightRadius, b.KeyLightRadius, t),
            RimLightOffset = Vector3.Lerp(a.RimLightOffset, b.RimLightOffset, t),
            RimLightBrightness = Mathf.Lerp(a.RimLightBrightness, b.RimLightBrightness, t),
            RimLightAngle = Mathf.Lerp(a.RimLightAngle, b.RimLightAngle, t),
            RoomDim = Mathf.Lerp(a.RoomDim, b.RoomDim, t),
            PoolRadius = Mathf.Lerp(a.PoolRadius, b.PoolRadius, t),
            PoolBrightness = Mathf.Lerp(a.PoolBrightness, b.PoolBrightness, t),
            BeamBrightness = Mathf.Lerp(a.BeamBrightness, b.BeamBrightness, t),
            BeamBottom = Mathf.Lerp(a.BeamBottom, b.BeamBottom, t),
            BeamTop = Mathf.Lerp(a.BeamTop, b.BeamTop, t),
        };
    }
}

/// <summary>
/// 영웅 스포트라이트 설정 — 시상식 팔로우 스포트처럼 테이블 위 높은 곳에 고정된 조명기가 영웅을 따라 고개만 돌린다.
/// 로비에서는 무대 조명, 게임 중에는 판 위에서 영웅을 놓치지 않게 하는 표시다. SO_HeroConfig가 참조한다(의존성으로 함께 로드).
/// </summary>
[CreateAssetMenu(menuName = "Def/Hero/Hero Spotlight Config", fileName = nameof(SO_HeroSpotlightConfig))]
public sealed class SO_HeroSpotlightConfig : SO_Config
{
    [Tooltip("로비에 있을 때.")]
    public HeroSpotlightLook Lobby;
    [Tooltip("판 위에 있을 때. 뛰어드는 동안 로비 값에서 이 값으로 넘어간다.")]
    public HeroSpotlightLook Game;

    [Header("조명기 자리")]
    [Tooltip("판 가운데 바닥에서 조명기까지(맵 칸): x = 카메라 오른쪽, y = 위, z = 카메라가 보는 쪽(음수면 카메라 쪽).\n" +
             "로비 영웅은 앞 난간에 카메라를 보고 앉으므로 z는 난간보다 카메라 쪽이어야 얼굴이 밝다.")]
    public Vector3 SourceOffset = new Vector3(-4f, 20f, -4f);

    [Header("공통")]
    public Color KeyLightColor = new Color(1f, 0.95f, 0.88f);
    [Tooltip("윤곽 색. UI 자홍에 맞춰 배경에서 떠오르게.")]
    public Color RimLightColor = new Color(1f, 0.35f, 0.65f);

    [Header("빛 웅덩이")]
    [Tooltip("셰이더(Def/LightPool). 비우면 그리지 않는다.")]
    public Shader PoolShader;
    [Tooltip("가장자리가 흐려지는 폭(반지름 비율). 1이면 가운데부터 서서히 사라진다.")]
    [Range(0.01f, 1f)] public float PoolSoftness = 1f;
    [Tooltip("발 높이에서 띄우는 높이(영웅 키 비율). 발이 바닥에 살짝 묻혀 있으면 웅덩이가 바닥 밑에 깔려 안 보이므로 그만큼 올린다.")]
    [Range(0f, 0.1f)] public float PoolLift = 0.01f;
    [Tooltip("웅덩이가 조명기 반대쪽으로 길어지는 상한(배). 빛이 낮게 올수록 길어지는데, 너무 길면 웅덩이가 영웅 자리를 못 가리킨다.")]
    [Min(1f)] public float PoolMaxStretch = 1.8f;

    [Header("빛줄기")]
    [Tooltip("셰이더(Def/LightBeam). 비우면 그리지 않는다. 바닥 쪽 반지름은 웅덩이와 같다.")]
    public Shader BeamShader;
    [Tooltip("조명기 쪽 반지름 / 바닥 쪽 반지름. 작을수록 한 점에서 쏘는 원뿔에 가깝다.")]
    [Range(0f, 1f)] public float BeamTopRatio = 0.1f;
    [Tooltip("가장자리 흐림. 클수록 빛줄기 가운데만 남고 윤곽이 부드럽다.")]
    [Range(0.1f, 8f)] public float BeamEdgeSoftness = 1.5f;
    [Tooltip("바닥에서 이 높이(영웅 키 배수)까지 빛줄기가 서서히 사라진다. 0에 가까우면 바닥이 빛줄기를 자른 자리가 선으로 보인다.")]
    [Min(0f)] public float BeamFloorFade = 0.8f;
}
