using System;

/// <summary>
/// 유저 설정 세이브. <see cref="SettingsManager"/>가 각 매니저에 적용하고, 오디오는 볼륨을 직접 기록한다.
/// </summary>
[Serializable]
public sealed class SettingsData : SaveSection
{
    // 클래스 이름이 바뀌어도 기존 세이브와 이어지도록 키를 고정한다.
    public override string Key => "Settings";

    // ── 오디오 ──
    public float MasterVolume = 1f;
    public float BgmVolume    = 1f;
    public float SfxVolume    = 1f;

    // ── 진동 ──
    public bool HapticEnabled = true;

    // ── 언어 (LocalizationManager가 적용한다) ──
    public string Language = "";        // 빈 값 = 고른 적 없음. 기기 언어를 따른다

    // ── 화면 (DisplayManager가 적용한다) ──
    // 해상도·전체화면은 데스크톱에서만 의미가 있다. 모바일에서는 저장은 되되 적용되지 않는다.
    public int  TargetFrameRate;        // 0 = 플랫폼 기본(모바일 60 · 데스크톱 주사율)
    public bool VSyncEnabled;
    public int  ScreenWidth;            // 0 = 저장된 값 없음. 현재 해상도를 그대로 둔다
    public int  ScreenHeight;
    public int  ScreenModeValue = -1;   // UnityEngine.FullScreenMode. -1 = 저장된 값 없음
}
