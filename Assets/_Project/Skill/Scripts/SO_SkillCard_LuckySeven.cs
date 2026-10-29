using UnityEngine;

/// <summary>
/// 럭키 세븐(777 전용, 판 전체): 판 위 모든 주사위가 한동안 각성한다(2026-10-29 사용자). 777은 놓을 때 늘 7이라 각성이 합성으로만 되는데,
/// 그 약점을 한 장으로 뒤집는다. 색마다의 각성 효과(확정 치명·사거리 등)가 모두 함께 터진다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Lucky Seven", fileName = "SO_SkillCard_LuckySeven")]
public sealed class SO_SkillCard_LuckySeven : SO_SkillCard
{
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 7f;

    public override SkillTarget Target => SkillTarget.Board;
}
