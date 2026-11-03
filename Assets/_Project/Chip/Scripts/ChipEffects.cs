using UnityEngine;

/// <summary>
/// 칩의 효과(기획서 7). 디버프 4장(방깍·슬로우·얼음·화상)은 칩 위 주사위가 맞히는 모든 적에게 건다 — 폭발 파편·체인 튕김도 포함.
/// 강화 3장(공격력·공격 속도·사거리)은 쏘기 전에 능력치를 바꾸므로 DiceCombat·미리보기가 아래 배율을 읽는다.
/// 칸 하나의 칩과 세기는 ChipSet이 묶어 이것을 부른다.
/// </summary>
public static class ChipEffects
{
    /// <param name="strength">세기 배율(보통 칩은 1, 빅 스택 칩은 그 카드의 Strength). 어느 값에 곱하는지는 Scaled가 정한다.</param>
    public static void OnHit(MonsterField monsters, int slot, SO_ChipCard chip, float strength)
    {
        switch (chip)
        {
            case SO_ChipCard_Slow slow:
                monsters.Slow(slot, Scaled(chip, nameof(slow.Ratio), slow.Ratio, strength), slow.Duration);
                break;
            case SO_ChipCard_Burn burn:
                monsters.Burn(slot, Scaled(chip, nameof(burn.MaxHealthRatio), burn.MaxHealthRatio, strength), burn.Duration,
                    Scaled(chip, nameof(burn.RegenReduction), burn.RegenReduction, strength));
                break;
            case SO_ChipCard_Vulnerable vulnerable:
                monsters.Vulnerable(slot, Scaled(chip, nameof(vulnerable.Ratio), vulnerable.Ratio, strength), vulnerable.Duration);
                break;
            case SO_ChipCard_Freeze freeze:
                monsters.Freeze(slot, Scaled(chip, nameof(freeze.Duration), freeze.Duration, strength), freeze.Immunity);
                break;
        }
    }

    /// <summary>
    /// 칩 필드 하나에 세기를 곱한 값. 효과(OnHit·능력치 배율)와 빅 스택 카드 글이 함께 읽는다 — 글이 보여 주는 강화 수치가 실제와 어긋나지 않게.
    /// 세기는 효과의 크기에만 곱하고 지속 시간은 그대로 둔다. 얼림만은 어는 시간이 곧 크기라 시간에 곱한다(면역 시간은 그대로).
    /// </summary>
    public static float Scaled(SO_ChipCard chip, string field, float value, float strength)
    {
        switch (chip)
        {
            case SO_ChipCard_Burn:
                if (field == nameof(SO_ChipCard_Burn.MaxHealthRatio)) return value * strength;
                // 재생 감소는 1(완전히 막음)을 넘지 않는다.
                if (field == nameof(SO_ChipCard_Burn.RegenReduction)) return Mathf.Min(1f, value * strength);
                return value;
            case SO_ChipCard_Freeze:
                return field == nameof(SO_ChipCard_Freeze.Duration) ? value * strength : value;
            default:
                // 둔화·방깍과 강화 3장은 Ratio가 크기다.
                return field == "Ratio" ? value * strength : value;
        }
    }

    // 공격과 미리보기가 같은 값을 쓰게 한곳에 둔다. 해당 칩이 아니면 0.

    /// <summary>사거리 칩이 늘리는 비율(0.5 = 1.5배).</summary>
    public static float RangeBonus(SO_ChipCard chip) => chip is SO_ChipCard_Range range ? range.Ratio : 0f;

    /// <summary>공격력 칩이 늘리는 비율.</summary>
    public static float DamageBonus(SO_ChipCard chip) => chip is SO_ChipCard_Damage damage ? damage.Ratio : 0f;

    /// <summary>공격 속도 칩이 늘리는 비율.</summary>
    public static float AttackSpeedBonus(SO_ChipCard chip) => chip is SO_ChipCard_AttackSpeed speed ? speed.Ratio : 0f;
}
