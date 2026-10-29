using UnityEngine;

/// <summary>
/// 초록 — 속사: 약한 공격을 빠르게(기획서 6.5). 빠르기는 기본 능력치의 공격 속도와 그 성장률에서 나온다.
/// 같은 적을 이어 맞힐수록 세진다(2026-10-26). 상한은 없다 — 선두가 바뀌거나 사거리를 벗어나면 처음부터라
/// 오래 붙잡히는 적(보스·단단함)에게만 크게 쌓인다. 더하기라 맞힌 수에 비례해 늘 뿐 폭주하지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Rapid", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Rapid : SO_DiceType
{
    [Tooltip("같은 적을 이어 맞힐 때마다 더하는 피해 비율(0.05 = 한 대마다 +5%). 대상이 바뀌면 처음부터.")]
    [Min(0f)] public float FocusBonusPerHit = 0.05f;

    [Header("각성 (눈 20)")]
    [Tooltip("공격 속도 증가 비율(1 = 2배).")]
    [Min(0f)] public float AwakenAttackSpeedBonus = 1f;
}
