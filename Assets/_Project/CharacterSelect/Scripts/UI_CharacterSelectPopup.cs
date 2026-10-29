using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 창(기획서 12.3, 시안 SampleImages/제목 없음7.png). 로비의 이름 버튼으로 연다.
/// 열린 캐릭터를 누르면 바로 고르고 닫힌다. 조건을 채운 잠긴 캐릭터를 누르면 실루엣이 걷힌 뒤 그 캐릭터를 고르고 열며 닫힌다
/// — 첫 능력치는 로비 칸이 굴려 보인다(선택 창에는 능력치를 보일 자리가 없다). 바깥·뒤로가기로 닫는다(고르기를 강제하지 않는다).
/// 닫기(X) 버튼은 두지 않는다 — 바깥을 누르면 닫히고, 그 자리를 제목이 창 위로 튀어나가게 쓰는 편이 공간이 덜 빈다.
///
/// 칸은 세로 스크롤 격자다(캐릭터가 늘 것에 대비). 픽셀 크기를 계산하지 않고 비율로 놓는다:
/// 내용 영역은 너비가 높이를 정하는 AspectRatioFitter(비율 = 열 수 × 칸 비율 ÷ 줄 수)이고, 칸은 그 안에서 (열, 줄) 몫의 앵커를 받는다.
/// 열 수는 보이는 영역이 세로로 길면 세로용, 아니면 가로용이다(화면이 바뀌면 다시 나눈다).
/// </summary>
public sealed class UI_CharacterSelectPopup : UI_Popup
{
    [SerializeField] private ScrollRect _scroll;
    [Tooltip("칸을 놓는 내용 영역(ScrollRect.content). 위쪽에 붙고 너비가 높이를 정하는 AspectRatioFitter가 있어야 한다.")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private AspectRatioFitter _contentFitter;
    [Tooltip("칸 견본(꺼 둔 채로 둔다). 캐릭터 수만큼 복제해 내용 영역에 놓는다.")]
    [SerializeField] private UI_CharacterSelectCell _cellTemplate;
    [Min(1)] [SerializeField] private int _landscapeColumns = 3;
    [Min(1)] [SerializeField] private int _portraitColumns = 2;
    [Tooltip("칸 가로 ÷ 세로.")]
    [Min(0.1f)] [SerializeField] private float _cellAspect = 1.45f;

    private readonly List<UI_CharacterSelectCell> _cells = new List<UI_CharacterSelectCell>();
    private IReadOnlyList<SO_HeroCharacter> _characters;
    private HeroStats _stats;
    private MetaProfile _meta;
    private SO_MetaDiceConfig _diceConfig;
    private bool _revealing;
    private int _columns;
    private bool _textDirty;

    public void Show(IReadOnlyList<SO_HeroCharacter> characters, HeroStats stats, MetaProfile meta, SO_MetaDiceConfig diceConfig)
    {
        Unsubscribe();
        _characters = characters;
        _stats = stats;
        _meta = meta;
        _diceConfig = diceConfig;
        _revealing = false;
        _stats.CharacterChanged += Refresh;
        _meta.Changed += Refresh;

        BuildCells();
        _columns = 0;
        Layout();
        Refresh();
        ScrollToTop();
    }

    // 캐릭터 수만큼 칸을 맞춘다(풀로 다시 쓰일 때 남은 칸을 다시 쓴다). 잇는 일은 만들 때 한 번만 — 몇 번째 칸인지로 캐릭터를 찾는다.
    private void BuildCells()
    {
        _cellTemplate.gameObject.SetActive(false);
        while (_cells.Count < _characters.Count)
        {
            UI_CharacterSelectCell cell = Instantiate(_cellTemplate, _content, false);
            int index = _cells.Count;
            cell.Clicked += () => OnCellClicked(index);
            _cells.Add(cell);
        }
        for (int i = 0; i < _cells.Count; i++) _cells[i].gameObject.SetActive(i < _characters.Count && _characters[i] != null);
    }

    // 맨 위에서 멈춘 채로 연다. 내용 영역 높이(AspectRatioFitter)는 레이아웃이 돌아야 정해지므로 먼저 계산시키고 위치를 놓는다
    // — 높이가 정해지기 전에 위치를 놓으면 어긋난 자리에서 탄성 복귀가 칸을 천천히 끌어내려 위에서 내려오는 것처럼 보였다.
    private void ScrollToTop()
    {
        Canvas.ForceUpdateCanvases();
        _content.anchoredPosition = Vector2.zero;
        _scroll.StopMovement();
    }

    // 열 수가 바뀌었을 때만 다시 놓는다(앵커만 바꾼다 — 크기는 레이아웃이 따라온다).
    private void Layout()
    {
        if (_characters == null) return;
        Rect viewport = _scroll.viewport != null ? _scroll.viewport.rect : _content.rect;
        int columns = viewport.height > viewport.width ? _portraitColumns : _landscapeColumns;
        if (columns == _columns) return;
        _columns = columns;

        int count = 0;
        for (int i = 0; i < _characters.Count; i++)
            if (_characters[i] != null) count++;
        int rows = Mathf.Max((count + columns - 1) / columns, 1);
        _contentFitter.aspectRatio = columns * _cellAspect / rows;

        int slot = 0;
        for (int i = 0; i < _characters.Count; i++)
        {
            if (_characters[i] == null) continue;
            int column = slot % columns;
            int row = slot / columns;
            RectTransform rect = (RectTransform)_cells[i].transform;
            rect.anchorMin = new Vector2(column / (float)columns, 1f - (row + 1) / (float)rows);
            rect.anchorMax = new Vector2((column + 1) / (float)columns, 1f - row / (float)rows);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            slot++;
        }
    }

    private void Refresh()
    {
        if (_characters == null) return;
        for (int i = 0; i < _characters.Count; i++)
        {
            SO_HeroCharacter character = _characters[i];
            if (character == null) continue;
            bool unlocked = _meta.IsUnlocked(character);
            bool unlockable = _meta.CanUnlock(character);
            _cells[i].Show(App.Localize(character.DisplayName), character.Portrait, character.PortraitFocusX, character.PortraitFocusY,
                character.PortraitZoom, unlocked, unlockable,
                character == _stats.Character,
                BandText(character, unlocked, unlockable));
        }
        _textDirty = true;
    }

    // 열린 캐릭터는 특성 이름(특성이 없으면 "특성 없음"), 열 수 있는 캐릭터는 "눌러서 열기", 잠긴 캐릭터는 해금 조건과 진행도.
    private string BandText(SO_HeroCharacter character, bool unlocked, bool unlockable)
    {
        if (unlocked) return character.Trait != null ? App.Localize(character.Trait.DisplayName) : App.Localize("특성 없음");
        if (unlockable) return App.Localize("눌러서 열기");
        return _meta.TryGetRule(character, out UnlockRule rule) ? UnlockText.WithProgress(rule, _meta.GetProgress(rule.Kind)) : string.Empty;
    }

    // 칸마다 글 길이가 달라 Auto Size가 칸마다 다른 크기를 고르면 들쭉날쭉해 보인다(시안은 같은 크기).
    // 각 칸이 고른 크기 중 가장 작은 값으로 모두 맞춘다. 영역 크기가 바뀌면(화면 회전) 다시 잰다.
    private void LateUpdate()
    {
        if (!_textDirty || _characters == null) return;
        _textDirty = false;
        Canvas.ForceUpdateCanvases();
        EqualizeSize(true);
        EqualizeSize(false);
    }

    private void EqualizeSize(bool name)
    {
        float size = float.MaxValue;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (!_cells[i].gameObject.activeInHierarchy) continue;
            TMP_Text text = name ? _cells[i].NameText : _cells[i].BandText;
            text.enableAutoSizing = true;
            text.ForceMeshUpdate();
            if (text.text.Length > 0 && text.fontSize < size) size = text.fontSize;
        }
        if (size == float.MaxValue) return;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (!_cells[i].gameObject.activeInHierarchy) continue;
            TMP_Text text = name ? _cells[i].NameText : _cells[i].BandText;
            text.enableAutoSizing = false;
            text.fontSize = size;
        }
    }

    private void OnCellClicked(int index)
    {
        if (_characters == null || index >= _characters.Count || _revealing) return;
        SO_HeroCharacter character = _characters[index];
        if (!_meta.IsUnlocked(character))
        {
            if (!_meta.CanUnlock(character)) return;
            _revealing = true;
            MetaProfile meta = _meta;
            HeroStats stats = _stats;
            _cells[index].Reveal(_diceConfig, () =>
            {
                // 고른 뒤에 연다 — 로비가 "지금 캐릭터가 열렸다"를 듣고 첫 능력치를 굴린다.
                stats.SetCharacter(character);
                meta.Unlock(character);
                if (_revealing && IsOpened) App.ClosePopup(this);
                _revealing = false;
            });
            return;
        }
        _stats.SetCharacter(character);
        App.ClosePopup(this);
    }

    // 화면이 돌거나 접히면 보이는 영역 비율이 바뀌어 열 수가 바뀔 수 있다.
    private void OnRectTransformDimensionsChange()
    {
        Layout();
        _textDirty = true;
    }

    private void Unsubscribe()
    {
        if (_stats != null) _stats.CharacterChanged -= Refresh;
        if (_meta != null) _meta.Changed -= Refresh;
    }

    private void OnEnable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged -= Refresh;
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        Unsubscribe();
        _characters = null;
        _stats = null;
        _meta = null;
        _diceConfig = null;
    }
}
