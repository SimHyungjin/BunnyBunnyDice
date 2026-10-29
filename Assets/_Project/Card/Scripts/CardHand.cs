using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 손패: 카드 목록·겹치기·정렬과 "집은 카드" 한 장의 상태만 다룬다. 집은 카드로 무엇을 할지(주사위 배치 등)는
/// 이 클래스의 이벤트를 받는 쪽(CardDragController)이 정한다.
///
/// 같은 카드는 한 장으로 겹쳐 x2, x3으로 표시한다 — 화면이 좁은 모바일 가로 화면에서 손패가 넘치지 않게.
/// 겹친 카드를 집으면 집은 한 장만 손에 들리고, 나머지는 같은 자리에 남는다.
///
/// 상한은 뭉치 수다(SO_CardHandConfig.MaxStacks). 넘치면 오래된 것이 아니라 무작위 뭉치가 사라진다 —
/// 쌓아 두지 말고 바로바로 쓰라는 압박이다. 고를 수 있으면 다들 쓸모없는 뭉치만 버려 압박이 사라진다(기획서 8).
/// </summary>
public sealed class CardHand
{
    private readonly RectTransform _root;
    private readonly SO_CardHandConfig _config;
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly HeroStats _heroStats;
    private readonly Canvas _canvas;
    private readonly RectTransform _canvasRect;
    private readonly List<CardView> _cards = new List<CardView>();
    private int _returnIndex;
    // 손패 카드 높이(캔버스 단위)와 프리팹 배율. 화면 크기에서 정해지므로 Relayout마다 다시 잰다.
    private float _cardHeight;
    private float _baseScale = 1f;

    public event Action<CardView, PointerEventData> CardPressed;
    public event Action<CardView, PointerEventData> CardDragged;
    public event Action<CardView, PointerEventData> CardReleased;

    /// <summary>지금 집은 카드. 없으면 null.</summary>
    public CardView Held { get; private set; }

    /// <summary>화면 픽셀 → 캔버스 단위 환산(데드존 같은 거리를 기기 해상도와 무관하게 쓰기 위해).</summary>
    public float ScaleFactor => _canvas.scaleFactor;

    public CardHand(RectTransform root, SO_CardHandConfig config, SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig, HeroStats heroStats)
    {
        _root = root;
        _config = config;
        _diceConfig = diceConfig;
        _statsConfig = statsConfig;
        _heroStats = heroStats;
        _canvas = root.GetComponentInParent<Canvas>().rootCanvas;
        _canvasRect = (RectTransform)_canvas.transform;
        // 첫 카드가 들어오기 전에 카드 크기를 재 둔다(Add가 그 크기로 날아오는 자리를 정한다).
        Relayout();
    }

    public void Add(HandCard card)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].Card == card)
            {
                _cards[i].SetStack(_cards[i].Stack + 1);
                return;
            }
        }

        DiscardForNewStack();
        CardView view = CreateView(card);
        view.SetTarget(_config.NewCardFrom * _cardHeight, 0f, _config.NewCardFromScale);
        view.SnapToTarget();
        _cards.Add(view);
        Relayout();
    }

    /// <summary>
    /// 화면의 screenPosition에 scale 크기로 있던 카드를 손패로 끌어온다(레벨업에서 고른 카드).
    /// 같은 카드가 이미 있으면 그 카드를 그 자리로 옮겨 겹친 채 날아 돌아오게 한다 — 어디에 합쳐졌는지 보이게.
    /// </summary>
    public void AddFrom(HandCard card, Vector2 screenPosition, float scale)
    {
        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPosition, cam, out Vector2 from);

        CardView view = null;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].Card != card) continue;
            view = _cards[i];
            view.SetStack(view.Stack + 1);
            break;
        }
        if (view == null)
        {
            DiscardForNewStack();
            view = CreateView(card);
            _cards.Add(view);
        }

        view.SetTarget(from, 0f, scale);
        view.SnapToTarget();
        Relayout();
    }

    /// <summary>카드를 손에 든다. 이미 든 카드가 있으면 실패한다(한 번에 한 장).</summary>
    public bool TryHold(CardView card)
    {
        if (Held != null) return false;
        int index = _cards.IndexOf(card);
        if (index < 0) return false;

        _returnIndex = index;
        if (card.Stack > 1)
        {
            // 나머지를 같은 자리에 새 카드로 남기고, 집은 카드는 한 장짜리가 된다.
            CardView rest = CreateView(card.Card);
            rest.SetStack(card.Stack - 1);
            rest.SetTarget(card.Rect.anchoredPosition, card.Rect.localEulerAngles.z, 1f);
            rest.SnapToTarget();
            _cards[index] = rest;
            card.SetStack(1);
        }
        else
        {
            _cards.RemoveAt(index);
        }

        Held = card;
        Relayout();
        return true;
    }

    /// <summary>
    /// 집은 카드가 손가락을 따라오게 한다. inspecting이면(끌기 전, 누르고 있는 동안) 설명을 읽을 수 있게 크게 키우고
    /// 손가락에 가리지 않게 위로 띄운다 — 설명창을 따로 두지 않고 카드 자체가 설명창이다.
    /// </summary>
    public void MoveHeld(Vector2 screenPosition, bool inspecting)
    {
        if (Held == null) return;
        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPosition, cam, out Vector2 local);
        Held.SetDetailed(inspecting);
        if (inspecting) Held.SetTarget(local + _config.InspectOffset * _cardHeight, 0f, _config.InspectScale);
        else Held.SetTarget(local, 0f, _config.HoldScale);
    }

    /// <summary>손패의 뭉치 수(겹친 카드는 하나).</summary>
    public int Count => _cards.Count;

    /// <summary>index번째 뭉치의 카드.</summary>
    public HandCard GetCard(int index) => _cards[index].Card;

    /// <summary>
    /// 끌지 않고 카드 한 장을 쓴다(봇 — UI를 거치지 않는 게임 행동, 기획서 11.2). 겹친 뭉치면 한 장만 줄인다. 없으면 false.
    /// </summary>
    public bool TryRemove(HandCard card)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            CardView view = _cards[i];
            if (view.Card != card) continue;
            if (view.Stack > 1)
            {
                view.SetStack(view.Stack - 1);
                return true;
            }
            _cards.RemoveAt(i);
            UnityEngine.Object.Destroy(view.gameObject);
            Relayout();
            return true;
        }
        return false;
    }

    /// <summary>집은 카드를 썼다 — 없앤다.</summary>
    public void ConsumeHeld()
    {
        if (Held == null) return;
        UnityEngine.Object.Destroy(Held.gameObject);
        Held = null;
        Relayout();
    }

    /// <summary>손패를 모두 버린다(새 판). 집은 카드가 있으면 먼저 드래그 쪽이 내려놓게 해야 한다(CardDragController.Cancel).</summary>
    public void Clear()
    {
        for (int i = 0; i < _cards.Count; i++) UnityEngine.Object.Destroy(_cards[i].gameObject);
        _cards.Clear();
        if (Held != null) UnityEngine.Object.Destroy(Held.gameObject);
        Held = null;
    }

    /// <summary>집은 카드를 손패로 되돌린다. 나머지가 남아 있으면(겹친 카드였으면) 다시 합친다.</summary>
    public void ReturnHeld()
    {
        CardView held = Held;
        if (held == null) return;
        Held = null;
        held.SetVisible(true);
        held.SetDetailed(false);

        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].Card == held.Card)
            {
                _cards[i].SetStack(_cards[i].Stack + 1);
                UnityEngine.Object.Destroy(held.gameObject);
                Relayout();
                return;
            }
        }

        _cards.Insert(Mathf.Min(_returnIndex, _cards.Count), held);
        Relayout();
    }

    /// <summary>
    /// 새 뭉치가 들어올 자리를 만든다 — 상한이 찼으면 손패의 뭉치 하나를 무작위로 통째로 없앤다.
    /// 집은 카드도 손패의 뭉치로 센다(들고 있는 동안 들어온 카드 때문에 되돌릴 때 상한을 넘지 않게).
    /// 집은 카드와 그 나머지 뭉치는 손가락에서 빼앗지 않도록 고르는 대상에서 뺀다.
    /// </summary>
    private void DiscardForNewStack()
    {
        int stacks = _cards.Count;
        if (Held != null && !HasStack(Held.Card)) stacks++;
        if (stacks < _config.MaxStacks) return;

        int candidates = 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (Held == null || _cards[i].Card != Held.Card) candidates++;
        }
        if (candidates == 0) return;

        int pick = GameRandom.Cards.Next(candidates);
        for (int i = 0; i < _cards.Count; i++)
        {
            if (Held != null && _cards[i].Card == Held.Card) continue;
            if (pick-- > 0) continue;
            UnityEngine.Object.Destroy(_cards[i].gameObject);
            _cards.RemoveAt(i);
            if (i < _returnIndex) _returnIndex--;
            return;
        }
    }

    private bool HasStack(HandCard card)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].Card == card) return true;
        }
        return false;
    }

    private CardView CreateView(HandCard card)
    {
        CardView view = UnityEngine.Object.Instantiate(_config.CardPrefab, _root, false);
        view.Init(card, _config, _diceConfig, _statsConfig, _heroStats);
        view.SetBaseScale(_baseScale);
        view.PointerDown += (v, e) => CardPressed?.Invoke(v, e);
        view.Dragged += (v, e) => CardDragged?.Invoke(v, e);
        view.PointerUp += (v, e) => CardReleased?.Invoke(v, e);
        return view;
    }

    /// <summary>현재 장수로 부채꼴을 다시 계산한다. 손패 설정·화면 크기가 바뀌었을 때도 부른다.</summary>
    public void Relayout()
    {
        _cardHeight = CardSizing.GetHandCardHeight(_canvasRect, _config);
        _baseScale = CardSizing.GetBaseScale(_cardHeight, _config);
        if (Held != null) Held.SetBaseScale(_baseScale);

        int count = _cards.Count;
        for (int i = 0; i < count; i++)
        {
            CardHandLayout.GetPose(i, count, Held != null, _cardHeight, _config, out Vector2 position, out float angle);
            _cards[i].SetBaseScale(_baseScale);
            _cards[i].SetTarget(position, angle, 1f);
            // 왼쪽 카드부터 그려 오른쪽 카드가 위에 겹친다.
            _cards[i].transform.SetSiblingIndex(i);
        }
        if (Held != null) Held.transform.SetAsLastSibling();
    }
}
