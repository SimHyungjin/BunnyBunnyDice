using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 지형 표시 설정. 블록 프리팹은 단위 큐브(1x1x1, 중심 피벗)이고 MapView가 칸 크기·높이에 맞춰 늘린다.
/// 판 바닥과 둘레 벽의 겉모습은 테이블(SO_EnvironmentConfig)이 정한다 — 여기는 칩·칸 표시와 칸 높이만.
/// 칸 윗면 높이(GetTileTop)는 표시뿐 아니라 칸 선택·주사위 배치 높이에도 쓰이므로 여기 한 곳에서 정한다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Map/Map View Config", fileName = nameof(SO_MapViewConfig))]
public sealed class SO_MapViewConfig : SO_Config
{
    [Header("프리팹")]
    public GameObject BlockPrefab;
    [Tooltip("목표물 표식. 발밑 가운데 피벗, 지름 약 1칸의 둥근 물건. 목표물 영역 정중앙에 그대로 세운다(늘리거나 색을 입히지 않는다). 비우면 잭팟 칩(아래 목표물 항목)을 만든다.")]
    public GameObject GoalMarkerPrefab;

    [Header("크기")]
    public float GroundThickness = 0.5f;
    [Tooltip("칩 한 장의 두께. 칩 위에 놓인 주사위는 이만큼 올라앉는다.")]
    [FormerlySerializedAs("HighHeight")]
    [Min(0.01f)] public float ChipHeight = 0.12f;
    [Tooltip("둘레 벽 윗면 높이(칸 선택용). 벽은 펠트에 인쇄된 경계 선으로 보이므로 0이다.")]
    public float WallHeight = 0f;
    [Tooltip("칸 하이라이트 크기를 칸보다 이만큼 작게 한다(PlacementPreview).")]
    [Range(0f, 0.2f)] public float Gap = 0.04f;

    [Header("바닥 칸 선 (MapGridView)")]
    [Tooltip("칸 선 머티리얼(URP Unlit, 투명). 알파로 나타나고 사라진다.")]
    public Material GridMaterial;
    [Tooltip("바닥 칸 사이 선 폭(칸 비율). 0이면 그리지 않는다.")]
    [Range(0f, 0.2f)] public float GridLineWidth = 0.03f;
    [Tooltip("선을 바닥 위로 띄우는 높이. 바닥과 같은 높이면 겹쳐 깜빡인다.")]
    [Min(0f)] public float GridLineLift = 0.003f;
    [Tooltip("선 색(알파는 아래 값이 정한다). 조명을 받지 않으므로 보이는 그대로의 색이다.")]
    public Color GridLineColor = new Color(0.75f, 0.6f, 1f);
    [Tooltip("평소 선 알파. 0이면 평소에는 보이지 않는다.")]
    [Range(0f, 1f)] public float GridIdleAlpha = 0f;
    [Tooltip("카드를 끌어 놓는 동안의 선 알파.")]
    [Range(0f, 1f)] public float GridShownAlpha = 0.35f;
    [Tooltip("나타나고 사라지는 데 걸리는 시간(초, 실제 시간).")]
    [Min(0f)] public float GridFadeDuration = 0.15f;

    [Header("칩 (색은 칩 카드의 IconColor)")]
    [Tooltip("칩 지름(칸 비율). 1보다 작아야 이웃 칩과 틈이 생겨 칸이 세어진다. 칩 위 주사위보다 커야 옆면 색이 드러난다.")]
    [Range(0.1f, 1f)] public float ChipDiameter = 0.86f;
    [Tooltip("윗모서리 둥글림(두께 비율).")]
    [Range(0f, 1f)] public float ChipBevelRatio = 0.4f;
    [Tooltip("원 둘레를 나누는 수(4의 배수).")]
    [Min(8)] public int ChipSegments = 32;
    [Tooltip("칩 윗면에 인쇄하는 링 색. 거의 검정 — 밝은 칩(둔화 흰색)에서도 어두운 칩에서도 링이 보여 칩으로 읽힌다(크림색은 흰 칩 위에서 사라졌다).")]
    public Color ChipRingColor = new Color(0.06f, 0.04f, 0.08f);
    [Tooltip("링 바깥·안쪽 지름(칩 지름 비율).")]
    [Range(0f, 1f)] public float ChipRingOuter = 0.64f;
    [Range(0f, 1f)] public float ChipRingInner = 0.56f;
    [Range(0f, 1f)] public float ChipSmoothness = 0.55f;

    [Header("목표물 잭팟 칩 (표식 프리팹이 없을 때)")]
    [Tooltip("칩 지름(칸). 1×2 영역 가운데(칸 경계)에 서므로 1칸 안팎이면 한 점짜리 목표로 보인다.")]
    [Min(0.1f)] public float GoalChipDiameter = 0.95f;
    [Min(0.01f)] public float GoalChipHeight = 0.18f;
    [Tooltip("강화 칩보다 크고 밝은 색(UI 청록).")]
    public Color GoalChipColor = new Color(0.247f, 0.878f, 1f);
    [Tooltip("윗면 링 색. 목표물은 눈에 띄어야 한다.")]
    public Color GoalRingColor = new Color(1f, 0.82f, 0.3f);
    [Tooltip("윗면 링 바깥·안쪽 지름(칩 지름 비율).")]
    [Range(0f, 1f)] public float GoalRingOuter = 0.78f;
    [Range(0f, 1f)] public float GoalRingInner = 0.64f;

    [Header("인쇄")]
    [Tooltip("칩 윗면 링을 윗면 위로 띄우는 높이. 같은 높이면 겹쳐 깜빡인다.")]
    [Min(0f)] public float PrintLift = 0.002f;

    /// <summary>가장 높은 칸 윗면. 카메라가 맵 전체를 담을 때 위쪽 경계로 쓴다.</summary>
    public float MaxTileTop => Mathf.Max(ChipHeight, WallHeight);

    public float GetTileTop(TileType type) => type == TileType.Wall ? WallHeight : 0f;

    /// <summary>칸 윗면 높이. 칩이 놓인 칸은 칩 윗면이다(주사위가 그 위에 앉는다).</summary>
    public float GetCellTop(MapData map, int x, int y) => map.HasChip(x, y) ? ChipHeight : GetTileTop(map.Get(x, y));
}
