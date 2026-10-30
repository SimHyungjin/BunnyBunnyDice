using System.Collections.Generic;

/// <summary>레벨업 때 봇이 보는 판 상황(GameBot이 채운다).</summary>
public struct BotChoiceContext
{
    /// <summary>미로를 "어느 정도" 지었다(SO_BotConfig.MazeEnoughDice) — d20·메인 딜 구간 칩으로 넘어간다.</summary>
    public bool MazeEnough;
    /// <summary>메인 딜 구간 주사위 밑에 이미 있는 디버프 칩 종류.</summary>
    public bool HasSlow;
    public bool HasFreeze;
    public bool HasVulnerable;
    /// <summary>캐시 아웃로 치울 칸(메인 딜 구간을 막는 칸)이 있다.</summary>
    public bool HasCashOutTarget;
    /// <summary>화상 칩을 깔 자리가 있다 — 칩 없는 범위 주사위 중 아직 화상이 안 닿는 길을 새로 덮는 것.</summary>
    public bool HasBurnSpot;
}

/// <summary>
/// 봇의 레벨업 고르기(기획서 11.4 — 사용자 판 교본 2차, 2026-10-28). 카드마다 점수(SO_BotConfig.Choice)를 매겨 가장 높은 것을 고른다.
/// 받는 장수(x2)만큼 곱한다(사용자: 두 배가 맞다). 미로를 짓는 동안은 미로 주사위가 영웅 능력치 카드보다 먼저, 그다음 능력치 카드, 어느 정도 지은 뒤엔 d20·메인 딜 구간 칩.
/// (처음엔 능력치 카드를 언제나 먼저 골랐다 — 주사위에 최대 체력 피해가 붙은 뒤로는 초반에 주사위를 못 모아 무너져 미로 주사위 아래로 내렸다, 2026-10-29.)
/// 봇은 기준선이지 최선이 아니다.
/// </summary>
public static class BotCardChoice
{
    /// <summary>후보 중 고를 번호.</summary>
    /// <param name="set">비교 묶음(카드 종류별 점수 배율). 없으면 null.</param>
    public static int Choose(SO_BotConfig config, IReadOnlyList<HandCard> candidates, IReadOnlyList<int> copies, in BotChoiceContext context,
        SO_BotConfig.RunSet set)
    {
        int best = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            int count = i < copies.Count ? copies[i] : 1;
            float score = Score(config.Choice, candidates[i], context) * KindScale(set, candidates[i]) * count;
            if (score <= bestScore) continue;
            bestScore = score;
            best = i;
        }
        return best;
    }

    private static float Score(SO_BotConfig.ChoiceScores scores, HandCard card, in BotChoiceContext context)
    {
        if (card.IsDice) return DiceScore(scores, card.Dice, context.MazeEnough);
        if (card.IsHero) return card.Hero is SO_HeroDebuffCard ? scores.HeroDebuff : scores.HeroStat;
        if (card.IsChip) return ChipScore(scores, card.Chip, context);

        SO_SkillCard skill = card.Skill;
        if (skill is SO_SkillCard_CashOut) return context.HasCashOutTarget ? scores.CashOut : 0f;
        if (skill is SO_SkillCard_FullHouse || skill is SO_SkillCard_BreakShot || skill is SO_SkillCard_SnakeEyes
            || skill is SO_SkillCard_DoubleUp) return scores.Exclusive;
        if (skill is SO_EnemySkillCard) return scores.EnemySkill;
        if (skill is SO_SkillCard_Check) return scores.Check;
        if (skill is SO_SkillCard_Raise) return scores.Raise;
        if (skill is SO_SkillCard_Jackpot) return scores.Jackpot;
        if (skill is SO_SkillCard_Reroll) return scores.Reroll;
        if (skill is SO_SkillCard_Paint) return scores.Paint;
        if (skill is SO_SkillCard_AllIn) return scores.AllIn;
        return 0f;
    }

    // d20은 메인 딜 구간용, d6·d12는 미로용(사용자). 미로용 색은 빨강 > 파랑·자홍 > 나머지.
    private static float DiceScore(SO_BotConfig.ChoiceScores scores, DiceCard dice, bool mazeEnough)
    {
        if (dice.Faces >= 20)
        {
            if (!mazeEnough) return scores.EarlyD20;
            return BotDice.IsArea(dice.Colors) ? scores.D20Area : scores.D20Single;
        }
        if (mazeEnough) return scores.LateDice;
        if (dice.Colors == DiceColor.Red) return scores.MazeDiceRed;
        if (dice.Colors == DiceColor.Blue || dice.Colors == (DiceColor.Red | DiceColor.Blue)) return scores.MazeDiceBlueMagenta;
        return scores.MazeDiceOther;
    }

    private static float ChipScore(SO_BotConfig.ChoiceScores scores, SO_ChipCard chip, in BotChoiceContext context)
    {
        // 화상은 깔 자리(새 길을 덮는 범위 주사위)가 있을 때만 미로의 일부로 친다 — 없는데 고르면 주사위를 못 받아 미로가 늦어진다.
        if (chip is SO_ChipCard_Burn) return !context.HasBurnSpot ? scores.EarlyChip : context.MazeEnough ? scores.BurnChip : scores.MazeBurnChip;
        // 미로를 짓는 동안엔 메인 딜 구간 칩보다 미로가 먼저다(사용자: 능력치가 없으면 미로 우선).
        if (!context.MazeEnough) return scores.EarlyChip;
        if (chip is SO_ChipCard_Slow) return context.HasSlow ? scores.DuplicateDebuffChip : scores.MissingDebuffChip;
        if (chip is SO_ChipCard_Freeze) return context.HasFreeze ? scores.DuplicateDebuffChip : scores.MissingDebuffChip;
        if (chip is SO_ChipCard_Vulnerable) return context.HasVulnerable ? scores.DuplicateDebuffChip : scores.MissingDebuffChip;
        if (chip is SO_ChipCard_Damage || chip is SO_ChipCard_AttackSpeed) return scores.AttackChip;
        return scores.RangeChip;
    }

    private static float KindScale(SO_BotConfig.RunSet set, HandCard card)
    {
        if (set == null) return 1f;
        if (card.IsDice) return set.DiceScale;
        if (card.IsHero) return set.HeroScale;
        if (card.IsChip) return set.ChipScale;
        return card.Skill is SO_EnemySkillCard ? set.DamageSkillScale : set.OtherSkillScale;
    }
}
