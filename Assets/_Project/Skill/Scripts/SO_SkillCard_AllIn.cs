using UnityEngine;

/// <summary>
/// 올인(판 전체): 목표물 체력을 가득 채우고 판의 모든 주사위를 다시 굴린다(면체 그대로, 기획서 5.3). 각성한 주사위도 같이 굴러가는 대가로 체력을 되찾는다
/// (2026-10-29 사용자 결정 — 처음엔 다시 굴리기만 했다).
/// 다시 굴리기와 같이 검정이 눈 20에 닿으면 무작위 색으로 각성한다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/All In", fileName = "SO_SkillCard_AllIn")]
public sealed class SO_SkillCard_AllIn : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Board;
}
