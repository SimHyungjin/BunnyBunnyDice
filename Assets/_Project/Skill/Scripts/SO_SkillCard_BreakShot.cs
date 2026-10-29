using UnityEngine;

/// <summary>
/// 브레이크 샷(브레이크 전용, 판 전체): 영웅이 한동안 오버드라이브 상태가 된다 — 영웅 자신의 공격력·공격 속도가 오른다(2026-10-29 사용자).
/// 주사위에 올라타는 영웅 오버드라이브(기획서 10.5)와는 따로다 — 이것은 영웅 몸이 세진다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Break Shot", fileName = "SO_SkillCard_BreakShot")]
public sealed class SO_SkillCard_BreakShot : SO_SkillCard
{
    [Tooltip("영웅 공격력 배율.")]
    [Min(1f)] public float DamageScale = 3f;
    [Tooltip("영웅 공격 속도 배율.")]
    [Min(1f)] public float SpeedScale = 3f;
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 10f;

    public override SkillTarget Target => SkillTarget.Board;
}
