using UnityEngine;

/// <summary>
/// 세이프 에어리어에 맞춰 패널 앵커를 조정하고, 잘려나간 가장자리를 filler로 채운다.
/// UI 루트 패널에 부착한다. 전 플랫폼에서 동작한다(모바일 노치, PC는 전체 화면).
/// </summary>
public class SafeArea : MonoBehaviour
{
    [Header("Safe Area Panel")]
    [SerializeField] private RectTransform _safeAreaPanel;

    [Header("Filler RectTransforms")]
    [SerializeField] private RectTransform _leftFiller;
    [SerializeField] private RectTransform _rightFiller;
    [SerializeField] private RectTransform _topFiller;
    [SerializeField] private RectTransform _bottomFiller;

    private Rect _lastSafeArea;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private bool _hasScreenState;

    private void Awake()
    {
        if (_safeAreaPanel == null) _safeAreaPanel = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (Core.Safe != null)
        {
            Core.Safe.OnSafeAreaChanged += Apply;
            RequestSafeAreaUpdate(true);
        }
    }

    private void OnDisable()
    {
        if (Core.Safe != null)
            Core.Safe.OnSafeAreaChanged -= Apply;
    }

    private void OnRectTransformDimensionsChange()
    {
        RequestSafeAreaUpdate(false);
    }

    private void RequestSafeAreaUpdate(bool force)
    {
        Rect safeArea = Screen.safeArea;
        int width = Screen.width;
        int height = Screen.height;

        if (!force
            && _hasScreenState
            && safeArea == _lastSafeArea
            && width == _lastScreenWidth
            && height == _lastScreenHeight)
            return;

        _lastSafeArea = safeArea;
        _lastScreenWidth = width;
        _lastScreenHeight = height;
        _hasScreenState = true;

        if (Core.Safe != null)
            Core.Safe.UpdateSafeArea();
    }

    private void Apply(SafeAreaManager.SafeAreaData data)
    {
        if (_safeAreaPanel == null) return;
        _safeAreaPanel.anchorMin = data.AnchorMin;
        _safeAreaPanel.anchorMax = data.AnchorMax;
        _safeAreaPanel.offsetMin = Vector2.zero;
        _safeAreaPanel.offsetMax = Vector2.zero;

        UpdateFiller(_leftFiller,   0f,          0f,        data.Left,  1f);
        UpdateFiller(_rightFiller,  data.Right,  0f,        1f,         1f);
        UpdateFiller(_topFiller,    0f,          data.Top,  1f,         1f);
        UpdateFiller(_bottomFiller, 0f,          0f,        1f,         data.Bottom);
    }

    private void UpdateFiller(RectTransform filler, float minX, float minY, float maxX, float maxY)
    {
        if (filler == null) return;
        filler.anchorMin = new Vector2(minX, minY);
        filler.anchorMax = new Vector2(maxX, maxY);
        filler.offsetMin = Vector2.zero;
        filler.offsetMax = Vector2.zero;
    }
}
