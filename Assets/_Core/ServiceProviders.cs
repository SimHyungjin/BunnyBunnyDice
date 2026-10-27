using System;
using System.Collections.Generic;

/// <summary>
/// 코어 밖에서 구현을 끼워 넣는 창구. 용도가 둘이라 API도 둘로 나뉜다.
///
/// <b>교체(<see cref="Override{T}"/>)</b> — 코어가 고르는 기본 구현(Null·Log·Mobile 등) 대신 이것을 쓴다.
/// 인터페이스 하나에 구현은 하나라, 같은 타입을 다시 등록하면 마지막 것이 이긴다.
/// - 테스트: 값을 미리 넣어 둔 흉내 구현으로 흐름을 실제로 돌려 보기
/// - 실제 SDK: 코어를 고치지 않고 게임 쪽에서 구현체를 붙이기
///
/// <b>추가(<see cref="Add{T}"/>)</b> — 여러 곳의 등록이 모두 모인다. 덮어쓰지 않는다.
/// 게임 매니저(<see cref="Manager"/>)·서비스 매니저·부팅 게이트(<see cref="IBootGate"/>)처럼
/// 게임과 서비스 레이어가 각자 덧붙이는 목록에 쓴다.
///
/// <b>등록은 코어가 부팅되기 전에 끝나야 한다.</b> 매니저·코어는 초기화할 때 한 번만 조회한다.
/// <see cref="UnityEngine.RuntimeInitializeOnLoadMethod"/>의
/// <see cref="UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration"/> 단계가 가장 안전하다
/// — 코어 부팅(BeforeSceneLoad)보다 확실히 먼저 실행된다.
///
/// <example>
/// <code>
/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
/// private static void Setup()
/// {
///     ServiceProviders.Override&lt;IHapticProvider&gt;(() => new SteamHapticProvider());
///     ServiceProviders.Add&lt;Manager&gt;(() => new BattleManager());
///     ServiceProviders.Add&lt;IBootGate&gt;(() => new TermsGate());
/// }
/// </code>
/// </example>
/// </summary>
public static class ServiceProviders
{
    private static readonly Dictionary<Type, Func<object>> _overrides = new();
    private static readonly Dictionary<Type, List<Func<object>>> _additions = new();

    #region 교체

    /// <summary>
    /// 기본 구현 대신 쓸 구현을 만드는 방법을 등록한다. 같은 타입을 다시 등록하면 덮어쓴다.
    /// null을 넘기면 교체를 취소한다(기본 구현으로 돌아간다).
    ///
    /// 인스턴스가 아니라 <b>팩토리</b>를 받는 이유는, 생성 시점을 매니저 초기화까지 미루기 위해서다.
    /// SDK 객체를 부팅 전에 만들어 두면 동의를 받기 전에 깨어나는 문제가 생길 수 있다.
    /// </summary>
    public static void Override<T>(Func<T> factory) where T : class
    {
        if (factory == null)
        {
            _overrides.Remove(typeof(T));
            return;
        }

        _overrides[typeof(T)] = () => factory();
    }

    /// <summary>교체 구현을 만든다. 등록된 것이 없으면 null — 호출자는 기본 구현을 쓴다.</summary>
    public static T CreateOverride<T>() where T : class
    {
        if (!_overrides.TryGetValue(typeof(T), out Func<object> factory)) return null;
        return Cast<T>(factory());
    }

    /// <summary>해당 타입의 교체가 등록돼 있는지.</summary>
    public static bool HasOverride<T>() where T : class => _overrides.ContainsKey(typeof(T));

    #endregion

    #region 추가

    /// <summary>목록에 항목을 만드는 방법을 추가한다. 이미 등록된 것은 그대로 남는다.</summary>
    public static void Add<T>(Func<T> factory) where T : class
    {
        if (factory == null) return;

        if (!_additions.TryGetValue(typeof(T), out var list))
            _additions[typeof(T)] = list = new List<Func<object>>();

        list.Add(() => factory());
    }

    /// <summary>
    /// 추가된 항목을 등록 순서대로 모두 만들고, <b>그 타입의 등록을 비운다.</b> 없으면 빈 목록. 팩토리가 null을 내면 건너뛴다.
    ///
    /// 비우는 이유: "도메인 리로드 없이 플레이"에서는 정적 필드가 남은 채로 등록 메서드가 플레이마다 다시 실행되어,
    /// 비우지 않으면 같은 매니저·게이트가 플레이할 때마다 쌓인다(게이트가 두 번 돈다).
    /// 코어가 부팅 때 한 번 가져가는 용도라 가져가면서 비우는 것이 맞다.
    /// (부팅 시점인 BeforeSceneLoad는 모든 SubsystemRegistration 등록이 끝난 뒤라, 새 등록을 지울 위험도 없다)
    /// </summary>
    public static List<T> TakeAll<T>() where T : class
    {
        var result = new List<T>();
        if (!_additions.Remove(typeof(T), out var list)) return result;

        foreach (var factory in list)
        {
            T item = Cast<T>(factory());
            if (item != null) result.Add(item);
        }

        return result;
    }

    #endregion

    /// <summary>
    /// 교체·추가 등록을 전부 지운다.
    ///
    /// 코어 부팅과 달리 이 목록은 자동으로 초기화되지 않는다. 등록이 일어나는 시점과
    /// 코어의 정적 초기화 시점이 둘 다 SubsystemRegistration이라 순서가 보장되지 않아,
    /// 자동으로 지우면 방금 등록한 것을 지워 버릴 수 있기 때문이다.
    /// ("도메인 리로드 없이 플레이"에서 교체 등록이 남아 있는 것은 같은 값으로 다시 덮일 뿐이라 문제되지 않고,
    ///  추가 등록은 <see cref="TakeAll{T}"/>가 가져가면서 비운다.)
    /// </summary>
    public static void Clear()
    {
        _overrides.Clear();
        _additions.Clear();
    }

    private static T Cast<T>(object created) where T : class
    {
        if (created == null) return null;
        if (created is T typed) return typed;

        Debug.LogError(
            $"[ServiceProviders] {typeof(T).Name}으로 등록된 팩토리가 {created.GetType().Name}을(를) 만들었습니다.");
        return null;
    }
}
