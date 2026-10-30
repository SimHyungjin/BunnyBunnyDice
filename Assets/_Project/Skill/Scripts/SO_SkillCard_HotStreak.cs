using UnityEngine;

/// <summary>
/// 핫 스트릭(하이 스테이크 전용, 판 전체): 한동안 영웅이 밟고 지나가는 주사위마다 레이즈가 걸린다(2026-10-30 사용자, HotStreak).
/// 레이즈 카드와 같은 레이즈라 눈이 깎이지 않는다 — 영웅이 올라탄 레이즈처럼 깎이면 열 개를 밟고 열 개가 모두 깎여 쓸 수 없다(2026-10-31 사용자).
/// 특성(레이즈가 세다)과 같은 테마를 "여러 주사위에 한꺼번에"로 푼다 — 어디로 달릴지가 판단이 된다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Hot Streak", fileName = "SO_SkillCard_HotStreak")]
public sealed class SO_SkillCard_HotStreak : SO_SkillCard
{
    [Tooltip("밟은 주사위에 레이즈를 거는 시간(초).")]
    [Min(0.1f)] public float Duration = 10f;
    [Tooltip("밟은 주사위 하나에 걸리는 레이즈 시간(초). 배율은 레이즈 카드 값(특성이 바꾸면 그 값).")]
    [Min(0.1f)] public float RaiseDuration = 10f;
    [Tooltip("쓰는 순간 영웅에서 퍼지는 고리 반경(칸).")]
    [Min(0f)] public float BurstRadius = 1.5f;

    public override SkillTarget Target => SkillTarget.Board;
}
