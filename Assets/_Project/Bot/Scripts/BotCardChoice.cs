using System.Collections.Generic;

/// <summary>
/// 봇의 레벨업 고르기(기획서 11.2·11.5). 웨이브 구간마다 카드 종류에 점수를 매겨(SO_BotConfig.Phases) 가장 높은 것을 고른다.
/// 점수는 사용자 판 기록에서 구간마다 고른 카드를 옮긴 것이다 — 봇은 기준선이지 최선이 아니다.
/// </summary>
public static class BotCardChoice
{
    /// <summary>후보 중 고를 번호.</summary>
    /// <param name="mazeBuilt">미로를 다 지었는지 — 아니면 주사위 카드에 MazeDiceBonus를 더한다.</param>
    /// <param name="set">비교 묶음(카드 종류별 점수 배율). 없으면 null.</param>
    public static int Choose(SO_BotConfig config, int wave, IReadOnlyList<HandCard> candidates, IReadOnlyList<int> copies, bool mazeBuilt,
        SO_BotConfig.RunSet set)
    {
        SO_BotConfig.PhaseScores phase = config.GetPhase(wave);
        if (phase == null) return 0;
        int best = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            int count = i < copies.Count ? copies[i] : 1;
            float score = Score(config, phase, candidates[i], mazeBuilt, set) * (1f + config.ExtraCopy * (count - 1));
            if (score <= bestScore) continue;
            bestScore = score;
            best = i;
        }
        return best;
    }

    private static float Score(SO_BotConfig config, SO_BotConfig.PhaseScores phase, HandCard card, bool mazeBuilt, SO_BotConfig.RunSet set)
    {
        if (card.IsDice) return (phase.Dice + phase.FacesWeight * card.Dice.Faces) * (set != null ? set.DiceScale : 1f) + (mazeBuilt ? 0f : config.MazeDiceBonus);
        return KindScore(phase, card) * KindScale(set, card);
    }

    private static float KindScale(SO_BotConfig.RunSet set, HandCard card)
    {
        if (set == null) return 1f;
        if (card.IsHero) return set.HeroScale;
        if (card.IsChip) return set.ChipScale;
        return card.Skill is SO_AreaSkillCard ? set.DamageSkillScale : set.OtherSkillScale;
    }

    private static float KindScore(SO_BotConfig.PhaseScores phase, HandCard card)
    {
        if (card.IsHero)
        {
            // 디버프 카드는 칩 디버프의 센 버전이라 칩 점수로 고른다.
            if (card.Hero is SO_HeroDebuffCard) return phase.Chip;
            switch (card.Hero.Stat)
            {
                case HeroStat.CritChance:
                case HeroStat.CritDamage: return phase.HeroCrit;
                // 공격력·투사체 — 둘 다 영웅 피해를 늘린다(떼냐 단단한 적이냐만 다르다).
                default: return phase.HeroDamage;
            }
        }
        if (card.IsChip) return phase.Chip;
        SO_SkillCard skill = card.Skill;
        if (skill is SO_SkillCard_Checkmate) return phase.Checkmate;
        if (skill is SO_AreaSkillCard) return phase.AreaSkill;
        if (skill is SO_SkillCard_Check) return phase.Check;
        if (skill is SO_SkillCard_Overdrive) return phase.Overdrive;
        if (skill is SO_SkillCard_Jackpot) return phase.Jackpot;
        if (skill is SO_SkillCard_Reroll) return phase.Reroll;
        if (skill is SO_SkillCard_Paint) return phase.Paint;
        if (skill is SO_SkillCard_AllIn) return phase.AllIn;
        return phase.Other;
    }
}
