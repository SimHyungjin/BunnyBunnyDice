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
/// 갈고리: 레벨업 후보 하나를 확률로 다른 카드로 덮어쓴다(전용 카드, 기획서 10.1).
/// 카드 풀에 더하지 않고 덮어쓰는 이유: 풀에 더하면 공용 카드가 늘어날수록 묻히지만, 덮어쓰기는 풀 크기와 상관없이 빈도가 고정된다.
/// </summary>
public interface ICandidateOverwrite
{
    /// <returns>이번 레벨업에 덮어쓸 카드. 덮어쓰지 않으면 null.</returns>
    SO_Card RollOverwrite();

    /// <summary>덮어쓸 수 있는 카드 전부(테스트 창이 보인다).</summary>
    IReadOnlyList<SO_Card> OverwriteCards { get; }
}

/// <summary>
/// 갈고리: 레벨업 후보 카드마다 고르면 몇 장 받을지. 후보를 띄울 때 카드마다 굴려 처음부터 보인다(x2)
/// — 고른 뒤에 굴리면 선택에 아무 영향이 없지만, 보이면 "x2 붙은 카드냐 더 좋은 카드냐"를 저울질하게 된다.
/// </summary>
public interface ILevelUpCopies
{
    /// <returns>받을 장수(1 = 그대로).</returns>
    int RollCopies(HandCard candidate);
}

/// <summary>갈고리: 몬스터를 처치해 얻는 경험치를 바꾼다.</summary>
public interface IKillExpModifier
{
    /// <returns>바뀐 경험치(그대로면 exp).</returns>
    int ModifyExp(int exp);
}
