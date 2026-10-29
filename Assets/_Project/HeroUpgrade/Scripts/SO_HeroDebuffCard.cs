using UnityEngine;

/// <summary>영웅 디버프 카드가 거는 것. 칩 디버프 넷과 같은 종류다(색 = 뜻 — 카드 색도 그 칩 색).</summary>
public enum HeroDebuff
{
    Vulnerable,
    Slow,
    Freeze,
    Burn,
}

/// <summary>
/// 영웅 디버프 카드(기획서 10.4, 2026-10-28). 고르면 이번 판 동안 영웅 카드에 맞은 적에게 디버프를 건다.
/// 칩 디버프의 센 버전이다 — 칩은 그 칸 주사위가 맞히는 모든 적에게 걸고 영웅은 몇 마리(투사체 수)에게만 걸어 세게 줘도 판이 깨지지 않고,
/// 디버프는 겹치지 않고 센 쪽이 남아 칩과 다투지 않는다. 영웅 피해를 곱하지 않고 주사위를 돕는 옆 갈래라
/// 영웅 강화 곱 갈래(공격력·투사체·치명) 공급을 묽힌다(레인보우 몰빵 대책).
/// 상속한 Stat·Amount·Flat은 쓰지 않는다(능력치 카드용). 여러 장은 **지속 시간을 더하고 세기는 그대로다**(2026-10-28 사용자 결정) —
/// 세기를 더하면 취약은 영웅 자신의 피해까지 키워 곱 갈래가 하나 더 생기고, 화상(최대 체력 비례)은 웨이브 체력 증가를 무시해 무한 모드를 깬다.
/// 저항(거대·보스)과 얼림 면역은 칩과 같이 MonsterField가 적용한다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroUpgrade/Hero Debuff Card", fileName = "SO_HeroCard_")]
public sealed class SO_HeroDebuffCard : SO_HeroCard
{
    [Header("디버프")]
    public HeroDebuff Debuff = HeroDebuff.Vulnerable;
    [Tooltip("세기 — 취약: 더 받는 피해 비율, 둔화: 느려지는 비율, 화상: 매초 최대 체력 비율. 얼림은 쓰지 않는다.")]
    [Min(0f)] public float Ratio = 0.6f;
    [Tooltip("지속 시간(초). 얼림은 한 장에 어는 시간이다.")]
    [Min(0f)] public float Duration = 2f;
    [Tooltip("얼림이 풀린 뒤 다시 얼지 않는 시간(초) — 투사체가 많아도 한 적을 계속 얼려 두지 못하게. 얼림만 쓴다.")]
    [Min(0f)] public float Immunity = 1.5f;

    /// <summary>copies장을 골랐을 때 지속 시간(초, 얼림은 어는 시간).</summary>
    public float DurationFor(int copies) => Duration * copies;

    public void Apply(MonsterField monsters, int slot, int copies)
    {
        if (copies <= 0) return;
        float duration = DurationFor(copies);
        switch (Debuff)
        {
            case HeroDebuff.Vulnerable: monsters.Vulnerable(slot, Ratio, duration); break;
            case HeroDebuff.Slow: monsters.Slow(slot, Ratio, duration); break;
            case HeroDebuff.Freeze: monsters.Freeze(slot, duration, Immunity); break;
            case HeroDebuff.Burn: monsters.Burn(slot, Ratio, duration); break;
        }
    }
}
