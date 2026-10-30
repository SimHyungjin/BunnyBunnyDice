using System;
using UnityEngine;

/// <summary>
/// 핫 스트릭이 켜진 동안 영웅이 밟는 주사위에 레이즈를 건다(SO_SkillCard_HotStreak). 칸에 들어설 때 한 번 건다 — 서 있는 동안 매번 다시 걸지 않는다.
/// 뛰어넘는 중(깡총)에 밟은 주사위도 건다 — 지나가며 거는 카드라서(영웅 레이즈는 눈이 깎이므로 깡총을 빼지만 이 레이즈는 깎이지 않는다).
/// 레이즈 카드와 같은 상태(DiceCombat.Raise)라 그 시간 동안은 영웅이 올라타도 눈이 깎이지 않는다(HeroRaise).
/// 게임 시간을 따른다(멈춤·배속).
/// </summary>
public sealed class HotStreak : IDisposable
{
    private readonly SO_HeroConfig _heroConfig;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly Hero _hero;
    private readonly DiceField _diceField;
    private readonly DiceCombat _diceCombat;
    private readonly AttackEffects _effects;
    private readonly TraitHooks _traits;
    private SO_SkillCard_HotStreak _card;
    private float _time;
    private Vector2Int _lastCell;

    private static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);

    public bool Active => _time > 0f;

    public HotStreak(SO_HeroConfig heroConfig, SO_MapViewConfig viewConfig, Hero hero, DiceField diceField, DiceCombat diceCombat,
        AttackEffects effects, TraitHooks traits)
    {
        _heroConfig = heroConfig;
        _viewConfig = viewConfig;
        _hero = hero;
        _diceField = diceField;
        _diceCombat = diceCombat;
        _effects = effects;
        _traits = traits;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    /// <summary>켠다. 켜진 동안 다시 쓰면 긴 쪽 시간을 남긴다. 지금 서 있는 주사위에도 바로 건다.</summary>
    public void Start(SO_SkillCard_HotStreak card)
    {
        _card = card;
        _time = Mathf.Max(_time, card.Duration);
        _lastCell = NoCell;
        _effects.ShowBurst(Ground(_hero.Position), card.BurstRadius, card.IconColor);
    }

    /// <summary>판이 바뀌면 끝낸다.</summary>
    public void Clear() => _time = 0f;

    private void Tick(float deltaTime)
    {
        if (_time <= 0f) return;
        _time -= deltaTime;
        Vector2Int cell = GridCircle.CellOf(_hero.Position);
        if (cell == _lastCell) return;
        _lastCell = cell;

        SO_SkillCard_Raise raise = _heroConfig.Raise;
        if (raise == null || !_diceField.TryGetPlaced(cell, out PlacedDice placed) || placed.Dice.IsSpent) return;
        raise.GetScales(_traits, out float damageScale, out float speedScale);
        _diceCombat.Raise(_diceField.IndexOf(cell), damageScale, speedScale, _card.RaiseDuration);
        // 레이즈 카드와 같은 고리 — 어느 주사위가 빨라졌는지 보이게.
        Vector3 ground = Ground(cell);
        _effects.ShowBurst(ground, raise.BurstRadius, _card.IconColor);
        _effects.ShowRing(ground, raise.RingRadius, _card.IconColor, _card.RaiseDuration);
    }

    private Vector3 Ground(Vector2 position) => new Vector3(position.x, _viewConfig.GetTileTop(TileType.Ground), position.y);
}
