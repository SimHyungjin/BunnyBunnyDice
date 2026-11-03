using UnityEngine;

/// <summary>
/// 하양 — 난사: 일정 간격으로 자기 주변 사거리 안의 가까운 적 여럿을 맞힌다(기획서 6.5). 기본값이 가장 낮고 사거리도 짧다.
/// 2026-11-04 전 이름은 파동(SO_DiceType_Pulse) — 맞히는 수 상한(가까운 순)이 붙어 퍼져 나가는 파동이 아니게 되어 바꿨다(사용자).
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Barrage", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Barrage : SO_DiceType
{
    [Header("난사")]
    [Tooltip("한 번에 맞히는 최대 수(가까운 순). 몹이 모일수록 끝없이 커지던 합계를 자른다 — 한 마리당 피해는 그대로라 방어력과 부딪치지 않고, " +
             "사거리 칩은 닿는 거리만 늘리고 맞히는 수는 늘리지 않는다(2026-11-04 사용자 — 맞힌 수로 피해를 나누면 조각이 방어력에 다 먹혀 뺐다).")]
    [Min(1)] public int MaxTargets = 20;

    [Header("표시 (범위는 그리지 않는다 — DiceAttack.Barrage)")]
    [Tooltip("맞은 적 발밑에 터지는 작은 불꽃 고리의 반지름(칸).")]
    [Min(0f)] public float HitSparkRadius = 0.35f;

    [Header("각성 (눈 20)")]
    [Tooltip("두 번째 난사가 첫 난사 뒤 나가는 시간.")]
    [Min(0.05f)] public float AwakenDelay = 0.2f;
    [Tooltip("두 번째 난사의 피해(첫 난사 대비). 하양은 각성이 흔해 작게 둔다(6.5).")]
    [Range(0f, 1f)] public float AwakenRatio = 0.5f;
}
