using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 레벨업 카드 선택 팝업. 후보 카드를 한 줄로 늘어놓고, 누른 카드와 그 화면 위치를 알린다(무엇을 할지는 LevelUpChoice가 정한다).
/// 고른 카드는 LevelUpChoice가 HideChosen으로 숨기고 손패 쪽이 같은 자리에서 끌어간다 — 팝업이 닫히는 동안 같은 카드가 두 장 보이지 않게.
/// 카드 외형은 손패와 같은 CardView 프리팹을 쓴다 — 고른 카드가 손패에 들어갔을 때 같은 모양이어야 무엇을 얻었는지 바로 보인다.
/// 클릭 가드(UIManager의 검정 패널)와 함께 띄운다: 고르는 동안 영웅·손패 조작을 막는다. 가드를 눌러도 닫히지 않는다 — 반드시 한 장을 고른다.
/// 뜬 직후 잠깐(SO_LevelUpConfig.InputLockDuration)은 카드를 눌러도 고르지 않는다 — 다른 곳을 누르던 손가락이 갑자기 뜬 카드를 고르지 않게.
/// 카드 밖을 누르고 있는 동안 제목·카드가 서서히 사라져 판이 보이고, 떼면 다시 나타난다(검정 가드는 그대로 — 반투명이라 판이 읽힌다).
/// 배치는 비율로 한다: 카드 영역(_cardRoot)은 프리팹 앵커로 정하고, 그 안에서 FlexLayout이 카드 비율의 슬롯을 늘어놓는다.
/// 카드는 슬롯의 자식으로 두고 슬롯 폭에 맞춰 크기만 바꾼다 — 레이아웃이 카드를 직접 움직이면 CardView의 목표 따라가기와 부딪힌다.
/// </summary>
public sealed class UI_LevelUpPopup : UI_Popup
{
    [Tooltip("카드를 늘어놓는 영역(앵커로 크기를 정한다). UI_Utility_FlexLayout이 붙어 있어야 한다 — 슬롯을 여기 만든다.")]
    [SerializeField] private RectTransform _cardRoot;
    [Tooltip("판 보기 때 흐리게 할 묶음(제목·카드). 루트의 CanvasGroup은 여닫기 연출이 쓰므로 따로 둔다.")]
    [SerializeField] private CanvasGroup _fadeGroup;

    private readonly List<CardView> _cards = new List<CardView>();
    // 팝업이 풀링되므로 슬롯은 지우지 않고 다음에 다시 쓴다(남는 슬롯은 꺼서 레이아웃에서 빠지게).
    private readonly List<RectTransform> _slots = new List<RectTransform>();
    private Action<HandCard, Vector2> _chosen;
    // 마지막으로 고른 카드(숨기기·여러 장 받기 연출용).
    private CardView _chosenView;
    private float _cardWidth;
    private float _baseScale = 1f;
    private SO_CardHandConfig _handConfig;
    private RectTransform _canvasRect;
    // 이 시각(실제 시간) 전에 시작한 누름은 고르지 않는다. 게임이 느려진 동안 뜨므로 게임 시간이 아니라 실제 시간으로 잰다.
    private float _unlockTime;
    // 잠금이 풀린 뒤 카드를 누른 손가락. 잠금 중에 눌러 풀린 뒤에 뗀 터치도 고르지 않게 누를 때 기록한다.
    private int _pressPointer = NoPointer;
    // 카드 밖을 누르고 있는지(판 보기)와 사라지는 시간.
    private bool _peeking;
    private float _peekFade = 0.15f;
    private static readonly List<RaycastResult> RaycastHits = new List<RaycastResult>();

    private const int NoPointer = int.MinValue;

    /// <summary>실제로 쓴 카드 크기(손패 카드 = 1). 카드 영역에 맞춰 정해진다 — 고른 카드가 손패로 날아갈 때 같은 크기에서 출발하게.</summary>
    public float CardScale { get; private set; }

    /// <param name="chosen">고른 카드와 그 카드의 화면 좌표.</param>
    /// <param name="heroStats">영웅 강화 카드의 "지금 → 고른 뒤" 수치.</param>
    /// <param name="inputLock">뜬 뒤 카드 누름을 받지 않는 시간(초, 실제 시간).</param>
    /// <param name="copies">카드마다 고르면 받을 장수(cards와 같은 순서). 2 이상이면 겹침 표시(x2)를 띄우고 copyPopScale에서 톡 줄어든다.</param>
    /// <param name="peekFade">카드 밖을 누르고 있을 때 제목·카드가 사라지는 시간(초, 실제 시간).</param>
    public void Show(IReadOnlyList<HandCard> cards, IReadOnlyList<int> copies, float copyPopScale, float inputLock, float peekFade, SO_CardHandConfig handConfig,
        SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig, HeroStats heroStats, Action<HandCard, Vector2> chosen)
    {
        ClearCards();
        _chosen = chosen;
        _unlockTime = Time.unscaledTime + inputLock;
        _pressPointer = NoPointer;
        _peekFade = peekFade;
        _peeking = false;
        _fadeGroup.alpha = 1f;

        // 슬롯은 카드 프리팹과 같은 가로세로 비율로 둔다 — FlexLayout이 영역 높이에 맞춰 폭을 정하고, 모자라면 같은 비율로 줄인다.
        Vector2 cardSize = ((RectTransform)handConfig.CardPrefab.transform).sizeDelta;
        _cardWidth = cardSize.x;
        _handConfig = handConfig;
        _canvasRect = (RectTransform)_cardRoot.GetComponentInParent<Canvas>().rootCanvas.transform;
        PrepareSlots(cards.Count, cardSize.x / cardSize.y);
        // 슬롯 크기를 바로 읽어야 첫 프레임부터 맞는 크기로 나온다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRoot);
        RefreshScale();

        for (int i = 0; i < cards.Count; i++)
        {
            CardView view = Instantiate(handConfig.CardPrefab, _slots[i], false);
            view.Init(cards[i], handConfig, diceConfig, statsConfig, heroStats);
            // 고르는 순간이 곧 배우는 순간이라(기획서 6.1) 레벨업에서는 자세히 보인다.
            view.SetDetailed(true);
            view.SetBaseScale(_baseScale);
            view.SetTarget(Vector2.zero, 0f, CardScale);
            view.SnapToTarget();
            if (i < copies.Count && copies[i] > 1)
            {
                view.SetStack(copies[i]);
                // 크게 시작해 목표 크기로 따라 줄어든다(CardView가 목표로 수렴한다).
                view.Rect.localScale *= copyPopScale;
            }
            view.PointerDown += OnCardDown;
            view.PointerUp += OnCardUp;
            _cards.Add(view);
        }
    }

    // 화면 크기·방향이 바뀌면 슬롯 크기와 손패 카드 크기가 바뀐다. 위치는 슬롯을 따라가지만 크기(scale)는 카드가 모르므로 여기서 맞춘다.
    private void LateUpdate()
    {
        if (_cards.Count == 0) return;
        float oldBase = _baseScale;
        float oldScale = CardScale;
        RefreshScale();
        if (Mathf.Approximately(oldBase, _baseScale) && Mathf.Approximately(oldScale, CardScale)) return;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].SetBaseScale(_baseScale);
            _cards[i].SetTarget(Vector2.zero, 0f, CardScale);
        }
    }

    private void PrepareSlots(int count, float aspect)
    {
        while (_slots.Count < count)
        {
            var go = new GameObject("Slot", typeof(RectTransform), typeof(UI_Utility_FlexLayoutElement));
            go.layer = _cardRoot.gameObject.layer;
            go.transform.SetParent(_cardRoot, false);
            _slots.Add((RectTransform)go.transform);
        }
        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < count;
            _slots[i].gameObject.SetActive(used);
            if (!used) continue;
            var element = _slots[i].GetComponent<UI_Utility_FlexLayoutElement>();
            element.type = UI_Utility_FlexLayoutElement.SizeType.Aspect;
            element.value = aspect;
        }
    }

    // 슬롯 폭 = 카드가 보일 폭. "손패 카드 = 1" 기준으로 바꾼다(CardView가 기준 배율을 곱하므로 나눠 둔다).
    private void RefreshScale()
    {
        _baseScale = CardSizing.GetBaseScale(CardSizing.GetHandCardHeight(_canvasRect, _handConfig), _handConfig);
        CardScale = _slots.Count == 0 || _cardWidth <= 0f || _baseScale <= 0f
            ? 1f
            : _slots[0].rect.width / (_cardWidth * _baseScale);
    }

    // 풀로 돌아갈 때 카드를 지운다 — 다음에 꺼냈을 때 이전 후보가 남아 있지 않게.
    protected override void OnDespawned()
    {
        base.OnDespawned();
        ClearCards();
        _peeking = false;
        _fadeGroup.alpha = 1f;
    }

    // 실제 시간이다 — 고르는 동안 게임은 거의 멈춰 있다.
    private void Update()
    {
        Pointer pointer = Pointer.current;
        if (pointer != null)
        {
            // 누른 순간 카드 위가 아니면 판 보기. 누른 채 카드 위로 지나가도 그대로 — 떼야 끝난다.
            if (pointer.press.wasPressedThisFrame) _peeking = !IsOverCard(pointer.position.ReadValue());
            else if (!pointer.press.isPressed) _peeking = false;
        }
        float target = _peeking ? 0f : 1f;
        if (!Mathf.Approximately(_fadeGroup.alpha, target))
            _fadeGroup.alpha = Mathf.MoveTowards(_fadeGroup.alpha, target, Time.unscaledDeltaTime / _peekFade);
    }

    private bool IsOverCard(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        PointerEventData data = new PointerEventData(eventSystem) { position = screenPosition };
        eventSystem.RaycastAll(data, RaycastHits);
        for (int i = 0; i < RaycastHits.Count; i++)
        {
            Transform hit = RaycastHits[i].gameObject.transform;
            for (int c = 0; c < _cards.Count; c++)
                if (_cards[c] != null && hit.IsChildOf(_cards[c].transform)) return true;
        }
        return false;
    }

    private void OnCardDown(CardView card, PointerEventData eventData)
    {
        if (Time.unscaledTime < _unlockTime) return;
        _pressPointer = eventData.pointerId;
    }

    private void OnCardUp(CardView card, PointerEventData eventData)
    {
        // 잠금이 풀린 뒤에 시작한 누름만 고른다.
        if (eventData.pointerId != _pressPointer) return;
        _pressPointer = NoPointer;

        // 누른 채 카드 밖으로 끌어 떼면 고르지 않는다(실수로 스친 터치).
        if (eventData.pointerCurrentRaycast.gameObject == null
            || !eventData.pointerCurrentRaycast.gameObject.transform.IsChildOf(card.transform)) return;

        // 두 번 고르지 않게 바로 끊는다(닫히는 연출 동안에도 카드는 눌린다).
        Action<HandCard, Vector2> chosen = _chosen;
        if (chosen == null) return;
        _chosen = null;

        _chosenView = card;
        Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        chosen(card.Card, RectTransformUtility.WorldToScreenPoint(cam, card.Rect.position));
    }

    /// <summary>
    /// 고른 카드를 숨긴다 — 손패로 가는 카드는 손패 쪽에 새로 생긴 카드가 같은 자리에서 날아가므로.
    /// 영웅 강화 카드는 갈 곳이 없어 부르지 않는다(팝업과 함께 닫히는 연출로 사라지게 — 뚝 끊기지 않게).
    /// </summary>
    public void HideChosen()
    {
        if (_chosenView != null) _chosenView.SetVisible(false);
    }

    private void ClearCards()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] != null) Destroy(_cards[i].gameObject);
        }
        _cards.Clear();
        _chosen = null;
        _chosenView = null;
    }
}
