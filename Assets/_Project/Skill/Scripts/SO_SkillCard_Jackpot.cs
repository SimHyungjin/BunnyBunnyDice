using UnityEngine;

/// <summary>잭팟(주사위 대상): 눈을 상한으로 — d20이면 바로 각성한다(기획서 9.7). 이미 상한이면 쓸 수 없다.</summary>
[CreateAssetMenu(menuName = "Def/Skill/Jackpot", fileName = "SO_SkillCard_Jackpot")]
public sealed class SO_SkillCard_Jackpot : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Dice;
}
