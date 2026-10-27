using Cysharp.Threading.Tasks;

/// <summary>
/// 진동(햅틱) 피드백. 실제 구현은 <see cref="IHapticProvider"/>가 맡고, 이 매니저는 고르고 끄고 켜는 일만 한다.
///
/// 예전에는 이 파일 안에서 <c>#if UNITY_ANDROID / UNITY_IOS</c>로 직접 갈랐는데 문제가 셋이었다.
/// 진동 플러그인 의존이 코어 한복판에 박혀 빌드 타겟을 모바일로 바꾸는 순간 패키지 없이는 컴파일이 깨졌고,
/// <c>#if UNITY_EDITOR</c>가 먼저라 모바일 분기가 에디터에서 아예 컴파일되지 않아 오타가 빌드 때까지 숨었으며,
/// PC·Steam처럼 코어가 모르는 플랫폼을 붙일 자리가 없었다.
/// 프로바이더로 분리해 셋 다 해결했다 — 새 플랫폼은 코어를 고치지 않고 <see cref="ServiceProviders"/>로 붙인다.
/// </summary>
public sealed class HapticManager : Manager
{
    private IHapticProvider _provider;

    /// <summary>
    /// 진동을 쓸지 여부. 끄면 <see cref="Vibrate"/>가 조용히 무시된다.
    /// 저장·복원은 <see cref="SettingsManager"/>가 맡는다.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>이 기기에서 실제로 진동이 울릴 수 있는지. 설정 화면에서 진동 토글을 감출 때 쓴다.</summary>
    public bool IsSupported => _provider != null && _provider.IsSupported;

    protected override UniTask OnInitializeAsync()
    {
        _provider = BuildProvider();
        _provider.Initialize();

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 구현을 고른다. 게임이 등록한 것이 항상 먼저다 — Steam·콘솔 등 코어가 모르는 플랫폼을 위한 통로다.
    ///
    /// 에디터를 모바일보다 먼저 보는 이유: 에디터는 진동 하드웨어가 없어 실제 구현을 골라도 아무 일도 일어나지 않는데,
    /// 그러면 "진동이 안 울린다"와 "호출이 아예 안 됐다"를 구분할 수 없다. 로그로 남겨 흐름을 눈으로 보게 한다.
    /// </summary>
    private static IHapticProvider BuildProvider()
    {
        IHapticProvider injected = ServiceProviders.CreateOverride<IHapticProvider>();
        if (injected != null) return injected;

#if UNITY_EDITOR
        return new LogHapticProvider();
#else
        if (MobileHapticProvider.IsAvailable) return new MobileHapticProvider();

        // 모바일 빌드인데 진동 플러그인이 닫혀 있으면 조용히 넘어가지 않고 알린다.
        // (USE_VIBRATION 심볼을 빠뜨린 채 출시되는 것이 가장 흔한 사고다)
        if (UnityEngine.Application.isMobilePlatform)
            Debug.LogWarning("[Haptic] 모바일 빌드지만 USE_VIBRATION 심볼이 없어 진동이 동작하지 않습니다.");

        return new NullHapticProvider();
#endif
    }

    /// <summary>진동을 울린다. 설정에서 꺼 두었거나 지원하지 않는 기기면 아무 일도 일어나지 않는다.</summary>
    public void Vibrate(HapticType type)
    {
        if (!IsEnabled) return;

        _provider?.Vibrate(type);
    }

    public void Weak()   => Vibrate(HapticType.Weak);
    public void Soft()   => Vibrate(HapticType.Soft);
    public void Medium() => Vibrate(HapticType.Medium);
    public void Hard()   => Vibrate(HapticType.Hard);
}
