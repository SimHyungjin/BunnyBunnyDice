using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바니걸(기본): 레벨업 후보 카드가 모두 서로 다른 종류(주사위·칩·스킬·영웅 강화)로 나온다(기획서 10.3).
/// 이미 뽑힌 종류의 몫을 0으로 만든다 — 남은 종류끼리는 원래 몫(가중치 합)대로라 종류 안의 희귀도도 그대로다.
/// 종류보다 후보가 많으면 남는 자리는 갈고리 없이 채운다(CardRoll).
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Distinct Kinds", fileName = "SO_HeroTrait_DistinctKinds")]
public sealed class SO_HeroTrait_DistinctKinds : SO_HeroTrait, ICardKindShare
{
    public float GetShare(CardKind kind, float total, IReadOnlyList<HandCard> picked)
    {
        for (int i = 0; i < picked.Count; i++)
        {
            if (picked[i].Kind == kind) return 0f;
        }
        return total;
    }
}
