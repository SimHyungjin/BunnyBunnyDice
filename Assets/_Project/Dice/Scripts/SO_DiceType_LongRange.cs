using UnityEngine;

/// <summary>
/// 파랑 — 저격: 사거리가 가장 긴 단일 공격(기획서 6.5, 2026-10-26 개편). 피해·속도는 중간이고, 어디에 놓아도 길에 닿는 것이 강점이다.
/// 각성하면 멀리 있는 적일수록 세게 맞힌다 — 긴 사거리를 실제로 써야 세지는 저격다운 이유. 기본은 평범하고 각성이 개성을 연다(2026-11-01, 빨강·초록과 같은 구조).
/// 칸 수에 비례하고 상한이 없다(2026-10-30 사용자) — 처음엔 거리/사거리라 끝에서 늘 3배였고, 사거리 칩·각성으로 사거리를 늘려도 피해는 그대로였다.
/// 이제 사거리를 늘리면 닿는 곳뿐 아니라 끝의 피해도 커진다(초록의 이어 맞히기처럼 한계 없이 자라는 축).
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Long Range", fileName = "SO_Dice_")]
public sealed class SO_DiceType_LongRange : SO_DiceType
{
    [Header("각성 (눈 20)")]
    [Tooltip("한 칸 멀어질 때마다 더하는 피해 배율. 피해 × (1 + 이 값 × 거리(칸)) — 0.3이면 기본 사거리(5.5칸) 끝에서 약 2.65배, 바로 옆은 그대로. 상한 없음.")]
    [Min(0f)] public float FarDamagePerCell = 0.3f;
}
