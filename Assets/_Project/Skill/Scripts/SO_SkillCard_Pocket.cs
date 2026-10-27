using UnityEngine;

/// <summary>
/// 포켓(범위·즉발): 원 안의 적 모두에게 피해(영웅 공격력 비례, SO_AreaSkillCard)를 준 뒤, 체력이 낮아진 적을 가장 가까운 포켓으로 굴려 넣어 처치한다(기획서 5.3).
/// 처음의 "약한 적만 처치"는 쓸 때를 맞추기가 너무 어려웠다(2026-10-24) — 피해가 먼저 들어가 처치선까지 끌어내리고, 처치할 적이 없어도 피해는 남는다.
/// 처치 연출(포켓으로 날아감)은 모든 스킬 처치가 같이 쓴다(SO_MonsterConfig.SkillFly*).
/// 경험치는 그대로 준다. 보스는 피해만 받고 빠지지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Pocket", fileName = "SO_SkillCard_Pocket")]
public sealed class SO_SkillCard_Pocket : SO_AreaSkillCard
{
    [Tooltip("피해 뒤 이 비율 이하로 체력이 남은 적을 처치한다(최대 체력 대비). 보스는 처치하지 않는다.")]
    [Range(0f, 1f)] public float HealthThreshold = 0.3f;
}
