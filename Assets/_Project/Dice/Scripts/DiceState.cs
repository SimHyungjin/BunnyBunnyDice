/// <summary>
/// 놓인 주사위 하나의 상태. 값만 두고 규칙은 DiceRules가 맡는다(몬스터와 같은 모양 — 상태와 규칙을 떼어 둔다).
/// 공격 수치는 들지 않는다 — 색·눈에서 매번 계산한다(DiceStats).
/// </summary>
public struct DiceState
{
    /// <summary>면 수. 지금까지 얹은 것 중 가장 큰 면체.</summary>
    public int Faces;
    /// <summary>지금까지 얹은 색의 합집합.</summary>
    public DiceColor Colors;
    /// <summary>눈. 0 ~ Faces — 굴리면 1 이상이고, 영웅 레이즈(HeroRaise)로 깎이면 0까지 내려간다.</summary>
    public int Pips;

    public bool IsMaxed => Pips >= Faces;

    /// <summary>눈을 다 태웠다 — 공격하지 않고 벽으로만 남는다(기획서 10.5).</summary>
    public bool IsSpent => Pips <= 0;
}
