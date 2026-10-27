using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전용 카드: 레벨업마다 확률로 후보 하나를 이 캐릭터만의 카드로 덮어쓴다(기획서 10.1 — 카드가 주인을 아는 대신 캐릭터가 전용 카드를 안다).
/// 풀에 더하지 않고 덮어써서 공용 카드가 늘어나도 빈도가 그대로다. 같은 카드를 여러 캐릭터가 쓸 수 있다. 건설가(폴드)가 첫 사용처다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Extra Cards", fileName = "SO_HeroTrait_ExtraCards")]
public sealed class SO_HeroTrait_ExtraCards : SO_HeroTrait, ICandidateOverwrite
{
    [Tooltip("전용 카드(칩·스킬·영웅 강화). 여러 장이면 각자의 Weight로 하나를 고른다. 공용 목록(SO_LevelUpConfig)에는 넣지 않는다 — 넣으면 모두에게 나온다.")]
    public List<SO_Card> Cards = new List<SO_Card>();
    [Tooltip("레벨업마다 후보 하나가 전용 카드로 바뀔 확률.")]
    [Range(0f, 1f)] public float Chance = 0.2f;

    public IReadOnlyList<SO_Card> OverwriteCards => Cards;

    public SO_Card RollOverwrite()
    {
        if (UnityEngine.Random.value >= Chance) return null;
        float total = 0f;
        for (int i = 0; i < Cards.Count; i++)
            if (Cards[i] != null) total += Cards[i].Weight;
        float roll = UnityEngine.Random.value * total;
        SO_Card picked = null;
        for (int i = 0; i < Cards.Count; i++)
        {
            SO_Card card = Cards[i];
            if (card == null || card.Weight <= 0f) continue;
            picked = card;
            if (roll < card.Weight) break;
            roll -= card.Weight;
        }
        return picked;
    }

    public override void SwapNested(Func<SO_Config, SO_Config> swap)
    {
        for (int i = 0; i < Cards.Count; i++) Cards[i] = (SO_Card)swap(Cards[i]);
    }
}
