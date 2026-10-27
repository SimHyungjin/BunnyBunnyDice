using UnityEngine;

/// <summary>
/// 도박사: 레벨업 후보 카드마다 낮은 확률로 x2가 붙는다 — 고르면 두 장, 영웅 강화 카드면 두 번 적용(기획서 10.3).
/// 후보를 띄울 때부터 보이므로 x2가 고르는 기준이 된다. 카드마다 따로 굴려 세 장 모두 붙을 수도 있다.
/// 같은 카드는 손패에서 겹치므로 손패 상한(뭉치 수)을 쓰지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Double Card", fileName = "SO_HeroTrait_DoubleCard")]
public sealed class SO_HeroTrait_DoubleCard : SO_HeroTrait, ILevelUpCopies
{
    [Tooltip("후보 카드 하나에 x2가 붙을 확률. 3장 중 하나라도 붙을 확률은 1 − (1 − 이 값)³(5%면 약 14%).")]
    [Range(0f, 1f)] public float Chance = 0.05f;

    public int RollCopies(HandCard candidate) => Random.value < Chance ? 2 : 1;
}
