using UnityEngine;

/// <summary>
/// 몬스터 특성 하나(기획서 9.11 — 단단함·빠름·분열·재생·거대, 특성마다 에셋 하나). 몬스터 = 기본 종류(SO_MonsterType) + 특성 몇 개이고,
/// 조합은 웨이브 설정(SO_WaveConfig)이 정한다 — "보스 = 거대 + 특성 하나" 같은 조합마다 몬스터 에셋을 따로 만들지 않게.
/// 겹치면 배율(체력·속도·크기·이동 방해)은 곱하고 고정값(방어력·재생·목표물 피해·경험치)은 더한다(MonsterKind).
/// 웨이브 설정이 참조하므로 의존성으로 함께 로드된다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Monster/Monster Trait", fileName = "SO_MonsterTrait_")]
public sealed class SO_MonsterTrait : ScriptableObject
{
    public string DisplayName;
    [Tooltip("기본 색에 섞는 색. 알파가 섞는 정도(0이면 색을 바꾸지 않는다). 임시 외형 — 모델이 생기면 뺀다.")]
    public Color Tint = new Color(1f, 1f, 1f, 0f);
    [Tooltip("겉모습 크기 배율. 걷는 길 계산의 반지름은 바꾸지 않는다(SO_MonsterConfig.Radius를 모두 같이 쓴다).")]
    [Min(0.1f)] public float VisualScale = 1f;

    [Header("배율 (겹치면 곱한다)")]
    [Min(0.01f)] public float HealthScale = 1f;
    [Min(0.01f)] public float SpeedScale = 1f;
    [Tooltip("둔화 비율·얼림 시간을 줄이는 비율. 0.5면 절반만 받는다 — 면역이 아니라 저항(거대).")]
    [Range(0f, 1f)] public float ImpairResist;

    [Header("고정값 (겹치면 더한다)")]
    [Tooltip("받는 피해에서 빼는 값(단단함). 최소 피해 비율은 SO_MonsterConfig.MinDamageRatio.")]
    [Min(0f)] public float Armor;
    [Tooltip("초당 회복량(최대 체력에 대한 비율, 재생). 막는 상태는 없다 — 큰 한 방(빨강 강타)으로 회복 전에 끊는다.")]
    [Min(0f)] public float RegenRatio;
    [Tooltip("목표물 피해에 더하는 값(거대).")]
    [Min(0)] public int GoalDamageBonus;
    [Tooltip("경험치에 더하는 값.")]
    [Min(0)] public int ExpBonus;

    [Header("분열")]
    [Tooltip("죽으면 나뉘는 수. 0이면 나뉘지 않는다. 나뉜 몹은 이 특성이 빠져 다시 나뉘지 않는다.")]
    [Min(0)] public int SplitCount;
    [Tooltip("나뉜 몹 한 마리의 체력(본체 최대 체력에 대한 비율).")]
    [Range(0.05f, 1f)] public float SplitHealthRatio = 0.5f;
    [Tooltip("나뉜 몹의 목표물 피해.")]
    [Min(0)] public int PieceGoalDamage = 1;
    [Tooltip("나뉜 몹의 경험치. 주면 분열이 이득이 되어 일부러 반기게 되므로 0(경험치는 본체만).")]
    [Min(0)] public int PieceExp;
}
