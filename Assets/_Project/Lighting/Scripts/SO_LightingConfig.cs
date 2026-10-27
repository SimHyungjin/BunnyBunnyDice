using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 조명·후처리 설정. 어두운 방 안에서 조명 받은 포커 테이블 느낌이 기본값이다:
/// 머리 위에서 떨어지는 따뜻한 빛 + 낮은 주변광 + 광택·금속이 비칠 반사 환경(StudioReflection) + 블룸 + 강한 비네트(바깥을 어둡게).
/// </summary>
[CreateAssetMenu(menuName = "Def/Lighting/Lighting Config", fileName = nameof(SO_LightingConfig))]
public sealed class SO_LightingConfig : SO_Config
{
    [Header("해 (씬의 Directional Light)")]
    public Vector3 SunRotation = new Vector3(62f, -30f, 0f);
    public Color SunColor = new Color(1f, 0.95f, 0.98f);
    public float SunIntensity = 1.2f;
    [Range(0f, 1f)] public float ShadowStrength = 0.75f;

    [Header("주변광 (하늘·지평선·땅 3색)")]
    public Color AmbientSky = new Color(0.34f, 0.29f, 0.4f);
    public Color AmbientEquator = new Color(0.2f, 0.15f, 0.25f);
    public Color AmbientGround = new Color(0.07f, 0.04f, 0.09f);

    [Header("반사 환경 (StudioReflection)")]
    [Tooltip("큐브맵 한 면 픽셀 수.")]
    [Min(8)] public int ReflectionSize = 64;
    [Range(0f, 2f)] public float ReflectionIntensity = 1f;
    public Color ReflectionTop = new Color(0.34f, 0.29f, 0.38f);
    public Color ReflectionHorizon = new Color(0.08f, 0.05f, 0.1f);
    public Color ReflectionBottom = new Color(0.01f, 0.01f, 0.012f);
    [Tooltip("머리 위 조명판 색. 레일 곡면에 길게 비치는 하이라이트가 된다.")]
    public Color SoftboxColor = new Color(0.984f, 0.933f, 0.988f);
    [Tooltip("조명판 밝기(색에 곱한다). 1보다 크면 반사가 블룸을 받아 반짝인다.")]
    [Min(0f)] public float SoftboxIntensity = 2.5f;
    [Tooltip("조명판 크기(높이 1 평면 위의 반폭 x, z). 클수록 하이라이트가 넓다.")]
    public Vector2 SoftboxHalfSize = new Vector2(0.9f, 0.35f);

    [Header("배경")]
    [Tooltip("카메라 배경색(하늘 대신 단색). 바닥 너머가 보여도 방 안처럼 어둡게.")]
    public Color BackgroundColor = new Color(0.035f, 0.02f, 0.05f);

    [Header("후처리")]
    [Tooltip("씬에 원래 있는 Global Volume보다 위에 오도록 한다.")]
    public float VolumePriority = 10f;
    public TonemappingMode Tonemapping = TonemappingMode.Neutral;
    public float PostExposure = 0f;
    [Range(-100f, 100f)] public float Contrast = 15f;
    [Range(-100f, 100f)] public float Saturation = 20f;
    [Min(0f)] public float BloomIntensity = 0.5f;
    [Min(0f)] public float BloomThreshold = 1f;
    [Range(0f, 1f)] public float BloomScatter = 0.7f;
    [Range(0f, 1f)] public float VignetteIntensity = 0.45f;
    [Tooltip("비네트 색. 검정보다 아주 어두운 보라가 방 분위기와 맞는다.")]
    public Color VignetteColor = new Color(0.035f, 0.02f, 0.05f);
    [Range(0f, 1f)] public float VignetteSmoothness = 0.45f;

    [Header("아웃라인 (게임 물체 — 주사위·칩·캐릭터)")]
    [Tooltip("툰 아웃라인 셰이더(Def/Outline). 비우면 아웃라인을 그리지 않는다. 테이블에는 넣지 않는다 — 판 위의 말이 배경에서 떠오르게.")]
    public Shader OutlineShader;
    [Tooltip("선 색. 거의 검정 — UI 카드의 굵은 테두리와 같은 문법.")]
    public Color OutlineColor = new Color(0.05f, 0.03f, 0.07f);
    [Tooltip("선 굵기(화면 높이 비율). 0.0025면 1080p에서 약 2.7px.")]
    [Range(0f, 0.02f)] public float OutlineWidth = 0.0025f;
    [Tooltip("선의 깊이만 카메라 쪽으로 당기는 거리(월드). 바닥에 닿는 테두리가 펠트에 가려지지 않게. 칩 두께(0.12)보다 작게 — 크면 물체 앞면을 뚫고 선이 보인다.")]
    [Range(0f, 0.2f)] public float OutlineDepthBias = 0.04f;
    [Tooltip("캐릭터(영웅) 아웃라인의 깊이 당김(월드). 캐릭터는 귀·팔다리가 OutlineDepthBias보다 얇아, 같은 값이면 선이 몸 앞으로 나와 가는 부분을 통째로 검게 덮는다. 색·굵기는 위와 같다.")]
    [Range(0f, 0.2f)] public float CharacterOutlineDepthBias = 0.005f;
}
