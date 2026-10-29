using UnityEngine;

/// <summary>
/// 리롤(주사위 대상): 면체와 눈을 새로 굴린다 — 면체는 SO_DiceConfig.Models에서 같은 확률로(2026-10-29 사용자 결정 — 처음엔 면체 그대로 눈만).
/// 이십면체가 육면체로 떨어질 수도 있는 도박이다. 상한에 닿은 주사위에도 막지 않고 판단에 맡긴다(기획서 7).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Reroll", fileName = "SO_SkillCard_Reroll")]
public sealed class SO_SkillCard_Reroll : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Dice;
}
