using System;

/// <summary>카드 종류(기획서 5장 — 놓는 곳 기준).</summary>
public enum CardKind
{
    Dice,
    Skill,
    Chip,
    Hero,
}

/// <summary>
/// 손패·레벨업에 오가는 카드 한 장: 주사위 카드, 칩 카드, 스킬 카드, 영웅 강화 카드(기획서 5장 카드 종류).
/// 주사위 카드는 색 × 면체 값이고(DiceCard) 나머지는 종류마다 SO 하나라 넷을 한 값에 담는다. 같은 값끼리 손패에서 겹친다.
/// 영웅 강화 카드는 레벨업에서 고르는 즉시 적용되어 손패에 들어가지 않는다.
/// </summary>
public readonly struct HandCard : IEquatable<HandCard>
{
    /// <summary>주사위 카드일 때의 값. 다른 종류면 쓰지 않는다.</summary>
    public readonly DiceCard Dice;
    /// <summary>스킬 카드. 아니면 null.</summary>
    public readonly SO_SkillCard Skill;
    /// <summary>칩 카드. 아니면 null.</summary>
    public readonly SO_ChipCard Chip;
    /// <summary>영웅 강화 카드. 아니면 null.</summary>
    public readonly SO_HeroCard Hero;

    public bool IsSkill => Skill != null;
    public bool IsChip => Chip != null;
    public bool IsHero => Hero != null;
    public bool IsDice => Skill == null && Chip == null && Hero == null;

    public CardKind Kind => IsSkill ? CardKind.Skill : IsChip ? CardKind.Chip : IsHero ? CardKind.Hero : CardKind.Dice;

    /// <summary>SO로 된 카드(칩·스킬·영웅 강화)의 공통 내용. 주사위 카드면 null.</summary>
    public SO_Card Asset => Chip != null ? Chip : Skill != null ? Skill : Hero;

    private HandCard(DiceCard dice, SO_SkillCard skill, SO_ChipCard chip, SO_HeroCard hero)
    {
        Dice = dice;
        Skill = skill;
        Chip = chip;
        Hero = hero;
    }

    public static HandCard Of(DiceCard dice) => new HandCard(dice, null, null, null);
    public static HandCard Of(SO_SkillCard skill) => new HandCard(default, skill, null, null);
    public static HandCard Of(SO_ChipCard chip) => new HandCard(default, null, chip, null);
    public static HandCard Of(SO_HeroCard hero) => new HandCard(default, null, null, hero);

    /// <summary>SO 카드의 종류를 보고 만든다(레벨업 뽑기·전용 카드처럼 종류가 섞인 목록에서). 모르는 종류면 주사위 카드(기본값)다.</summary>
    public static HandCard Of(SO_Card card)
    {
        switch (card)
        {
            case SO_SkillCard skill: return Of(skill);
            case SO_ChipCard chip: return Of(chip);
            case SO_HeroCard hero: return Of(hero);
            default: return default;
        }
    }

    public bool Equals(HandCard other) => IsDice ? other.IsDice && Dice == other.Dice : Asset == other.Asset;
    public override bool Equals(object obj) => obj is HandCard other && Equals(other);
    public override int GetHashCode() => IsDice ? Dice.GetHashCode() : Asset.GetHashCode();
    public static bool operator ==(HandCard a, HandCard b) => a.Equals(b);
    public static bool operator !=(HandCard a, HandCard b) => !a.Equals(b);
}
