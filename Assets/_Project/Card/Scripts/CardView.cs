using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 카드 한 장(프리팹). 설명창이 곧 카드다 — 작은 카드를 따로 두지 않고, 손패에서는 줄여 보이고(왼쪽 위 아이콘으로 알아본다)
/// 누르고 있거나 레벨업에서 고를 때는 크게 보인다. 프리팹은 큰 크기(설명창)로 만들고 기준 배율(SetBaseScale)로 줄인다.
/// 입력은 이벤트로 내보내고, 자기는 목표 자세(위치·회전·크기)로 부드럽게 따라가기만 한다.
/// 트윈 대신 매 프레임 목표로 수렴시키는 이유: 손패 정렬이 연달아 바뀌어도(카드 추가·사용·되돌림) 진행 중 트윈을
/// 끊고 새로 거는 처리가 필요 없고, 드래그 중에는 목표만 손가락 위치로 바꾸면 된다.
///
/// 카드 종류(주사위·칩·스킬)는 프리팹을 나누지 않고 한 프리팹 안의 내용 묶음을 바꿔 끼운다 — 테두리·제목·아이콘·손패 모드는
/// 모든 카드가 같아서, 나누면 프레임을 고칠 때마다 종류 수만큼 고쳐야 한다. 묶음은 카드 전체를 덮는 영역이라 안쪽 앵커는 카드 기준 그대로다.
/// </summary>
public sealed class CardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private CanvasGroup _group;
    [Header("카드 종류 색(테두리 등)")]
    [SerializeField] private Graphic[] _kindTinted;
    [SerializeField] private Graphic _titleBack;
    [Header("공통 머리")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _subText;
    [Header("주사위 카드 내용")]
    [SerializeField] private TMP_Text _damageText;
    [SerializeField] private TMP_Text _speedText;
    [SerializeField] private TMP_Text _rangeText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Image _strongTagBack;
    [SerializeField] private TMP_Text _strongTagText;
    [SerializeField] private Image _weakTagBack;
    [SerializeField] private TMP_Text _weakTagText;
    [SerializeField] private TMP_Text _awakenText;
    [Header("칩·스킬·영웅 강화 카드 내용 — 수치 칸 자리까지 설명이 넓고, 각성 칸 자리에 놓는 곳")]
    [SerializeField] private TMP_Text _upgradeDescriptionText;
    [SerializeField] private Image _upgradeStrongTagBack;
    [SerializeField] private TMP_Text _upgradeStrongTagText;
    [SerializeField] private TMP_Text _upgradeKindLabel;
    [SerializeField] private TMP_Text _upgradeKindText;
    [Tooltip("칩·스킬 카드의 넓은 설명 묶음. 영웅 강화 카드는 수치 칸이 들어가므로 끄고 HeroContent의 설명을 쓴다.")]
    [SerializeField] private GameObject _upgradeDesc;
    [Header("영웅 강화 카드 내용 — 주사위 수치 칸 모양의 한 칸(능력치: 지금 → 고른 뒤) + 설명")]
    [SerializeField] private TMP_Text _heroStatLabel;
    [SerializeField] private TMP_Text _heroStatValue;
    [SerializeField] private TMP_Text _heroDescriptionText;
    [Header("겹친 장수(두 모드 공통)")]
    [SerializeField] private TMP_Text _stackText;
    [SerializeField] private GameObject _stackBack;
    [Header("손패(간단) — 큰 아이콘 + 공격 방식 이름")]
    [SerializeField] private Image _compactIcon;
    [SerializeField] private TMP_Text _compactTitle;
    [Header("묶음: 모드(자세히/간단)와 카드 종류별 내용(자세히 안)")]
    [SerializeField] private GameObject _detail;
    [SerializeField] private GameObject _compact;
    [SerializeField] private GameObject _diceContent;
    [SerializeField] private GameObject _upgradeContent;
    [SerializeField] private GameObject _heroContent;

    private RectTransform _rect;
    // 스킬 피해·영웅 강화 수치처럼 영웅 능력치에 따라 바뀌는 글을 채운다. 손패에 든 동안 영웅이 강해질 수 있어 크게 볼 때마다 다시 채운다(SetDetailed).
    private HeroStats _heroStats;
    // 값을 복사해 두지 않고 설정을 들고 있는다 — 실행 중 조정한 빠르기가 바로 반영되게.
    private SO_CardHandConfig _config;
    // 주사위 카드 글을 다시 채울 때(언어 변경) 쓴다.
    private SO_DiceConfig _diceConfig;
    private SO_DiceStatsConfig _statsConfig;
    private Vector2 _targetPosition;
    private float _targetAngle;
    private float _targetScale = 1f;
    private float _handScale = 1f;
    // 프리팹 → 손패 카드 크기 배율. 화면 크기에서 정해지므로(CardSizing) 만드는 쪽이 넣고, 화면이 바뀌면 다시 넣는다.
    private float _baseScale = 1f;

    public event Action<CardView, PointerEventData> PointerDown;
    public event Action<CardView, PointerEventData> Dragged;
    public event Action<CardView, PointerEventData> PointerUp;

    public HandCard Card { get; private set; }
    public int Stack { get; private set; }
    public RectTransform Rect => _rect;

    /// <param name="heroStats">영웅 능력치에 따라 바뀌는 글(스킬 피해, 영웅 강화 "지금 → 고른 뒤")을 채운다. null이면 배율만 보인다.</param>
    public void Init(HandCard card, SO_CardHandConfig config, SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig,
        HeroStats heroStats = null)
    {
        _rect = (RectTransform)transform;
        _config = config;
        _heroStats = heroStats;
        Card = card;

        // 테두리 등은 카드 종류(주사위·칩·스킬·영웅 강화)를 나타낸다. 주사위의 색과 면체는 아이콘이 보인다
        // — 흰 기반 아이콘 3장(면체별)에 색을 곱해 8색 × 3면체를 모두 그린다.
        Color kind = card.IsChip ? config.ChipCardColor : card.IsSkill ? config.SkillCardColor
            : card.IsHero ? config.HeroCardColor : config.DiceCardColor;
        for (int i = 0; i < _kindTinted.Length; i++) _kindTinted[i].color = kind;
        _titleBack.color = new Color(kind.r, kind.g, kind.b, config.TitleBackAlpha);

        _diceConfig = diceConfig;
        _statsConfig = statsConfig;
        ShowContent();
        // 칩·스킬·영웅 강화 카드는 같은 내용 묶음(넓은 설명 + 종류 칸)을 쓴다.
        _diceContent.SetActive(card.IsDice);
        _upgradeContent.SetActive(!card.IsDice);
        // 영웅 강화 카드는 종류 칸은 같이 쓰고, 설명 자리에 수치 칸 + 줄인 설명을 둔다.
        _upgradeDesc.SetActive(!card.IsHero);
        _heroContent.SetActive(card.IsHero);

        _compactIcon.sprite = _icon.sprite;
        _compactIcon.color = _icon.color;
        // 처음엔 손패 모드. 크게 보이는 쪽(누르고 있을 때·레벨업)이 SetDetailed(true)로 바꾼다.
        SetDetailed(false);
        SetStack(1);
        SetVisible(true);
    }

    // 카드 종류별 내용(아이콘·글)을 채운다. 언어가 바뀌면 이것만 다시 불러 글을 바꾼다 — 카드를 새로 만들지 않아
    // 손패 자리·집은 상태가 그대로 남는다.
    private void ShowContent()
    {
        HandCard card = Card;
        if (card.IsChip) ShowCardAsset(card.Chip, _config.ChipSubText, _config.ChipKindLabel, _config.ChipKindText, _config);
        else if (card.IsSkill) ShowSkill(card.Skill, _config);
        else if (card.IsHero) ShowHero(card.Hero, _config);
        else ShowDice(card.Dice, _config, _diceConfig, _statsConfig);
        _compactTitle.text = _titleText.text;
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

    private void OnLanguageChanged()
    {
        // Init 전(풀에서 막 꺼낸 직후)에는 채울 카드가 없다.
        if (_config != null) ShowContent();
    }

    private void ShowDice(DiceCard card, SO_CardHandConfig config, SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig)
    {
        SO_DiceConfig.FaceModel face = diceConfig.GetModel(card.Faces);
        SO_DiceType type = diceConfig.GetDiceType(card.Colors);
        _icon.sprite = face.Icon;
        _icon.color = diceConfig.GetColor(card.Colors);
        _titleText.text = type != null ? App.Localize(type.AttackName) : "?";
        _subText.text = App.Localize("{0} {1}, 눈 1~{2}", type != null ? App.Localize(type.DisplayName) : "?",
            App.Localize(face.DisplayName), card.Faces);
        ShowStats(card, type, statsConfig);
        _descriptionText.text = type != null ? DiceDescription(type) : string.Empty;
        ShowTags(config, _strongTagBack, _strongTagText, type != null ? type.StrongAgainst : null,
            _weakTagBack, _weakTagText, type != null ? type.WeakAgainst : null);
        // 각성 공통 배율(SO_DiceStatsConfig.AwakenDamageScale)은 적지 않는다 — 모든 색에 걸리는 시스템 규칙이라 색의 개성이 아니다(2026-10-29 사용자).
        _awakenText.text = type != null ? CardText.Fill(type.AwakenDescription, type) : string.Empty;
    }

    // 최대 체력 피해는 색마다 값이 달라(색의 개성) 적되, 문장은 여기서 붙인다 — 색 글에 적으면 색을 더하거나 값을 0으로 둘 때 글을 따로 고쳐야 한다.
    private static string DiceDescription(SO_DiceType type)
    {
        string text = CardText.Fill(type.Description, type);
        if (type.MaxHealthDamage <= 0f) return text;
        return $"{text} {App.Localize("최대 체력의 {0} 피해를 더 준다.", CardText.FormatPercent(type.MaxHealthDamage))}";
    }

    // 스킬은 쓰는 곳(주사위·적 전체·판 전체·칸)을 종류 칸에 적는다 — 놓을 곳이 카드마다 달라서.
    private void ShowSkill(SO_SkillCard card, SO_CardHandConfig config)
    {
        switch (card.Target)
        {
            case SkillTarget.Enemies:
                ShowCardAsset(card, config.SkillSubText, config.SkillEnemiesLabel, config.SkillEnemiesText, config);
                break;
            case SkillTarget.Board:
                ShowCardAsset(card, config.SkillSubText, config.SkillBoardLabel, config.SkillBoardText, config);
                break;
            case SkillTarget.Cell:
                ShowCardAsset(card, config.SkillSubText, config.SkillCellLabel, config.SkillCellText, config);
                break;
            default:
                ShowCardAsset(card, config.SkillSubText, config.SkillDiceLabel, config.SkillDiceText, config);
                break;
        }
    }

    // 수치 칸에 "지금 → 고른 뒤"를 보인다 — 같은 카드라도 캐릭터·단계·이미 받은 강화에 따라 오르는 양이 달라서, 눈으로 비교하게.
    private void ShowHero(SO_HeroCard card, SO_CardHandConfig config)
    {
        HeroStats heroStats = _heroStats;
        ShowCardAsset(card, config.HeroSubText, config.HeroKindLabel, config.HeroKindText, config);
        // 오르는 %는 제목에 코드로 붙인다 — 설명에 숫자를 적어 두면 SO 값을 고칠 때 글과 실제가 어긋난다.
        string percent = HeroStatText.FormatAmount(card);
        _titleText.text = percent.Length > 0 ? $"{App.Localize(card.DisplayName)} {percent}" : App.Localize(card.DisplayName);
        if (card is SO_HeroDebuffCard debuff)
        {
            int copies = heroStats != null ? heroStats.CopiesOf(debuff) : 0;
            _heroStatLabel.text = HeroStatText.GetDebuffName(debuff.Debuff);
            _heroStatValue.text = $"{HeroStatText.FormatDebuff(debuff, copies)} → {HeroStatText.FormatDebuff(debuff, copies + 1)}";
            _heroDescriptionText.text = CardText.Fill(card.Description, card, heroStats);
            return;
        }
        _heroStatLabel.text = HeroStatText.GetName(card.Stat);
        _heroStatValue.text = heroStats != null ? FormatHeroChange(card, card.Stat, heroStats) : percent;
        // 치명타 확률이 100%를 넘으면 넘친 몫이 치명타 피해로 간다 — 확률만 보이면 "100% → 100%"라 고를 이유가 안 보인다.
        if (heroStats != null && card.Stat == HeroStat.CritChance
            && heroStats.GetAfter(card, HeroStat.CritDamage) > heroStats.Get(HeroStat.CritDamage))
        {
            _heroStatValue.text += $"  ({HeroStatText.GetName(HeroStat.CritDamage)} {FormatHeroChange(card, HeroStat.CritDamage, heroStats)})";
        }
        _heroDescriptionText.text = CardText.Fill(card.Description, card, heroStats);
    }

    private static string FormatHeroChange(SO_HeroCard card, HeroStat stat, HeroStats heroStats) =>
        $"{HeroStatText.Format(stat, heroStats.Get(stat))} → {HeroStatText.Format(stat, heroStats.GetAfter(card, stat))}";

    // 칩·스킬·영웅 강화 카드는 수치·각성 칸 대신 넓은 설명과 종류 칸(어디에 놓는지)이다 — 놓을 곳이 고르는 기준이라 각성 칸 자리에 크게 보인다.
    private void ShowCardAsset(SO_Card card, string subText, string kindLabel, string kindText, SO_CardHandConfig config)
    {
        _icon.sprite = card.Icon;
        _icon.color = card.IconColor;
        _titleText.text = App.Localize(card.DisplayName);
        _subText.text = App.Localize(subText);
        _upgradeDescriptionText.text = CardText.Fill(card.Description, card, _heroStats);
        ShowTags(config, _upgradeStrongTagBack, _upgradeStrongTagText, card.StrongAgainst);
        _upgradeKindLabel.text = App.Localize(kindLabel);
        _upgradeKindText.text = App.Localize(kindText);
    }

    // 눈 1일 때 → 눈이 상한일 때. 카드는 아직 굴리지 않았으므로 나올 수 있는 범위를 보인다(상한이면 각성까지 반영).
    private void ShowStats(DiceCard card, SO_DiceType type, SO_DiceStatsConfig statsConfig)
    {
        DiceStats low = DiceStats.Compute(DiceRules.Create(card, 1), type, statsConfig);
        DiceStats high = DiceStats.Compute(DiceRules.Create(card, card.Faces), type, statsConfig);
        // 공격력은 정수로 들어가므로 정수로 보인다(공격 속도·사거리는 소수 그대로).
        _damageText.text = FormatRange(Mathf.Round(low.Damage), Mathf.Round(high.Damage));
        _speedText.text = FormatRange(Rate(low), Rate(high));
        _rangeText.text = FormatRange(low.Range, high.Range);
    }

    private static float Rate(in DiceStats stats) => stats.AttackInterval > 0f ? 1f / stats.AttackInterval : 0f;

    // 소수 한 자리까지. 범위가 같으면 하나만 쓴다.
    private static string FormatRange(float low, float high)
    {
        string a = low.ToString("0.#");
        string b = high.ToString("0.#");
        return a == b ? a : $"{a} → {b}";
    }

    /// <summary>
    /// 자세히(설명창) / 간단히(손패) 배치를 바꾼다. 손패 크기에서는 설명이 읽히지 않고 작은 아이콘으로는
    /// 십이·이십면체가 뭉개지므로, 손패에서는 아이콘을 크게 키우고 공격 방식 이름만 보인다.
    /// </summary>
    public void SetDetailed(bool detailed)
    {
        if (detailed) RefreshHeroTexts();
        _detail.SetActive(detailed);
        _compact.SetActive(!detailed);
    }

    // 영웅 능력치를 쓰는 글만 다시 채운다(손패에 든 동안 영웅 강화를 고르면 스킬 피해가 바뀐다).
    private void RefreshHeroTexts()
    {
        if (_heroStats == null) return;
        if (Card.IsSkill) _upgradeDescriptionText.text = CardText.Fill(Card.Skill.Description, Card.Skill, _heroStats);
        else if (Card.IsHero) ShowHero(Card.Hero, _config);
    }

    // 태그는 띠(Image) + 글자 한 쌍이고, 띠 폭을 글자 길이에 맞춘다. 약함 태그는 강함 태그 바로 뒤에 붙인다.
    // 글자 길이로 폭이 정해지므로 가로(위치·폭)만 여기서 정하고, 세로는 프리팹 앵커가 정한다(sizeDelta.y·anchoredPosition.y는 0 그대로 둔다).
    // (TMP의 mark는 띠를 글자 위에 덮어 그려 불투명한 띠로는 글자가 가려진다.) 값이 없으면 숨긴다.
    // 한 줄이 카드 폭을 넘으면(긴 번역 등) 두 태그의 글자를 같은 비율로 줄인다 — 띠가 카드 밖으로 삐져나가지 않게.
    // 오른쪽 한계는 왼쪽 시작 여백과 같은 거리다(좌우 대칭). 글자 기본 크기는 프리팹의 Font Size Max.
    private static void ShowTags(SO_CardHandConfig config, Image strongBack, TMP_Text strongLabel, string strong,
        Image weakBack = null, TMP_Text weakLabel = null, string weak = null)
    {
        bool hasStrong = !string.IsNullOrEmpty(strong);
        bool hasWeak = weakBack != null && !string.IsNullOrEmpty(weak);
        strongBack.gameObject.SetActive(hasStrong);
        strongLabel.gameObject.SetActive(hasStrong);
        if (weakBack != null)
        {
            weakBack.gameObject.SetActive(hasWeak);
            weakLabel.gameObject.SetActive(hasWeak);
        }
        if (!hasStrong && !hasWeak) return;

        string strongText = hasStrong ? App.Localize("강함: {0}", App.Localize(strong)) : null;
        string weakText = hasWeak ? App.Localize("약함: {0}", App.Localize(weak)) : null;

        // 글자 폭만 글자 크기에 비례하고 여백·간격은 그대로이므로, 남는 폭을 글자 폭에만 나눠 맞춘다.
        float start = strongBack.rectTransform.anchoredPosition.x;
        float available = ((RectTransform)strongBack.rectTransform.parent).rect.width - start * 2f;
        float textWidth = 0f;
        int count = 0;
        if (hasStrong)
        {
            strongLabel.fontSize = strongLabel.fontSizeMax;
            textWidth += strongLabel.GetPreferredValues(strongText).x;
            count++;
        }
        if (hasWeak)
        {
            weakLabel.fontSize = weakLabel.fontSizeMax;
            textWidth += weakLabel.GetPreferredValues(weakText).x;
            count++;
        }
        float fixedWidth = config.TagPadding * 2f * count + config.TagGap * (count - 1);
        float scale = textWidth > 0f ? Mathf.Clamp01((available - fixedWidth) / textWidth) : 1f;

        float x = start;
        if (hasStrong) x = LayoutTag(strongBack, strongLabel, strongText, config.StrongTagColor, config.StrongTagTextColor, scale, x, config);
        if (hasWeak) LayoutTag(weakBack, weakLabel, weakText, config.WeakTagColor, config.TextColor, scale, x, config);
    }

    /// <returns>다음 태그가 놓일 x.</returns>
    private static float LayoutTag(Image back, TMP_Text label, string text, Color backColor, Color textColor,
        float scale, float x, SO_CardHandConfig config)
    {
        label.text = text;
        label.color = textColor;
        label.fontSize = label.fontSizeMax * scale;
        back.color = backColor;
        float width = label.GetPreferredValues(text).x + config.TagPadding * 2f;
        RectTransform backRect = back.rectTransform;
        RectTransform labelRect = label.rectTransform;
        backRect.anchoredPosition = new Vector2(x, backRect.anchoredPosition.y);
        backRect.sizeDelta = new Vector2(width, backRect.sizeDelta.y);
        labelRect.anchoredPosition = backRect.anchoredPosition;
        labelRect.sizeDelta = backRect.sizeDelta;
        return x + width + config.TagGap;
    }

    public void SetStack(int stack)
    {
        Stack = stack;
        _stackText.text = stack > 1 ? $"x{stack}" : string.Empty;
        _stackBack.SetActive(stack > 1);
    }

    public void SetTarget(Vector2 position, float angle, float scale)
    {
        _targetPosition = position;
        _targetAngle = angle;
        // 크기는 "손패 카드 = 1" 기준으로 받는다. 프리팹은 설명창 크기로 만들었으므로 여기서 줄인다.
        _handScale = scale;
        _targetScale = _handScale * _baseScale;
    }

    /// <summary>프리팹을 손패 카드 크기로 줄이는 배율(CardSizing.GetBaseScale). 목표 크기도 함께 다시 계산한다.</summary>
    public void SetBaseScale(float baseScale)
    {
        _baseScale = baseScale;
        _targetScale = _handScale * _baseScale;
    }

    /// <summary>목표 자세로 즉시 옮긴다(새로 만든 카드가 엉뚱한 곳에서 날아오지 않게).</summary>
    public void SnapToTarget()
    {
        _rect.anchoredPosition = _targetPosition;
        _rect.localRotation = Quaternion.Euler(0f, 0f, _targetAngle);
        _rect.localScale = Vector3.one * _targetScale;
    }

    /// <summary>드래그 중 주사위 고스트로 바뀌면 카드는 숨긴다. 끄지 않고 투명하게만 해야 드래그 이벤트를 계속 받는다.</summary>
    public void SetVisible(bool visible) => _group.alpha = visible ? 1f : 0f;

    private void Update()
    {
        // 카드를 집은 동안 게임 속도를 늦추므로 카드 연출은 unscaled 시간으로 돌린다.
        float t = 1f - Mathf.Exp(-_config.FollowSharpness * Time.unscaledDeltaTime);
        _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, _targetPosition, t);
        _rect.localRotation = Quaternion.Slerp(_rect.localRotation, Quaternion.Euler(0f, 0f, _targetAngle), t);
        _rect.localScale = Vector3.Lerp(_rect.localScale, Vector3.one * _targetScale, t);
    }

    private void OnDestroy()
    {
        PointerDown = null;
        Dragged = null;
        PointerUp = null;
    }

    public void OnPointerDown(PointerEventData eventData) => PointerDown?.Invoke(this, eventData);

    public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(this, eventData);

    public void OnPointerUp(PointerEventData eventData) => PointerUp?.Invoke(this, eventData);
}
