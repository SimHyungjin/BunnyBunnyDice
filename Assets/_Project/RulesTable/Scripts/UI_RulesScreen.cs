using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 규칙 책상 윗면에 박힌 네온 화면(월드 공간 캔버스, 2026-11-13 정식 UI): 위에 탭(합성 · 공격 · 칩), 아래에 장.
/// 장은 탭을 누르거나 저절로 넘어갈 때 옆으로 밀려 바뀐다 — 그냥 바뀌면 언제 넘어갔는지, 몇 번째 장인지 알기 어렵다(사용자).
/// 탭은 그대로 있고 장만 밀린다(마스크 안). 쉬는 자리는 앵커(offset 0)이고 미는 동안만 anchoredPosition을 쓴다.
///
/// 누르기는 이 캔버스가 직접 받지 않는다 — 화면 전체를 덮은 HUD 터치 영역(UI_TouchArea)이 먼저 받으므로,
/// RulesScreenTapInput이 그 누름을 받아 TryPickTab으로 묻는다.
/// 색 배수(밝기)는 그림·글 머티리얼에 곱한다 — 1을 넘겨야 블룸을 받아 네온처럼 빛난다(정점 색은 1에서 잘린다).
/// </summary>
public sealed class UI_RulesScreen : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Serializable]
    private struct Tab
    {
        public RectTransform Root;
        public Image Fill;
        public TMP_Text Label;
    }

    [SerializeField] private Canvas _canvas;
    [Tooltip("장이 밀릴 때 잘라 보이는 영역(RectMask2D).")]
    [SerializeField] private RectTransform _viewport;
    [Tooltip("탭과 같은 순서의 장.")]
    [SerializeField] private RectTransform[] _pages;
    [SerializeField] private Tab[] _tabs;
    [SerializeField] private UI_RulesMergePage _merge;
    [SerializeField] private UI_RulesAttackPage _attack;
    [SerializeField] private UI_RulesChipPage _chip;

    private SO_RulesTableConfig _config;
    private SO_DiceConfig _dice;
    private SO_DiceStatsConfig _stats;
    private int _current;
    // 미는 중인 장: 나가는 장(_from) → 들어오는 장(_current). -1이면 멈춰 있다.
    private int _from = -1;
    private float _direction;
    private float _slide;

    private Material _imageMaterial;
    private readonly Dictionary<Material, Material> _textMaterials = new Dictionary<Material, Material>();
    private readonly List<UI_Utility_FlexLayout> _layouts = new List<UI_Utility_FlexLayout>();

    public int PageCount => _pages.Length;
    public int Current => _current;

    /// <summary>캔버스 기준 가로(UI 단위). 책상에 깔 때 이 가로를 상판 폭에 맞춰 줄인다.</summary>
    public float ReferenceWidth => ((RectTransform)transform).sizeDelta.x;

    public void Init(SO_RulesTableConfig config, SO_DiceConfig dice, SO_DiceStatsConfig stats, Camera camera)
    {
        _config = config;
        _dice = dice;
        _stats = stats;
        _canvas.worldCamera = camera;
        Fill();
        Select(_current, false, 0);
    }

    private void OnEnable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged += Fill;
    }

    private void OnDisable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged -= Fill;
    }

    private void OnDestroy()
    {
        if (_imageMaterial != null) Destroy(_imageMaterial);
        foreach (Material material in _textMaterials.Values) Destroy(material);
        _textMaterials.Clear();
    }

    /// <summary>장 내용을 SO에서 다시 채운다(언어·주사위 설정이 바뀌었을 때도).</summary>
    public void Fill()
    {
        if (_config == null) return;
        _merge.Fill(_config, _dice, _stats);
        _attack.Fill(_config, _dice);
        _chip.Fill(_config);
        ApplyBrightness();
        ShowTabs();
        for (int i = 0; i < _pages.Length; i++)
        {
            if (_pages[i].gameObject.activeSelf) Relayout(_pages[i]);
        }
    }

    /// <param name="direction">+1 = 새 장이 오른쪽에서 들어온다, -1 = 왼쪽에서. 0이면 번호로 정한다.</param>
    public void Select(int index, bool animate, float direction)
    {
        if (_pages.Length == 0) return;
        index = Mathf.Clamp(index, 0, _pages.Length - 1);
        if (_from >= 0) FinishSlide();
        // 바로 바꾸기는 같은 장이어도 나머지 장을 끈다 — 프리팹에서는 장이 모두 켜져 있어 처음 깔 때 겹쳐 보인다.
        if (!animate)
        {
            _current = index;
            ShowTabs();
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].gameObject.SetActive(i == index);
                _pages[i].anchoredPosition = Vector2.zero;
            }
            Relayout(_pages[index]);
            return;
        }
        if (index == _current)
        {
            ShowTabs();
            return;
        }

        int previous = _current;
        _current = index;
        ShowTabs();

        _from = previous;
        _direction = direction != 0f ? Mathf.Sign(direction) : (index > previous ? 1f : -1f);
        _slide = 0f;
        _pages[index].gameObject.SetActive(true);
        Relayout(_pages[index]);
        Place(0f);
    }

    /// <summary>미는 연출을 진행한다(실제 시간).</summary>
    public void Tick(float deltaTime)
    {
        if (_from < 0 || _config == null) return;
        _slide += deltaTime / Mathf.Max(_config.SlideDuration, 0.01f);
        if (_slide >= 1f)
        {
            FinishSlide();
            return;
        }
        // 빨리 출발해 천천히 멈춘다(ease-out 세제곱) — 눌렀을 때 바로 반응하는 느낌.
        float t = 1f - _slide;
        Place(1f - t * t * t);
    }

    // 레이아웃 안의 레이아웃(세로 목록 안의 가로 줄)은 한 번 계산으로는 맞지 않는다 — 가로를 먼저 정할 때 줄 높이가 아직 0이라
    // 줄 안의 Aspect 칸(주사위) 폭이 0이 된다. 두 번째 계산에서는 높이가 정해져 있어 맞는다. 장을 켜거나 다시 채울 때마다 한다.
    // 장 자체에는 레이아웃이 없어 장을 넘기면 아무것도 계산하지 않으므로(그 아래를 건너뛴다) 장 안의 레이아웃을 하나씩 넘긴다 — 위에서부터.
    private void Relayout(RectTransform page)
    {
        page.GetComponentsInChildren(false, _layouts);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < _layouts.Count; i++) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_layouts[i].transform);
        }
        _layouts.Clear();
    }

    private void Place(float progress)
    {
        float width = _viewport.rect.width;
        _pages[_from].anchoredPosition = new Vector2(-_direction * progress * width, 0f);
        _pages[_current].anchoredPosition = new Vector2(_direction * (1f - progress) * width, 0f);
    }

    private void FinishSlide()
    {
        _pages[_from].gameObject.SetActive(false);
        _pages[_from].anchoredPosition = Vector2.zero;
        _pages[_current].anchoredPosition = Vector2.zero;
        _from = -1;
    }

    /// <summary>화면 좌표가 누른 탭. 카메라는 이 화면을 그리는 카메라.</summary>
    public bool TryPickTab(Vector2 screenPosition, Camera camera, out int index)
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(_tabs[i].Root, screenPosition, camera)) continue;
            index = i;
            return true;
        }
        index = -1;
        return false;
    }

    private void ShowTabs()
    {
        if (_config == null) return;
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool active = i == _current;
            _tabs[i].Fill.color = active ? _config.TabActiveColor : _config.TabIdleColor;
            _tabs[i].Label.color = active ? _config.TabActiveTextColor : _config.TabIdleTextColor;
        }
    }

    // 새로 만든 칸까지 모든 그림·글에 밝기 배수 머티리얼을 건다. 글은 폰트 머티리얼마다 복사본 하나를 같이 쓴다.
    private void ApplyBrightness()
    {
        float brightness = _config.ScreenBrightness;
        if (_imageMaterial == null) _imageMaterial = new Material(Graphic.defaultGraphicMaterial) { name = "RulesScreenImage" };
        // 알파는 곱하지 않는다 — 옅게 깐 칸 바탕이 함께 진해진다.
        _imageMaterial.SetColor(ColorId, new Color(brightness, brightness, brightness, 1f));

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++) images[i].material = _imageMaterial;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Material source = texts[i].fontSharedMaterial;
            if (source == null || _textMaterials.ContainsValue(source)) continue;
            if (!_textMaterials.TryGetValue(source, out Material bright))
            {
                bright = new Material(source) { name = source.name + " (RulesScreen)" };
                _textMaterials.Add(source, bright);
            }
            Color face = source.GetColor(ShaderUtilities.ID_FaceColor);
            bright.SetColor(ShaderUtilities.ID_FaceColor, new Color(face.r * brightness, face.g * brightness, face.b * brightness, face.a));
            texts[i].fontSharedMaterial = bright;
        }
    }
}
