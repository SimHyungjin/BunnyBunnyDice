using UnityEngine;

/// <summary>
/// 올인(판 전체): 판의 모든 주사위를 다시 굴린다(면체 그대로, 기획서 5.3). 낮게 나온 판을 뒤집는 도박이다 — 높게 나온 주사위도 같이 굴러간다.
/// 다시 굴리기와 같이 검정이 눈 20에 닿으면 무작위 색으로 각성한다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/All In", fileName = "SO_SkillCard_AllIn")]
public sealed class SO_SkillCard_AllIn : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Board;
}
