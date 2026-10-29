using System;
using UnityEngine;

/// <summary>
/// 영웅 레이즈 표시: 레이즈 카드와 같은 연출을 그대로 쓴다 — 올라타는 순간 퍼지는 고리 + 올라탄 동안 주사위 밑에 머무는 고리(카드 색).
/// 같은 상태이므로 같은 모습이어야 플레이어가 하나로 읽는다. 크기·색은 레이즈 카드 값을 읽는다.
/// 주사위 몸 색은 건드리지 않는다 — 색은 공격 방식이자 얹었을 때의 결과라 가리면 끌어 보기 전까지 무슨 주사위인지 모른다.
/// </summary>
public sealed class HeroRaiseView : IDisposable
{
    private readonly SO_HeroConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly HeroRaise _heroRaise;
    private readonly AttackEffects _effects;
    private bool _wasActive;
    private Vector2Int _lastCell;

    public HeroRaiseView(SO_HeroConfig config, SO_MapViewConfig viewConfig, HeroRaise heroRaise, AttackEffects effects)
    {
        _config = config;
        _viewConfig = viewConfig;
        _heroRaise = heroRaise;
        _effects = effects;
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnLateUpdate -= LateTick;
        _effects.HideHeldRing();
    }

    private void LateTick(float _)
    {
        SO_SkillCard_Raise raise = _config.Raise;
        bool active = _heroRaise.Active && raise != null;
        if (!active)
        {
            if (_wasActive) _effects.HideHeldRing();
            _wasActive = false;
            return;
        }

        Vector2Int cell = _heroRaise.Cell;
        // 레이즈 고리처럼 주사위 밑 바닥에 그린다.
        Vector3 ground = new Vector3(cell.x, _viewConfig.GetTileTop(TileType.Ground), cell.y);
        // 새로 올라탄 순간에만 퍼지는 고리 — 카드를 쓰는 순간과 같은 모습.
        if (!_wasActive || cell != _lastCell) _effects.ShowBurst(ground, raise.BurstRadius, raise.IconColor);
        _effects.ShowHeldRing(ground, raise.RingRadius, raise.IconColor);
        _wasActive = true;
        _lastCell = cell;
    }
}
