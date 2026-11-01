using UnityEngine;

/// <summary>
/// 스택 덱(카드 카운터 전용, 판 전체): 손패의 스킬 카드가 종류마다 몇 장씩 늘어난다(2026-10-30 사용자). 특성(스킬을 고르면 2장)과 같은 테마를
/// 손에 쥔 종류로 푼다 — 모아 둔 스킬이 많을수록 세므로 언제 쓸지가 판단이 된다. 전용 카드는 스킬이 아니라 늘지 않는다.
/// 처음엔 장수 x2였는데, 스택 덱이 레벨업마다 다시 나와 거듭 쓰면 배로 불어났다 — 올인(목표물 체력 최대)·포켓(20% 처형)처럼
/// 비율로 효과가 나는 스킬은 웨이브 복리에 밀리지 않아 죽을 수가 없었다(2026-11-01). 더하기면 공급이 일정해 언젠가 밀린다.
/// +2가 아니라 +1인 이유: 리롤·잭팟을 한 장씩 쥐고 쓰면 스택 덱 한 번이 각성 주사위 둘이 된다 — +1이면 한 장씩 쥔 손은 x2 때와 같다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Stacked Deck", fileName = "SO_SkillCard_StackedDeck")]
public sealed class SO_SkillCard_StackedDeck : SO_SkillCard
{
    [Tooltip("손패의 스킬 카드 종류(뭉치)마다 더하는 장수.")]
    [Min(1)] public int AddPerStack = 1;

    public override SkillTarget Target => SkillTarget.Board;
}
