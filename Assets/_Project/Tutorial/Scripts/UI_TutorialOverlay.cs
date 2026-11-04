using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 팝업(표시만): 어두운 덮개 + 구멍 둘 + 설명 상자. 무엇을 언제 띄울지는 TutorialFlow가 정하고,
/// 여기는 받은 화면 영역을 비추고 글을 보일 뿐이다.
///
/// 설명 상자는 구멍과 겹치지 않게 구멍 반대쪽(위·아래) 자리에 붙는다 — 두 자리는 프리팹의 빈 영역(_topSlot·_bottomSlot)이고
/// 상자는 그 앵커를 그대로 옮겨 받는다(픽셀 계산 없이).
/// 구멍은 대상이 움직이면(손패 부채꼴·영웅) 따라가고, 장이 바뀌면 새 자리로 미끄러진다. 시간은 실제 시간(판이 멈춰 있다).
/// </summary>
public sealed class UI_TutorialOverlay : UI_Popup
{
    [Tooltip("팝업 전체 투명도(나타나기·사라지기, 카드를 끄는 동안 걷기).")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private UI_TutorialDim _dim;
    [Header("설명 상자")]
    [SerializeField] private RectTransform _box;
    [Tooltip("구멍이 아래쪽에 있을 때 상자가 붙는 자리(빈 영역 — 앵커만 쓴다).")]
    [SerializeField] private RectTransform _topSlot;
    [Tooltip("구멍이 위쪽에 있을 때 상자가 붙는 자리.")]
    [SerializeField] private RectTransform _bottomSlot;
    [SerializeField] private TMP_Text _text;
    [Tooltip("\"눌러서 계속\" — 행동을 해야 넘어가는 장에서는 숨긴다.")]
    [SerializeField] private TMP_Text _hint;
    [Tooltip("장 번호(2/6). 한 장짜리 팁에서는 숨긴다.")]
    [SerializeField] private TMP_Text _page;
    private SO_TutorialConfig _config;
    private string _source;
    private object[] _args;
    private bool _faded;
    private float _openedAt;
    // 구멍의 지금 자리(덮개 로컬 좌표)와 목표. 대상이 없으면 HasValue = false.
    private UnityEngine.Rect? _holeA;
    private UnityEngine.Rect? _holeB;
    private UnityEngine.Rect? _targetA;
    private UnityEngine.Rect? _targetB;
    private bool _snap;
    // 상자가 아래 자리에 있는지(구멍이 위쪽). null = 이 장에서 아직 정하지 않음.
    private bool? _boxLow;

    /// <summary>덮개를 눌렀다(구멍 밖, 또는 통과시키지 않는 장이면 어디든).</summary>
    public event Action Tapped;

    protected override void Awake()
    {
        base.Awake();
        _dim.Clicked += () => Tapped?.Invoke();
    }

    public void Init(SO_TutorialConfig config)
    {
        _config = config;
        _dim.color = config.DimColor;
        _group.alpha = 0f;
        _faded = false;
        _snap = true;
        _holeA = _holeB = _targetA = _targetB = null;
    }

    /// <summary>새 장을 보인다. source = 원문(번역 키), args = {0} 자리 값. tap = 눌러서 넘기는 장.</summary>
    public void SetPage(string source, object[] args, int index, int count, bool tap)
    {
        _source = source;
        _args = args;
        RefreshText();
        _hint.gameObject.SetActive(tap);
        _page.gameObject.SetActive(count > 1);
        _page.text = $"{index + 1}/{count}";
        _dim.PassThrough = !tap;
        // 새 장은 자리를 새로 정한다(같은 장 안에서만 흔들림을 막는다).
        _boxLow = null;
    }

    /// <summary>
    /// 비출 곳(화면 픽셀 영역, 없으면 null)을 정한다. 매 프레임 불러도 된다 — 구멍은 목표를 부드럽게 따라간다.
    /// </summary>
    /// <param name="input">비추지 않고 누름만 통과시키는 곳(화면 픽셀, 없으면 null).</param>
    public void SetTargets(UnityEngine.Rect? a, UnityEngine.Rect? b, UnityEngine.Rect? input)
    {
        _dim.SetInputArea(input.HasValue ? ToLocal(input.Value) : (UnityEngine.Rect?)null);
        _targetA = a.HasValue ? ToLocal(Pad(a.Value)) : (UnityEngine.Rect?)null;
        _targetB = b.HasValue ? ToLocal(Pad(b.Value)) : (UnityEngine.Rect?)null;
        PlaceBox(a, b);
    }

    /// <summary>카드를 끄는 동안은 걷어 둔다 — 끌던 카드가 덮개 밑에서 어둡게 보이지 않게.</summary>
    public void SetFaded(bool faded) => _faded = faded;

    /// <summary>누름을 받을 수 있는지(장이 바뀐 직후 잠깐 막는다).</summary>
    public bool AcceptsTap => Time.unscaledTime - _openedAt >= (_config != null ? _config.InputLockDuration : 0f);

    /// <summary>장이 바뀌었다 — 누름 잠금을 다시 건다.</summary>
    public void LockInput() => _openedAt = Time.unscaledTime;

    private void RefreshText() => _text.text = _args != null && _args.Length > 0 ? App.Localize(_source, _args) : App.Localize(_source);

    private void RefreshHint() => _hint.text = App.Localize("눌러서 계속");

    private void OnEnable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged += OnLanguageChanged;
        RefreshHint();
    }

    private void OnDisable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (_source != null) RefreshText();
        RefreshHint();
    }

    private void Update()
    {
        if (_config == null) return;
        float dt = Time.unscaledDeltaTime;

        float alpha = _faded ? 0f : 1f;
        _group.alpha = _config.FadeDuration > 0f ? Mathf.MoveTowards(_group.alpha, alpha, dt / _config.FadeDuration) : alpha;

        float follow = _snap ? 1f : 1f - Mathf.Exp(-_config.HoleFollowSharpness * dt);
        _holeA = Follow(_holeA, _targetA, follow);
        _holeB = Follow(_holeB, _targetB, follow);
        _snap = false;
        _dim.SetHoles(_holeA, _holeB, _config.HoleCorner, ShortSide() * _config.HoleSoftness);
    }

    private static UnityEngine.Rect? Follow(UnityEngine.Rect? current, UnityEngine.Rect? target, float t)
    {
        if (!target.HasValue) return null;
        if (!current.HasValue) return target;
        UnityEngine.Rect c = current.Value;
        UnityEngine.Rect g = target.Value;
        return UnityEngine.Rect.MinMaxRect(Mathf.Lerp(c.xMin, g.xMin, t), Mathf.Lerp(c.yMin, g.yMin, t), Mathf.Lerp(c.xMax, g.xMax, t), Mathf.Lerp(c.yMax, g.yMax, t));
    }

    // 구멍 반대쪽에 상자를 붙인다. 구멍 가운데가 화면 위쪽이면 아래 자리, 아니면 위 자리.
    // 화면 픽셀로 잰다 — 덮개 로컬 좌표는 팝업이 뜨는 연출(크기 변화) 동안 흔들려 상자가 위아래로 튀었다.
    // 한 번 정한 뒤에는 구멍 가운데가 화면 가운데를 여유(BoxSwitchMargin)만큼 넘어야 옮긴다 — 가운데쯤에서 움직이는 대상(영웅)에 왔다 갔다 하지 않게.
    private void PlaceBox(UnityEngine.Rect? a, UnityEngine.Rect? b)
    {
        float sum = 0f;
        int count = 0;
        if (a.HasValue) { sum += a.Value.center.y; count++; }
        if (b.HasValue) { sum += b.Value.center.y; count++; }
        float height = Mathf.Max(Screen.height, 1);
        float center = count > 0 ? sum / count / height : 0f;
        float margin = _config != null ? _config.BoxSwitchMargin : 0f;
        bool low;
        if (!_boxLow.HasValue) low = center > 0.5f;
        else if (_boxLow.Value) low = center > 0.5f - margin;
        else low = center > 0.5f + margin;
        _boxLow = low;
        RectTransform slot = low ? _bottomSlot : _topSlot;
        if (_box.anchorMin == slot.anchorMin && _box.anchorMax == slot.anchorMax) return;
        _box.anchorMin = slot.anchorMin;
        _box.anchorMax = slot.anchorMax;
        _box.offsetMin = Vector2.zero;
        _box.offsetMax = Vector2.zero;
    }

    private UnityEngine.Rect Pad(UnityEngine.Rect screen)
    {
        float shortSide = Mathf.Min(Screen.width, Screen.height);
        float pad = shortSide * _config.HolePadding;
        // 작은 대상(멀리 보이는 영웅)도 구멍이 눈에 띄게 — 가운데를 두고 최소 크기까지 넓힌다.
        float min = shortSide * _config.MinHoleSize;
        float halfW = Mathf.Max(screen.width * 0.5f + pad, min * 0.5f);
        float halfH = Mathf.Max(screen.height * 0.5f + pad, min * 0.5f);
        Vector2 c = screen.center;
        return UnityEngine.Rect.MinMaxRect(c.x - halfW, c.y - halfH, c.x + halfW, c.y + halfH);
    }

    private UnityEngine.Rect ToLocal(UnityEngine.Rect screen)
    {
        Vector2 min = ScreenToLocal(screen.min);
        Vector2 max = ScreenToLocal(screen.max);
        return UnityEngine.Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    // 화면 픽셀 → 덮개 로컬(왼쪽 아래 원점, 캔버스 단위).
    private Vector2 ScreenToLocal(Vector2 screen)
    {
        RectTransform rect = (RectTransform)_dim.transform;
        Canvas canvas = _dim.canvas != null ? _dim.canvas.rootCanvas : null;
        Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, cam, out Vector2 local);
        return local - rect.rect.min;
    }

    private float ShortSide()
    {
        UnityEngine.Rect r = ((RectTransform)_dim.transform).rect;
        return Mathf.Min(r.width, r.height);
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        Tapped = null;
        _config = null;
        _source = null;
        _args = null;
    }
}
