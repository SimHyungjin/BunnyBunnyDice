using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 카운터: 레벨업 후보에 스킬 카드가 늘 몇 장 이상 나온다(2026-10-30 사용자). 나올 카드를 읽는 사람이라 원하는 스킬을 놓치지 않는다.
/// 스킬이 그 장수만큼 뽑히기 전까지는 스킬 몫만 남기고, 다 채운 뒤엔 원래 몫대로 뽑는다. 앞자리부터 채워지므로 CardRoll이 자리를 섞는다.
/// 전용 카드 자리는 세지 않는다(스킬이 아니다).
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Skill Count", fileName = "SO_HeroTrait_SkillCount")]
public sealed class SO_HeroTrait_SkillCount : SO_HeroTrait, ICardKindShare
{
    [Tooltip("후보에 늘 나오는 스킬 카드 수.")]
    [Min(0)] public int SkillCount = 2;

    public float GetShare(CardKind kind, float total, IReadOnlyList<HandCard> picked)
    {
        int skills = 0;
        for (int i = 0; i < picked.Count; i++)
        {
            if (picked[i].Kind == CardKind.Skill) skills++;
        }
        if (skills >= SkillCount) return total;
        return kind == CardKind.Skill ? total : 0f;
    }
}
