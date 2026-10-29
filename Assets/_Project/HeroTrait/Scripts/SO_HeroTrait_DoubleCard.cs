using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 더블: 레벨업마다 확률로 후보 하나에 x2가 붙는다 — 고르면 두 장, 영웅 강화 카드면 두 번 적용(기획서 10.3).
/// 후보를 띄울 때부터 보이므로 x2가 고르는 기준이 된다. 처음엔 후보마다 굴려 레벨업당 확률이 장수에 따라 달랐다 — 레벨업당 한 번으로(2026-10-29 사용자).
/// 같은 카드는 손패에서 겹치므로 손패 상한(뭉치 수)을 쓰지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Double Card", fileName = "SO_HeroTrait_DoubleCard")]
public sealed class SO_HeroTrait_DoubleCard : SO_HeroTrait, ILevelUpCopies
{
    [Tooltip("레벨업마다 후보 하나(무작위)에 x2가 붙을 확률.")]
    [Range(0f, 1f)] public float Chance = 0.1f;

    public void RollCopies(IReadOnlyList<HandCard> candidates, List<int> copies)
    {
        for (int i = 0; i < candidates.Count; i++) copies.Add(1);
        if (candidates.Count == 0 || GameRandom.Cards.Value >= Chance) return;
        copies[GameRandom.Cards.Next(candidates.Count)] = 2;
    }
}
