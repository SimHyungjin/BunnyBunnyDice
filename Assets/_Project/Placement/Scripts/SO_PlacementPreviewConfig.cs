using UnityEngine;

/// <summary>카드를 끄는 동안의 미리보기 설정 — 주사위·칩 고스트·칸 하이라이트·사거리 고리, 스킬 표시. 고스트 모양은 주사위 모델(SO_DiceConfig)을 그대로 쓴다.</summary>
[CreateAssetMenu(menuName = "Def/Placement/Placement Preview Config", fileName = nameof(SO_PlacementPreviewConfig))]
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

    [Header("스킬 (기획서 8)")]
    [Tooltip("적 대상 스킬을 끄는 동안 판 전체에 까는 색 — 모든 적이 대상이라는 것이 보이게. 주사위·적을 가리지 않게 아주 연하게(알파). " +
             "손가락이 판 밖이면(쓸 수 없으면) 깔지 않는다. 재질·띄우는 높이는 사거리와 같다.")]
    public Color SkillBoardColor = new Color(0.3f, 0.95f, 0.4f, 0.12f);
    [Tooltip("레이크 모음점 밑 고리 반경(칸) — 레이크는 어디로 모으는지가 가장 중요한 정보라 크게. 색은 카드 색(IconColor).")]
    [Min(0.05f)] public float SkillGatherMarkRadius = 0.7f;
    [Tooltip("체크메이트의 왕(큰 피해를 받는 적) 밑 고리 반경(칸). 색은 카드 색(IconColor).")]
    [Min(0.05f)] public float SkillKingMarkRadius = 0.5f;
    [Tooltip("모음점·왕 고리의 선 굵기(칸).")]
    [Min(0.005f)] public float SkillMarkWidth = 0.06f;

}
