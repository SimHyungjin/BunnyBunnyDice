using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 화면(기획서 12.2, 시안 SampleImages/제목 없음6.png). 왼쪽은 비워 두고(3D 영웅이 앉는 자리, LobbyStage) 로고·이름 버튼만,
/// 오른쪽은 특성 · 능력치 6칸 · 리롤 · 최고 기록 패널, 오른쪽 위에 재화·설정, 오른쪽 아래에 게임 시작.
/// 가로·세로 배치는 프리팹의 두 벌 자리(UI_Utility_OrientationAnchors)가 정한다.
///
/// 판 뒤에 뜨는 화면이라 팝업 층에 띄우되 클릭 가드 없이 띄운다(빈 곳 터치는 아래로 새도 HUD가 숨어 있어 받는 것이 없다).
/// 뒤로가기로 닫히지 않는다(닫으면 판이 시작되지 않은 빈 테이블에 갇힌다).
///
/// 재화·최고 기록(지금 캐릭터의 기록)은 세이브(MetaProfile)에서 읽는다.
/// 이름 버튼은 캐릭터 선택 창을 연다. 능력치 칸을 고르고 리롤을 누르면 그 능력치를 다시 굴린다(MetaProfile.Reroll).
/// 구르는 동안 또 누르면 결과를 보이지 않고 이어서 구른다(6이 될 때까지 계속 굴리는 사람을 위해 — 6이 나오면 버튼이 꺼져 저절로 멈춘다).
/// 캐릭터가 열리면(선택 창) 지금 캐릭터인 경우 6칸이 차례로 굴러 첫 능력치를 보인다.
/// 설정 버튼은 설정 창을 연다.
/// </summary>
public sealed class UI_LobbyPopup : UI_Popup
{
    [SerializeField] private TMP_Text _characterName;
    [SerializeField] private Button _characterButton;
    [SerializeField] private TMP_Text _traitName;
    [SerializeField] private TMP_Text _traitDescription;
    [Tooltip("HeroStat 순서(공격력·공격 속도·이동 속도·사거리·치명타 확률·치명타 피해).")]
    [SerializeField] private UI_LobbyStatCell[] _statCells;
    [SerializeField] private Button _startButton;
    [Header("재화 · 설정")]
    [SerializeField] private TMP_Text _currencyText;
    [SerializeField] private Button _settingsButton;
    [Header("리롤 · 기록")]
    [SerializeField] private Button _rerollButton;
    [SerializeField] private TMP_Text _rerollLabel;
    [SerializeField] private TMP_Text _rerollCostText;
    [SerializeField] private TMP_Text _bestRecordText;

    private HeroStats _stats;
    private MetaProfile _meta;
    private SO_MetaDiceConfig _diceConfig;
    private Action _start;
    private Action _selectCharacter;
    private Action _openSettings;
    private HeroStat _selected = HeroStat.Attack;
    private bool _cellsHooked;

    /// <param name="selectCharacter">이름 버튼. 없으면 눌러도 아무 일도 없다.</param>
    /// <param name="openSettings">설정 버튼. 없으면 눌러도 아무 일도 없다.</param>
    public void Show(HeroStats stats, MetaProfile meta, SO_MetaDiceConfig diceConfig, Action start, Action selectCharacter = null,
        Action openSettings = null)
    {
        Unsubscribe();
        _stats = stats;
        _meta = meta;
        _diceConfig = diceConfig;
        _stats.CharacterChanged += Refresh;
        _stats.StagesChanged += Refresh;
        _meta.Changed += Refresh;
        _meta.Unlocked += OnUnlocked;
        _start = start;
        _selectCharacter = selectCharacter;
        _openSettings = openSettings;
        _startButton.onClick.RemoveListener(OnStart);
        _startButton.onClick.AddListener(OnStart);
        _characterButton.onClick.RemoveListener(OnSelectCharacter);
        _characterButton.onClick.AddListener(OnSelectCharacter);
        _rerollButton.onClick.RemoveListener(OnReroll);
        _rerollButton.onClick.AddListener(OnReroll);
        _settingsButton.onClick.RemoveListener(OnSettings);
        _settingsButton.onClick.AddListener(OnSettings);
        HookCells();
        Refresh();
    }

    private void Refresh()
    {
        if (_stats == null) return;
        SO_HeroCharacter character = _stats.Character;
        _characterName.text = character != null ? App.Localize(character.DisplayName) : "";

        SO_HeroTrait trait = character != null ? character.Trait : null;
        _traitName.text = trait != null ? App.Localize(trait.DisplayName) : App.Localize("특성 없음");
        _traitDescription.text = trait != null ? CardText.Fill(trait.Description, trait) : "";

        for (int i = 0; i < _statCells.Length && i < HeroStats.StatCount; i++)
        {
            HeroStat stat = (HeroStat)i;
            _statCells[i].Show(stat, _stats.GetStage(stat), _stats.GetBase(stat));
            _statCells[i].SetSelected(stat == _selected);
        }

        // 고른 능력치가 이미 최대이거나 재화가 모자라면 버튼이 꺼진다(기획서 12.2).
        _rerollLabel.text = App.Localize("{0} 다시 굴리기", HeroStatText.GetName(_selected));
        _rerollCostText.text = _meta.RerollCost.ToString();
        _rerollButton.interactable = _meta.CanReroll(_selected);

        _currencyText.text = _meta.Currency.ToString();
        // 판 기록의 웨이브는 클리어한 웨이브다(RunSummary.Wave).
        _bestRecordText.text = App.Localize("웨이브 {0} 클리어", _meta.GetBestWave(character));
    }

    // 칸은 프리팹에 고정이라 한 번만 잇는다(풀로 다시 쓰여도 같은 칸이다).
    private void HookCells()
    {
        if (_cellsHooked) return;
        _cellsHooked = true;
        for (int i = 0; i < _statCells.Length; i++)
        {
            HeroStat stat = (HeroStat)i;
            _statCells[i].Clicked += () =>
            {
                _selected = stat;
                Refresh();
            };
        }
    }

    private void OnStart()
    {
        // 닫히는 연출 동안 다시 눌러도 한 번만.
        Action start = _start;
        _start = null;
        start?.Invoke();
    }

    private void OnSelectCharacter() => _selectCharacter?.Invoke();

    private void OnSettings() => _openSettings?.Invoke();

    private void OnReroll()
    {
        if (_meta == null) return;
        // 다시 그리기는 MetaProfile.Changed가 부른다(칸은 새 값을 받아 두고 착지 때 그린다).
        int rolled = _meta.Reroll(_selected);
        if (rolled > 0) _statCells[(int)_selected].Roll(_diceConfig, rolled);
    }

    // 지금 캐릭터가 열렸다: 첫 능력치를 하나씩 굴려 보인다. 단계는 이미 정해져 칸에 들어와 있다.
    private void OnUnlocked(SO_HeroCharacter character)
    {
        if (_stats == null || character != _stats.Character) return;
        for (int i = 0; i < _statCells.Length && i < HeroStats.StatCount; i++)
            _statCells[i].Roll(_diceConfig, _stats.GetStage((HeroStat)i), i * _diceConfig.Stagger, reveal: true);
    }

    private void Unsubscribe()
    {
        if (_stats == null) return;
        _stats.CharacterChanged -= Refresh;
        _stats.StagesChanged -= Refresh;
        _meta.Changed -= Refresh;
        _meta.Unlocked -= OnUnlocked;
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
        _stats = null;
        _meta = null;
        _diceConfig = null;
        _start = null;
        _selectCharacter = null;
        _openSettings = null;
        _startButton.onClick.RemoveListener(OnStart);
        _characterButton.onClick.RemoveListener(OnSelectCharacter);
        _rerollButton.onClick.RemoveListener(OnReroll);
        _settingsButton.onClick.RemoveListener(OnSettings);
    }
}
