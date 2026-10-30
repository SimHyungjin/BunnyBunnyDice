/// <summary>
/// 칩의 효과(기획서 7). 디버프 4장(방깍·슬로우·얼음·화상)은 칩 위 주사위가 맞히는 모든 적에게 건다 — 폭발 파편·체인 튕김도 포함.
/// 강화 3장(공격력·공격 속도·사거리)은 쏘기 전에 능력치를 바꾸므로 DiceCombat·미리보기가 아래 배율을 읽는다.
/// 칸 하나의 칩(두 장까지)과 세기는 ChipSet이 묶어 이것을 부른다.
/// </summary>
public static class ChipEffects
{
    /// <param name="strength">세기 배율(IChipScale). 지속 시간은 그대로 두고 세기에만 곱한다 — 얼림은 어는 시간이 곧 세기다.</param>
    public static void OnHit(MonsterField monsters, int slot, SO_ChipCard chip, float strength)
    {
        switch (chip)
        {
            case SO_ChipCard_Slow slow:
                monsters.Slow(slot, slow.Ratio * strength, slow.Duration);
                break;
            case SO_ChipCard_Burn burn:
                monsters.Burn(slot, burn.MaxHealthRatio * strength, burn.Duration);
                break;
            case SO_ChipCard_Vulnerable vulnerable:
                monsters.Vulnerable(slot, vulnerable.Ratio * strength, vulnerable.Duration);
                break;
            case SO_ChipCard_Freeze freeze:
                monsters.Freeze(slot, freeze.Duration * strength, freeze.Immunity);
                break;
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
