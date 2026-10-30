using UnityEngine;

/// <summary>
/// 스택 덱(카드 카운터 전용, 판 전체): 손패의 스킬 카드가 몇 배로 늘어난다(2026-10-30 사용자). 특성(후보에 스킬이 늘 나옴)과 같은 테마를
/// 손에 쥔 장수로 푼다 — 모아 둔 스킬이 많을수록 세므로 언제 쓸지가 판단이 된다. 전용 카드는 스킬이 아니라 늘지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Stacked Deck", fileName = "SO_SkillCard_StackedDeck")]
public sealed class SO_SkillCard_StackedDeck : SO_SkillCard
{
    [Tooltip("손패의 스킬 카드 장수에 곱하는 값.")]
    [Min(1)] public int StackScale = 2;

    public override SkillTarget Target => SkillTarget.Board;
}
