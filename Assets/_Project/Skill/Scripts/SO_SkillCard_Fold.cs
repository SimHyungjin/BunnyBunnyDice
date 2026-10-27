using UnityEngine;

/// <summary>
/// 폴드(칸 대상): 칸의 주사위와 칩을 모두 없앤다. 돌려받는 것은 없다(기획서 10.3 — 건설가 전용 카드).
/// 출현 길을 완전히 막는 배치는 처음부터 거부되므로, 쓰임새는 이미 짠 미로를 다시 설계하거나 시작 칩을 치워 더 좋은 통로를 내는 것이다.
/// 이름은 테이블 게임 용어(패를 접어 판에서 내려놓는다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Fold", fileName = "SO_SkillCard_Fold")]
public sealed class SO_SkillCard_Fold : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Cell;
}
