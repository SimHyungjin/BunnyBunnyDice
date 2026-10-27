using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 코어 점검용 씬. <b>씬 파일 이름도 반드시 "TestCoreScene"이어야 한다</b>
/// — <see cref="SceneFlowManager"/>가 씬 이름으로 같은 이름의 <see cref="SceneBase"/>를 찾아 붙인다.
///
/// 화면에 IMGUI 패널을 띄우는 이유: 점검용 UI를 만들려면 그 UI를 띄우는 계통(Addressables·풀·UIManager)이
/// 이미 정상이어야 하는데, 지금 확인하려는 것이 바로 그 계통이다. OnGUI는 아무것에도 기대지 않으므로
/// 코어가 부팅에 실패한 상태에서도 원인을 볼 수 있다.
/// </summary>
public sealed class TestCoreScene : SceneBase
{
    public override UniTask EnterScene(CancellationToken token)
    {
        // 점검 씬의 일반 오브젝트로 둔다. DontDestroyOnLoad에는 코어 것만 남기고, 씬을 벗어나면 패널도 함께 사라진다.
        new GameObject("TestCoreRunner").AddComponent<TestCoreRunner>();

        Debug.Log("[TestCore] 점검 씬에 진입했습니다. 화면 좌측 패널로 각 계통을 확인하세요.");
        return UniTask.CompletedTask;
    }
}

/// <summary>
/// 실제 점검 버튼들. 결과는 화면 하단과 콘솔 양쪽에 남긴다
/// — 콘솔만 보면 프레임레이트처럼 "계속 변하는 값"을 확인할 수 없고,
///   화면만 보면 지나간 로그를 놓치기 때문이다.
/// </summary>
public sealed class TestCoreRunner : MonoBehaviour
{
    private const int PANEL_WIDTH = 330;
    private const int LOG_LINES   = 8;

    private readonly List<string> _log = new();
    private readonly StringBuilder _sb = new();

    private Vector2 _scroll;
    private UIAnimationType _animType = UIAnimationType.Scale;

    // 실측 프레임레이트. targetFrameRate가 실제로 먹었는지는 이 값으로만 확인할 수 있다.
    private float _fpsAccum;
    private int   _fpsFrames;
    private float _fps;

    private void Update()
    {
        _fpsAccum += Time.unscaledDeltaTime;
        _fpsFrames++;

        if (_fpsAccum < 0.5f) return;

        _fps = _fpsFrames / _fpsAccum;
        _fpsAccum = 0f;
        _fpsFrames = 0;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, PANEL_WIDTH, Screen.height - 20), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawStatus();
        DrawConfig();
        DrawTween();
        DrawDisplay();
        DrawHaptic();
        DrawSettings();
        DrawNavigation();
        DrawTime();
        DrawLog();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    #region 부팅 상태

    private void DrawStatus()
    {
        GUILayout.Label("── 부팅 ──");
        GUILayout.Label($"IsCoreReady: {Core.IsCoreReady}   IsReady: {Core.IsReady}");

        // 초기화에 실패한 매니저만 짚어 준다. 전부 나열하면 정작 문제를 못 본다.
        _sb.Clear();
        AppendIfFailed(_sb, "Loop", Core.Loop);
        AppendIfFailed(_sb, "AppState", Core.AppState);
        AppendIfFailed(_sb, "Resource", Core.Resource);
        AppendIfFailed(_sb, "Pool", Core.Pool);
        AppendIfFailed(_sb, "Scene", Core.Scene);
        AppendIfFailed(_sb, "Input", Core.Input);
        AppendIfFailed(_sb, "Data", Core.Data);
        AppendIfFailed(_sb, "Camera", Core.Camera);
        AppendIfFailed(_sb, "Audio", Core.Audio);
        AppendIfFailed(_sb, "UI", Core.UI);
        AppendIfFailed(_sb, "Safe", Core.Safe);
        AppendIfFailed(_sb, "Haptic", Core.Haptic);
        AppendIfFailed(_sb, "Display", Core.Display);
        AppendIfFailed(_sb, "Settings", Core.Settings);

        GUILayout.Label(_sb.Length == 0 ? "코어 매니저: 전부 정상" : $"<b>실패:</b>{_sb}");
        GUILayout.Space(6);
    }

    private static void AppendIfFailed(StringBuilder sb, string name, Manager manager)
    {
        if (manager == null) { sb.Append(' ').Append(name).Append("(없음)"); return; }
        if (!manager.IsInitialized) sb.Append(' ').Append(name);
    }

    #endregion

    #region 설정 에셋

    private void DrawConfig()
    {
        GUILayout.Label("── SO_CoreConfig ──");

        SO_CoreConfig config = SO_CoreConfig.Instance;
        GUILayout.Label($"애니메이션 {config.UIAnimationDuration:0.00}s · 가드알파 {config.PopupGuardAlpha:0.00}");
        GUILayout.Label($"모바일FPS {config.MobileTargetFrameRate} · SFX풀 {config.SfxPoolSize}");

        // 에셋을 만들지 않았어도 기본값으로 도는 설계라, 값만 봐서는 어느 쪽인지 알 수 없다.
        if (GUILayout.Button("에셋에서 읽혔는지 확인"))
        {
            var loaded = Resources.Load<SO_CoreConfig>("SO_CoreConfig");
            Write(loaded == null
                ? "SO_CoreConfig 에셋 없음 → 코드 기본값으로 동작 중"
                : $"에셋 로드됨: {UnityEditorSafeName(loaded)}");
        }

        GUILayout.Space(6);
    }

    private static string UnityEditorSafeName(Object obj) => obj != null ? obj.name : "(null)";

    #endregion

    #region CoreTween · 팝업

    private void DrawTween()
    {
        GUILayout.Label("── CoreTween (팝업) ──");
        GUILayout.Label($"연출: {_animType}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Scale"))  _animType = UIAnimationType.Scale;
        if (GUILayout.Button("Fade"))   _animType = UIAnimationType.Fade;
        if (GUILayout.Button("Slide")) _animType = UIAnimationType.SlideFromBottom;
        GUILayout.EndHorizontal();

        if (GUILayout.Button("팝업 열기")) OpenPopupAsync().Forget();
        if (GUILayout.Button("최상위 팝업 닫기")) Core.UI?.CloseTopPopup();

        GUILayout.Space(6);
    }

    private async UniTaskVoid OpenPopupAsync()
    {
        // 연출은 열기 전에 정해야 한다. ShowPopup이 스폰과 동시에 Open()을 부르므로,
        // 결과를 받은 뒤에 바꾸면 이미 한 번 재생된 뒤라 연출이 두 번 겹친다.
        UI_TestPopup.NextAnimation = _animType;

        // 이 한 줄이 Addressables → Resource → Pool → UI 레이어 → CoreTween을 모두 지난다.
        UI_TestPopup popup = await App.ShowPopup<UI_TestPopup>();

        if (popup == null)
        {
            Write("팝업 스폰 실패 — 'UI_TestPopup' 주소의 Addressables 프리팹이 있는지 확인하세요.");
            return;
        }

        popup.SetLabel($"{_animType} 연출 테스트");
        Write($"팝업 열림 ({_animType}) — 열린 팝업 {(Core.UI.HasOpenPopup ? "있음" : "없음")}");
    }

    #endregion

    #region Display

    private void DrawDisplay()
    {
        GUILayout.Label("── Display ──");
        GUILayout.Label($"실측 {_fps:0.0} fps · 목표 {Application.targetFrameRate} · vSync {QualitySettings.vSyncCount}");
        GUILayout.Label($"{Screen.width}x{Screen.height} · {(App.IsDesktop ? "데스크톱" : "모바일")}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("30")) SetFps(30);
        if (GUILayout.Button("60")) SetFps(60);
        if (GUILayout.Button("기본")) SetFps(DisplayManager.FRAME_RATE_PLATFORM_DEFAULT);
        GUILayout.EndHorizontal();

        if (GUILayout.Button($"vSync {(App.VSyncEnabled ? "끄기" : "켜기")}"))
        {
            App.VSyncEnabled = !App.VSyncEnabled;
            Write($"vSync {App.VSyncEnabled} (켜면 목표 프레임레이트는 무시된다)");
        }

        if (App.IsDesktop)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전체화면 토글"))
            {
                bool full = Screen.fullScreenMode != FullScreenMode.Windowed;
                Core.Display?.SetFullScreen(!full);
                Write($"전체화면 → {!full}");
            }
            if (GUILayout.Button("커서 숨김/잠금"))
            {
                App.SetCursor(false, CursorLockMode.Locked);
                Write("커서 잠금 — ESC로 해제됩니다");
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
    }

    private void SetFps(int value)
    {
        App.TargetFrameRate = value;
        Write($"목표 프레임레이트 {value} (0=플랫폼 기본) → 실제 {Application.targetFrameRate}");
    }

    #endregion

    #region Haptic

    private void DrawHaptic()
    {
        GUILayout.Label("── Haptic ──");
        GUILayout.Label($"지원: {App.HapticSupported} · 사용: {App.HapticEnabled}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Weak"))   App.Vibrate(HapticType.Weak);
        if (GUILayout.Button("Soft"))   App.Vibrate(HapticType.Soft);
        if (GUILayout.Button("Medium")) App.Vibrate(HapticType.Medium);
        if (GUILayout.Button("Hard"))   App.Vibrate(HapticType.Hard);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
    }

    #endregion

    #region 설정 저장 · 복원

    private void DrawSettings()
    {
        GUILayout.Label("── 설정 저장 ──");

        SettingsData data = Core.Data?.Get<SettingsData>();
        GUILayout.Label(data == null
            ? "SettingsData 없음"
            : $"저장값: FPS {data.TargetFrameRate} · vSync {data.VSyncEnabled} · 진동 {data.HapticEnabled}");

        if (GUILayout.Button("진동 토글 후 즉시 저장"))
        {
            App.HapticEnabled = !App.HapticEnabled;
            Core.Data?.SaveDirty();
            Write($"진동 {App.HapticEnabled} 저장 — 플레이를 멈췄다 다시 켜서 유지되는지 확인하세요");
        }

        if (GUILayout.Button("저장 파일 초기화"))
        {
            Core.Data?.ResetAll();
            Write("세이브 초기화 — 설정이 기본값으로 되돌아갑니다");
        }

        GUILayout.Space(6);
    }

    #endregion

    #region 뒤로가기 · 앱 상태

    private void DrawNavigation()
    {
        GUILayout.Label("── 뒤로가기 ──");

        if (GUILayout.Button("HandleBackPressed()"))
        {
            bool handled = App.HandleBackPressed();
            Write(handled
                ? "팝업이 닫혔습니다 (코어가 처리)"
                : "코어가 처리하지 않음 → AppState.OnBackPressed로 넘어감");
        }

        if (GUILayout.Button($"포커스 상실=백그라운드: {AppStateFocusPolicy}"))
        {
            if (Core.AppState != null)
            {
                Core.AppState.TreatFocusLossAsBackground = !Core.AppState.TreatFocusLossAsBackground;
                Write($"TreatFocusLossAsBackground = {Core.AppState.TreatFocusLossAsBackground} " +
                      "(끄면 alt-tab이 백그라운드로 취급되지 않습니다)");
            }
        }

        GUILayout.Space(6);
    }

    private static string AppStateFocusPolicy
        => Core.AppState != null ? Core.AppState.TreatFocusLossAsBackground.ToString() : "?";

    #endregion

    #region 시간 배율

    private void DrawTime()
    {
        GUILayout.Label("── 시간 배율 ──");
        GUILayout.Label($"월드 {App.WorldTimeScale:0.00} · 플레이어 {App.PlayerTimeScale:0.00}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("히트스톱")) { App.FreezeWorldOnly(); Write("월드만 정지 (팝업 연출은 계속 돌아야 정상)"); }
        if (GUILayout.Button("0.2배"))   { App.SetGameSpeed(0.2f); Write("전체 슬로모션"); }
        if (GUILayout.Button("복구"))     { App.ResumeAll(); Write("시간 배율 복구"); }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
    }

    #endregion

    #region 로그

    private void DrawLog()
    {
        GUILayout.Label("── 결과 ──");
        for (int i = 0; i < _log.Count; i++)
            GUILayout.Label(_log[i]);
    }

    private void Write(string message)
    {
        Debug.Log($"[TestCore] {message}");

        _log.Add(message);
        if (_log.Count > LOG_LINES) _log.RemoveAt(0);
    }

    #endregion
}
