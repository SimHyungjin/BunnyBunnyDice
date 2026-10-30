using UnityEngine;

/// <summary>
/// 더블 업(더블 전용, 판 전체): 바로 몇 번 레벨업하고, 한동안 처치 경험치가 몇 배가 된다 — 이름처럼 두 번 겹친다(2026-10-31 사용자).
/// 경험치 배율만 있던 때는 후보 한 자리를 쓰고 1분에 걸쳐 2~3장 받는 셈이라 약했고, 쓰는 순간 보이는 것이 없었다.
/// 특성(고른 카드가 두 장)과 같은 테마를 "고르는 횟수"로 푼다. 시간은 게임 시간(ExpBoost).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Double Up", fileName = "SO_SkillCard_DoubleUp")]
public sealed class SO_SkillCard_DoubleUp : SO_SkillCard
{
    [Tooltip("쓰자마자 오르는 레벨 수(레벨마다 카드를 한 번 고른다).")]
    [Min(0)] public int LevelUps = 2;
    [Tooltip("처치 경험치 배율. 여러 장이면 더 얹는 몫을 더한다(x2 두 장 = x3).")]
    [Min(1f)] public float ExpScale = 2f;
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 60f;

    public override SkillTarget Target => SkillTarget.Board;
}
