using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 레벨업 → 카드 고르기 흐름. 레벨이 오르면 바로 팝업을 띄우고, 고른 카드를 그 자리에서 손패로 끌어온다.
/// 영웅 강화 카드는 손패로 가지 않고 고르는 즉시 HeroCardChosen으로 내보낸다(적용은 받는 쪽 — 이 클래스가 영웅을 모르게).
/// 여러 레벨이 연달아 오르면 쌓아 두었다가 하나 고를 때마다 다음 것을 띄운다.
/// 고르는 동안은 게임을 느리게 한다(멈추지 않는다 — 기획: 50% 감속).
/// 팝업은 클릭 가드(검정 패널)로 뒤를 막는다 — 고르는 동안 손패·영웅 조작은 받지 않는다.
/// </summary>
public sealed class LevelUpChoice
{
    private readonly SO_LevelUpConfig _config;
    private readonly SO_CardHandConfig _handConfig;
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly HeroStats _heroStats;
    private readonly TraitHooks _traits;
    private readonly CardHand _hand;
    private readonly GameSpeed _speed;
    private readonly List<HandCard> _picked = new List<HandCard>();
    // 후보마다 고르면 받을 장수(_picked와 같은 순서). 특성 갈고리(ILevelUpCopies)가 띄울 때 굴린다.
    private readonly List<int> _copies = new List<int>();

    /// <summary>영웅 강화 카드를 골랐다.</summary>
    public event Action<SO_HeroCard> HeroCardChosen;

    /// <summary>카드를 골랐다(종류 상관없이): 카드와 받은 장수. 측정 기록(WaveLog)이 듣는다.</summary>
    public event Action<HandCard, int> CardChosen;

    /// <summary>
    /// 팝업이 떴다. 끌던 카드를 받는 쪽이 취소한다 — 가드가 새 터치는 막지만 이미 시작된 드래그는 막지 못해,
    /// 팝업 뒤에서 카드를 놓는 일이 생긴다.
    /// </summary>
    public event Action Opened;

    /// <summary>후보가 나왔다(고르기 전). 측정 기록(WaveLog)이 종류별로 센다 — 봇이 대신 고를 때도 온다.</summary>
    public event Action<IReadOnlyList<HandCard>> Offered;

    /// <summary>
    /// 있으면 팝업을 띄우지 않고 이 함수가 고른다(봇 — 후보와 장수를 받아 고를 번호를 낸다, 기획서 11.2).
    /// </summary>
    public Func<IReadOnlyList<HandCard>, IReadOnlyList<int>, int> AutoChoose { get; set; }

    private int _pending;
    private bool _showing;
    private UI_LevelUpPopup _popup;
    // 팝업을 불러오는 동안 새 판이 시작되면, 다 불러온 팝업을 띄우지 않고 닫기 위해 세대를 센다.
    private int _generation;

    public LevelUpChoice(SO_LevelUpConfig config, SO_CardHandConfig handConfig, SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig,
        HeroStats heroStats, TraitHooks traits, CardHand hand, GameSpeed speed)
    {
        _config = config;
        _handConfig = handConfig;
        _diceConfig = diceConfig;
        _statsConfig = statsConfig;
        _heroStats = heroStats;
        _traits = traits;
        _hand = hand;
        _speed = speed;
    }

    /// <summary>레벨이 하나 올랐다.</summary>
    public void Enqueue()
    {
        _pending++;
        if (!_showing) ShowNextAsync().Forget();
    }

    /// <summary>남은 선택을 버리고 팝업을 닫는다(새 판·게임 오버).</summary>
    public void Cancel()
    {
        _generation++;
        _pending = 0;
        _showing = false;
        if (_popup != null) App.ClosePopup(_popup);
        _popup = null;
        _speed.Release(this);
    }

    // 골라도 바뀌는 것이 없는 영웅 카드(치명타 100%의 확률 카드)는 후보에 내지 않는다.
    private bool IsUseful(SO_Card card) => !(card is SO_HeroCard hero) || _heroStats == null || _heroStats.IsUseful(hero);

    private async UniTaskVoid ShowNextAsync()
    {
        _pending--;
        CardRoll.Pick(_config, _traits, _config.ChoiceCount, _picked, IsUseful, _heroStats?.Character?.ExclusiveCard);
        RollCopies();
        // 후보가 없으면(설정 비어 있음) 고를 것이 없으니 넘어간다 — 빈 팝업에 갇히지 않게.
        if (_picked.Count == 0)
        {
            Debug.LogWarning("[LevelUpChoice] 후보 카드가 없다 — SO_LevelUpConfig의 가중치·강화 카드 목록을 확인");
            Finish();
            return;
        }
        Offered?.Invoke(_picked);
        if (AutoChoose != null)
        {
            int chosen = Mathf.Clamp(AutoChoose(_picked, _copies), 0, _picked.Count - 1);
            OnChosen(_picked[chosen], Vector2.zero);
            return;
        }

        _showing = true;
        _speed.Request(this, _config.ChoiceTimeScale);

        int generation = _generation;
        UI_LevelUpPopup popup = await App.ShowPopup<UI_LevelUpPopup>(clickGuard: true, clickToClose: false);
        if (generation != _generation)
        {
            if (popup != null) App.ClosePopup(popup);
            return;
        }
        if (popup == null)
        {
            Debug.LogWarning("[LevelUpChoice] UI_LevelUpPopup을 띄우지 못했다 — Addressables 주소를 확인");
            _pending = 0;
            Finish();
            return;
        }

        _popup = popup;
        Opened?.Invoke();
        popup.Show(_picked, _copies, _config.CopyPopScale, _config.InputLockDuration, _config.PeekFadeDuration, _handConfig, _diceConfig, _statsConfig, _heroStats, OnChosen);
    }

    // 특성이 후보마다 장수를 정한다(더블 — 레벨업마다 확률로 후보 하나가 x2). 띄울 때 굴려 두어야 처음부터 보이고 고르는 기준이 된다.
    private void RollCopies()
    {
        _copies.Clear();
        ILevelUpCopies copies = _traits?.Find<ILevelUpCopies>();
        if (copies != null) copies.RollCopies(_picked, _copies);
        // 갈고리가 덜 채웠거나 1 아래를 넣었으면 그대로(1장)로 맞춘다.
        while (_copies.Count < _picked.Count) _copies.Add(1);
        for (int i = 0; i < _copies.Count; i++) _copies[i] = Mathf.Max(_copies[i], 1);
    }

    // 고른 카드를 띄울 때 정한 장수만큼 받고 팝업을 닫는다. 손패 카드는 같은 카드라 한 장으로 겹쳐(x2) 날아간다.
    private void OnChosen(HandCard card, Vector2 screenPosition)
    {
        int index = _picked.IndexOf(card);
        int count = index >= 0 && index < _copies.Count ? _copies[index] : 1;
        if (!card.IsHero && _popup != null) _popup.HideChosen();
        CardChosen?.Invoke(card, count);
        for (int i = 0; i < count; i++)
        {
            if (card.IsHero) HeroCardChosen?.Invoke(card.Hero);
            else _hand.AddFrom(card, screenPosition, _popup != null ? _popup.CardScale : 1f);
        }
        if (_popup != null) App.ClosePopup(_popup);
        _popup = null;
        Finish();
    }

    // 하나를 끝냈다. 쌓인 것이 있으면 이어 띄우고, 없으면 속도를 되돌린다.
    private void Finish()
    {
        if (_pending > 0)
        {
            ShowNextAsync().Forget();
            return;
        }
        _showing = false;
        _speed.Release(this);
    }
}
