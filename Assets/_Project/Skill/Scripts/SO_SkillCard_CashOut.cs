using UnityEngine;

/// <summary>
/// 캐시 아웃(하이 롤러 전용, 칸 대상): 칸의 주사위와 칩을 거둬 손패로 돌려받는다 — 장마다 Copies장씩(2026-10-29 사용자).
/// 거두기만 하면 자리만 옮기는 카드라 힘이 늘지 않아 약하다 — 그래서 여러 장으로 돌려받는다. 돌려받는 주사위는 면체·색만 남고 눈은 놓을 때 다시 굴린다.
/// 쓰임새는 이미 짠 미로를 다시 설계하거나 시작 칩을 치워 더 좋은 통로를 내는 것이다.
/// 이름은 테이블 게임 용어(칩을 현금으로 바꿔 판에서 거둔다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Cash Out", fileName = "SO_SkillCard_CashOut")]
public sealed class SO_SkillCard_CashOut : SO_SkillCard
{
    [Tooltip("거둔 주사위·칩마다 손패로 돌려받는 장수.")]
    [Min(1)] public int Copies = 2;

    public override SkillTarget Target => SkillTarget.Cell;
}
