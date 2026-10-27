using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창(기획서 12.2 — 로비·HUD 오른쪽 위 설정 버튼). 언어와 음량(전체·배경음·효과음). 선택 창과 같은 모양이고, 바깥·뒤로가기로 닫힌다.
/// 값은 바꾸는 즉시 적용·저장된다(Core SettingsManager) — 확인 버튼이 없다.
/// 판 중에 열면 아래에 "포기하기"가 붙는다. 확인 창은 두지 않는다 — 판 중 설정 창을 여는 일은 거의 포기뿐이라(사용자 결정) 한 번 더 묻는 건 번거롭기만 하다.
///
/// 언어 버튼은 번역표의 언어 수만큼 만든다(표에 언어를 더하면 저절로 늘어난다). 이름은 번역하지 않고 그 언어 자기 말로 적는다
/// — 영어 화면에서도 "한국어"로 보여야 한국어 사용자가 찾는다.
/// 음량은 소리가 아직 없어도 저장되고, 소리가 생기면 그대로 적용된다.
/// </summary>
public sealed class UI_SettingsPopup : UI_Popup
{
    // 언어 코드 → 그 언어로 쓴 이름. 여기 없는 코드는 코드를 대문자로 보인다.
    private static readonly Dictionary<string, string> NativeNames = new Dictionary<string, string>
    {
        { "ko", "한국어" },
        { "en", "English" },
        { "ja", "日本語" },
        { "zh", "中文" },
    };

    [Tooltip("언어 버튼을 놓는 영역(FlexLayout).")]
    [SerializeField] private RectTransform _languageArea;
    [Tooltip("언어 버튼 견본(꺼 둔 채로 둔다).")]
    [SerializeField] private UI_SettingsLanguageButton _languageTemplate;
    [Header("음량 (전체 · 배경음 · 효과음)")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private TMP_Text _masterValue;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private TMP_Text _musicValue;
    [SerializeField] private Slider _soundSlider;
    [SerializeField] private TMP_Text _soundValue;
    [Header("판 중")]
    [Tooltip("판을 포기하고 결과 화면으로(판 중에 열 때만 보인다).")]
    [SerializeField] private Button _giveUpButton;

    private readonly List<UI_SettingsLanguageButton> _languageButtons = new List<UI_SettingsLanguageButton>();
    private bool _hooked;
    private System.Action _giveUp;

    /// <param name="giveUp">판 중이면 포기하기. 없으면(로비) 버튼을 숨긴다.</param>
    public void Show(System.Action giveUp = null)
    {
        Hook();
        _giveUp = giveUp;
        _giveUpButton.gameObject.SetActive(giveUp != null);
        BuildLanguages();
        RefreshLanguages();
        // 값을 넣는 동안 onValueChanged가 저장을 다시 부르지 않게(같은 값이면 SettingsManager가 무시하지만 굳이 부르지 않는다).
        _masterSlider.SetValueWithoutNotify(App.GetVolume(AudioChannelType.Master));
        _musicSlider.SetValueWithoutNotify(App.GetVolume(AudioChannelType.Music));
        _soundSlider.SetValueWithoutNotify(App.GetVolume(AudioChannelType.Sound));
        RefreshVolumes();
    }

    // 슬라이더는 프리팹에 고정이라 한 번만 잇는다(풀로 다시 쓰여도 같은 슬라이더다).
    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        _masterSlider.onValueChanged.AddListener(value => SetVolume(AudioChannelType.Master, value));
        _musicSlider.onValueChanged.AddListener(value => SetVolume(AudioChannelType.Music, value));
        _soundSlider.onValueChanged.AddListener(value => SetVolume(AudioChannelType.Sound, value));
        _giveUpButton.onClick.AddListener(OnGiveUp);
    }

    // 닫히는 연출 동안 다시 눌러도 한 번만.
    private void OnGiveUp()
    {
        System.Action giveUp = _giveUp;
        _giveUp = null;
        giveUp?.Invoke();
    }

    private void SetVolume(AudioChannelType channel, float value)
    {
        App.SetVolume(channel, value);
        RefreshVolumes();
    }

    private void RefreshVolumes()
    {
        _masterValue.text = Percent(_masterSlider.value);
        _musicValue.text = Percent(_musicSlider.value);
        _soundValue.text = Percent(_soundSlider.value);
    }

    // 숫자와 기호뿐이라 번역하지 않는다.
    private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

    // 언어 수만큼 버튼을 맞춘다(풀로 다시 쓰일 때 남은 버튼을 다시 쓴다).
    private void BuildLanguages()
    {
        _languageTemplate.gameObject.SetActive(false);
        IReadOnlyList<string> languages = Core.Localization != null ? Core.Localization.Languages : null;
        int count = languages != null ? languages.Count : 0;
        while (_languageButtons.Count < count)
        {
            UI_SettingsLanguageButton button = Instantiate(_languageTemplate, _languageArea, false);
            int index = _languageButtons.Count;
            button.Clicked += () => OnLanguageClicked(index);
            _languageButtons.Add(button);
        }
        for (int i = 0; i < _languageButtons.Count; i++)
        {
            bool used = i < count;
            _languageButtons[i].gameObject.SetActive(used);
            if (used) _languageButtons[i].SetLabel(NativeName(languages[i]));
        }
    }

    private void RefreshLanguages()
    {
        if (Core.Localization == null) return;
        IReadOnlyList<string> languages = Core.Localization.Languages;
        for (int i = 0; i < _languageButtons.Count && i < languages.Count; i++)
            _languageButtons[i].SetSelected(languages[i] == Core.Localization.Language);
    }

    private void OnLanguageClicked(int index)
    {
        if (Core.Localization == null || index >= Core.Localization.Languages.Count) return;
        // 저장까지 한다(LocalizationManager.SetLanguage는 저장하지 않는다). 바뀌면 OnLanguageChanged로 강조를 다시 칠한다.
        Core.Settings.Language = Core.Localization.Languages[index];
    }

    private static string NativeName(string code) =>
        NativeNames.TryGetValue(code, out string name) ? name : code.ToUpperInvariant();

    private void OnEnable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged += RefreshLanguages;
    }

    private void OnDisable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged -= RefreshLanguages;
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        _giveUp = null;
    }
}
