using UnityEngine;

/// <summary>
/// 영웅 능력치의 이름·숫자 표시. 카드(영웅 강화)와 테스트 창이 같은 글로 보이게 한곳에 둔다.
/// </summary>
public static class HeroStatText
{
    // 배열이 아니라 switch인 이유: 번역표 갱신 툴이 App.Localize("…") 모양만 찾아 모은다.
    public static string GetName(HeroStat stat)
    {
        switch (stat)
        {
            case HeroStat.Attack: return App.Localize("공격력");
            case HeroStat.AttackSpeed: return App.Localize("공격 속도");
            case HeroStat.MoveSpeed: return App.Localize("이동 속도");
            case HeroStat.Range: return App.Localize("사거리");
            case HeroStat.CritChance: return App.Localize("치명타 확률");
            case HeroStat.CritDamage: return App.Localize("치명타 피해");
            case HeroStat.Projectile: return App.Localize("투사체");
            default: return stat.ToString();
        }
    }

    // 공격력은 정수로 읽히는 값이라 소수 자리를 붙이지 않고, 나머지(속도·사거리·배율)는 소수 한 자리를 늘 붙인다
    // — "2"와 "1.6"이 나란히 있으면 자릿수가 달라 들쭉날쭉해 보인다.
    public static string Format(HeroStat stat, float value)
    {
        switch (stat)
        {
            case HeroStat.Attack: return value.ToString("0.#");
            case HeroStat.Projectile: return Mathf.RoundToInt(value).ToString();
            case HeroStat.CritChance: return $"{value * 100f:0.#}%";
            case HeroStat.CritDamage: return $"×{value:0.0#}";
            default: return value.ToString("0.0#");
        }
    }

    /// <summary>카드 제목에 붙이는 오르는 양 — 기본값의 %면 "+100%", 그대로 더하면 확률 "+20%p" · 배율 "+1".</summary>
    public static string FormatAmount(SO_HeroCard card)
    {
        if (!card.Flat) return $"+{card.Amount * 100f:0.#}%";
        return card.Stat == HeroStat.CritChance ? $"+{card.Amount * 100f:0.#}%p" : $"+{card.Amount:0.##}";
    }
}
