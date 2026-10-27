using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 매니저 기능에 짧게 접근하기 위한 정적 진입점.
/// 호출부가 Core.Xxx.Method() 대신 App.Method()로 쓰게 해서, 자주 쓰는 동작을 한눈에 모은다.
///
/// 코어 매니저가 늘어나면 이 파일에 구획을 추가하거나, 코어 안에서 App.Audio.cs 같은 partial 파일로 나눠 담는다.
/// 코어는 별도 어셈블리라 게임 쪽에서 App을 partial로 확장할 수는 없다 — 게임 매니저 접근자는 게임의 정적 클래스에 둔다.
/// </summary>
public static partial class App
{
    #region Time / Loop

    /// <summary>월드·플레이어를 같은 배율로. (완전 일시정지·전체 슬로모션)</summary>
    public static void SetGameSpeed(float speed) => Core.Loop?.SetGameSpeed(speed);

    /// <summary>월드와 플레이어 배율을 따로.</summary>
    public static void SetTimeScales(float worldScale, float playerScale)
        => Core.Loop?.SetTimeScales(worldScale, playerScale);

    /// <summary>월드만 멈추고 플레이어는 정상 동작. (히트스톱)</summary>
    public static void FreezeWorldOnly() => Core.Loop?.FreezeWorldOnly();

    /// <summary>시간 배율을 기본값(1배)으로 되돌린다.</summary>
    public static void ResumeAll() => Core.Loop?.ResumeAll();

    public static float WorldTimeScale  => Core.Loop != null ? Core.Loop.WorldTimeScale  : 1f;
    public static float PlayerTimeScale => Core.Loop != null ? Core.Loop.PlayerTimeScale : 1f;

    #endregion

    #region App State

    // 이벤트 구독(OnEnterForeground / OnEnterBackground)은 Core.AppState에서 직접 한다.
    // 파사드는 메서드·상태만 미러링한다.

    /// <summary>
    /// 광고·결제창 등 잠깐 포커스를 뺏는 동안 백그라운드 전환 처리를 미룬다. using으로 감싼다.
    /// (자세한 동작은 <see cref="AppStateManager.SuppressStateEvents"/>)
    /// </summary>
    public static IDisposable SuppressStateEvents()
        => Core.AppState != null ? Core.AppState.SuppressStateEvents() : NoopScope.Instance;

    /// <summary>지금 백그라운드 전환 처리를 미루는 중인지.</summary>
    public static bool IsStateEventsSuppressed => Core.AppState != null && Core.AppState.IsSuppressingStateEvents;

    // 코어가 없을 때 돌려주는 빈 스코프. 호출부가 null 검사 없이 using을 쓸 수 있게 한다.
    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }

    #endregion

    #region Resource

    // 취소는 에셋 스코프가 스스로 한다 — Scene 수명이면 씬 전환 때, Persistent면 취소되지 않는다.
    // 그래서 여기서는 씬 토큰을 묶지 않고 호출자 토큰만 넘긴다.

    /// <summary>키로 에셋을 로드한다. 키를 비우면 타입 이름이 키가 된다. (Scene 수명이면 씬 전환 시 대기가 취소된다 — 스코프 토큰)</summary>
    public static async UniTask<T> LoadAsset<T>(
        string key = null,
        AssetLifetime life = AssetLifetime.Scene,
        CancellationToken ct = default) where T : Object
    {
        if (Core.Resource == null) return null;
        return await Core.Resource.LoadAsync<T>(key, life, ct);
    }

    /// <summary>라벨에 속한 에셋들을 한 번에 미리 로드한다. (로딩 타이밍에 그룹 프리로드)</summary>
    public static async UniTask PreloadLabel(
        string label,
        AssetLifetime life = AssetLifetime.Scene,
        CancellationToken ct = default)
    {
        if (Core.Resource == null) return;
        await Core.Resource.PreloadLabelAsync(label, life, ct);
    }

    /// <summary>이미 로드된 에셋을 즉시 가져온다(동기). 씬·영구 스코프 순으로 찾는다. 없으면 null.</summary>
    public static T GetAssetNow<T>(string key = null) where T : Object
        => Core.Resource != null ? Core.Resource.GetNow<T>(key) : null;

    // 개별 해제는 스코프 단위로 한다: Core.Resource.Scene.Release(key) 등.
    // 코어 스코프의 에셋은 보통 씬 전환(Scene)이나 앱 종료(Persistent)에 맡긴다.

    #endregion

    #region Pool

    // 위치·회전을 넘기면 월드 좌표, 부모만 넘기면 부모 기준 로컬 원점에 놓인다.

    /// <summary>동기 스폰(월드 좌표). 예열됐거나 프리팹이 프리로드돼 있어야 한다. (뜨거운 스폰용)</summary>
    public static T Spawn<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null,
        AssetLifetime life = AssetLifetime.Scene) where T : Component
        => Core.Pool != null ? Core.Pool.Spawn<T>(key, position, rotation, parent, life) : null;

    /// <summary>동기 스폰(부모 아래 로컬 원점).</summary>
    public static T Spawn<T>(string key, Transform parent = null, AssetLifetime life = AssetLifetime.Scene) where T : Component
        => Core.Pool != null ? Core.Pool.Spawn<T>(key, parent, life) : null;

    /// <summary>비동기 스폰(부모 아래 로컬 원점). 풀이 없으면 프리팹을 로드해 만든 뒤 스폰한다. (콜드 스폰용)</summary>
    public static async UniTask<T> SpawnAsync<T>(string key = null, Transform parent = null,
        AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default) where T : Component
    {
        if (Core.Pool == null) return null;
        return await Core.Pool.SpawnAsync<T>(key, parent, life, ct);
    }

    /// <summary>비동기 스폰(월드 좌표).</summary>
    public static async UniTask<T> SpawnAsync<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null,
        AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default) where T : Component
    {
        if (Core.Pool == null) return null;
        return await Core.Pool.SpawnAsync<T>(key, position, rotation, parent, life, ct);
    }

    /// <summary>풀을 미리 채운다. (로딩 타이밍에 예열)</summary>
    public static async UniTask PrewarmPool(string key, int count, AssetLifetime life = AssetLifetime.Scene,
        CancellationToken ct = default)
    {
        if (Core.Pool == null) return;
        await Core.Pool.PrewarmAsync(key, count, life, ct);
    }

    public static void Despawn(GameObject go)   => Core.Pool?.Despawn(go);
    public static void Despawn(Component comp)  => Core.Pool?.Despawn(comp);

    /// <summary>delay초 뒤 자동 반납을 예약한다.</summary>
    public static void ScheduleDespawn(GameObject go, float delay, bool unscaled = false)
        => Core.Pool?.ScheduleDespawn(go, delay, unscaled);

    /// <summary>이 오브젝트에 걸린 반납 예약을 모두 취소한다. (수명을 스스로 관리하는 오브젝트용)</summary>
    public static void CancelScheduledDespawn(GameObject go)
        => Core.Pool?.CancelScheduledDespawn(go);

    #endregion

    #region Scene

    /// <summary>다른 씬으로 전환한다.</summary>
    public static UniTask ChangeScene(string sceneName, Func<UniTask> onBeforeLoad = null, Func<UniTask> onAfterLoad = null)
        => Core.Scene != null ? Core.Scene.ChangeSceneAsync(sceneName, onBeforeLoad, onAfterLoad) : UniTask.CompletedTask;

    /// <summary>현재 씬을 다시 로드한다. (재시작) 훅은 전환 연출 자리다.</summary>
    public static UniTask ReloadScene(Func<UniTask> onBeforeLoad = null, Func<UniTask> onAfterLoad = null)
        => Core.Scene != null ? Core.Scene.ReloadCurrentSceneAsync(onBeforeLoad, onAfterLoad) : UniTask.CompletedTask;

    /// <summary>씬 토큰에 묶인 지연. 씬이 바뀌면 자동 취소된다.</summary>
    public static async UniTask SceneDelay(float seconds, CancellationToken ct = default)
    {
        using var cts = LinkScene(ct);
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: cts.Token);
    }

    #endregion

    #region Audio

    // 음수는 "설정(SO_CoreConfig)의 기본 시간을 쓰라"는 뜻이다.
    public static void PlayMusic(SO_MusicTrack track, float crossfade = -1f) => Core.Audio?.PlayMusic(track, crossfade);
    public static void StopMusic(float fade = -1f)                           => Core.Audio?.StopMusic(fade);
    public static SO_MusicTrack CurrentMusic => Core.Audio?.CurrentMusic;

    /// <summary>효과음 큐를 재생한다. 위치를 넘기면 3D, 아니면 2D. 루프 큐라면 돌려받은 핸들로 멈춘다.</summary>
    public static SoundHandle PlaySfx(SO_SoundCue cue, Vector3? position = null)
        => Core.Audio != null ? Core.Audio.PlaySfx(cue, position) : default;

    /// <summary>큐 없이 클립을 그대로 낸다. 반복해서 쓰는 소리라면 큐로 만드는 편이 낫다.</summary>
    public static SoundHandle PlaySfx(AudioClip clip, Vector3? position = null, float volume = 1f)
        => Core.Audio != null ? Core.Audio.PlaySfx(clip, position, volume) : default;

    // 볼륨은 유저 설정이라 저장까지 하는 SettingsManager를 거친다.
    public static void SetVolume(AudioChannelType channel, float value) => Core.Settings?.SetVolume(channel, value);
    public static float GetVolume(AudioChannelType channel) => Core.Settings != null ? Core.Settings.GetVolume(channel) : 1f;

    #endregion

    #region Localization

    /// <summary>
    /// 원문(한국어)의 지금 언어 글. 번역이 없으면 원문 그대로. SO의 [Localized] 필드·코드의 글을 화면에 보일 때 거친다.
    /// 코드에 글을 적을 때는 <c>App.Localize("원문")</c>처럼 <b>문자열을 이 호출 안에 바로</b> 적는다 — 번역표 갱신 툴이 그 모양을 찾아 모은다.
    /// 숫자가 들어가는 글은 "웨이브 {0}"처럼 자리를 남겨 번역한 뒤 채운다(어순이 언어마다 다르다).
    /// </summary>
    public static string Localize(string source) => Core.Localization != null ? Core.Localization.Get(source) : source;

    /// <summary>원문을 번역한 뒤 {0}·{1} 자리를 채운다.</summary>
    public static string Localize(string source, params object[] args) => string.Format(Localize(source), args);

    /// <summary>지금 보이는 언어 코드.</summary>
    public static string Language => Core.Localization != null ? Core.Localization.Language : SO_CoreConfig.Instance.SourceLanguage;

    #endregion

    #region Haptic

    /// <summary>진동 피드백. (설정에서 꺼 두었거나 지원하지 않는 기기면 무시된다)</summary>
    public static void Vibrate(HapticType type) => Core.Haptic?.Vibrate(type);

    /// <summary>이 기기에서 진동이 실제로 울리는지. 설정 화면에서 진동 항목을 감출 때 쓴다.</summary>
    public static bool HapticSupported => Core.Haptic != null && Core.Haptic.IsSupported;

    #endregion

    #region Navigation

    /// <summary>
    /// 뒤로가기 요청을 처리한다. Android 뒤로가기 키와 PC의 ESC가 같은 곳으로 들어오게 하는 진입점이다.
    ///
    /// 열린 팝업이 있으면 가장 위의 것을 닫고 true를 돌려준다.
    /// 그 팝업이 뒤로가기로 닫히지 않게 설정돼 있으면(<see cref="UI_Popup.CloseOnBack"/>) 닫지 않고 소비만 한다.
    /// 남은 요청은 <see cref="AppStateManager.OnBackPressed"/>로 넘어가며 이때 false를 돌려준다
    /// — 일시정지 메뉴를 열지, 종료를 물을지는 게임이 정할 일이다.
    ///
    /// 키 입력은 코어가 읽지 않는다. 게임의 입력 모듈에서 이 메서드를 부르면 된다.
    /// <example>
    /// <code>
    /// // 게임 쪽 InputActions
    /// if (_cancelAction.WasPressedThisFrame()) App.HandleBackPressed();
    /// </code>
    /// </example>
    /// </summary>
    /// <returns>코어가 처리했으면(팝업을 닫았으면) true.</returns>
    public static bool HandleBackPressed()
    {
        if (Core.UI != null && Core.UI.HandleBack()) return true;

        Core.AppState?.RaiseBackPressed();
        return false;
    }

    #endregion

    #region Display

    /// <summary>해상도·전체화면·커서를 다룰 수 있는 플랫폼인지. (데스크톱 true, 모바일 false)</summary>
    public static bool IsDesktop => Core.Display != null && Core.Display.IsDesktop;

    /// <summary>커서 표시·잠금. 모바일에서는 무시된다.</summary>
    public static void SetCursor(bool visible, CursorLockMode lockMode = CursorLockMode.None)
        => Core.Display?.SetCursor(visible, lockMode);

    #endregion

    #region Settings

    // 볼륨은 위 Audio 구획의 SetVolume/GetVolume을 쓴다.

    /// <summary>진동 사용 여부. 설정 화면에서 토글한다.</summary>
    public static bool HapticEnabled
    {
        get => Core.Settings != null && Core.Settings.HapticEnabled;
        set { if (Core.Settings != null) Core.Settings.HapticEnabled = value; }
    }

    /// <summary>목표 프레임레이트. 0이면 플랫폼 기본(모바일 60 · 데스크톱 주사율).</summary>
    public static int TargetFrameRate
    {
        get => Core.Settings != null ? Core.Settings.TargetFrameRate : 0;
        set { if (Core.Settings != null) Core.Settings.TargetFrameRate = value; }
    }

    /// <summary>수직동기. 켜면 프레임레이트 제한은 무시된다.</summary>
    public static bool VSyncEnabled
    {
        get => Core.Settings != null && Core.Settings.VSyncEnabled;
        set { if (Core.Settings != null) Core.Settings.VSyncEnabled = value; }
    }

    /// <summary>해상도·전체화면을 바꾸고 저장한다. 모바일에서는 무시된다.</summary>
    public static void SetResolution(int width, int height, FullScreenMode mode)
        => Core.Settings?.SetResolution(width, height, mode);

    #endregion

    #region Safe Area

    /// <summary>현재 세이프 에어리어 데이터. (구독은 Core.Safe.OnSafeAreaChanged에서 직접)</summary>
    public static SafeAreaManager.SafeAreaData GetSafeArea()
        => Core.Safe != null ? Core.Safe.GetCurrentData() : default;

    #endregion

    #region Camera

    public static void SetCameraFollow(Transform target, float speed = 12f, bool snap = false)
        => Core.Camera?.SetFollow(target, speed, snap);

    public static void SetCameraFollow(Transform target, Vector3 offset, float speed = 12f, bool snap = true)
        => Core.Camera?.SetFollow(target, offset, speed, snap);

    public static void ClearCameraFollow()                 => Core.Camera?.ClearFollow();
    public static void ClearCameraFollow(Transform target) => Core.Camera?.ClearFollow(target);

    public static void SetCameraView(Vector3 position, Quaternion rotation)
        => Core.Camera?.SetView(position, rotation);

    public static void SetCameraView(Vector3 position, Quaternion rotation, bool orthographic, float orthographicSize)
        => Core.Camera?.SetView(position, rotation, orthographic, orthographicSize);

    /// <summary>화면을 흔든다. (히트스톱 중에도 흔들림)</summary>
    public static void ShakeCamera(float amplitude, float duration, float frequency = 25f)
        => Core.Camera?.Shake(amplitude, duration, frequency);

    #endregion

    #region Data

    /// <summary>세이브 섹션을 가져온다. 값을 바꾼 뒤엔 섹션의 MarkDirty()를 부른다.</summary>
    public static T GetData<T>() where T : SaveSection, new() => Core.Data?.Get<T>();

    /// <summary>바뀐 섹션만 지금 저장한다. (보통은 백그라운드 전환·씬 전환·자동 저장에 맡긴다)</summary>
    public static void SaveData()  => Core.Data?.SaveDirty();

    /// <summary>모든 세이브를 지우고 기본값으로 되돌린다.</summary>
    public static void ResetData() => Core.Data?.ResetAll();

    #endregion

    #region UI

    // 대기는 에셋 스코프의 수명 토큰에 묶여 있다 — Scene 수명 UI의 로드는 씬 전환 때 자동 취소된다.

    public static UniTask<T> ShowWorld<T>(string key = null, AssetLifetime life = AssetLifetime.Scene,
        CancellationToken ct = default) where T : UI_World
        => Core.UI != null ? Core.UI.ShowWorld<T>(key, life, ct) : UniTask.FromResult<T>(null);

    public static UniTask<T> ShowHud<T>(string key = null, AssetLifetime life = AssetLifetime.Scene,
        CancellationToken ct = default) where T : UI_Hud
        => Core.UI != null ? Core.UI.ShowHud<T>(key, life, ct) : UniTask.FromResult<T>(null);

    public static UniTask<T> ShowPopup<T>(
        string key = null,
        AssetLifetime life = AssetLifetime.Scene,
        bool clickGuard = true,
        float clickGuardAlpha = -1f,
        bool clickToClose = true,
        CancellationToken ct = default) where T : UI_Popup
        => Core.UI != null
            ? Core.UI.ShowPopup<T>(key, life, clickGuard, clickGuardAlpha, clickToClose, ct)
            : UniTask.FromResult<T>(null);

    /// <summary>전환 커튼·로딩 화면. 기본 수명이 Persistent라 씬이 바뀌어도 남는다 — 다 쓰면 CloseTransition으로 닫는다.</summary>
    public static UniTask<T> ShowTransition<T>(string key = null, AssetLifetime life = AssetLifetime.Persistent,
        CancellationToken ct = default) where T : UI_Transition
        => Core.UI != null ? Core.UI.ShowTransition<T>(key, life, ct) : UniTask.FromResult<T>(null);

    public static void CloseWorld(UI_World panel)               => Core.UI?.CloseWorld(panel);
    public static void CloseAllWorld()                          => Core.UI?.CloseAllWorld();
    public static void CloseHud()                               => Core.UI?.CloseHud();
    public static void ClosePopup(UI_Popup popup)               => Core.UI?.ClosePopup(popup);
    public static void CloseTopPopup(bool withAnimation = true) => Core.UI?.CloseTopPopup(withAnimation);
    public static void CloseAllPopups(bool instant = false)     => Core.UI?.CloseAllPopups(instant);
    public static void CloseTransition()                        => Core.UI?.CloseTransition();

    #endregion

    #region Input

    /// <summary>모듈 T를 활성화하며 초기 설정을 준다.</summary>
    public static T SetInput<T>(Action<T> onInit) where T : InputActions
        => Core.Input != null ? Core.Input.SetInput(onInit) : null;

    public static void SetInput<T>() where T : InputActions => Core.Input?.SetInput<T>();
    public static void SetInput<T1, T2>() where T1 : InputActions where T2 : InputActions => Core.Input?.SetInput<T1, T2>();
    public static void SetInput<T1, T2, T3>() where T1 : InputActions where T2 : InputActions where T3 : InputActions => Core.Input?.SetInput<T1, T2, T3>();

    public static void AddInput<T>()    where T : InputActions => Core.Input?.AddInput<T>();
    public static void RemoveInput<T>() where T : InputActions => Core.Input?.RemoveInput<T>();
    public static void RemoveAllInputs()                       => Core.Input?.RemoveAllInputs();

    public static bool IsInputActive<T>() where T : InputActions => Core.Input != null && Core.Input.IsActive<T>();

    #endregion

    // 호출자 토큰과 현재 씬 토큰을 묶는다. 씬이 바뀌면 진행 중 작업이 자동 취소된다.
    private static CancellationTokenSource LinkScene(CancellationToken ct)
        => CancellationTokenSource.CreateLinkedTokenSource(
            ct, Core.Scene != null ? Core.Scene.CurrentToken : CancellationToken.None);
}
