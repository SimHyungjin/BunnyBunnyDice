/// <summary>
/// 한 칸의 칩 — 칩 한 장과 그 칩의 세기(보통 1, 빅 스택으로 놓은 칩은 그 카드의 Strength — 기획서 10.3).
/// 칩 효과를 읽는 곳(공격·미리보기·봇)이 모두 이것을 지나 세기를 같이 받는다.
/// 쏠 때 묶어 넘긴다(DiceAttack) — 두 번째 파동처럼 늦게 맞히는 공격도 쏜 순간의 칩으로 친다.
/// </summary>
public readonly struct ChipSet
{
    public readonly SO_ChipCard Card;
    public readonly float Strength;

    public ChipSet(SO_ChipCard card, float strength)
    {
        Card = card;
        Strength = strength;
    }

    public bool IsEmpty => Card == null;

    public bool Contains<T>() where T : SO_ChipCard => Card is T;

    /// <summary>사거리를 늘리는 비율(0.5 = 1.5배).</summary>
    public float RangeBonus => ChipEffects.Scaled(Card, "Ratio", ChipEffects.RangeBonus(Card), Strength);

    public float DamageBonus => ChipEffects.Scaled(Card, "Ratio", ChipEffects.DamageBonus(Card), Strength);

    public float AttackSpeedBonus => ChipEffects.Scaled(Card, "Ratio", ChipEffects.AttackSpeedBonus(Card), Strength);

    /// <summary>칩 위 주사위가 적을 맞힐 때 디버프를 건다.</summary>
    public void OnHit(MonsterField monsters, int slot)
    {
        if (Card != null) ChipEffects.OnHit(monsters, slot, Card, Strength);
    }
}
