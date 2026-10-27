using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum UIAnimationType
{
    None,
    Scale,
    Fade,
    SlideFromTop,
    SlideFromBottom,
    SlideFromLeft,
    SlideFromRight
}

public static class UI_Animation
{
    /// <summary>등장/퇴장 애니메이션 길이. 값은 SO_CoreConfig에서 조정한다.</summary>
    private static float AnimDuration => SO_CoreConfig.Instance.UIAnimationDuration;

    private static readonly Dictionary<Transform, CoreTween> _activeTweens = new();

    private static Vector2 GetCanvasSize(Transform tr)
    {
        var canvas = tr.GetComponentInParent<Canvas>(true);
        if (canvas == null) return new Vector2(Screen.width, Screen.height);
        var rt = canvas.rootCanvas.GetComponent<RectTransform>();
        return rt != null ? rt.rect.size : new Vector2(Screen.width, Screen.height);
    }

    /// <summary>
    /// 등장 직전에 이전 연출이 남긴 흔적을 지운다.
    ///
    /// UI 인스턴스는 <see cref="PoolManager"/>가 재사용하므로, 닫을 때의 끝 상태가 그대로 남아 돌아온다.
    /// Fade로 닫으면 알파 0, Slide로 닫으면 화면 밖 위치, Scale로 닫으면 스케일 0인 채다.
    /// 각 연출은 자기가 쓰는 속성의 시작값만 세팅하므로, 지우지 않으면
    /// "Fade로 닫았다가 Scale로 열면 알파 0이라 영영 보이지 않는" 상태가 된다.
    ///
    /// 0·1이 아니라 <see cref="UI.RestAnchoredPosition"/>·<see cref="UI.RestScale"/>로 되돌린다.
    /// 프리팹에 저작된 위치(예: 화면 위쪽에 놓인 배너)를 지우지 않기 위해서다.
    /// </summary>
    private static void ResetVisualState(UI ui)
    {
        ui.Initialize();
        Transform tr = ui.transform;
        tr.localScale = ui.RestScale;
        if (ui.Rect != null) ui.Rect.anchoredPosition = ui.RestAnchoredPosition;

        if (tr.TryGetComponent(out CanvasGroup group)) group.alpha = 1f;
    }

    /// <summary>등장 연출. 끝까지 재생됐거나 연출이 없으면 true, 중간에 끊겼으면 false.</summary>
    public static async UniTask<bool> PlayIn(this UI ui, UIAnimationType type)
    {
        if (ui == null) return false;
        Transform tr = ui.transform;

        StopTween(tr);
        ResetVisualState(ui);

        switch (type)
        {
            case UIAnimationType.Scale:
                tr.localScale = Vector3.zero;
                return await PlayTween(tr, CoreTween.Scale(tr, ui.RestScale, AnimDuration, CoreEase.OutBack, useUnscaledTime: true));

            case UIAnimationType.Fade:
                if (!tr.TryGetComponent(out CanvasGroup group)) return true;
                group.alpha = 0f;
                return await PlayTween(tr, CoreTween.Alpha(group, 1f, AnimDuration, useUnscaledTime: true));

            case UIAnimationType.SlideFromTop:
            case UIAnimationType.SlideFromBottom:
            case UIAnimationType.SlideFromLeft:
            case UIAnimationType.SlideFromRight:
                Vector2 sIn = GetCanvasSize(tr);
                return await PlaySlide(tr, ui.RestAnchoredPosition + OffscreenOffset(type, sIn), ui.RestAnchoredPosition, CoreEase.OutQuad);

            default:
                return true;
        }
    }

    /// <summary>퇴장 연출. 끝까지 재생됐거나 연출이 없으면 true, 중간에 끊겼으면 false.</summary>
    public static async UniTask<bool> PlayOut(this UI ui, UIAnimationType type)
    {
        if (ui == null) return false;
        Transform tr = ui.transform;
        StopTween(tr);

        switch (type)
        {
            case UIAnimationType.Scale:
                return await PlayTween(tr, CoreTween.Scale(tr, Vector3.zero, AnimDuration, CoreEase.InBack, useUnscaledTime: true));

            case UIAnimationType.Fade:
                if (!tr.TryGetComponent(out CanvasGroup group)) return true;
                return await PlayTween(tr, CoreTween.Alpha(group, 0f, AnimDuration, useUnscaledTime: true));

            case UIAnimationType.SlideFromTop:
            case UIAnimationType.SlideFromBottom:
            case UIAnimationType.SlideFromLeft:
            case UIAnimationType.SlideFromRight:
                Vector2 sOut = GetCanvasSize(tr);
                return await PlaySlide(tr, ui.RestAnchoredPosition, ui.RestAnchoredPosition + OffscreenOffset(type, sOut), CoreEase.InQuad);

            default:
                return true;
        }
    }

    /// <summary>연출 방향에 대응하는, 제자리에서 화면 밖까지의 거리. 들어올 때는 시작점, 나갈 때는 도착점이 된다.</summary>
    private static Vector2 OffscreenOffset(UIAnimationType type, Vector2 canvasSize)
    {
        switch (type)
        {
            case UIAnimationType.SlideFromTop:    return new Vector2(0f,  canvasSize.y);
            case UIAnimationType.SlideFromBottom: return new Vector2(0f, -canvasSize.y);
            case UIAnimationType.SlideFromLeft:   return new Vector2(-canvasSize.x, 0f);
            case UIAnimationType.SlideFromRight:  return new Vector2( canvasSize.x, 0f);
            default:                              return Vector2.zero;
        }
    }

    /// <summary>
    /// localPosition이 아니라 anchoredPosition으로 움직인다.
    /// 팝업은 <see cref="UI.SetupAsNestedCanvas"/>에서 stretch 앵커로 맞춰지는데,
    /// 그런 RectTransform에 localPosition을 쓰면 레이아웃이 다시 계산될 때 anchoredPosition 기준으로
    /// 위치가 되돌아가 애니메이션이 통째로 무시된다.
    /// </summary>
    private static async UniTask<bool> PlaySlide(Transform tr, Vector2 start, Vector2 end, CoreEase ease)
    {
        if (tr is not RectTransform rect) return true;

        rect.anchoredPosition = start;
        return await PlayTween(tr, CoreTween.AnchoredPosition(rect, end, AnimDuration, ease, useUnscaledTime: true));
    }

    public static void StopAnimation(this UI ui)
    {
        if (ui == null) return;
        StopTween(ui.transform);
    }

    private static async UniTask<bool> PlayTween(Transform tr, CoreTween tween)
    {
        _activeTweens[tr] = tween;
        try { return await tween; }
        finally
        {
            // 중단 뒤 곧바로 다음 연출이 걸렸다면 사전에는 이미 새 트윈이 들어와 있다.
            // 무조건 지우면 방금 시작한 연출의 핸들을 잃어버려, 그다음 StopAnimation이 먹지 않는다.
            if (_activeTweens.TryGetValue(tr, out CoreTween current) && current.Equals(tween))
                _activeTweens.Remove(tr);
        }
    }

    private static void StopTween(Transform tr)
    {
        if (tr == null) return;
        if (!_activeTweens.TryGetValue(tr, out CoreTween tween)) return;

        _activeTweens.Remove(tr);
        if (tween.isAlive) tween.Stop();
    }
}
