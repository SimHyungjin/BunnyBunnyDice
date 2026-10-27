using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// UI를 4개 레이어로 나눠 관리한다. 아래부터 World(월드 위 UI, 여러 개) · Hud(하나) · Popup(스택) · Transition(전환 커튼, 하나).
/// 각 레이어는 sortingOrder로 층이 갈린다(<see cref="SO_CoreConfig"/>). UI 인스턴스는 PoolManager로 스폰/반납한다.
///
/// 수명: Show할 때 <see cref="AssetLifetime"/>을 고른다. 씬 전환 시 Scene 수명 UI는 즉시 닫히고, Persistent UI는 남는다.
/// Transition의 기본 수명은 Persistent다 — 전환 중 씬이 바뀌는 동안 화면을 덮고 있어야 하므로.
/// 대기는 에셋 스코프의 수명 토큰에 묶여 있어, Scene 수명 UI의 로드는 씬 전환 때 자동 취소된다.
///
/// 초기화 시 Resources에서 캔버스·이벤트시스템 프리팹을 불러 레이어 캔버스를 만든다.
/// </summary>
public sealed class UIManager : Manager
{
    // Unity는 프로젝트 안의 모든 Resources 폴더를 하나의 이름 공간으로 합친다.
    // 그래서 "Canvas" 같은 흔한 이름을 쓰면 게임 프로젝트가 같은 이름의 프리팹을 두는 순간
    // 어느 쪽이 로드될지 보장되지 않는다 — 코어가 게임의 캔버스로 UI 레이어를 만들어 버릴 수 있고,
    // 원인을 찾기 매우 어려운 종류의 사고다. 코어 소유임이 드러나는 이름을 쓴다.
    private const string CANVAS_RESOURCE       = "CoreCanvas";
    private const string EVENT_SYSTEM_RESOURCE = "CoreEventSystem";

    private UIManager_WorldLayer                  _worldLayer;
    private UIManager_SingleLayer<UI_Hud>         _hudLayer;
    private UIManager_PopupLayer                  _popupLayer;
    private UIManager_SingleLayer<UI_Transition>  _transitionLayer;

    public override Type[] Dependencies => new[] { typeof(PoolManager) };

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        GameObject uiRoot = new("@UI");
        Object.DontDestroyOnLoad(uiRoot);

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esPrefab = Resources.Load<UnityEngine.EventSystems.EventSystem>(EVENT_SYSTEM_RESOURCE);
            if (esPrefab != null)
            {
                // UI 루트 아래에 두면 루트의 DontDestroyOnLoad를 함께 따르고, 하이어라키에서도 UI 계통으로 묶인다.
                var esObj = Object.Instantiate(esPrefab, uiRoot.transform);
                esObj.name = "EventSystem";
            }
            else
            {
                // 이벤트시스템이 없으면 버튼이 눌리지 않는다. 조용히 넘어가면
                // "UI는 보이는데 아무것도 안 눌린다"는 증상으로만 드러나 원인을 찾기 어렵다.
                Debug.LogError($"[UI] Resources에서 '{EVENT_SYSTEM_RESOURCE}' 프리팹을 찾지 못했습니다. UI 입력이 동작하지 않습니다.");
            }
        }

        Canvas canvasPrefab = Resources.Load<Canvas>(CANVAS_RESOURCE);
        if (canvasPrefab == null)
        {
            // 여기서 멈추지 않으면 Instantiate가 null로 예외를 던져, 진짜 원인(프리팹 없음)이
            // 스택 트레이스에 드러나지 않는다. UI 레이어 없이 계속 가되 무엇이 없는지는 분명히 남긴다.
            Debug.LogError($"[UI] Resources에서 '{CANVAS_RESOURCE}' 프리팹을 찾지 못해 UI 레이어를 만들지 못했습니다.");
            return;
        }

        SO_CoreConfig config = SO_CoreConfig.Instance;

        _worldLayer      = new UIManager_WorldLayer(CreateCanvas(canvasPrefab, uiRoot.transform, "World", config.WorldOrder));
        _hudLayer        = new UIManager_SingleLayer<UI_Hud>(CreateCanvas(canvasPrefab, uiRoot.transform, "Hud", config.HudOrder).transform, config.HudOrder);
        _popupLayer      = new UIManager_PopupLayer(CreateCanvas(canvasPrefab, uiRoot.transform, "Popup", config.PopupOrder).transform, config.PopupOrder);
        _transitionLayer = new UIManager_SingleLayer<UI_Transition>(CreateCanvas(canvasPrefab, uiRoot.transform, "Transition", config.TransitionOrder).transform, config.TransitionOrder);
    }

    private Canvas CreateCanvas(Canvas prefab, Transform parent, string name, int order)
    {
        Canvas canvas = Object.Instantiate(prefab, parent);
        canvas.name = name;
        canvas.sortingOrder = order;
        return canvas;
    }

    /// <summary>UI 프리팹이 자체 Canvas를 가졌다면 레이어 순서로 정렬되는 중첩 캔버스로 맞춘다.</summary>
    internal static void SetupLayerCanvas(UI ui, int order)
    {
        if (!ui.TryGetComponent(out Canvas canvas)) return;

        canvas.overrideSorting = true;
        canvas.sortingOrder    = order;
        ui.SetupAsNestedCanvas();
    }

    #region Show

    public UniTask<T> ShowWorld<T>(string key = null, AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default)
        where T : UI_World
        => Ready(_worldLayer) ? _worldLayer.Show<T>(key, life, ct) : UniTask.FromResult<T>(null);

    public UniTask<T> ShowHud<T>(string key = null, AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default)
        where T : UI_Hud
        => Ready(_hudLayer) ? _hudLayer.Show<T>(key, life, ct) : UniTask.FromResult<T>(null);

    public UniTask<T> ShowPopup<T>(
        string key = null,
        AssetLifetime life = AssetLifetime.Scene,
        bool clickGuard = true,
        float clickGuardAlpha = -1f,
        bool clickToClose = true,
        CancellationToken ct = default) where T : UI_Popup
        => Ready(_popupLayer)
            ? _popupLayer.Show<T>(key, life, clickGuard, clickGuardAlpha, clickToClose, ct)
            : UniTask.FromResult<T>(null);

    /// <summary>전환 커튼·로딩 화면. 기본 수명이 Persistent라 씬이 바뀌어도 남는다 — 다 쓰면 CloseTransition으로 닫는다.</summary>
    public UniTask<T> ShowTransition<T>(string key = null, AssetLifetime life = AssetLifetime.Persistent, CancellationToken ct = default)
        where T : UI_Transition
        => Ready(_transitionLayer) ? _transitionLayer.Show<T>(key, life, ct) : UniTask.FromResult<T>(null);

    // 캔버스 프리팹이 없어 레이어를 만들지 못했으면 초기화 때 이미 에러를 남겼다. 여기서는 조용히 null을 돌려준다.
    private static bool Ready(object layer) => layer != null;

    #endregion

    #region Close

    /// <summary>닫히는 중이 아닌 팝업이 열려 있는지.</summary>
    public bool HasOpenPopup => _popupLayer != null && _popupLayer.HasOpen;

    /// <summary>뒤로가기를 팝업에서 처리한다. 열린 팝업이 있어 소비했으면 true. (닫지 않는 팝업도 소비한다)</summary>
    public bool HandleBack() => _popupLayer != null && _popupLayer.HandleBack();

    public void CloseWorld(UI_World panel)               => _worldLayer?.Close(panel);
    public void CloseAllWorld()                          => _worldLayer?.CloseAll();
    public void CloseHud()                               => _hudLayer?.Close();
    public void ClosePopup(UI_Popup popup)               => _popupLayer?.RequestClose(popup);
    public void CloseTopPopup(bool withAnimation = true) => _popupLayer?.CloseTop(withAnimation);
    public void CloseAllPopups(bool instant = false)     => _popupLayer?.ClearAll(instant);
    public void CloseTransition()                        => _transitionLayer?.Close();

    #endregion

    /// <summary>
    /// 씬 전환 정리. Scene 수명 UI를 연출 없이 즉시 닫고 반납한다(Persistent는 남는다).
    /// UI는 Pool에 의존하므로 Pool·씬 에셋이 정리되기 전에 불려, 인스턴스를 정상적으로 반납할 수 있다.
    /// 이것이 없으면 레이어가 파괴된 인스턴스를 붙든 채 남아, 클릭 가드가 새 씬을 덮거나 뒤로가기가 예외를 낸다.
    /// </summary>
    public override void Clear()
    {
        _popupLayer?.ClearScene();
        _hudLayer?.ClearScene();
        _worldLayer?.ClearScene();
        _transitionLayer?.ClearScene();
    }
}
