using UnityEngine;

/// <summary>들어간 피해의 종류. 피해 숫자 색을 고를 때 쓴다(치명타가 방어력에 깎여도 치명타로 본다).</summary>
public enum DamageKind
{
    Normal,
    Critical,
    Reduced, // 방어력에 깎였다
    Burn,  // 화상(지속 피해)
    BonusExp, // 피해가 아니라 더블 업(ExpBoost)으로 더 받은 경험치 — 같은 튀는 숫자로 "+N"을 보인다
}

/// <summary>
/// 피해 공식(기획서 9.1). 배율(치명타·저주)은 먼저 곱해 들어오고, 방어력은 나중에 뺀다
/// — 그래서 치명타·저주가 단단한 적에게 특히 강하다. 방어력을 넘는 피해가 없어도 최소 비율은 들어간다(면역 없음).
/// </summary>
public static class MonsterDamage
{
    /// <param name="damage">배율을 모두 곱한 피해.</param>
    /// <param name="armor">취약으로 깎은 뒤의 방어력. 0보다 작으면 그만큼 피해가 더 들어간다(취약이 방어력 없는 적에게도 통하게).</param>
    /// <param name="penetration">방어력에서 빼는 양(관통). 남은 방어력이 있을 때만 깎는다.</param>
    /// <param name="minRatio">방어력에 막혀도 들어가는 피해 비율.</param>
    public static float Apply(float damage, float armor, float penetration, float minRatio)
    {
        float remainingArmor = armor > 0f ? Mathf.Max(armor - penetration, 0f) : armor;
        return Mathf.Max(damage - remainingArmor, damage * minRatio);
    }
}
