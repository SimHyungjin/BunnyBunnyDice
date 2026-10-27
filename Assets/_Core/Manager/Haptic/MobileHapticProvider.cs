/// <summary>
/// Android·iOS 네이티브 진동. BenoitFreslon/Vibration 플러그인을 감싼다.
///
/// <b>플러그인 의존이 이 파일 하나에만 있고 <c>USE_VIBRATION</c> 심볼로 닫혀 있다.</b>
/// 심볼이 없으면 플러그인을 참조하는 코드가 컴파일 대상에서 통째로 빠지므로,
/// 패키지가 설치되지 않은 프로젝트에 코어를 그대로 옮겨도 어떤 빌드 타겟에서든 컴파일된다.
/// (다른 SDK 매니저들이 <c>USE_FIREBASE</c> 등으로 하는 것과 같은 방식이다.)
///
/// 진동 세기를 여기서 정하는 이유: ms와 iOS 임팩트 스타일은 플랫폼의 사정이지 게임의 개념이 아니다.
/// 게임은 <see cref="HapticType"/>로 의미만 고르고, 그 해석은 플랫폼 구현이 책임진다.
/// </summary>
public sealed class MobileHapticProvider : IHapticProvider
{
    /// <summary>이 구현이 실제로 진동을 울릴 수 있는 상태인지. 심볼과 빌드 타겟이 모두 맞아야 한다.</summary>
    public static bool IsAvailable =>
#if USE_VIBRATION && (UNITY_ANDROID || UNITY_IOS)
        true;
#else
        false;
#endif

    bool IHapticProvider.IsSupported => IsAvailable;

    public void Initialize()
    {
#if USE_VIBRATION && (UNITY_ANDROID || UNITY_IOS)
        Vibration.Init();
#endif
    }

    public void Vibrate(HapticType type)
    {
#if USE_VIBRATION && UNITY_ANDROID
        Vibration.VibrateAndroid(DurationMs(type));
#elif USE_VIBRATION && UNITY_IOS
        Vibration.VibrateIOS(ImpactStyle(type));
#endif
    }

#if USE_VIBRATION && UNITY_ANDROID
    /// <summary>Android는 지속 시간으로만 세기를 표현한다. 실제 ms는 <see cref="SO_CoreConfig"/>에서 조정한다.</summary>
    private static long DurationMs(HapticType type)
    {
        SO_CoreConfig config = SO_CoreConfig.Instance;

        switch (type)
        {
            case HapticType.Weak:   return config.HapticWeakMs;
            case HapticType.Soft:   return config.HapticSoftMs;
            case HapticType.Medium: return config.HapticMediumMs;
            case HapticType.Hard:   return config.HapticHardMs;
            default:                return config.HapticWeakMs;
        }
    }
#endif

#if USE_VIBRATION && UNITY_IOS
    /// <summary>iOS는 시스템이 정의한 임팩트 스타일을 쓴다.</summary>
    private static ImpactFeedbackStyle ImpactStyle(HapticType type)
    {
        switch (type)
        {
            case HapticType.Weak:   return ImpactFeedbackStyle.Light;
            case HapticType.Soft:   return ImpactFeedbackStyle.Soft;
            case HapticType.Medium: return ImpactFeedbackStyle.Medium;
            case HapticType.Hard:   return ImpactFeedbackStyle.Heavy;
            default:                return ImpactFeedbackStyle.Light;
        }
    }
#endif
}
