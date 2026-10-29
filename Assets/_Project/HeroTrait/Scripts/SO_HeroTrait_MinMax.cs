using UnityEngine;

/// <summary>
/// 더블 오어 낫싱: 타워 주사위를 굴리면 가장 낮은 눈(1) 아니면 가장 높은 눈만 나온다(2026-10-29 사용자 — 가운데 눈만 빼던 "극과 극"에서 끝까지 밀었다).
/// 평균은 같아도 이십면체는 절반이 바로 각성하고 절반은 1이라, 합성으로 1을 메우는 판이 된다. 놓기·얹기·리롤·올인 모두에 건다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Min Max", fileName = "SO_HeroTrait_MinMax")]
public sealed class SO_HeroTrait_MinMax : SO_HeroTrait, IDicePipRoller
{
    [Tooltip("가장 높은 눈이 나올 확률(나머지는 1).")]
    [Range(0f, 1f)] public float HighChance = 0.5f;

    public int RollPips(int faces, bool placing) => GameRandom.Dice.Value < HighChance ? faces : 1;
}
