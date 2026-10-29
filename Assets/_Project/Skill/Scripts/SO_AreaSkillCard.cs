using UnityEngine;

/// <summary>
/// 적 대상 스킬(포켓·레이크·백스핀·체크메이트·체크, 기획서 5.3): 판 위의 모든 적에게 쓴다(2026-10-29 사용자 결정 — 처음엔 한 점의 원이었다).
/// 모두 영웅 공격력 비례 피해를 먼저 주고(스킬 발사대, 기획서 10.1), 스킬마다 특징(처형·끌어모으기·밀어내기·멈추기)이 하나씩 붙는다.
/// 피해는 영웅 치명타를 굴리고 방어력에 막힌다(모든 피해가 같은 공식을 지나 단단함 상성이 유지되게).
/// 판 위 아무 곳에나 놓으면 쓴다 — 놓은 자리를 쓰는 것은 레이크(모으는 점)뿐이다.
/// </summary>
public abstract class SO_AreaSkillCard : SO_SkillCard, ICardTextTokens
{
    [Tooltip("적 하나마다 주는 피해 = 영웅 공격력 × 이 값. 0이면 피해 없이 특징만.")]
    [Min(0f)] public float DamageMultiplier = 3f;

    public override SkillTarget Target => SkillTarget.Area;

    /// <summary>{HeroDamage}: 영웅을 알면 지금 피해와 배율, 모르면 배율만. 하위 클래스가 토큰을 더 낼 수 있다(체크메이트의 {KingDamage}).</summary>
    public virtual bool TryGetToken(string key, HeroStats hero, out string text)
    {
        text = null;
        if (key != "HeroDamage") return false;
        text = HeroDamageText(hero, DamageMultiplier);
        return true;
    }

    protected static string HeroDamageText(HeroStats hero, float multiplier)
    {
        string label = $"{HeroStatText.GetName(HeroStat.Attack)} ×{multiplier:0.##}";
        return hero != null ? $"{hero.Attack * multiplier:0}({label})" : label;
    }
}
