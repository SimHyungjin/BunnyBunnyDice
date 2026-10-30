using UnityEngine;

/// <summary>
/// 코인 플립(더블 오어 낫싱 전용, 판 전체): 확률로 무작위 카드 여러 장을 손패로, 아니면 꽝(2026-10-30 사용자).
/// 특성(눈이 1 아니면 최대)과 같은 반반 테마를 카드 수로 푼다 — 위험한 쪽이라 평균이 다른 전용 카드보다 낮은 것이 정상이다(로또).
/// 카드는 레벨업과 같은 몫으로 모든 종류에서 뽑는다(CardRoll.RollOne). 영웅 강화 카드는 손패로 들어와 판에 끌어 놓아 쓴다.
/// 처음 전용 카드는 스네이크 아이즈(눈이 1인 주사위만 다시 굴림)였는데 특성과 크기만 다른 같은 방식이라 바꿨다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Coin Flip", fileName = "SO_SkillCard_CoinFlip")]
public sealed class SO_SkillCard_CoinFlip : SO_SkillCard
{
    [Tooltip("카드를 받을 확률.")]
    [Range(0f, 1f)] public float Chance = 0.5f;
    [Tooltip("받는 카드 수.")]
    [Min(1)] public int Count = 10;

    public override SkillTarget Target => SkillTarget.Board;
}
