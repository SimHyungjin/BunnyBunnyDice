using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팝업 스택. 위에 뜬 팝업일수록 sortingOrder가 크고, 클릭 가드 패널은 하나를 두고
/// 가드를 쓰는 가장 위 팝업 바로 아래로 옮겨 가며 쓴다.
/// </summary>
public sealed class UIManager_PopupLayer
{
    private sealed class PopupData
    {
        public UI_Popup      Popup;
        public Canvas        Canvas;
        public AssetLifetime Life;
        public bool          IsClickGuard;
        public float         ClickGuardAlpha;
        public bool          ClickClose;
        public bool          IsClosing;
        public CancellationTokenSource TimeoutCts;
    }

    private static SO_CoreConfig Config => SO_CoreConfig.Instance;

    private readonly int       _startOrder;
    private readonly Transform _root;
    private readonly List<PopupData> _popups = new();

    private Canvas    _panelCanvas;
    private Image     _panelImage;
    private CoreTween _panelTween;

    public UIManager_PopupLayer(Transform root, int order)
    {
        _root       = root;
        _startOrder = order;
        InitBackgroundPanel();
    }

    private void InitBackgroundPanel()
    {
        var go = new GameObject("[GlobalPopupPanel]", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        go.transform.SetParent(_root, false);

        // 새 RectTransform은 100x100에 가운데 앵커로 시작한다. 중첩 캔버스는 renderMode를 무시하고
        // 부모 크기를 자동으로 따라가지도 않으므로, 여기서 직접 화면 전체로 펴 주지 않으면
        // 클릭 가드가 화면 가운데 100x100짜리 작은 사각형으로만 깔린다.
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;

        _panelCanvas = go.GetComponent<Canvas>();
        _panelCanvas.overrideSorting = true;

        var panelGo = new GameObject("Image", typeof(RectTransform));
        panelGo.transform.SetParent(go.transform, false);
        _panelImage = panelGo.AddComponent<Image>();
        _panelImage.color = new Color(0, 0, 0, 0);

        var rt = _panelImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var btn = panelGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnClickPanel);

        go.SetActive(false);
    }

    public async UniTask<T> Show<T>(
        string key,
        AssetLifetime life,
        bool clickGuard,
        float clickGuardAlpha,
        bool clickToClose,
        CancellationToken ct) where T : UI_Popup
    {
        T instance = await Core.Pool.SpawnAsync<T>(key ?? typeof(T).Name, _root, life, ct);
        if (instance == null) return null;

        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            instance.SetupAsNestedCanvas();
        }

        // 등록을 먼저 한다. Open() 안에서 곧바로 닫혀도 레이어가 그 팝업을 알고 있어야 정리된다.
        instance.OnCloseRequest = () => Close(instance);
        _popups.Add(new PopupData
        {
            Popup           = instance,
            Canvas          = canvas,
            Life            = life,
            IsClickGuard    = clickGuard,
            ClickGuardAlpha = clickGuardAlpha < 0 ? Config.PopupGuardAlpha : clickGuardAlpha,
            ClickClose      = clickToClose,
        });
        RefreshState();

        instance.Open();
        return instance;
    }

    /// <summary>닫기 연출을 거쳐 닫는다. 연출이 멈춰도 제한 시간이 지나면 강제로 정리된다.</summary>
    public void RequestClose(UI_Popup popup)
    {
        PopupData data = Find(popup);
        if (data == null || data.IsClosing) return;

        data.IsClosing = true;
        data.TimeoutCts = new CancellationTokenSource();
        StartCloseTimeout(popup, data.TimeoutCts.Token).Forget();
        popup.Close();
        RefreshState();
    }

    /// <summary>연출 없이 즉시 닫고 반납한다.</summary>
    public void Close(UI_Popup popup)
    {
        int idx = IndexOf(popup);
        if (idx < 0) return;

        PopupData data = _popups[idx];
        _popups.RemoveAt(idx);

        data.TimeoutCts?.Cancel();
        data.TimeoutCts?.Dispose();
        data.TimeoutCts = null;

        if (data.Popup != null) Core.Pool.Despawn(data.Popup);

        RefreshState();
    }

    /// <summary>닫히는 중이 아닌 팝업이 하나라도 열려 있는지.</summary>
    public bool HasOpen => TopOpenIndex() >= 0;

    /// <summary>
    /// 뒤로가기를 처리한다. 열린 팝업이 있으면 소비하고 true.
    /// 맨 위 팝업이 <see cref="UI_Popup.CloseOnBack"/>을 끄고 있으면 닫지 않고 소비만 한다(뒤로가기가 그 팝업에 막힌다).
    /// </summary>
    public bool HandleBack()
    {
        int idx = TopOpenIndex();
        if (idx < 0) return false;

        UI_Popup popup = _popups[idx].Popup;
        if (popup.CloseOnBack) RequestClose(popup);
        return true;
    }

    public void CloseTop(bool withAnimation = true)
    {
        int idx = TopOpenIndex();
        if (idx < 0) return;

        if (withAnimation) RequestClose(_popups[idx].Popup);
        else               Close(_popups[idx].Popup);
    }

    public void ClearAll(bool instant = false)
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (i >= _popups.Count) continue;   // 앞선 Close가 목록을 줄였을 수 있다
            PopupData data = _popups[i];

            if (instant) Close(data.Popup);
            else if (!data.IsClosing) RequestClose(data.Popup);
        }
    }

    /// <summary>
    /// 씬 전환 정리. Scene 수명 팝업을 연출 없이 즉시 닫는다. 남는 가드 팝업이 없으면 가드 패널도 바로 끈다
    /// — 페이드를 기다리면 새 씬이 잠깐 어둡게 덮인다.
    /// </summary>
    public void ClearScene()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (i >= _popups.Count) continue;
            PopupData data = _popups[i];
            if (data.Popup == null || data.Life == AssetLifetime.Scene) Close(data.Popup);
        }

        _popups.RemoveAll(p => p.Popup == null);

        if (TopGuardIndex() < 0)
        {
            StopPanelTween();
            _panelImage.color = new Color(0, 0, 0, 0);
            _panelCanvas.gameObject.SetActive(false);
        }
    }

    #region State

    private void RefreshState()
    {
        StopPanelTween();
        _popups.RemoveAll(p => p.Popup == null);

        for (int i = 0; i < _popups.Count; i++)
            if (_popups[i].Canvas != null)
                _popups[i].Canvas.sortingOrder = _startOrder + (i * Config.PopupOrderStep);

        int topGuardIdx = TopGuardIndex();
        if (topGuardIdx == -1)
        {
            // 클릭 가드 팝업이 없는 상태. 가드 패널이 이미 꺼져 있으면 페이드아웃할 대상이 없다.
            // 가드 없는 배너 팝업은 열고 닫을 때마다 여기를 지나므로, 이미 꺼진 패널에
            // 알파 0 → 0 트윈을 매번 새로 거는 낭비를 막는다.
            if (!_panelCanvas.gameObject.activeSelf) return;

            _panelTween = CoreTween.Alpha(_panelImage, 0f, Config.PopupGuardFadeDuration, useUnscaledTime: true)
                .OnComplete(() =>
                {
                    if (TopGuardIndex() == -1) _panelCanvas.gameObject.SetActive(false);
                });
        }
        else
        {
            PopupData top = _popups[topGuardIdx];
            _panelCanvas.gameObject.SetActive(true);
            _panelCanvas.sortingOrder = top.Canvas.sortingOrder - 1;
            _panelTween = CoreTween.Alpha(_panelImage, top.ClickGuardAlpha, Config.PopupGuardFadeDuration, useUnscaledTime: true);
        }
    }

    private void StopPanelTween()
    {
        if (_panelTween.isAlive) _panelTween.Stop();
    }

    private void OnClickPanel()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            PopupData data = _popups[i];
            if (data.IsClosing || !data.IsClickGuard || data.Popup == null) continue;

            // 가드를 쓰는 맨 위 팝업만 본다. 그 팝업이 탭으로 닫히지 않으면 아무 일도 없다.
            if (data.ClickClose) RequestClose(data.Popup);
            return;
        }
    }

    private int TopOpenIndex()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
            if (!_popups[i].IsClosing && _popups[i].Popup != null) return i;
        return -1;
    }

    private int TopGuardIndex()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            PopupData data = _popups[i];
            if (!data.IsClosing && data.IsClickGuard && data.Canvas != null && data.Popup != null) return i;
        }
        return -1;
    }

    private PopupData Find(UI_Popup popup)
    {
        int idx = IndexOf(popup);
        return idx >= 0 ? _popups[idx] : null;
    }

    private int IndexOf(UI_Popup popup)
    {
        for (int i = 0; i < _popups.Count; i++)
            if (ReferenceEquals(_popups[i].Popup, popup)) return i;
        return -1;
    }

    #endregion

    private async UniTaskVoid StartCloseTimeout(UI_Popup popup, CancellationToken ct)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Config.PopupCloseTimeout), delayType: DelayType.Realtime, cancellationToken: ct);
            if (IndexOf(popup) >= 0) Close(popup);
        }
        catch (OperationCanceledException) { }
    }
}
