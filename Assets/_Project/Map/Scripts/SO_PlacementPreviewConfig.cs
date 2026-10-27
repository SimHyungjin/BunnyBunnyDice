using UnityEngine;

/// <summary>주사위 드래그 중 칸 위에 띄우는 고스트·칸 하이라이트 설정. 고스트 모양은 주사위 모델(SO_DiceConfig)을 그대로 쓴다.</summary>
[CreateAssetMenu(menuName = "Def/Map/Placement Preview Config", fileName = nameof(SO_PlacementPreviewConfig))]
public sealed class SO_PlacementPreviewConfig : SO_Config
{
    [Tooltip("1x1 칸 기준으로 만들고, 블록 틈만큼 줄여서 쓴다.")]
    public GameObject HighlightPrefab;
    public Color ValidColor = new Color(0.3f, 0.95f, 0.4f);
    public Color InvalidColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("고스트를 놓일 자리보다 띄우는 높이 — 놓으면 떨어질 주사위라는 것이 보이게.")]
    [Min(0f)] public float GhostHover = 0.15f;

    [Header("사거리")]
    [Tooltip("정점 색을 쓰는 반투명 셰이더(파티클 Unlit, 알파 블렌드). 비우면 사거리를 그리지 않는다.")]
    public Material RangeMaterial;
    public Color RangeColor = new Color(1f, 1f, 1f, 0.7f);
    [Tooltip("고리 선 굵기(칸).")]
    [Min(0.005f)] public float RangeWidth = 0.05f;
    [Tooltip("고리를 이루는 점 수. 사거리가 길수록 많아야 둥글게 보인다.")]
    [Min(8)] public int RangeSegments = 64;
    [Tooltip("놓일 칸 바닥 위로 띄우는 높이 — 바닥과 겹쳐 깜빡이지 않게.")]
    [Min(0f)] public float RangeLift = 0.03f;

    [Header("스킬 (기획서 5.3)")]
    [Tooltip("범위 스킬 원의 선 굵기(칸). 색은 쓸 수 있으면 ValidColor, 아니면 InvalidColor. 재질·점 수·띄우는 높이는 사거리와 같다.")]
    [Min(0.005f)] public float SkillAreaWidth = 0.08f;
    [Tooltip("범위 스킬이 닿을 적·모음점 밑에 그리는 작은 고리 반경(칸). 색은 카드 색(IconColor).")]
    [Min(0.05f)] public float SkillMarkRadius = 0.3f;
    [Tooltip("작은 고리의 선 굵기(칸).")]
    [Min(0.005f)] public float SkillMarkWidth = 0.06f;
    [Tooltip("체크메이트의 왕(큰 피해를 더 받는 적) 밑 고리 반경(칸) — 작은 고리 바깥에 하나 더 그려 누가 왕인지 보이게.")]
    [Min(0.05f)] public float SkillKingMarkRadius = 0.5f;


    [Header("미로 계획 (기획서 11.4 — 어시스트·봇)")]
    [Tooltip("계획할 주사위 총수(놓인 것 포함). 이만큼으로 지을 수 있는 가장 좋은 미로를 찾는다.")]
    [Min(1)] public int MazeTargetDice = 20;
    [Tooltip("판이 바뀔 때마다 돌리는 담금질 걸음 수. 많을수록 좋은 배치를 찾지만 오래 걸린다.")]
    [Min(0)] public int MazeIterations = 20000;
    [Tooltip("한 프레임에 탐색에 쓰는 시간(ms) — 프레임이 끊기지 않게 나눠 돌린다.")]
    [Min(0.1f)] public float MazeFrameBudgetMs = 2f;
    [Tooltip("계획한 칸이 길을 덮는다고 칠 사거리(칸). 색이 정해지지 않은 칸이라 주사위 사거리의 대략값.")]
    [Min(0f)] public float MazeAssumedRange = 2.8f;
    [Tooltip("점수 = 길 길이 + 길 칸마다 사거리 안 주사위 수 × 이 값. 0이면 길 길이만 본다.")]
    [Min(0f)] public float MazeCoverageWeight = 0.3f;
    [Tooltip("보스 구멍의 길을 출현 몫(그 구멍에 들어간 판 주사위 수)에 더해 이만큼 더 쳐 준다. 0이면 출현 몫만 본다.")]
    [Min(0f)] public float MazeBossWeight = 1f;
    [Tooltip("담금질 시작 온도 — 높을수록 처음에 나쁜 배치도 받아들여 넓게 찾는다.")]
    [Min(0.001f)] public float MazeStartTemperature = 3f;
    [Tooltip("담금질 끝 온도.")]
    [Min(0.001f)] public float MazeEndTemperature = 0.05f;
    [Tooltip("맵 생성 검사(MazeRouteProbe)의 담금질 걸음 수 — 판 시작 전에 한 번에 도는 계산이라 위보다 적게 둔다.")]
    [Min(0)] public int MazeProbeIterations = 3000;
}
