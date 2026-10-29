using UnityEngine;

/// <summary>
/// 주사위 하나의 공격 능력치. 주사위 상태(DiceState)와 종류(SO_DiceType)에서 매번 계산하고 들고 있지 않는다
/// — 얹기·페인트로 색과 눈이 바뀌거나 실행 중 설정을 고쳐도 따로 갱신할 것이 없게.
/// </summary>
public struct DiceStats
{
    public float Damage;
    /// <summary>한 방마다 더하는 피해의 맞은 적 최대 체력 비율(SO_DiceType.MaxHealthDamage — 각성·오버드라이브·칩 배율은 고정 피해와 함께 곱한다).</summary>
    public float MaxHealthRatio;
    /// <summary>공격 간격(초).</summary>
    public float AttackInterval;
    /// <summary>칩 보너스를 뺀 사거리(종류마다 고정, 눈으로 자라지 않는다 — 그래서 따로 상한을 두지 않는다). 파랑은 각성하면 늘어난다.</summary>
    public float Range;
    public float CritChance;

    /// <summary>종류의 기본값을 눈으로 키운다(기획서 6.5). 속사(초록) 각성의 공격 속도, 저격(파랑) 각성의 사거리도 여기서 더한다.</summary>
    public static DiceStats Compute(in DiceState dice, SO_DiceType type, SO_DiceStatsConfig config) =>
        Compute(dice, type, config, config.IsAwakened(dice));

    /// <param name="awakened">각성으로 칠지 — 눈이 모자라도 스킬(세븐스 헤븐)이 잠깐 각성시킬 수 있다.</param>
    public static DiceStats Compute(in DiceState dice, SO_DiceType type, SO_DiceStatsConfig config, bool awakened)
    {
        if (type == null) return default;
        float growth = config.GetGrowth(dice.Pips);
        float speed = type.AttackSpeed * (1f + type.AttackSpeedGrowth * growth);
        if (type is SO_DiceType_Rapid rapid && awakened)
            speed *= 1f + rapid.AwakenAttackSpeedBonus;
        if (speed <= 0f) return default;
        float range = type.Range;
        if (type is SO_DiceType_LongRange longRange && awakened)
            range *= 1f + longRange.AwakenRangeBonus;

        // 각성은 한 방 피해 전체(고정 + 최대 체력 피해)를 키운다. 최대 체력 피해는 눈으로 자라지 않는다(보수적으로 — 각성 때만 커진다).
        float awaken = awakened ? config.AwakenDamageScale : 1f;
        return new DiceStats
        {
            Damage = type.Damage * (1f + type.DamageGrowth * growth) * awaken,
            MaxHealthRatio = type.MaxHealthDamage * awaken,
            AttackInterval = 1f / speed,
            Range = range,
            CritChance = Mathf.Clamp01(dice.Pips * config.CritChancePerPip),
        };
    }

    /// <summary>칩 보너스(사거리 칩 — ChipEffects.RangeBonus, 비율)를 곱한 실제 사거리.</summary>
    public float GetRange(float bonus) => Range * (1f + bonus);
}
