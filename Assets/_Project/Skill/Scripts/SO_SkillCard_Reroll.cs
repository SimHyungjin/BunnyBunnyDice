using UnityEngine;

/// <summary>
/// 리롤(주사위 대상): 면체를 한 단계 올리고(육면체 → 십이면체 → 이십면체) 눈을 다시 굴린다(2026-10-31 사용자). 가장 큰 면체에는 못 쓴다.
/// 면체를 무작위로 다시 뽑던 때는 잭팟(눈 최대)의 하위 호환이라 "육면체를 쓸 수 없을 때"만 썼다 — 그 쓰임새를 카드의 역할로 삼았다.
/// 이름은 일단 그대로 둔다(사용자).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Reroll", fileName = "SO_SkillCard_Reroll")]
public sealed class SO_SkillCard_Reroll : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Dice;
}
