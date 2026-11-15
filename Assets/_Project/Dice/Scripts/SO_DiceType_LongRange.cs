using UnityEngine;

/// <summary>
/// 파랑 — 저격: 사거리가 가장 긴 단일 공격(기획서 6.5, 2026-10-26 개편). 어디에 놓아도 길에 닿는 것이 강점이다.
/// 기본으로 멀리 있는 적일수록 세게 맞힌다 — 긴 사거리를 실제로 써야 세지는 저격다운 이유. 칸 수에 비례하고 상한이 없다(2026-10-30 사용자,
/// 사거리 칩으로 사거리를 늘리면 끝의 피해도 커진다). 각성하면 한 칸마다 더하는 값이 더 커진다(2026-11-14 사용자 — 단색 각성은 자기 피해를 키운다.
/// 둔화·얼림·밀기처럼 적에게 거는 효과는 칩과 겹치고, 같은 색을 둘째부터 놓을 이유가 없다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Long Range", fileName = "SO_Dice_")]
public sealed class SO_DiceType_LongRange : SO_DiceType
{
    [Header("거리 보너스")]
    [Tooltip("한 칸 멀어질 때마다 더하는 피해 배율. 피해 × (1 + 이 값 × 거리(칸)) — 0.5면 기본 사거리(5.5칸) 끝에서 약 3.75배, 바로 옆은 그대로. 상한 없음.")]
    [Min(0f)] public float FarDamagePerCell = 0.5f;

    [Header("각성 (눈 20)")]
    [Tooltip("각성하면 한 칸마다 더 얹는 피해 배율(FarDamagePerCell에 더한다).")]
    [Min(0f)] public float AwakenFarDamagePerCell = 0.5f;
}
