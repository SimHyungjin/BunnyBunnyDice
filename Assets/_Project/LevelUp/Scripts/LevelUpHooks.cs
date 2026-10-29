using System.Collections.Generic;

// 레벨업·경험치에 끼어드는 갈고리들. 특성 등 바깥에서 구현한다(SO_HeroTrait) — 레벨업 쪽은 누가 구현하는지 모른다.

/// <summary>갈고리: 레벨업 후보에서 카드 종류별 몫을 바꾼다. 후보를 한 장씩 뽑을 때마다 묻는다.</summary>
public interface ICardKindShare
{
    /// <param name="total">그 종류 카드 가중치의 합(0이면 그 종류는 나올 카드가 없다).</param>
    /// <param name="picked">이번 선택에서 이미 뽑힌 후보.</param>
    /// <returns>그 종류가 뽑힐 몫. 0이면 나오지 않는다.</returns>
    float GetShare(CardKind kind, float total, IReadOnlyList<HandCard> picked);
}

/// <summary>
/// 갈고리: 레벨업 후보마다 고르면 몇 장 받을지. 후보를 띄울 때 굴려 처음부터 보인다(x2)
/// — 고른 뒤에 굴리면 선택에 아무 영향이 없지만, 보이면 "x2 붙은 카드냐 더 좋은 카드냐"를 저울질하게 된다.
/// </summary>
public interface ILevelUpCopies
{
    /// <summary>copies에 candidates와 같은 순서로 받을 장수(1 = 그대로)를 채운다. copies는 비어서 온다.</summary>
    void RollCopies(IReadOnlyList<HandCard> candidates, List<int> copies);
}

/// <summary>갈고리: 레벨업 주사위 카드의 면체 가중치에 곱한다(SO_LevelUpConfig.FacesWeights — 0이면 그 면체가 나오지 않는다).</summary>
public interface ILevelUpFaces
{
    float ScaleFaces(int faces);
}
