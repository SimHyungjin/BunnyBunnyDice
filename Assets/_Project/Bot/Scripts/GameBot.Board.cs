using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>GameBot의 판 읽기 — 뚫림·메인 딜 구간을 넘어온 적·사거리 계산. 스킬·영웅 판단이 같이 쓴다.</summary>
public sealed partial class GameBot
{
    /// <summary>
    /// 메인 딜 구간이 뚫릴 것 같은지: 구간을 넘어온 적 중 체력이 넉넉히 남은 적이 BreachCount 이상이거나, 보스가 구간에 들어왔거나,
    /// 목표물 바로 앞(DangerRadius)까지 적이 왔다.
    /// </summary>
    private bool Breached(out bool bossBreaching)
    {
        bossBreaching = false;
        if (!_board.HasKillZone) return false;
        int strong = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            MonsterKind kind = _monsters.KindOf(i);
            if (kind.IsBoss && !kind.IsPiece && IsPastKillZone(states[i].Position, _config.KillZoneMargin)) bossBreaching = true;
            if (IsPastKillZone(states[i].Position, 0) && states[i].Health >= states[i].MaxHealth * _config.BreachHealthRatio) strong++;
        }
        return bossBreaching || strong >= _config.BreachCount || CountNearGoal(_config.DangerRadius) > 0;
    }

    // 목표물까지 길 거리가 메인 딜 구간 중심 + margin보다 가깝다 — 구간에 들어왔거나 넘어왔다(margin이 클수록 일찍 본다).
    private bool IsPastKillZone(Vector2 position, int margin)
    {
        int distance = _board.DistanceAt(Vector2Int.RoundToInt(position));
        return distance >= 0 && distance <= _board.KillZoneDistance + margin;
    }

    private int CountPastKillZone()
    {
        int count = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && IsPastKillZone(states[i].Position, 0)) count++;
        return count;
    }

    private bool CoversKillZone(Vector2Int cell, float range) =>
        _board.HasKillZone && ((Vector2)(_board.KillZone - cell)).sqrMagnitude <= range * range;

    // 목표물 중심에서 radius 안의 산 적 수.
    private int CountNearGoal(float radius)
    {
        MapData map = _getMap();
        if (map == null) return 0;
        Vector2 goal = map.GoalRect.center - new Vector2(0.5f, 0.5f);
        float sqr = radius * radius;
        int count = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && (states[i].Position - goal).sqrMagnitude <= sqr) count++;
        return count;
    }

    // 칸의 주사위가 colors일 때의 사거리(칩 보너스 포함 — 실제 공격과 같은 계산, ChipEffects.RangeBonus).
    private float RangeAt(Vector2Int cell, DiceColor colors, SO_ChipCard chip)
    {
        SO_DiceType type = _diceConfig.GetDiceType(colors);
        return type != null ? type.Range * (1f + ChipEffects.RangeBonus(chip)) : 0f;
    }
}
