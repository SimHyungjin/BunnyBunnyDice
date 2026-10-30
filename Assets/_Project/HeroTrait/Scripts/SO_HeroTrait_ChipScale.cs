using UnityEngine;

/// <summary>
/// 칩 리더: 모든 칩 효과가 세진다(2026-10-31 사용자 — x1.5, 강화 칩 셋 포함. 처음 x2는 영웅 디버프 카드 값이었는데, 그 카드는 영웅이 맞힌 몇 마리에만 걸려 세게 둔 것이라 주사위가 맞히는 모든 적에게 거는 칩에는 과했다). 판 시작 칩·놓은 칩·사이드 팟으로 얹은 칩 모두.
/// 지운 영웅 디버프 카드(마킹·슬로우롤·콜드 덱·핫 핸드)의 몫을 이 캐릭터가 맡는다.
/// 지속 시간은 그대로 두고 세기만 곱한다(ChipEffects).
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Chip Scale", fileName = "SO_HeroTrait_ChipScale")]
public sealed class SO_HeroTrait_ChipScale : SO_HeroTrait, IChipScale
{
    [Tooltip("모든 칩 효과에 곱하는 세기.")]
    [Min(1f)] public float Strength = 1.5f;

    public float ChipStrength => Strength;
}
