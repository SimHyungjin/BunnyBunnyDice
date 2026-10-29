using UnityEngine;

/// <summary>
/// 파랑 — 저격: 사거리가 가장 긴 단일 공격(기획서 6.5, 2026-10-26 개편). 피해·속도는 중간이고, 어디에 놓아도 길에 닿는 것이 강점이다.
/// 멀리 있는 적일수록 세게 맞힌다(2026-10-26) — 긴 사거리를 실제로 써야 세지는 저격다운 이유를 준다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Long Range", fileName = "SO_Dice_")]
public sealed class SO_DiceType_LongRange : SO_DiceType
{
    [Tooltip("거리에 비례해 더하는 피해 배율. 피해 × (1 + 이 값 × 거리/사거리) — 2면 사거리 끝에서 3배, 바로 옆은 그대로.")]
    [Min(0f)] public float FarDamageBonus = 2f;

    [Header("각성 (눈 20)")]
    [Tooltip("사거리 증가 비율(0.5 = 1.5배). 칩 보너스는 늘어난 사거리 위에 더한다.")]
    [Min(0f)] public float AwakenRangeBonus = 0.5f;
}
