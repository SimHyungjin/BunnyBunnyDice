using UnityEngine;

/// <summary>
/// 포켓(적 대상): 모든 적에게 피해(영웅 공격력 비례, SO_EnemySkillCard)를 준 뒤, 적마다 확률로 가장 가까운 포켓에 굴려 넣어 처치한다(기획서 8).
/// 확률은 체력·보스를 가리지 않는다 — "인생은 운빨"(2026-10-31 사용자). 처음엔 체력이 처치선 아래인 적만 처치했는데,
/// 피해가 약한 때는 아무도 선 아래로 안 내려가 빈손이 많았다. 확률이면 판 크기와 상관없이 늘 몇 마리꼴로 들어간다.
/// 처치 연출(포켓으로 날아감)은 모든 스킬 처치가 같이 쓴다(SO_MonsterConfig.SkillFly*). 경험치는 그대로 준다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Pocket", fileName = "SO_SkillCard_Pocket")]
public sealed class SO_SkillCard_Pocket : SO_EnemySkillCard
{
    [Tooltip("적마다 처치될 확률(보스 포함).")]
    [Range(0f, 1f)] public float ExecuteChance = 0.2f;
}
