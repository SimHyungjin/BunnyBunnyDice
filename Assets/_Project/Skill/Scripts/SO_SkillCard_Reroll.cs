using UnityEngine;

/// <summary>다시 굴리기(주사위 대상): 지금 면체로 눈을 새로 굴린다. 상한에 닿은 주사위에도 막지 않고 판단에 맡긴다(기획서 9.7).</summary>
[CreateAssetMenu(menuName = "Def/Skill/Reroll", fileName = "SO_SkillCard_Reroll")]
public sealed class SO_SkillCard_Reroll : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Dice;
}
