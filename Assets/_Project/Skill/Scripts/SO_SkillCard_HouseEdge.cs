using UnityEngine;

/// <summary>하우스 엣지(더블 전용, 판 전체): 한동안 처치 경험치가 몇 배가 된다(2026-10-29 사용자). 시간은 게임 시간(ExpBoost).</summary>
[CreateAssetMenu(menuName = "Def/Skill/House Edge", fileName = "SO_SkillCard_HouseEdge")]
public sealed class SO_SkillCard_HouseEdge : SO_SkillCard
{
    [Tooltip("처치 경험치 배율.")]
    [Min(1f)] public float ExpScale = 2f;
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 60f;

    public override SkillTarget Target => SkillTarget.Board;
}
