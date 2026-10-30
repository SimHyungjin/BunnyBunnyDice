using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 게임 화면 HUD(프리팹). 아래부터 맵 터치 영역(화면 전체) · 손패 · 상단 바 순으로 쌓아
/// 위에 있는 UI를 누르면 아래 조작으로 새지 않게 한다.
/// 상단 바(기획서 4장 — 목표물 체력·웨이브·레벨과 경험치 바)는 카드와 같은 모양(자홍 테두리·기울어진 띠)이고 터치를 받지 않는다
/// — 맵 위쪽 가장자리를 덮으므로 그 아래 탭 이동이 막히지 않게.
/// 바의 채움은 바 안에서 앵커 비율로 늘이고 줄인다(픽셀 계산 없이 화면 크기와 무관하게).
/// 오른쪽 위 설정 버튼은 로비와 같은 모양이고, 판 중 설정 창(멈춤 + 로비로)을 연다. 그 왼쪽에 테두리 없는 배속 버튼(글자만)이 있다.
/// </summary>
public sealed class UI_GameHud : UI_Hud
{
    [Tooltip("손패 기준선. 화면 아래 가장자리(앵커 x 0~1, y 0)에 두고 피벗은 아래 가운데.")]
    [SerializeField] private RectTransform _handRoot;
    [Tooltip("맵을 직접 누르는 조작(탭 이동)을 받는 화면 전체 영역.")]
    [SerializeField] private UI_TouchArea _touchArea;
    [Tooltip("로비 동안 HUD 전체를 숨기고 터치를 막는다(루트의 CanvasGroup).")]
    [SerializeField] private CanvasGroup _group;
    [Header("상단 바")]
    [Tooltip("목표물 체력 바의 채움. 바 바탕의 자식이고 가로 앵커(0 ~ 비율)로 늘인다.")]
    [SerializeField] private RectTransform _healthFill;
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private TMP_Text _levelText;
    [Tooltip("경험치 바의 채움. 체력 바와 같은 방식.")]
    [SerializeField] private RectTransform _expFill;
    [SerializeField] private TMP_Text _expText;
    [Tooltip("오른쪽 위 설정 버튼.")]
    [SerializeField] private Button _settingsButton;
    [Tooltip("설정 버튼 왼쪽 배속 버튼과 그 글(x1 등).")]
    [SerializeField] private Button _speedButton;
    [SerializeField] private TMP_Text _speedText;
    [Tooltip("배속 버튼 왼쪽의 시작·일시정지 버튼. 첫 웨이브 전엔 ▶(시작), 그 뒤로는 일시정지(II)·재개(▶). PC는 Space도 같다.")]
    [SerializeField] private Button _pauseButton;
    [SerializeField] private TMP_Text _pauseText;
    [Header("보스 체력 바 (상단 바 아래, 보스가 살아 있는 동안)")]
    [SerializeField] private GameObject _bossBar;
    [Tooltip("보스 채움(자홍)과 잭팟 보스 채움(노랑). 둘 중 하나만 켜고 가로 앵커(0 ~ 비율)로 늘인다.")]
    [SerializeField] private RectTransform _bossFill;
    [SerializeField] private RectTransform _jackpotFill;
    [SerializeField] private TMP_Text _bossText;
    // 스킬 발동 컷인(SkillCutIn이 띄운다). HUD 맨 위에 그려진다.
    [SerializeField] private UI_SkillCutIn _cutIn;

    public UI_SkillCutIn CutIn => _cutIn;

    // 언어가 바뀌면 웨이브·보스 글을 다시 쓴다(나머지는 숫자). 다음 갱신까지 기다리지 않게 마지막 값을 들고 있는다.
    private int _wave;
    // 보스 바 글도 언어가 바뀌면 다시 쓴다.
    private long _bossHealth;
    private long _bossMax;
    private bool _bossJackpot;

    public RectTransform HandRoot => _handRoot;

    /// <summary>설정 버튼을 눌렀다.</summary>
    public event Action SettingsClicked;

    /// <summary>배속 버튼을 눌렀다.</summary>
    public event Action SpeedClicked;
    /// <summary>시작·일시정지 버튼(또는 Space)을 눌렀다.</summary>
    public event Action PauseClicked;

    /// <summary>HUD 크기가 바뀌었다(화면 크기·방향). 손패처럼 화면 비율로 크기를 정하는 쪽이 다시 배치한다.</summary>
    public event Action Resized;
    public UI_TouchArea TouchArea => _touchArea;

    /// <summary>
    /// 로비 동안 숨긴다. 끄지(SetActive) 않고 투명하게 하고 터치를 막는 이유: 손패·터치 영역을 쥔 쪽(CardHand·HeroTapInput)이
    /// 로비를 지나도 같은 HUD를 그대로 이어 쓰게(HUD 층은 하나라 로비 화면을 HUD로 띄우면 이 HUD가 반납된다).
    /// </summary>
    public void SetShown(bool shown)
    {
        _group.alpha = shown ? 1f : 0f;
        _group.blocksRaycasts = shown;
        _group.interactable = shown;
    }

    /// <summary>
    /// 설정 버튼을 켜고 끈다. 판 주사위 연출 동안은 끈다 — 연출은 실제 시간으로 흘러 판을 멈춰도 창 뒤에서 계속 움직인다.
    /// 꺼진 모습(어둡게)은 버튼의 UI_Utility_DisabledDim이 맡는다.
    /// </summary>
    public void SetSettingsEnabled(bool enabled) => _settingsButton.interactable = enabled;

    public void ShowStatus(int health, int maxHealth, int wave, int level, int exp, int expToNext)
    {
        SetFill(_healthFill, maxHealth > 0 ? (float)health / maxHealth : 0f);
        _healthText.text = $"{health}/{maxHealth}";
        _wave = wave;
        ShowWave();
        _levelText.text = $"Lv.{level}";
        SetFill(_expFill, expToNext > 0 ? (float)exp / expToNext : 0f);
        _expText.text = $"{exp}/{expToNext}";
    }

    /// <summary>보스 체력 바를 띄운다(기획서 9.4). 잭팟 보스면 노랑 채움과 "잭팟 보스".</summary>
    public void ShowBoss(long health, long maxHealth, bool jackpot)
    {
        _bossBar.SetActive(true);
        _bossFill.gameObject.SetActive(!jackpot);
        _jackpotFill.gameObject.SetActive(jackpot);
        SetFill(jackpot ? _jackpotFill : _bossFill, maxHealth > 0 ? (float)((double)health / maxHealth) : 0f);
        _bossHealth = health;
        _bossMax = maxHealth;
        _bossJackpot = jackpot;
        ShowBossText();
    }

    public void HideBoss() => _bossBar.SetActive(false);

    private void ShowBossText()
    {
        // 세 자리마다 쉼표(언어와 상관없이 같은 모양) — 후반 보스는 수십억이라 끊지 않으면 읽히지 않는다.
        string health = _bossHealth.ToString("N0", CultureInfo.InvariantCulture);
        string max = _bossMax.ToString("N0", CultureInfo.InvariantCulture);
        if (_bossJackpot) _bossText.text = App.Localize("잭팟 보스 {0}/{1}", health, max);
        else _bossText.text = App.Localize("보스 {0}/{1}", health, max);
    }

    // 언어가 바뀌면 숫자가 아닌 글(웨이브·보스)만 다시 쓴다.
    private void OnLanguageChanged()
    {
        ShowWave();
        if (_bossBar.activeSelf) ShowBossText();
    }

    /// <summary>배속 버튼 글. 숫자뿐이라 번역하지 않는다.</summary>
    public void ShowSpeed(float speed) => _speedText.text = $"x{speed:0.#}";

    /// <summary>시작·일시정지 버튼 글: 멈춰 있으면(첫 웨이브 전 포함) ▶, 돌고 있으면 II. 기호뿐이라 번역하지 않는다.</summary>
    public void ShowPlayPause(bool showPlay) => _pauseText.text = showPlay ? "▶" : "II";

    private void ShowWave() => _waveText.text = App.Localize("웨이브 {0}", _wave);

    protected override void Awake()
    {
        base.Awake();
        _settingsButton.onClick.AddListener(() => SettingsClicked?.Invoke());
        _speedButton.onClick.AddListener(() => SpeedClicked?.Invoke());
        _pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
    }

    private void OnEnable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged -= OnLanguageChanged;
    }

    private static void SetFill(RectTransform fill, float ratio)
    {
        Vector2 max = fill.anchorMax;
        max.x = Mathf.Clamp01(ratio);
        fill.anchorMax = max;
    }

    private void OnRectTransformDimensionsChange() => Resized?.Invoke();

    // Space = 시작·일시정지 버튼(PC 단축키). HUD가 보일 때(판 안)만 받는다.
    private void Update()
    {
        // 숨겨진 동안(로비·판 주사위 연출)은 받지 않는다 — 숨김은 투명도라 Update는 계속 돈다.
        if (!_group.interactable) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) PauseClicked?.Invoke();
    }

    // 풀로 돌아가면 구독을 끊는다 — 다음 판의 HUD로 다시 쓰일 때 이전 판의 손패가 불리지 않게.
    protected override void OnDespawned()
    {
        base.OnDespawned();
        Resized = null;
        SettingsClicked = null;
        SpeedClicked = null;
        PauseClicked = null;
    }
}
