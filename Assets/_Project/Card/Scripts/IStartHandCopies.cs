/// <summary>
/// 영웅 특성 갈고리: 시작 손패를 몇 벌 받는지(TraitHooks로 묻는다 — 없으면 한 벌). 판을 시작할 때 손패를 나눠 주는 쪽(GameScene)이 묻는다.
/// 같은 카드는 손패에서 한 뭉치로 겹친다(x2).
/// </summary>
public interface IStartHandCopies
{
    int StartHandCopies { get; }
}
