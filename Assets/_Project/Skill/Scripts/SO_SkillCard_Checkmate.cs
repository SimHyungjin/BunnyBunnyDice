using UnityEngine;

/// <summary>
/// 체크메이트(적 대상): 모든 적에게 작은 피해를 주고, 체력이 가장 많이 남은 적(왕) 하나에게는 큰 피해를 준다(체스의 외통수 — 왕을 잡는다).
/// 다른 적 대상 스킬은 떼를 치고, 이것은 보스·단단한 적처럼 주사위가 오래 붙잡혀 있는 한 마리를 지운다.
/// 왕이 받는 피해는 영웅 공격력이 아니라 왕 자신의 최대 체력의 배수다(2026-10-31 사용자, 방어력 무시) — 보통 적은 왕이 되면 쓰러지고,
/// 보스는 피해 저항(90%)을 그대로 받아 최대 체력의 절반쯤을 잃는다. 저항을 무시하는 예외 없이 "보스 절반"이 저항 규칙에서 나온다.
/// 처음엔 영웅 공격력 × 500이라 영웅이 약한 캐릭터·초반엔 왕을 거의 못 깎았다.
/// 왕 고르기는 SkillCaster.FindKing이 맡아 미리보기도 같은 왕 밑에 큰 고리를 띄운다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Checkmate", fileName = "SO_SkillCard_Checkmate")]
public sealed class SO_SkillCard_Checkmate : SO_EnemySkillCard
{
    [Tooltip("왕(체력이 가장 많은 적)이 받는 피해 = 왕의 최대 체력 × 이 값(방어력 무시, 피해 저항은 받는다). 왕은 DamageMultiplier 몫 대신 이것만 받는다.")]
    [Min(0f)] public float KingMaxHealth = 5f;
}
