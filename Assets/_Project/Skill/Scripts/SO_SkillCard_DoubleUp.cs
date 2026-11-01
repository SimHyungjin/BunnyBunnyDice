using UnityEngine;

/// <summary>
/// 더블 업(더블 전용, 판 전체): 바로 몇 번 레벨업한다. 특성(고른 카드가 두 장)과 같은 테마를 "고르는 횟수"로 푼다.
/// 한동안 처치 경험치 x2도 있었는데 지웠다(2026-11-01 사용자) — 특성·레벨업과 같은 "카드를 더 받는다"를 세 번째로 겹쳤고,
/// 그 시간에 얼마나 잡느냐로 값이 널뛰어 세기가 읽히지 않았다. 그 전(경험치 배율만)은 쓰는 순간 보이는 것이 없어 약했다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Double Up", fileName = "SO_SkillCard_DoubleUp")]
public sealed class SO_SkillCard_DoubleUp : SO_SkillCard
{
    [Tooltip("쓰자마자 오르는 레벨 수(레벨마다 카드를 한 번 고른다).")]
    [Min(0)] public int LevelUps = 2;

    public override SkillTarget Target => SkillTarget.Board;
}
