using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 튜토리얼(상황 팁)의 규칙: 어떤 팁을 언제 띄우고 언제 넘기는지. 표시는 UI_TutorialOverlay, 비출 곳은 TutorialTargets.
///
/// 팁은 처음 그 상황이 오면 한 번만 뜬다(TutorialSave). 때는 두 군데다.
/// - 판: 영웅 이동(판에 들어와 판 주사위가 흩뿌려질 때 — 그때부터 영웅을 움직일 수 있다. 연출은 실제 시간이라 팁이 멈춰도 이어진다),
///   주사위 카드(연출 뒤 — 끌어 놓기를 직접 해 보게 하고, 놓은 주사위를 비추며 주사위 규칙을 한 번에 넘겨 본다),
///   ▶(첫 웨이브 시작), 레이즈(영웅이 처음 주사위에 올라탔을 때), 손패 넘침(첫 레벨업에서 카드를 고른 뒤).
/// - 레벨업 창: 칩·스킬·전용·영웅 강화 카드가 처음 후보로 나왔을 때 그 후보를 비춘다. 고른 뒤 손패에서 알려 주면 이미 모르고 고른 뒤라서다.
///   처음 보는 종류가 한 번에 여럿이면 덮개를 닫지 않고 이어서 넘긴다(장 번호도 이어서). 팁을 다 넘길 때까지 후보는 고를 수 없다.
/// 팁이 떠 있는 동안 판을 멈춘다(GameSpeed 0 — 일시정지·레벨업 감속과 같은 묶음이라 겹쳐도 가장 느린 쪽이 이긴다).
///
/// 판 상태는 바꾸지 않고 이벤트만 듣는다. 봇·시뮬 동안은 띄우지 않는다(판 결과가 사람 입력을 기다리지 않게).
/// </summary>
public sealed class TutorialFlow : IDisposable
{
    // 같은 순간에 여럿이 걸리면 이 순서로 이어 보인다. 영웅 이동 → 주사위 → ▶은 첫 판의 흐름 순서다.
    private static readonly TutorialTipId[] Order =
    {
        TutorialTipId.HeroMove,
        TutorialTipId.DiceCard,
        TutorialTipId.WaveStart,
        TutorialTipId.Raise,
        TutorialTipId.ChipCard,
        TutorialTipId.SkillCard,
        TutorialTipId.ExclusiveCard,
        TutorialTipId.HeroCard,
        TutorialTipId.HandLimit,
    };

    private readonly SO_TutorialConfig _config;
    private readonly SO_CardHandConfig _handConfig;
    private readonly TutorialTargets _targets;
    private readonly CardHand _hand;
    private readonly DiceField _diceField;
    private readonly Hero _hero;
    private readonly HeroRaise _heroRaise;
    private readonly WaveStartGate _waveStart;
    private readonly LevelUpChoice _levelUp;
    private readonly GameSpeed _speed;
    private readonly Func<bool> _runReady;
    private readonly Func<bool> _boardReady;
    private readonly Func<bool> _offerReady;
    private readonly Func<bool> _blocked;
    private readonly Func<bool> _dragging;
    private readonly TutorialSave _save;
    private readonly object[] _args = new object[1];

    private UI_TutorialOverlay _overlay;
    // 지금 이어 보이는 팁들과 그중 몇 번째 팁의 몇 번째 장인지.
    private readonly List<TutorialTip> _tips = new List<TutorialTip>();
    private int _tipIndex;
    private int _pageIndex;
    // 레벨업 창 위에 떠 있다(그 창이 닫히면 같이 닫고, 다 넘기면 고르기를 풀어 준다).
    private UI_LevelUpPopup _lockedOffer;
    private bool _loading;
    // 팝업을 불러오는 동안 취소되면(새 판·로비) 다 불러온 팝업을 띄우지 않고 닫는다.
    private int _generation;
    // 판 안에서 한 번이라도 있었던 일(그 팁의 때가 왔는지): 레벨업 카드를 골랐다(손패 넘침), 영웅이 주사위에 올라탔다(레이즈).
    private bool _cardChosen;
    private bool _mounted;
    // 주사위 강조(TutorialPage.PulseDice) 다음 튐까지 쌓인 시간.
    private float _pulseTimer;

    /// <param name="runReady">참일 때만 영웅 이동 팁을 띄운다(판 안, 다른 창이 없음 — 판 주사위 연출 중이어도 된다).</param>
    /// <param name="boardReady">참일 때만 나머지 판 팁을 띄운다(판 안, 판 주사위 연출·다른 창·카드 끌기 중이 아님).</param>
    /// <param name="offerReady">참일 때만 레벨업 창 팁을 띄운다(고르기 창이 떠 있고 그 위에 다른 창이 없음).</param>
    /// <param name="blocked">참이면 팁을 띄우지 않고, 떠 있으면 닫는다(봇·시뮬·테스트로 끔).</param>
    /// <param name="dragging">카드를 끄는 중(덮개를 걷어 둔다).</param>
    public TutorialFlow(SO_TutorialConfig config, SO_CardHandConfig handConfig, TutorialTargets targets, CardHand hand, DiceField diceField, Hero hero,
        HeroRaise heroRaise, WaveStartGate waveStart, LevelUpChoice levelUp, GameSpeed speed, Func<bool> runReady, Func<bool> boardReady,
        Func<bool> offerReady, Func<bool> blocked, Func<bool> dragging)
    {
        _config = config;
        _handConfig = handConfig;
        _targets = targets;
        _hand = hand;
        _diceField = diceField;
        _hero = hero;
        _heroRaise = heroRaise;
        _waveStart = waveStart;
        _levelUp = levelUp;
        _speed = speed;
        _runReady = runReady;
        _boardReady = boardReady;
        _offerReady = offerReady;
        _blocked = blocked;
        _dragging = dragging;
        _save = Core.Data.Get<TutorialSave>();

        _diceField.DicePlaced += OnDicePlaced;
        _diceField.DiceStacked += OnDicePlaced;
        _waveStart.WaitingChanged += OnWaitingChanged;
        _hero.MoveOrdered += OnHeroMoved;
        _levelUp.CardChosen += OnCardChosen;
        _heroRaise.Mounted += OnMounted;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Cancel();
        _diceField.DicePlaced -= OnDicePlaced;
        _diceField.DiceStacked -= OnDicePlaced;
        _waveStart.WaitingChanged -= OnWaitingChanged;
        _hero.MoveOrdered -= OnHeroMoved;
        _levelUp.CardChosen -= OnCardChosen;
        _heroRaise.Mounted -= OnMounted;
        if (Core.Loop != null) Core.Loop.OnUpdate -= Tick;
    }

    /// <summary>팁이 떠 있거나 불러오는 중이다.</summary>
    public bool Showing => _overlay != null || _loading;

    /// <summary>지금 장이 이 행동을 기다린다(덮개를 거치지 않는 입력 — Space 등 — 을 그 장에서만 받게).</summary>
    public bool WaitsFor(TutorialAction action) => _overlay != null && CurrentPage != null && CurrentPage.Action == action;

    private TutorialPage CurrentPage => _tipIndex < _tips.Count ? _tips[_tipIndex].Pages[_pageIndex] : null;

    /// <summary>떠 있는 팁을 본 것으로 치지 않고 닫는다(새 판·로비·게임 오버). 다음에 때가 오면 다시 뜬다. 이미 다 넘긴 팁은 본 것으로 남는다.</summary>
    public void Cancel()
    {
        _generation++;
        _loading = false;
        Close();
    }

    /// <summary>본 팁 기록을 지운다(테스트 — 처음 하는 사람처럼 다시 본다).</summary>
    public void ResetSeen()
    {
        Cancel();
        _save.ResetSeen();
        _cardChosen = false;
        _mounted = false;
    }

    private void Tick(float deltaTime)
    {
        if (_blocked())
        {
            if (_overlay != null || _loading) Cancel();
            return;
        }
        if (_overlay != null)
        {
            // 레벨업 창이 먼저 닫혔으면(새 판 등) 비출 후보가 없다.
            if (_lockedOffer != null && _levelUp.Popup != _lockedOffer)
            {
                Cancel();
                return;
            }
            RefreshTargets();
            TickPulse(deltaTime);
            return;
        }
        if (_loading) return;
        bool offer = _offerReady();
        bool board = !offer && _boardReady();
        bool run = !offer && _runReady();
        if (!offer && !board && !run) return;
        if (CollectDue(offer, board)) ShowAsync(offer).Forget();
    }

    // 지금 때가 온 팁을 순서대로 모은다. 레벨업 창이 떠 있으면 그 창의 팁만, 아니면 판 팁만(판 주사위 연출 중이면 영웅 이동만).
    private bool CollectDue(bool offer, bool board)
    {
        _tips.Clear();
        for (int i = 0; i < Order.Length; i++)
        {
            TutorialTipId id = Order[i];
            if (IsOfferTip(id) != offer || _save.HasSeen(id) || !IsDue(id)) continue;
            if (!offer && !board && id != TutorialTipId.HeroMove) continue;
            TutorialTip tip = _config.Find(id);
            if (tip == null || tip.Pages.Count == 0 || !HasPages(tip)) continue;
            // 첫 장이 비출 곳이 아직 화면에 없으면(손패를 다 씀 등) 다음 기회에.
            TutorialPage first = tip.Pages[0];
            if (!_targets.Exists(first.TargetA) || !_targets.Exists(first.TargetB) || !_targets.Exists(first.InputArea)) continue;
            _tips.Add(tip);
        }
        return _tips.Count > 0;
    }

    private static bool HasPages(TutorialTip tip)
    {
        for (int i = 0; i < tip.Pages.Count; i++)
        {
            if (tip.Pages[i] == null) return false;
        }
        return true;
    }

    private static bool IsOfferTip(TutorialTipId id) => id == TutorialTipId.ChipCard || id == TutorialTipId.SkillCard
        || id == TutorialTipId.ExclusiveCard || id == TutorialTipId.HeroCard;

    private bool IsDue(TutorialTipId id)
    {
        switch (id)
        {
            case TutorialTipId.HeroMove: return true;
            case TutorialTipId.DiceCard: return HasKind(CardKind.Dice);
            case TutorialTipId.WaveStart: return _save.HasSeen(TutorialTipId.DiceCard) && _waveStart.Waiting;
            case TutorialTipId.Raise: return _mounted;
            case TutorialTipId.ChipCard: return Offers(CardKind.Chip);
            case TutorialTipId.SkillCard: return Offers(CardKind.Skill);
            case TutorialTipId.ExclusiveCard: return Offers(CardKind.Exclusive);
            case TutorialTipId.HeroCard: return Offers(CardKind.Hero);
            case TutorialTipId.HandLimit: return _cardChosen;
            default: return false;
        }
    }

    private bool HasKind(CardKind kind)
    {
        for (int i = 0; i < _hand.Count; i++)
        {
            if (_hand.GetCard(i).Kind == kind) return true;
        }
        return false;
    }

    private bool Offers(CardKind kind)
    {
        UI_LevelUpPopup popup = _levelUp.Popup;
        if (popup == null) return false;
        for (int i = 0; i < popup.CardCount; i++)
        {
            CardView view = popup.GetCard(i);
            if (view != null && view.Card.Kind == kind) return true;
        }
        return false;
    }

    private async UniTaskVoid ShowAsync(bool offer)
    {
        _loading = true;
        int generation = _generation;
        // 불러오는 동안에도 판이 흐르지 않게 먼저 멈춘다.
        _speed.Request(this, 0f);
        // 레벨업 창 위라면 고르기를 먼저 막는다 — 덮개가 뜨기 전 누름으로 카드를 고르지 않게.
        _lockedOffer = offer ? _levelUp.Popup : null;
        if (_lockedOffer != null) _lockedOffer.Locked = true;
        UI_TutorialOverlay overlay = await App.ShowPopup<UI_TutorialOverlay>(clickGuard: false, clickToClose: false);
        if (generation != _generation)
        {
            if (overlay != null) App.ClosePopup(overlay);
            return;
        }
        _loading = false;
        if (overlay == null)
        {
            // 못 띄우면 같은 팁을 매 프레임 다시 부르지 않게 본 것으로 친다.
            Debug.LogWarning("[TutorialFlow] UI_TutorialOverlay를 띄우지 못했다 — Addressables 주소를 확인");
            for (int i = 0; i < _tips.Count; i++) _save.MarkSeen(_tips[i].Id);
            Close();
            return;
        }

        _overlay = overlay;
        _overlay.Init(_config);
        _overlay.Tapped += OnTapped;
        _tipIndex = 0;
        _pageIndex = 0;
        ShowPage();
    }

    private void ShowPage()
    {
        TutorialPage page = CurrentPage;
        _args[0] = _handConfig.MaxStacks;
        _overlay.SetPage(page.Text, _args, PageNumber(), PageTotal(), page.Action == TutorialAction.Tap);
        _overlay.LockInput();
        RefreshTargets();
        // 장이 뜨자마자 한 번 튀게.
        _pulseTimer = _config.DicePulseInterval;
    }

    // 카드를 끄는 동안은 덮개를 걷어 두므로 쉰다.
    private void TickPulse(float deltaTime)
    {
        TutorialPage page = CurrentPage;
        if (page == null || !page.PulseDice || _dragging()) return;
        _pulseTimer += deltaTime;
        if (_pulseTimer < _config.DicePulseInterval) return;
        _pulseTimer = 0f;
        if (_targets.TryGetDiceCell(out Vector2Int cell)) _diceField.PlayStackPop(cell);
    }

    // 이어 보이는 팁 전체에서 지금 장이 몇 번째인지(0부터)와 모두 몇 장인지.
    private int PageNumber()
    {
        int number = _pageIndex;
        for (int i = 0; i < _tipIndex; i++) number += _tips[i].Pages.Count;
        return number;
    }

    private int PageTotal()
    {
        int total = 0;
        for (int i = 0; i < _tips.Count; i++) total += _tips[i].Pages.Count;
        return total;
    }

    private void RefreshTargets()
    {
        TutorialPage page = CurrentPage;
        if (page == null) return;
        _overlay.SetTargets(GetRect(page.TargetA), GetRect(page.TargetB), GetRect(page.InputArea));
        _overlay.SetFaded(_dragging());
    }

    private Rect? GetRect(TutorialTarget target)
    {
        if (target == TutorialTarget.None) return null;
        return _targets.TryGetRect(target, out Rect rect) ? rect : (Rect?)null;
    }

    private void OnTapped()
    {
        if (_overlay == null || !_overlay.AcceptsTap) return;
        if (CurrentPage.Action == TutorialAction.Tap) Next();
    }

    // 행동형 장: 구멍 안에서 그 행동을 하면 넘어간다.
    private void OnAction(TutorialAction action)
    {
        if (_overlay == null || CurrentPage.Action != action) return;
        Next();
    }

    private void OnDicePlaced(Vector2Int cell)
    {
        _targets.LastDice = cell;
        OnAction(TutorialAction.PlaceDice);
    }

    private void OnWaitingChanged(bool waiting)
    {
        if (!waiting) OnAction(TutorialAction.StartWave);
    }

    private void OnHeroMoved(Vector2Int _) => OnAction(TutorialAction.MoveHero);

    private void OnCardChosen(HandCard card, int _) => _cardChosen = true;

    // 올라탄 주사위를 비춘다.
    private void OnMounted(Vector2Int cell)
    {
        _mounted = true;
        _targets.LastDice = cell;
    }

    private void Next()
    {
        _pageIndex++;
        if (_pageIndex < _tips[_tipIndex].Pages.Count)
        {
            ShowPage();
            return;
        }
        // 팁 하나를 다 넘겼다 — 뒤 팁에서 판을 접어도 이 팁은 다시 뜨지 않게 바로 남긴다.
        _save.MarkSeen(_tips[_tipIndex].Id);
        _tipIndex++;
        _pageIndex = 0;
        if (_tipIndex < _tips.Count)
        {
            ShowPage();
            return;
        }
        Close();
    }

    private void Close()
    {
        if (_overlay != null)
        {
            _overlay.Tapped -= OnTapped;
            App.ClosePopup(_overlay);
        }
        _overlay = null;
        _tips.Clear();
        _tipIndex = 0;
        _pageIndex = 0;
        if (_lockedOffer != null) _lockedOffer.Locked = false;
        _lockedOffer = null;
        _speed.Release(this);
    }
}
