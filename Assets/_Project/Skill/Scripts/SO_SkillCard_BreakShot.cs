using UnityEngine;

/// <summary>
/// 브레이크 샷(브레이크 전용, 판 전체): 영웅이 한동안 강해진다 — 영웅 자신의 공격력·공격 속도가 오른다(2026-10-29 사용자).
/// 주사위에 올라타는 영웅 레이즈(기획서 10.5)와는 따로다 — 이것은 영웅 몸이 세진다.
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
    [Tooltip("쓰는 순간 영웅 발밑에서 퍼지는 고리의 반지름(칸 = 1). 강화 중에는 영웅 외곽선이 카드 색(IconColor)이 된다.")]
    [Min(0f)] public float BurstRadius = 1.2f;

    public override SkillTarget Target => SkillTarget.Board;
}
