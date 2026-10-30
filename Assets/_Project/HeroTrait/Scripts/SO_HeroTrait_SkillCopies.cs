using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 카운터: 레벨업에서 스킬 카드를 고르면 몇 장 받는다(2026-10-31 사용자). 후보를 띄울 때부터 스킬 카드에 x2가 보인다(ILevelUpCopies).
/// 처음엔 "후보에 스킬이 늘 2장 나온다"로 잘못 만들었는데, 후보 대부분이 스킬이라 주사위를 못 받아 미로를 만들 수 없었다.
/// 전용 카드는 스킬이 아니라 늘지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Skill Copies", fileName = "SO_HeroTrait_SkillCopies")]
public sealed class SO_HeroTrait_SkillCopies : SO_HeroTrait, ILevelUpCopies
{
    [Tooltip("스킬 카드를 고르면 받는 장수.")]
    [Min(1)] public int Copies = 2;

    public void RollCopies(IReadOnlyList<HandCard> candidates, List<int> copies)
    {
        for (int i = 0; i < candidates.Count; i++) copies.Add(candidates[i].Kind == CardKind.Skill ? Copies : 1);
    }
}
