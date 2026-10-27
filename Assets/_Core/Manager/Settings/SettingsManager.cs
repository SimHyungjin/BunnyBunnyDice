using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 유저 설정을 한곳에서 읽고 쓴다. 설정 화면이 여러 매니저를 직접 만지지 않게 하는 것이 목적이다.
///
/// 값을 스스로 들고 있지는 않는다. 실제 상태는 각 매니저와 <see cref="SettingsData"/> 세이브에 있고,
/// 이 매니저는 그 사이를 잇고 <b>다시 적용해야 할 때</b> 밀어 넣는 일을 한다.
///
/// 그 "다시 적용"이 이 매니저의 존재 이유에 가깝다. 클라우드 세이브를 내려받거나 초기화하면
/// <see cref="SettingsData"/>의 내용이 통째로 바뀌는데, 그때 오디오·진동은 이전 값을 그대로 물고 있다.
/// <see cref="DataManager.OnDataReplaced"/>를 구독해 그 순간 설정을 되살린다.
/// </summary>
public sealed class SettingsManager : Manager
{
    /// <summary>설정이 바뀌었을 때. 설정 화면이 표시를 갱신하는 데 쓴다.</summary>
    public event Action OnSettingsChanged;

    // 값을 밀어 넣을 대상들이 먼저 준비돼야 한다.
    public override Type[] Dependencies
        => new[] { typeof(DataManager), typeof(AudioManager), typeof(HapticManager), typeof(DisplayManager), typeof(LocalizationManager) };

    protected override UniTask OnInitializeAsync()
    {
        ApplyAll();

        if (Core.Data != null) Core.Data.OnDataReplaced += ApplyAll;

        return UniTask.CompletedTask;
    }

    #region Audio

    public float GetVolume(AudioChannelType channel)
        => Core.Audio != null ? Core.Audio.GetVolume(channel) : 1f;

    /// <summary>
    /// 볼륨을 바꾸고 저장한다. 값이 그대로면 아무것도 하지 않는다 —
    /// 같은 값을 다시 넣는 것만으로 저장이 필요한 상태가 되면, 클라우드 동기화에서 "변경됨"으로 잡힌다.
    /// (설정 화면을 열 때 현재 값을 그대로 밀어 넣는 경로가 있다)
    /// </summary>
    public void SetVolume(AudioChannelType channel, float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(GetVolume(channel), value)) return;

        Core.Audio?.SetVolume(channel, value);
        Store(data =>
        {
            switch (channel)
            {
                case AudioChannelType.Master: data.MasterVolume = value; break;
                case AudioChannelType.Music:  data.BgmVolume    = value; break;
                case AudioChannelType.Sound:  data.SfxVolume    = value; break;
            }
        });
    }

    #endregion

    #region Haptic

    public bool HapticEnabled
    {
        get => Core.Haptic != null && Core.Haptic.IsEnabled;
        set
        {
            if (HapticEnabled == value) return;
            if (Core.Haptic != null) Core.Haptic.IsEnabled = value;

            Store(data => data.HapticEnabled = value);
        }
    }

    #endregion

    #region Display

    /// <summary>해상도·전체화면을 다룰 수 있는 플랫폼인지. 설정 화면에서 항목을 감출 때 쓴다.</summary>
    public bool SupportsScreenSettings => Core.Display != null && Core.Display.IsDesktop;

    /// <summary>목표 프레임레이트. 0이면 플랫폼 기본(모바일 60 · 데스크톱 주사율).</summary>
    public int TargetFrameRate
    {
        get => Core.Display != null ? Core.Display.TargetFrameRate : 0;
        set
        {
            if (TargetFrameRate == value) return;
            if (Core.Display != null) Core.Display.TargetFrameRate = value;

            Store(data => data.TargetFrameRate = value);
        }
    }

    public bool VSyncEnabled
    {
        get => Core.Display != null && Core.Display.VSyncEnabled;
        set
        {
            if (VSyncEnabled == value) return;
            if (Core.Display != null) Core.Display.VSyncEnabled = value;

            Store(data => data.VSyncEnabled = value);
        }
    }

    /// <summary>해상도와 전체화면을 바꾸고 저장한다. 모바일에서는 적용되지 않는다.</summary>
    public void SetResolution(int width, int height, FullScreenMode mode)
    {
        if (Core.Display == null || !Core.Display.IsDesktop) return;

        Core.Display.SetResolution(width, height, mode);

        Store(data =>
        {
            data.ScreenWidth = width;
            data.ScreenHeight = height;
            data.ScreenModeValue = (int)mode;
        });
    }

    #endregion

    #region Language

    /// <summary>
    /// 고른 언어 코드. 빈 값이면 기기 언어를 따른다(처음 실행). 실제로 보이는 언어는 <see cref="LocalizationManager.Language"/>
    /// — 표에 없는 언어를 고르면 대체 언어가 보인다.
    /// </summary>
    public string Language
    {
        get
        {
            SettingsData data = Core.Data?.Get<SettingsData>();
            return data != null ? data.Language : string.Empty;
        }
        set
        {
            value ??= string.Empty;
            if (Language == value) return;
            Core.Localization?.SetLanguage(value);

            Store(data => data.Language = value);
        }
    }

    #endregion

    // 그래픽 품질은 해당 매니저가 생기면 여기에 같은 방식으로 추가한다.

    /// <summary>바뀐 값을 SettingsData에 기록하고 변경을 알린다.</summary>
    private void Store(Action<SettingsData> write)
    {
        SettingsData data = Core.Data?.Get<SettingsData>();
        if (data != null)
        {
            write(data);
            data.MarkDirty();
        }

        OnSettingsChanged?.Invoke();
    }

    /// <summary>저장된 설정을 각 매니저에 다시 밀어 넣는다.</summary>
    public void ApplyAll()
    {
        SettingsData data = Core.Data?.Get<SettingsData>();
        if (data == null) return;

        if (Core.Audio != null)
        {
            Core.Audio.SetVolume(AudioChannelType.Master, data.MasterVolume);
            Core.Audio.SetVolume(AudioChannelType.Music, data.BgmVolume);
            Core.Audio.SetVolume(AudioChannelType.Sound, data.SfxVolume);
        }

        if (Core.Haptic != null) Core.Haptic.IsEnabled = data.HapticEnabled;

        Core.Localization?.SetLanguage(data.Language);

        if (Core.Display != null)
        {
            // vSync를 먼저 넣는다. 프레임레이트 적용이 vSync 상태를 보고 갈리기 때문이다.
            Core.Display.VSyncEnabled = data.VSyncEnabled;
            Core.Display.TargetFrameRate = data.TargetFrameRate;

            // 저장된 해상도가 있을 때만 되돌린다. 값이 없는데 건드리면 첫 실행에서 창이 흔들린다.
            if (Core.Display.IsDesktop && data.ScreenWidth > 0 && data.ScreenHeight > 0 && data.ScreenModeValue >= 0)
                Core.Display.SetResolution(data.ScreenWidth, data.ScreenHeight, (FullScreenMode)data.ScreenModeValue);
        }

        OnSettingsChanged?.Invoke();
    }
}
