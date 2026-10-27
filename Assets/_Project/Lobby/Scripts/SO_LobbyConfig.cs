using UnityEngine;

/// <summary>
/// 로비 설정(기획서 12.2). 로비는 판과 같은 씬이다 — 영웅이 테이블 앞 난간에 걸터앉아 있고 카메라가 영웅을 가까이 비춘다.
/// 게임 시작을 누르면 영웅이 일어서며 웅크렸다가 판 안으로 뛰어들고, 그동안 카메라가 판 전체를 비추는 자리(MapCameraFitter)로 옮겨 간다.
/// 거리·높이는 칸 단위(한 칸 = 1, 펠트 윗면 = 0), 시간은 실제 시간(초)이다. 자세는 동작 묶음(SO_HeroAnimationSet)이 맡는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Lobby/Lobby Config", fileName = nameof(SO_LobbyConfig))]
public sealed class SO_LobbyConfig : SO_Config
{
    [Header("영웅 자리 (판 앞 가장자리 바깥)")]
    [Tooltip("판 앞 가장자리(게임 카메라 쪽)에서 바깥으로 떨어진 거리. 앉은 엉덩이가 난간 위에 오도록 맞춘다.")]
    public float HeroEdgeOffset = 0.45f;
    [Tooltip("로비에서의 영웅 크기(판 위 크기의 배수). 카메라 거리는 테이블 폭으로 정하므로 이 값은 영웅 크기만 바꾼다. 뛰어드는 동안 1로 줄어든다.")]
    [Min(0.1f)] public float HeroScale = 6f;
    [Tooltip("앉아 있을 때 발(모델 원점)을 난간 윗면에서 내리는 깊이(로비 크기 키의 비율). 앉기 동작은 원점 위에 엉덩이가 떠 있어, 내려야 엉덩이가 난간에 닿고 다리가 늘어진다.")]
    [Range(0f, 1f)] public float HeroSitDrop = 0.28f;
    [Tooltip("일어선 발 높이(난간 윗면). 여기서 판으로 뛴다.")]
    public float HeroStandHeight = 0.15f;
    [Tooltip("앞 가장자리를 따라 옆으로 민 거리(+ = 게임 화면의 오른쪽). 가장자리 가운데에는 포켓이 있다.")]
    public float HeroSideOffset = -2.5f;

    [Header("로비 카메라")]
    [Tooltip("수평 기준 기울기. 높을수록 판 위가 많이 보인다.")]
    [Range(-10f, 70f)] public float CameraTilt = 30f;
    [Range(15f, 60f)] public float FieldOfView = 30f;
    [Tooltip("테이블 폭이 화면 가로에서 차지하는 비율(영웅 자리 깊이에서) — 가로 화면. 카메라 거리를 이것으로 정한다. 1보다 크면 테이블이 화면 밖으로 넘친다.")]
    [Range(0.2f, 3f)] public float TableScreenWidth = 1.1f;
    [Tooltip("세로 화면. 화면 가로가 좁아 같은 값이면 테이블이 너무 작으므로 더 크게 둔다.")]
    [Range(0.2f, 4f)] public float PortraitTableScreenWidth = 1.6f;
    [Tooltip("영웅 몸(키 × 아래 비율)이 놓일 화면 자리(뷰포트 0~1) — 가로 화면. 로비 화면의 왼쪽 캐릭터 영역과 맞춘다.")]
    public Vector2 HeroViewport = new Vector2(0.27f, 0.45f);
    [Tooltip("세로 화면. 로고와 이름 버튼 사이에 오게 맞춘다.")]
    public Vector2 PortraitHeroViewport = new Vector2(0.5f, 0.68f);
    [Tooltip("화면 자리를 맞출 영웅 몸의 높이(서 있는 키 비율, 난간 위 기준). 0 = 발, 1 = 머리.")]
    [Range(0f, 1f)] public float HeroFocusHeight = 0.5f;

    [Header("테이블 장식 (로비 동안 판 위에 흩어 두는 주사위·칩)")]
    [Tooltip("판 위 크기에 곱한다. 로비 카메라는 판을 가까이 비추므로 판 크기 그대로면 장난감 블록처럼 커 보인다.")]
    [Range(0.1f, 1.5f)] public float DecorScale = 0.6f;
    [Min(0)] public int DecorDiceCount = 7;
    [Tooltip("칩 더미 수. 더미마다 1 ~ DecorChipStackMax장.")]
    [Min(0)] public int DecorChipStacks = 8;
    [Min(1)] public int DecorChipStackMax = 3;
    [Tooltip("더미 안에서 칩이 어긋나는 정도(칸).")]
    [Min(0f)] public float DecorChipJitter = 0.04f;
    [Tooltip("펠트 가장자리에서 띄우는 거리(칸).")]
    [Min(0f)] public float DecorEdgeMargin = 0.6f;
    [Tooltip("장식끼리의 최소 거리(칸).")]
    [Min(0f)] public float DecorMinSpacing = 0.8f;
    [Tooltip("게임 시작 때 하나가 사라지는 시간과, 전체가 사라지기 시작하는 시점이 흩어지는 폭.")]
    [Min(0f)] public float DecorHideDuration = 0.2f;
    [Min(0f)] public float DecorHideSpread = 0.35f;
    [Tooltip("로비로 돌아올 때 장식이 생기기 시작하기까지(판 위 물건이 날아가는 동안 기다린다).")]
    [Min(0f)] public float DecorShowDelay = 0.35f;
    [Tooltip("장식 하나가 톡 생기는 시간.")]
    [Min(0f)] public float DecorShowDuration = 0.22f;
    [Tooltip("장식이 생기는 시각을 이 안에서 무작위로 흩는다.")]
    [Min(0f)] public float DecorShowSpread = 0.7f;

    [Header("게임 시작 — 뛰어들기")]
    [Tooltip("일어서며 웅크리는 시간. 점프 클립의 웅크림 구간(0 ~ 발이 뜨는 시점)을 이 시간에 재생한다.")]
    [Min(0f)] public float StandUpDuration = 0.45f;
    [Tooltip("난간에서 판 안 시작 칸까지 나는 시간. 점프 클립의 공중 구간을 이 시간에 재생한다.")]
    [Min(0.05f)] public float JumpDuration = 0.6f;
    [Tooltip("두 자리 중 높은 쪽에서 더 솟는 높이.")]
    [Min(0f)] public float JumpHeight = 1.2f;
    [Tooltip("닿은 뒤 일어서기까지. 점프 클립의 착지 구간을 이 시간에 재생한다.")]
    [Min(0f)] public float LandDuration = 0.3f;

    [Header("게임 시작 — 카메라")]
    [Tooltip("뛰기 시작한 뒤(일어서기 다음) 카메라가 움직이기 시작할 때까지.")]
    [Min(0f)] public float CameraDelay = 0f;
    [Tooltip("로비 자리에서 게임 자리까지 옮겨 가는 시간. 뛰는 시간보다 길게 두면 착지를 위에서 내려다보며 끝난다.")]
    [Min(0.05f)] public float CameraDuration = 0.9f;
}
