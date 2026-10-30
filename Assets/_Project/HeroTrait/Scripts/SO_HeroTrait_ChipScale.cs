using UnityEngine;

/// <summary>
/// 칩 리더: 모든 칩 효과가 세진다(2026-10-31 사용자 — x2, 강화 칩 셋 포함). 판 시작 칩·놓은 칩·사이드 팟으로 얹은 칩 모두.
/// 디버프 칩 x2는 지운 영웅 디버프 카드(마킹·슬로우롤·콜드 덱·핫 핸드)와 거의 같은 세기다 — 그 몫을 이 캐릭터가 맡는다.
/// 지속 시간은 그대로 두고 세기만 곱한다(ChipEffects).
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Chip Scale", fileName = "SO_HeroTrait_ChipScale")]
public sealed class SO_HeroTrait_ChipScale : SO_HeroTrait, IChipScale
{
    [Tooltip("모든 칩 효과에 곱하는 세기.")]
    [Min(1f)] public float Strength = 2f;

    public float ChipStrength => Strength;
}
