using UnityEngine;

/// <summary>
/// 럭키보이: 몬스터를 처치할 때 낮은 확률로 경험치가 두 배(기획서 10.3). 도박사(고른 카드 복제)와 달리 고를 기회 자체가 늘어난다.
/// 터지면 금색 숫자가 뜬다(GameScene) — 처치가 많아 결국 고른 보너스처럼 느껴지더라도 작은 행운이 자주 보이게.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Double Exp", fileName = "SO_HeroTrait_DoubleExp")]
public sealed class SO_HeroTrait_DoubleExp : SO_HeroTrait, IKillExpModifier
{
    [Tooltip("경험치가 두 배가 될 확률(처치마다).")]
    [Range(0f, 1f)] public float Chance = 0.1f;

    public int ModifyExp(int exp) => exp > 0 && GameRandom.Exp.Value < Chance ? exp * 2 : exp;
}
