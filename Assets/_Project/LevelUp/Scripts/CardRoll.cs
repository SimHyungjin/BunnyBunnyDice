using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨업 후보 카드 뽑기. 먼저 카드 종류(주사위·스킬·칩·영웅 강화)를 고르고, 그 종류 안에서 한 장을 고른다.
/// 종류의 몫은 기본이 그 종류 카드 가중치의 합(카드 한 장 한 장이 같은 확률)이고, 갈고리(ICardKindShare)가 바꿀 수 있다.
/// 주사위 카드 몫은 색 가중치 합 × 면체 가중치 합이고, 뽑히면 색과 면체를 가중치로 따로 뽑아 한 장을 만든다(기획서 13).
/// 전용 카드는 먼저 확률(SO_LevelUpConfig.ExclusiveChance)로 한 자리를 차지하고, 나머지 자리만 공용 카드로 뽑는다(2026-10-29 사용자 —
/// 다 뽑은 뒤 덮어쓰던 방식은 레인보우의 "종류가 모두 다름"을 깨고 고르려던 카드를 지웠다). 주사위 카드 보장은 두지 않는다(5.2, 잠정).
/// </summary>
public static class CardRoll
{
    // 한 번의 선택 안에서 같은 카드가 겹치면 다시 뽑는다. 후보가 아주 적은 설정에서 끝없이 돌지 않게 횟수를 막는다.
    private const int MaxAttemptsPerCard = 20;

    // 종류별 후보. 뽑을 때마다 다시 채운다 — 메인 스레드에서만 부르므로 할당 없이 재사용한다.
    private static readonly List<SO_Card> Skills = new List<SO_Card>();
    private static readonly List<SO_Card> Chips = new List<SO_Card>();
    private static readonly List<SO_Card> Heroes = new List<SO_Card>();
    // 이번 Pick의 후보 거르기(없으면 null).
    private static Func<SO_Card, bool> _useful;

    /// <summary>겹치지 않는 카드를 count장까지 result에 채운다. 뽑을 수 있는 카드가 없으면 비어 있다.</summary>
    /// <param name="traits">특성 갈고리. 없으면 null.</param>
    /// <param name="useful">false면 그 카드를 후보에서 뺀다(골라도 바뀌는 것이 없는 카드 — 치명타 100%의 확률 카드). 없으면 null.</param>
    /// <param name="exclusive">캐릭터 전용 카드. 없으면 null.</param>
    public static void Pick(SO_LevelUpConfig config, TraitHooks traits, int count, List<HandCard> result, Func<SO_Card, bool> useful = null,
        SO_SkillCard exclusive = null)
    {
        _useful = useful;
        result.Clear();
        bool hasExclusive = exclusive != null && count > 0 && GameRandom.Cards.Value < config.ExclusiveChance;
        if (hasExclusive) count--;
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < MaxAttemptsPerCard; attempt++)
            {
                // 갈고리가 모든 종류를 막으면(종류보다 장수가 많을 때 등) 빈칸으로 두지 않고 갈고리 없이 채운다.
                if (!TryRoll(config, traits, result, out HandCard card) && !TryRoll(config, null, result, out card)) return;
                if (result.Contains(card)) continue;
                result.Add(card);
                break;
            }
        }
        // 종류 몫 갈고리는 앞자리부터 채운다(카드 카운터의 스킬 2장) — 늘 같은 자리에 나오지 않게 자리를 섞는다.
        if (traits?.Find<ICardKindShare>() != null) Shuffle(result);
        // 전용 카드는 무작위 자리에 끼운다 — 늘 같은 자리면 보지도 않고 알게 된다. 공용 카드끼리의 종류 몫(레인보우)은 전용 카드를 세지 않는다.
        if (hasExclusive) result.Insert(GameRandom.Cards.Next(result.Count + 1), HandCard.Of(exclusive));
    }

    /// <summary>
    /// 레벨업 후보와 같은 몫(종류·가중치·특성 갈고리)으로 공용 카드 한 장을 뽑는다 — 코인 플립처럼 카드를 여러 장 주는 스킬이 쓴다.
    /// 장마다 따로 굴리므로 같은 카드가 겹칠 수 있다(손패에서 쌓인다). 전용 카드는 나오지 않는다. 뽑을 것이 없으면 false.
    /// </summary>
    public static bool RollOne(SO_LevelUpConfig config, TraitHooks traits, Func<SO_Card, bool> useful, out HandCard card)
    {
        _useful = useful;
        NoPicked.Clear();
        return TryRoll(config, traits, NoPicked, out card) || TryRoll(config, null, NoPicked, out card);
    }

    private static void Shuffle(List<HandCard> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = GameRandom.Cards.Next(i + 1);
            HandCard swap = cards[i];
            cards[i] = cards[j];
            cards[j] = swap;
        }
    }

    // RollOne에는 함께 뽑힌 후보가 없다.
    private static readonly List<HandCard> NoPicked = new List<HandCard>();

    /// <param name="picked">이번 선택에서 이미 뽑힌 후보(종류 몫 갈고리가 본다).</param>
    private static bool TryRoll(SO_LevelUpConfig config, TraitHooks traits, IReadOnlyList<HandCard> picked, out HandCard card)
    {
        card = default;
        FillPools(config);

        float colorTotal = 0f;
        for (int i = 0; i < config.ColorWeights.Count; i++) colorTotal += config.ColorWeights[i].Weight;
        ILevelUpFaces faces = traits?.Find<ILevelUpFaces>();
        float facesTotal = 0f;
        for (int i = 0; i < config.FacesWeights.Count; i++) facesTotal += FacesWeight(config.FacesWeights[i], faces);

        ICardKindShare share = traits?.Find<ICardKindShare>();
        float dice = Share(share, CardKind.Dice, colorTotal * facesTotal, picked);
        float skill = Share(share, CardKind.Skill, SumWeights(Skills), picked);
        float chip = Share(share, CardKind.Chip, SumWeights(Chips), picked);
        float hero = Share(share, CardKind.Hero, SumWeights(Heroes), picked);
        float total = dice + skill + chip + hero;
        if (total <= 0f) return false;

        // 종류를 고른 뒤 그 안에서 다시 굴린다 — 종류 몫이 가중치 합과 달라도(갈고리) 종류 안의 희귀도는 그대로 지켜진다.
        float roll = GameRandom.Cards.Value * total;
        if (roll < dice || skill + chip + hero <= 0f)
        {
            card = HandCard.Of(RollDice(config, colorTotal, facesTotal, faces));
            return true;
        }
        roll -= dice;
        List<SO_Card> pool = roll < skill || chip + hero <= 0f ? Skills
            : roll - skill < chip || hero <= 0f ? Chips
            : Heroes;
        // 갈고리가 카드 없는 종류에 몫을 주면 고를 것이 없다.
        SO_Card chosen = PickWeighted(pool, GameRandom.Cards.Value * SumWeights(pool));
        if (chosen == null) return false;
        card = HandCard.Of(chosen);
        return true;
    }

    private static float Share(ICardKindShare share, CardKind kind, float total, IReadOnlyList<HandCard> picked) =>
        share != null ? Mathf.Max(share.GetShare(kind, total, picked), 0f) : total;

    private static void FillPools(SO_LevelUpConfig config)
    {
        Skills.Clear();
        Chips.Clear();
        Heroes.Clear();
        for (int i = 0; i < config.SkillCards.Count; i++) AddUseful(Skills, config.SkillCards[i]);
        for (int i = 0; i < config.ChipCards.Count; i++) AddUseful(Chips, config.ChipCards[i]);
        for (int i = 0; i < config.HeroCards.Count; i++) AddUseful(Heroes, config.HeroCards[i]);
    }

    private static void AddUseful(List<SO_Card> pool, SO_Card card)
    {
        if (_useful == null || card == null || _useful(card)) pool.Add(card);
    }

    private static float SumWeights(List<SO_Card> cards)
    {
        float total = 0f;
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null) total += cards[i].Weight;
        return total;
    }

    // 굴림 값이 합계에 닿으면(부동소수 오차) 끝까지 못 고르는 경우가 있다. 그때는 가중치가 있는 마지막 항목을 쓴다(0인 항목은 나오지 않게).
    private static SO_Card PickWeighted(List<SO_Card> cards, float roll)
    {
        SO_Card picked = null;
        for (int i = 0; i < cards.Count; i++)
        {
            SO_Card candidate = cards[i];
            if (candidate == null || candidate.Weight <= 0f) continue;
            picked = candidate;
            if (roll < candidate.Weight) break;
            roll -= candidate.Weight;
        }
        return picked;
    }

    // 면체 가중치(특성이 바꾸면 곱한다).
    private static float FacesWeight(SO_LevelUpConfig.FacesWeight entry, ILevelUpFaces faces) =>
        entry.Weight * (faces != null ? Mathf.Max(faces.ScaleFaces(entry.Faces), 0f) : 1f);

    private static DiceCard RollDice(SO_LevelUpConfig config, float colorTotal, float facesTotal, ILevelUpFaces facesHook)
    {
        float colorRoll = GameRandom.Cards.Value * colorTotal;
        DiceColor colors = DiceColor.Clear;
        for (int i = 0; i < config.ColorWeights.Count; i++)
        {
            float weight = config.ColorWeights[i].Weight;
            if (weight <= 0f) continue;
            colors = DiceColors.Normalize(config.ColorWeights[i].Colors);
            if (colorRoll < weight) break;
            colorRoll -= weight;
        }

        float facesRoll = GameRandom.Cards.Value * facesTotal;
        int faces = 0;
        for (int i = 0; i < config.FacesWeights.Count; i++)
        {
            float weight = FacesWeight(config.FacesWeights[i], facesHook);
            if (weight <= 0f) continue;
            faces = config.FacesWeights[i].Faces;
            if (facesRoll < weight) break;
            facesRoll -= weight;
        }
        return new DiceCard(faces, colors);
    }
}
