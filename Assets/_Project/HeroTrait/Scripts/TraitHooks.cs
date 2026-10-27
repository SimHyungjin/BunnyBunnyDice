/// <summary>
/// 특성이 게임 규칙에 끼어드는 창구(SO_HeroTrait 참고). 시스템은 특성 종류가 아니라 자기 갈고리 인터페이스만 묻는다.
/// 지금 캐릭터의 특성을 매번 읽는다 — 테스트 창에서 캐릭터를 바꾸면 바로 따라가게.
/// </summary>
public sealed class TraitHooks
{
    private readonly HeroStats _stats;

    public TraitHooks(HeroStats stats)
    {
        _stats = stats;
    }

    /// <summary>지금 특성이 T 갈고리를 구현하면 그것, 아니면 null.</summary>
    public T Find<T>() where T : class => _stats.Character != null ? _stats.Character.Trait as T : null;
}
