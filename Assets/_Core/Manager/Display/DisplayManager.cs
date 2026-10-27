using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 프레임레이트·화면 절전·수직동기·해상도·커서를 한곳에서 다룬다.
///
/// 코어에 이런 매니저가 없어서 생기던 문제가 양쪽 플랫폼에 하나씩 있었다.
/// 모바일은 Unity 기본 <see cref="Application.targetFrameRate"/>가 30이라 아무도 손대지 않으면 30fps로 돌고,
/// 화면 절전도 막지 않아 조작이 뜸한 구간에서 화면이 꺼졌다.
/// PC는 해상도·전체화면·커서를 다룰 방법이 아예 없었다.
///
/// 플랫폼 분기를 <c>#if</c>가 아니라 런타임 판정(<see cref="Application.isMobilePlatform"/>)으로 하는 이유:
/// 에디터에서 모바일 타겟으로 전환해도 코드가 그대로 살아 있어야 동작을 눈으로 확인할 수 있고,
/// 데스크톱 전용 API를 모바일에서 부르는 실수도 런타임에 조용히 무시되면 되기 때문이다.
/// (해상도 변경 같은 것은 모바일에서 의미가 없을 뿐 예외를 던지지는 않는다)
///
/// 외부 의존이 없어 코어 스테이지다. 저장·복원은 <see cref="SettingsManager"/>가 맡는다.
/// </summary>
public sealed class DisplayManager : Manager
{
    /// <summary>플랫폼 기본값을 쓰라는 뜻. <see cref="TargetFrameRate"/>에 넣으면 기기 주사율을 따른다.</summary>
    public const int FRAME_RATE_PLATFORM_DEFAULT = 0;

    private static SO_CoreConfig Config => SO_CoreConfig.Instance;

    private int _targetFrameRate = FRAME_RATE_PLATFORM_DEFAULT;
    private bool _vSyncEnabled;

    /// <summary>해상도·전체화면·커서를 실제로 다룰 수 있는 플랫폼인지. 설정 화면에서 항목을 감출 때 쓴다.</summary>
    public bool IsDesktop => !Application.isMobilePlatform;

    /// <summary>화면 상태(해상도·전체화면·주사율)가 바뀌었을 때. 설정 화면이 표시를 갱신하는 데 쓴다.</summary>
    public event Action OnDisplayChanged;

    protected override UniTask OnInitializeAsync()
    {
        // 모바일은 조작이 뜸해도 화면이 꺼지면 안 된다. 데스크톱에서는 의미가 없고 무시된다.
        if (Application.isMobilePlatform && Config.PreventScreenSleepOnMobile)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // 저장된 값이 없을 때의 출발점. SettingsManager가 곧 SettingsData의 값으로 덮어쓴다.
        ApplyFrameRate(FRAME_RATE_PLATFORM_DEFAULT);

        return UniTask.CompletedTask;
    }

    #region 프레임레이트 · 수직동기

    /// <summary>
    /// 목표 프레임레이트. <see cref="FRAME_RATE_PLATFORM_DEFAULT"/>(0)이면 플랫폼에 맞는 값을 고른다
    /// — 모바일은 60, 데스크톱은 기기 주사율.
    /// </summary>
    public int TargetFrameRate
    {
        get => _targetFrameRate;
        set
        {
            if (_targetFrameRate == value) return;

            ApplyFrameRate(value);
            OnDisplayChanged?.Invoke();
        }
    }

    /// <summary>
    /// 수직동기. 켜면 <see cref="TargetFrameRate"/>는 무시된다 — Unity가 vSyncCount를 우선하기 때문이다.
    /// 이 사실을 모르면 "프레임 제한이 안 먹는다"로 한참 헤매게 되므로 여기서 함께 다룬다.
    /// </summary>
    public bool VSyncEnabled
    {
        get => _vSyncEnabled;
        set
        {
            if (_vSyncEnabled == value) return;

            _vSyncEnabled = value;
            ApplyFrameRate(_targetFrameRate);
            OnDisplayChanged?.Invoke();
        }
    }

    private void ApplyFrameRate(int requested)
    {
        _targetFrameRate = requested;

        QualitySettings.vSyncCount = _vSyncEnabled ? 1 : 0;

        // vSync가 켜져 있으면 targetFrameRate는 어차피 무시된다. -1(무제한)로 두어 의도를 분명히 한다.
        if (_vSyncEnabled)
        {
            Application.targetFrameRate = -1;
            return;
        }

        Application.targetFrameRate = requested != FRAME_RATE_PLATFORM_DEFAULT
            ? requested
            : (Application.isMobilePlatform ? Config.MobileTargetFrameRate : CurrentRefreshRate());
    }

    /// <summary>기기 주사율. 읽지 못하면 설정의 폴백 값을 쓴다.</summary>
    private static int CurrentRefreshRate()
    {
        double hz = Screen.currentResolution.refreshRateRatio.value;
        return hz > 1d ? Mathf.RoundToInt((float)hz) : Config.FallbackRefreshRate;
    }

    #endregion

    #region 해상도 · 전체화면 (데스크톱)

    public int Width  => Screen.width;
    public int Height => Screen.height;
    public FullScreenMode ScreenMode => Screen.fullScreenMode;

    /// <summary>이 기기가 지원하는 해상도 목록. 모바일에서는 의미가 없어 빈 배열을 준다.</summary>
    public Resolution[] AvailableResolutions => IsDesktop ? Screen.resolutions : Array.Empty<Resolution>();

    /// <summary>
    /// 해상도와 전체화면 상태를 바꾼다. 모바일에서는 아무 일도 하지 않는다
    /// — 화면 크기가 OS 소유라 바꿀 수 없고, 억지로 부르면 렌더 타깃만 흔들린다.
    /// 실제 반영은 다음 프레임이다. 호출 직후 <see cref="Width"/>·<see cref="Height"/>는 아직 이전 값이다.
    /// </summary>
    public void SetResolution(int width, int height, FullScreenMode mode)
    {
        if (!IsDesktop) return;
        if (width <= 0 || height <= 0) return;

        Screen.SetResolution(width, height, mode);
        OnDisplayChanged?.Invoke();
    }

    /// <summary>전체화면만 전환한다. 해상도는 현재 값을 유지한다.</summary>
    public void SetFullScreen(bool fullScreen)
    {
        if (!IsDesktop) return;

        Screen.SetResolution(
            Screen.width,
            Screen.height,
            fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

        OnDisplayChanged?.Invoke();
    }

    #endregion

    #region 커서 (데스크톱)

    /// <summary>
    /// 커서 표시와 잠금. 모바일에서는 의미가 없어 무시한다.
    /// 잠금 상태에서는 Unity가 커서를 자동으로 숨기므로 둘을 한 번에 다룬다.
    /// </summary>
    public void SetCursor(bool visible, CursorLockMode lockMode = CursorLockMode.None)
    {
        if (!IsDesktop) return;

        Cursor.visible = visible;
        Cursor.lockState = lockMode;
    }

    #endregion

    public override void Clear()
    {
        // 화면 설정은 씬에 속한 상태가 아니다. 씬이 바뀐다고 해상도나 프레임레이트를 되돌리면
        // 유저가 설정한 값이 씬 전환마다 날아간다. 커서만 씬 연출에 쓰이므로 기본값으로 되돌린다.
        if (IsDesktop) SetCursor(true);
    }
}
