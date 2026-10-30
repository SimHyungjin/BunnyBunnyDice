/// <summary>
/// 한 칸의 칩 묶음 — 칩 한 장, 또는 사이드 팟으로 얹은 다른 종류 한 장까지 두 장(기획서 10.3)과 그때의 세기(IChipScale).
/// 칩 효과를 읽는 곳(공격·미리보기·봇)이 모두 이것을 지나 두 장의 효과를 같이 받는다. 두 장은 늘 종류가 달라 같은 효과가 겹치지 않는다.
/// 쏠 때 묶어 넘긴다(DiceAttack) — 두 번째 파동처럼 늦게 맞히는 공격도 쏜 순간의 칩으로 친다.
/// </summary>
public readonly struct ChipSet
{
    public readonly SO_ChipCard First;
    /// <summary>얹은 두 번째 칩. 없으면 null.</summary>
    public readonly SO_ChipCard Second;
    public readonly float Strength;

    public ChipSet(SO_ChipCard first, SO_ChipCard second, float strength)
    {
        First = first;
        Second = second;
        Strength = strength;
    }

    public bool IsEmpty => First == null && Second == null;

    /// <summary>쌓인 칩 장수 — 그 위 주사위가 앉는 높이(ChipHeight × 장수).</summary>
    public int Layers => (First != null ? 1 : 0) + (Second != null ? 1 : 0);

    public bool Contains<T>() where T : SO_ChipCard => First is T || Second is T;

    /// <summary>사거리를 늘리는 비율(0.5 = 1.5배).</summary>
    public float RangeBonus => (ChipEffects.RangeBonus(First) + ChipEffects.RangeBonus(Second)) * Strength;

    public float DamageBonus => (ChipEffects.DamageBonus(First) + ChipEffects.DamageBonus(Second)) * Strength;

    public float AttackSpeedBonus => (ChipEffects.AttackSpeedBonus(First) + ChipEffects.AttackSpeedBonus(Second)) * Strength;

    /// <summary>칩 위 주사위가 적을 맞힐 때 디버프를 건다.</summary>
    public void OnHit(MonsterField monsters, int slot)
    {
        if (First != null) ChipEffects.OnHit(monsters, slot, First, Strength);
        if (Second != null) ChipEffects.OnHit(monsters, slot, Second, Strength);
    }
}
