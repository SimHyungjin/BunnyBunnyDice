/// <summary>
/// 플랫폼별 진동 구현. <see cref="HapticManager"/>가 하나를 골라 쓴다.
///
/// 서비스 레이어의 3단 어댑터(Null / Log / 실제)와 같은 구조지만
/// 서비스 매니저(Stage = Service)가 아니라 코어 매니저에 붙는다. 진동은 개인정보 동의와 무관하고,
/// <see cref="SettingsManager"/>(코어 스테이지)가 <see cref="HapticManager"/>에 의존하기 때문에
/// 서비스 스테이지로 내려가면 스테이지 역행이 된다.
///
/// Steam·콘솔 등 코어가 모르는 플랫폼은 <see cref="ServiceProviders"/>로 끼워 넣는다.
/// <example>
/// <code>
/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
/// private static void SetupHaptic()
///     => ServiceProviders.Override&lt;IHapticProvider&gt;(() => new SteamHapticProvider());
/// </code>
/// </example>
/// </summary>
public interface IHapticProvider
{
    /// <summary>이 기기에서 실제로 진동을 낼 수 있는지. 설정 화면이 진동 항목을 보일지 정하는 데 쓴다.</summary>
    bool IsSupported { get; }

    /// <summary>네이티브 초기화. 매니저 초기화 시 한 번 호출된다.</summary>
    void Initialize();

    /// <summary>진동을 울린다. 켜짐/꺼짐 판정은 <see cref="HapticManager"/>가 이미 끝낸 뒤다.</summary>
    void Vibrate(HapticType type);
}
