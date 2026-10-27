using UnityEngine;

/// <summary>
/// 페인트(주사위 대상): 주사위의 색을 덮어쓴다 → 공격 방식과 등급이 바뀐다. 눈은 그대로.
/// 7색(단색 3·2색 3·하양) 모두 나온다 — 하양은 등급을 내리는 꽝에 가깝지만 같이 나온다(기획서 9.7). 검정 페인트는 없다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Paint", fileName = "SO_SkillCard_Paint_")]
public sealed class SO_SkillCard_Paint : SO_SkillCard
{
    [Tooltip("칠할 색. 검정(아무 색 없음)은 두지 않는다.")]
    public DiceColor Colors;

    public override SkillTarget Target => SkillTarget.Dice;
}
