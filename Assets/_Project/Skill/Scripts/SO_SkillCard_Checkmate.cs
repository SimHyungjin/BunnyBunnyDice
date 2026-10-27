using UnityEngine;

/// <summary>
/// 체크메이트(범위·즉발): 원 안의 적 모두에게 작은 피해를 주고, 그중 체력이 가장 많이 남은 적(왕) 하나에게 큰 피해를 더 준다(체스의 외통수 — 왕을 잡는다).
/// 처음엔 왕 하나만 쳤으나(2026-10-26) 보스가 없는 웨이브에선 쓸 데가 없고, 강한 영웅이면 일반 적에게 넘친 피해가 다 버려졌다(2026-10-27 사용자 결정).
/// 다른 범위 스킬은 떼를 치고, 이것은 보스·단단한 적처럼 주사위가 오래 붙잡혀 있는 한 마리를 지운다. 보스도 깎지 않고 그대로 받는다.
/// 왕 고르기는 SkillCaster.FindKing이 맡아 미리보기도 같은 왕 밑에 큰 고리를 띄운다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Checkmate", fileName = "SO_SkillCard_Checkmate")]
public sealed class SO_SkillCard_Checkmate : SO_AreaSkillCard
{
    [Tooltip("왕(원 안에서 체력이 가장 많은 적)에게 더 주는 피해 = 영웅 공격력 × 이 값. 왕은 DamageMultiplier 몫도 함께 받는다.")]
    [Min(0f)] public float KingMultiplier = 30f;

    /// <summary>{HeroDamage}에 더해 {KingDamage}: 왕이 더 받는 피해.</summary>
    public override bool TryGetToken(string key, HeroStats hero, out string text)
    {
        if (key != "KingDamage") return base.TryGetToken(key, hero, out text);
        text = HeroDamageText(hero, KingMultiplier);
        return true;
    }
}
