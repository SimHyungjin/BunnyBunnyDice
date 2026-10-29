using UnityEngine;

/// <summary>
/// 체크메이트(적 대상): 모든 적에게 작은 피해를 주고, 체력이 가장 많이 남은 적(왕) 하나에게는 큰 피해를 준다(체스의 외통수 — 왕을 잡는다).
/// 다른 적 대상 스킬은 떼를 치고, 이것은 보스·단단한 적처럼 주사위가 오래 붙잡혀 있는 한 마리를 지운다.
/// 왕은 보스면 피해 저항(90%)을 그대로 받는다 — 그래서 왕 배율이 아주 크다.
/// 왕 고르기는 SkillCaster.FindKing이 맡아 미리보기도 같은 왕 밑에 큰 고리를 띄운다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Checkmate", fileName = "SO_SkillCard_Checkmate")]
public sealed class SO_SkillCard_Checkmate : SO_AreaSkillCard
{
    [Tooltip("왕(체력이 가장 많은 적)이 받는 피해 = 영웅 공격력 × 이 값. 왕은 DamageMultiplier 몫 대신 이것만 받는다.")]
    [Min(0f)] public float KingMultiplier = 500f;

    /// <summary>{HeroDamage}에 더해 {KingDamage}: 왕이 받는 피해.</summary>
    public override bool TryGetToken(string key, HeroStats hero, out string text)
    {
        if (key != "KingDamage") return base.TryGetToken(key, hero, out text);
        text = HeroDamageText(hero, KingMultiplier);
        return true;
    }
}
