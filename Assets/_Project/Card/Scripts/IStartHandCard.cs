/// <summary>
/// 갈고리: 판 시작 손패(SO_CardHandConfig.StartHand)의 주사위 카드를 바꾼다. 카드마다 한 번 묻는다.
/// 특성 등 바깥에서 구현한다(SO_HeroTrait) — 777처럼 육면체를 안 쓰는 특성이 육면체 시작 손패로 시작하지 않게.
/// </summary>
public interface IStartHandCard
{
    DiceCard StartCard(DiceCard card);
}
