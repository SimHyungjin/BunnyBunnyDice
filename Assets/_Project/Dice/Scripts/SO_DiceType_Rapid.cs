using UnityEngine;

/// <summary>
/// 초록 — 속사: 약한 공격을 빠르게(기획서 6.5). 빠르기는 기본 능력치의 공격 속도와 그 성장률에서 나온다.
/// 기본으로 같은 적을 이어 맞힐수록 세진다 — 선두가 바뀌거나 사거리를 벗어나면 처음부터라 오래 붙잡히는 적(보스·단단함)에게만 크게 쌓인다.
/// 각성하면 공격 속도가 오른다. 빨리 쏘면 이어 맞히기도 빨리 쌓여 시간당 피해가 곱으로 커지므로 쌓이는 값은 옛 각성(5%)보다 낮게 둔다(2026-11-14 사용자).
/// 옛 실험(2026-11-01): 이어 맞히기를 기본으로 두고 각성에 공격 속도 2배를 얹었을 때 모든 특성·보스를 혼자 막았다 — 각성 공속은 그보다 작게.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Rapid", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Rapid : SO_DiceType
{
    [Header("이어 맞히기")]
    [Tooltip("같은 적을 이어 맞힐 때마다 더하는 피해 비율(0.03 = 한 대마다 +3%, 더하기). 대상이 바뀌면 처음부터. 상한 없음.")]
    [Min(0f)] public float FocusBonusPerHit = 0.03f;

    [Header("각성 (눈 20)")]
    [Tooltip("각성하면 더하는 공격 속도 비율(0.5 = 1.5배).")]
    [Min(0f)] public float AwakenAttackSpeedBonus = 0.5f;

    public override float AwakenAttackSpeedScale => 1f + AwakenAttackSpeedBonus;
}
