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
        if (_hero.Held || !ReadBoard(map)) return;
        // 핫 스트릭: 켜진 동안 밟은 주사위마다 레이즈가 걸린다 — 아직 안 걸린 센 주사위를 가까운 순으로 밟고 다닌다.
        if (_config.HotStreakHop && _skills.HotStreakActive)
        {
            if (FindHotStreakStep(out Vector2Int step) && _hero.MoveTo(step))
            {
                _hasHeroCell = true;
                _heroCell = step;
            }
            return;
        }
        SO_SkillCard_Raise raise = _heroRaise();
        float damageScale = 1f, speedScale = 1f;
        raise?.GetScales(_traits, out damageScale, out speedScale);
        // 각성한 빨강·초록 사거리 안에 적이 오면 바로 올라탄다(사용자) — 보스가 들어온 주사위가 먼저.
        if (raise != null && _config.RideAwakenedInRange && FindAwakenedRide(out Vector2Int awakenedRide))
        {
            if (_hasHeroCell && awakenedRide == _heroCell) return;
            if (_hero.MoveTo(awakenedRide))
            {
                _hasHeroCell = true;
                _heroCell = awakenedRide;
            }
            return;
        }
        // 보스 정리: 보스를 사거리에 넣는 가장 센 주사위(각성한 빨강·초록 먼저)에 올라타 레이즈를 건다(사용자).
        if (raise != null && _config.BossFocus && TryFindBossInReach(out Vector2 bossAt) && FindBossDice(bossAt, false, out Vector2Int bossRide))
        {
            if (_hasHeroCell && bossRide == _heroCell) return;
            if (_hero.MoveTo(bossRide))
            {
                _hasHeroCell = true;
                _heroCell = bossRide;
            }
            return;
        }
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

    // 핫 스트릭으로 다음에 밟을 주사위: 레이즈가 안 걸린 주사위 중 (눈 × 색 × 각성 × 몰림) ÷ (1 + 거리 × 몫)이 가장 큰 곳.
    private bool FindHotStreakStep(out Vector2Int cell)
    {
        cell = default;
        float best = 0f;
        Vector2 from = _hero.Position;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            Vector2Int at = placed[i].Cell;
            if (dice.IsSpent || _skills.IsDiceRaised(at)) continue;
            float range = RangeAt(at, dice.Colors, _chips.GetSet(at));
            float value = (1f + dice.Pips) * ColorWeight(dice.Colors) * (_statsConfig.IsAwakened(dice) ? _config.AwakenStackWeight : 1f)
                * (1f + _board.Traffic(at, range));
            float score = value / (1f + Vector2.Distance(from, at) * _config.HotStreakDistanceCost);
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best > 0f;
    }

    // 각성한 빨강·초록 중 사거리 안 적이 가장 많은 주사위(보스 하나는 20마리로 친다 — 보스가 들어온 주사위가 먼저). 사거리 안에 적이 없으면 false.
    private bool FindAwakenedRide(out Vector2Int cell)
    {
        const float bossWeight = 20f;
        cell = default;
        float best = 0f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            if (dice.IsSpent || !_statsConfig.IsAwakened(dice) || (dice.Colors != DiceColor.Red && dice.Colors != DiceColor.Green)) continue;
            Vector2Int at = placed[i].Cell;
            float range = RangeAt(at, dice.Colors, _chips.GetSet(at));
            float sqr = range * range;
            float score = 0f;
            for (int m = 0; m < _monsters.Count; m++)
            {
                if (!states[m].IsAlive || (states[m].Position - (Vector2)at).sqrMagnitude > sqr) continue;
                score += _monsters.KindOf(m).IsBoss ? bossWeight : 1f;
            }
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best > 0f;
    }
}
