using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>GameBot의 영웅 이동 — 레이즈로 올라탈 주사위, 정리할 적, 디버프를 묻힐 자리.</summary>
public sealed partial class GameBot
{
    // 영웅(사용자 판 교본 2차):
    // 1) 빠져나갈 적을 레이즈로 잡을 수 있으면 그 주사위에 올라탄다(BotEscape) — 올라탄 동안 눈이 영구로 깎이니 그때만.
    // 2) 미로를 짓는 동안엔 주사위가 못 잡은 적을 쫓아 정리한다.
    // 3) 미로를 지은 뒤엔 메인 딜 구간 앞부터 디버프가 묻는 구역까지 오가며 싸운다 — 구역을 지난(뚫린) 적이 있으면 그쪽부터,
    //    없으면 구역 안에서 가장 앞선 적, 적이 없으면 구역 앞에서 기다린다. 영웅이 주 딜이라 취약(+30%)이 묻는 곳에서 때려야 한다.
    private void MoveHero(MapData map)
    {
        if (_hero.Held || !_board.Read(map, _config.KillZoneRadius)) return;
        SO_SkillCard_Raise raise = _heroRaise();
        float damageScale = 1f, speedScale = 1f;
        raise?.GetScales(_traits, out damageScale, out speedScale);
        if (raise != null && FindRaiseTarget(damageScale, speedScale, float.MaxValue, false, out Vector2Int ride))
        {
            if (_hasHeroCell && ride == _heroCell) return;
            if (_hero.MoveTo(ride))
            {
                _hasHeroCell = true;
                _heroCell = ride;
            }
            return;
        }

        if (!MazeBuilt())
        {
            MoveHeroTo(TryFindLead(0, int.MaxValue, out Vector2Int lead) ? lead : ZoneFrontCell(map));
            return;
        }

        int back = _board.KillZoneDistance - _config.KiteBack;
        int front = _board.KillZoneDistance + _config.KiteFront;
        if (TryFindLead(0, back - 1, out Vector2Int breach)) MoveHeroTo(breach);
        else if (TryFindLead(back, front, out Vector2Int kite)) MoveHeroTo(kite);
        else MoveHeroTo(ZoneFrontCell(map));
    }

    // 주사위에서 내려 cell로 간다(길 칸이라 주사위가 없다).
    private void MoveHeroTo(Vector2Int cell)
    {
        if (_hero.MoveTo(cell)) _hasHeroCell = false;
    }

    // 목표물까지 길 거리가 [min, max]인 산 적 중 가장 앞선(목표물에 가까운) 적이 선 칸. 주사위 칸에 선 적은 뺀다(올라타지 않게).
    private bool TryFindLead(int min, int max, out Vector2Int cell)
    {
        cell = default;
        int lead = int.MaxValue;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            Vector2Int at = Vector2Int.RoundToInt(states[i].Position);
            int distance = _board.DistanceAt(at);
            if (distance < min || distance > max || distance >= lead || _diceField.TryGetPlaced(at, out _)) continue;
            lead = distance;
            cell = at;
        }
        return lead != int.MaxValue;
    }

    // 메인 딜 구간 앞(출현 쪽으로 KiteFront칸)의 길 칸 — 적이 없을 때 기다리는 곳. 구간이 없으면 목표물 앞.
    private Vector2Int ZoneFrontCell(MapData map)
    {
        if (!_board.HasKillZone) return new Vector2Int(map.GoalRect.xMin, map.GoalRect.yMin);
        int target = _board.KillZoneDistance + _config.KiteFront;
        Vector2Int best = _board.KillZone;
        int bestGap = int.MaxValue;
        IReadOnlyList<Vector2Int> route = _board.Route;
        for (int i = 0; i < route.Count; i++)
        {
            int gap = Mathf.Abs(_board.DistanceAt(route[i]) - target);
            if (gap >= bestGap) continue;
            bestGap = gap;
            best = route[i];
        }
        return best;
    }
}
