using UnityEngine;

/// <summary>
/// 체크메이트(적 대상): 모든 적에게 작은 피해를 주고, 체력이 가장 많이 남은 적(왕) 하나에게는 큰 피해를 준다(체스의 외통수 — 왕을 잡는다).
/// 다른 적 대상 스킬은 떼를 치고, 이것은 보스·단단한 적처럼 주사위가 오래 붙잡혀 있는 한 마리를 지운다.
/// 왕은 영웅 공격력이 아니라 자기 최대 체력의 비율을 잃는다(MonsterField.LoseMaxHealth — 방어력·저항·보호막 무시) — 보통 적은 쓰러지고 보스는 BossKingLoss만큼.
/// 처음엔 영웅 공격력 × 500이라 영웅이 약한 캐릭터·초반엔 왕을 거의 못 깎았고, 다음엔 최대 체력 × 5에 저항을 받게 해 "보스 절반"을 저항 규칙에서 냈는데
/// 보스 저항을 0.9 → 0.8로 풀자 보스가 한 번에 지워졌다(2026-11-01). 저항과 떼어 보스 몫을 따로 둔다.
/// 왕 고르기는 SkillCaster.FindKing이 맡아 미리보기도 같은 왕 밑에 큰 고리를 띄운다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Checkmate", fileName = "SO_SkillCard_Checkmate")]
public sealed class SO_SkillCard_Checkmate : SO_EnemySkillCard
{
    [Tooltip("왕이 보스일 때 잃는 최대 체력 비율(0.5 = 절반). 보스가 아닌 왕은 쓰러진다. 왕은 DamageMultiplier 몫 대신 이것만 받는다.")]
    [Range(0f, 1f)] public float BossKingLoss = 0.5f;
}
