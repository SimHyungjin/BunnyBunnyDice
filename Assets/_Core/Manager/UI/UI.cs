using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public RectTransform Rect        { get; private set; }
    public bool          IsInitialized { get; private set; }

    /// <summary>
    /// 연출 전의 제자리. 프리팹에 저작된 위치·크기를 처음 초기화할 때 기억해 둔다.
    /// 풀로 재사용되면 직전 연출의 끝 상태(화면 밖·스케일 0)로 돌아오므로, 다음 등장은 이 값에서 다시 시작한다.
    /// </summary>
    public Vector2 RestAnchoredPosition { get; private set; }
    public Vector3 RestScale            { get; private set; } = Vector3.one;

    protected virtual void Awake() { Initialize(); }

    public virtual bool Initialize()
    {
        if (IsInitialized) return false;
        Rect = GetComponent<RectTransform>();
        RestAnchoredPosition = Rect != null ? Rect.anchoredPosition : Vector2.zero;
        RestScale = transform.localScale;
        IsInitialized = true;
        return true;
    }

    internal void SetupAsNestedCanvas()
    {
        Initialize();
        if (TryGetComponent<CanvasScaler>(out var scaler))
            scaler.enabled = false;
        // 루트에 Canvas가 붙은 프리팹은 RectTransform을 Canvas가 구동하므로 피벗·스케일·크기가 0으로 저장된다.
        // 중첩 캔버스가 되면 그 값이 그대로 살아나므로 전부 다시 맞춘다. 피벗을 빼먹으면 (0,0) — 화면 왼쪽 아래 —
        // 를 기준으로 스케일 연출이 커진다.
        transform.localScale  = Vector3.one;
        Rect.anchorMin        = Vector2.zero;
        Rect.anchorMax        = Vector2.one;
        Rect.pivot            = new Vector2(0.5f, 0.5f);
        Rect.sizeDelta        = Vector2.zero;
        Rect.anchoredPosition = Vector2.zero;

        // 화면 전체로 편 것이 이 UI의 제자리다.
        RestAnchoredPosition = Vector2.zero;
        RestScale = Vector3.one;
    }

    #region Rect Helpers

    public UI SetRectAnchor(Vector2 anchorMin, Vector2 anchorMax)
    {
        Initialize();
        Rect.anchorMin = anchorMin;
        Rect.anchorMax = anchorMax;
        return this;
    }

    public UI SetRectPivot(Vector2 pivot)
    {
        Initialize();
        Rect.pivot = pivot;
        return this;
    }

    public UI SetRectAnchoredPosition(Vector2 position)
    {
        Initialize();
        Rect.anchoredPosition = position;
        return this;
    }

    public UI SetSize(Vector2 size)
    {
        Initialize();
        Rect.sizeDelta = size;
        return this;
    }

    public UI SetOffset(Vector2 offsetMin, Vector2 offsetMax)
    {
        Initialize();
        Rect.offsetMin = offsetMin;
        Rect.offsetMax = offsetMax;
        return this;
    }

    #endregion
}
