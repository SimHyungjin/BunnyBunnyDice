using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>GameBot의 주사위 카드 놓기 — 미로(계획 칸)와 메인 딜 구간(d20), 얹기 자리 고르기.</summary>
public sealed partial class GameBot
{
    // d20은 메인 딜 구간 쪽, d6·d12는 미로(사용자). 생각 중(판이 바뀐 뒤 새 계획을 아직 받지 않음)이면 주사위는 쥐고 있는다 — 칩·스킬은 그대로 쓴다.
    private bool TryPlaceDice(MapData map, HandCard card)
    {
        DiceCard dice = card.Dice;
        if (UsesKillBox()) return TryPlaceKillBox(map, card, dice);
        if (_planner.Busy) return false;
        if (dice.Faces >= 20)
        {
            // 각성 전 빨강·초록·하양 d20에 먼저 모은다 → 메인 딜 구간의 빈 칸 → 구간 딜링 주사위에 얹기 → 그것도 없으면 미로 계획 칸.
            if (_config.StackForAwaken && TryFindAwakenTarget(dice, false, out Vector2Int core)) return _placement.TryPlace(map, core, card);
            if (TryFindZoneCell(map, dice, out Vector2Int zone)) return _placement.TryPlace(map, zone, card);
            if (TryFindStack(dice, true, out Vector2Int zoneStack)) return _placement.TryPlace(map, zoneStack, card);
            if (_planner.TryGetNext(null, out Vector2Int any)) return _placement.TryPlace(map, any, card);
            return TryFindOpenCell(map, dice, out Vector2Int open20) && _placement.TryPlace(map, open20, card);
        }
        // 미로를 다 지은 뒤엔 메인 딜 구간 딜링 주사위를 키운다(사용자). 구간에 얹을 곳이 없으면 어디든 가장 이득인 곳.
        if (MazeBuilt())
        {
            if (TryFindStack(dice, true, out Vector2Int stack) || TryFindStack(dice, false, out stack)) return _placement.TryPlace(map, stack, card);
            return TryFindOpenCell(map, dice, out Vector2Int openBuilt) && _placement.TryPlace(map, openBuilt, card);
        }
        // 목표물의 세 면을 먼저 막는다(사용자) — 미로는 거기서 뻗어 나간다.
        if (_config.GoalWallFirst && TryFindGoalWallCell(map, out Vector2Int wall)) return _placement.TryPlace(map, wall, card);
        // 작은 주사위라도 빨강·초록·하양 d20을 각성시키는 자리면 미로보다 먼저(사용자: 초반 각성이 판을 가른다).
        if (_config.StackForAwaken && TryFindAwakenTarget(dice, !_config.FeedAwakenEarly, out Vector2Int finish)) return _placement.TryPlace(map, finish, card);
        // 미로 칸은 메인 딜 구간 밖부터 — 구간 칸은 d20 자리로 남긴다. 밖에 남은 계획 칸이 없으면 아무 계획 칸.
        if (_config.MazeNearGoalFirst)
        {
            Vector2 goal = map.GoalRect.center - new Vector2(0.5f, 0.5f);
            Func<Vector2Int, bool> notGap = _config.GoalWallFirst ? c => !IsGoalRingGap(map, c) : (Func<Vector2Int, bool>)null;
            if (_planner.TryGetNextBy(notGap, c => (c - goal).sqrMagnitude, out Vector2Int near)) return _placement.TryPlace(map, near, card);
        }
        Func<Vector2Int, bool> allow = _config.MazeOutsideZoneFirst ? IsOutsideZone : null;
        if (_planner.TryGetNext(allow, out Vector2Int cell) || _planner.TryGetNext(null, out cell))
            return _placement.TryPlace(map, cell, card);
        return TryFindOpenCell(map, dice, out Vector2Int open) && _placement.TryPlace(map, open, card);
    }

    // 계획 칸도 얹을 곳도 없을 때: 길을 막지 않는 빈 칸 중 그 색 사거리로 덮는 몰림이 가장 큰 칸.
    // 계획이 비면(벽이 바뀐 뒤 다시 찾은 계획에 남은 칸이 없음) 주사위를 영영 쥐고만 있어 판이 무너졌다(2026-10-29, 시드 70030003 — 고른 주사위 11개 중 7개만 놓음).
    private bool TryFindOpenCell(MapData map, DiceCard dice, out Vector2Int cell)
    {
        cell = default;
        SO_DiceType type = _diceConfig.GetDiceType(dice.Colors);
        float range = type != null ? type.Range : 0f;
        int best = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (map.HasChip(x, y) || DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
                if (_config.GoalWallFirst && IsGoalRingGap(map, candidate)) continue;
                int traffic = _board.Traffic(candidate, range);
                if (traffic <= best) continue;
                best = traffic;
                cell = candidate;
            }
        }
        return best > 0;
    }

    private bool IsInZone(Vector2Int cell) =>
        _board.HasKillZone && ((Vector2)(cell - _board.KillZone)).sqrMagnitude <= _config.KillZoneRadius * _config.KillZoneRadius;

    private bool IsOutsideZone(Vector2Int cell) => !IsInZone(cell);

    // d20을 놓을 메인 딜 구간의 빈 칸: 그 색 사거리로 덮는 몰림이 가장 큰 칸. 미로 계획 밖이어도 놓는다
    // (사용자는 d20을 구간에 최대한 모은다 — 길을 막지 않는지만 본다, 계획은 MazePlanner가 다시 찾는다).
    private bool TryFindZoneCell(MapData map, DiceCard dice, out Vector2Int cell)
    {
        cell = default;
        if (!_board.HasKillZone) return false;
        SO_DiceType type = _diceConfig.GetDiceType(dice.Colors);
        float range = type != null ? type.Range : 0f;
        int radius = Mathf.CeilToInt(_config.KillZoneRadius);
        float best = 0f;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int candidate = _board.KillZone + new Vector2Int(x, y);
                if (!IsInZone(candidate) || !map.InBounds(candidate.x, candidate.y)) continue;
                if (DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
                // 목표물 입구·모서리는 비워 둔다(세 면 벽 — 사용자).
                if (_config.GoalWallFirst && IsGoalRingGap(map, candidate)) continue;
                float score = _board.Traffic(candidate, range);
                if (score <= best) continue;
                best = score;
                cell = candidate;
            }
        }
        return best > 0f;
    }

    // 얹을 주사위: 얹은 뒤의 모습으로 점수를 매긴다 — 몰림 × (1 + 기대 눈), 범위 주사위(섞인 색·하양)가 되면 더,
    // 메인 딜 구간에 닿으면 더 쳐 준다. 사용자는 메인 딜 구간의 범위 주사위를 키웠다. zoneOnly면 구간에 닿는 주사위만.
    private bool TryFindStack(DiceCard card, bool zoneOnly, out Vector2Int cell)
    {
        cell = default;
        float best = 0f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            if (!DiceRules.CanStack(dice, card)) continue;
            DiceState after = dice;
            DiceRules.Stack(ref after, card, (card.Faces + 1) / 2);
            if (after.Pips <= dice.Pips) continue;
            Vector2Int at = placed[i].Cell;
            float range = RangeAt(at, after.Colors, _chips.GetSet(at));
            if (zoneOnly && !CoversKillZone(at, range)) continue;
            float score = _board.Traffic(at, range) * (1f + after.Pips) * ColorWeight(after.Colors)
                * (!_statsConfig.IsAwakened(dice) && _statsConfig.IsAwakened(after) ? _config.AwakenStackWeight : 1f)
                * (CoversKillZone(at, range) ? _config.KillZoneStackWeight : 1f);
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best > 0f;
    }

    // 얹은 뒤 색의 배율: 빨강·초록·하양(초반 각성의 주인공) > 섞인 두 색(범위) > 단색, 파랑은 낮게.
    private float ColorWeight(DiceColor colors)
    {
        if (colors == DiceColors.All) return _config.WhiteColorWeight;
        if (BotDice.IsCoreAwaken(colors)) return _config.CoreColorWeight;
        if (colors == DiceColor.Blue) return _config.BlueColorWeight;
        return BotDice.IsArea(colors) ? _config.AreaStackWeight : 1f;
    }

    // 각성 전인 빨강·초록·하양 주사위 중 얹은 뒤 d20이고 그 색(또는 하양)으로 남는 것 — 눈이 가장 많은(각성에 가까운) 것, 같으면 몰림이 큰 것.
    // d20 카드는 d6·d12 위에도 얹는다: 면이 d20으로 커지며 눈이 합쳐져 빈 칸에 새로 놓는 것보다 각성이 훨씬 빠르다
    // (초반엔 d20이 드물어 d20끼리만 모으면 모을 곳이 없었다 — 2026-11-02 봇 비교).
    // finishOnly면 이번에 얹어 바로 각성하는 것만(미로를 짓는 동안의 d6·d12 — 미로를 미루는 값어치가 있을 때만).
    private bool TryFindAwakenTarget(DiceCard card, bool finishOnly, out Vector2Int cell)
    {
        cell = default;
        int bestPips = -1;
        int bestTraffic = 0;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            if (Mathf.Max(dice.Faces, card.Faces) < _statsConfig.AwakenPips || _statsConfig.IsAwakened(dice) || !BotDice.IsCoreAwaken(dice.Colors)) continue;
            if (!DiceRules.CanStack(dice, card)) continue;
            DiceState after = dice;
            DiceRules.Stack(ref after, card, (card.Faces + 1) / 2);
            if (!BotDice.IsCoreAwaken(after.Colors)) continue;
            // 굴림은 운이라 기대 눈(면 수의 절반)으로 본다 — 못 미치면 미로만 늦어진다.
            if (finishOnly && !_statsConfig.IsAwakened(after)) continue;
            Vector2Int at = placed[i].Cell;
            int traffic = _board.Traffic(at, RangeAt(at, after.Colors, _chips.GetSet(at)));
            if (traffic <= 0) continue;
            if (dice.Pips < bestPips || (dice.Pips == bestPips && traffic <= bestTraffic)) continue;
            bestPips = dice.Pips;
            bestTraffic = traffic;
            cell = at;
        }
        return bestPips >= 0;
    }

    // ── 딜 몰빵 구역(BotKillBox) ──
    // 작은 주사위: 길을 모을 막을 칸 → 구역의 각성 전 d20에 보태기 → 구역 바깥쪽 빈 칸 → 아무 데나.
    // d20: 구역의 각성 전 d20 중 눈이 가장 높은 것에 얹기(한 칸에 거듭 얹어 각성 — 사용자) → 구역 빈 칸(중심 가까운 순) → 구역 주사위에 얹기.
    private bool TryPlaceKillBox(MapData map, HandCard card, DiceCard dice)
    {
        if (dice.Faces < 20)
        {
            if (TryFindKillBoxCut(map, out Vector2Int cut)) return _placement.TryPlace(map, cut, card);
            // 길을 모은 뒤엔 딜 점수를 가장 많이 올리는 자리(디버프 묻은 적에게 박히는 피해 — 사용자).
            if (_config.KillBoxScorePlace && TryPlaceByScore(map, card, out Vector2Int scored)) return _placement.TryPlace(map, scored, card);
            // 길을 모은 뒤엔 구역 안에서 적이 더 오래 돌게(사용자) — 머무는 길을 늘리는 칸이 있으면 먼저.
            if (TryFindDwellCell(map, out Vector2Int dwell)) return _placement.TryPlace(map, dwell, card);
            if (TryFindZoneD20(dice, out Vector2Int feed)) return _placement.TryPlace(map, feed, card);
            if (TryFindZoneOpen(map, false, out Vector2Int outer)) return _placement.TryPlace(map, outer, card);
            return TryFindOpenCell(map, dice, out Vector2Int open) && _placement.TryPlace(map, open, card);
        }
        if (_config.KillBoxScorePlace && TryPlaceByScore(map, card, out Vector2Int best20)) return _placement.TryPlace(map, best20, card);
        if (TryFindZoneD20(dice, out Vector2Int stack)) return _placement.TryPlace(map, stack, card);
        if (TryFindZoneOpen(map, true, out Vector2Int inner)) return _placement.TryPlace(map, inner, card);
        if (TryFindStack(dice, true, out Vector2Int any)) return _placement.TryPlace(map, any, card);
        return TryFindOpenCell(map, dice, out Vector2Int rest) && _placement.TryPlace(map, rest, card);
    }

    // 구역 둘레 빈 칸 중 막았을 때 머무는 길(구역 안 걷는 칸 수)이 가장 많이 느는 칸. 늘지 않으면 false.
    private bool TryFindDwellCell(MapData map, out Vector2Int cell)
    {
        cell = default;
        float radius = _config.KillBoxRadius;
        int now = BotKillBox.Dwell(map, null, null, _killBox.Center, radius);
        if (now < 0) return false;
        int best = now;
        List<Vector2Int> zone = _killBox.Zone;
        for (int i = 0; i < zone.Count; i++)
        {
            Vector2Int candidate = zone[i];
            if (DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
            int after = BotKillBox.Dwell(map, null, candidate, _killBox.Center, radius);
            if (after <= best) continue;
            best = after;
            cell = candidate;
        }
        return best > now;
    }

    private bool KillBoxCutDone()
    {
        MapData map = _getMap();
        return map == null || !TryFindKillBoxCut(map, out _);
    }

    private bool TryFindKillBoxCut(MapData map, out Vector2Int cell)
    {
        cell = default;
        List<Vector2Int> cut = _killBox.Cut;
        for (int i = 0; i < cut.Count; i++)
        {
            if (DicePlacement.Check(map, cut[i]) != PlacementResult.Ok) continue;
            cell = cut[i];
            return true;
        }
        return false;
    }

    // 구역 안 각성 전 d20 중 얹어서 눈이 오르는 것 — 눈이 가장 높은(각성에 가까운) 것. 색은 가리지 않는다(사용자는 구역 한 칸에 거듭 얹는다).
    private bool TryFindZoneD20(DiceCard card, out Vector2Int cell)
    {
        cell = default;
        int bestPips = -1;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            if (dice.Faces < _statsConfig.AwakenPips || _statsConfig.IsAwakened(dice) || !_killBox.Zone.Contains(placed[i].Cell)) continue;
            if (!DiceRules.CanStack(dice, card)) continue;
            DiceState after = dice;
            DiceRules.Stack(ref after, card, (card.Faces + 1) / 2);
            if (after.Pips <= dice.Pips || dice.Pips <= bestPips) continue;
            bestPips = dice.Pips;
            cell = placed[i].Cell;
        }
        return bestPips >= 0;
    }

    // 구역의 빈 칸: inner면 중심 가까운 칸부터(d20 자리), 아니면 바깥쪽부터(작은 주사위가 d20 자리를 차지하지 않게).
    private bool TryFindZoneOpen(MapData map, bool inner, out Vector2Int cell)
    {
        cell = default;
        List<Vector2Int> zone = _killBox.Zone;
        for (int k = 0; k < zone.Count; k++)
        {
            Vector2Int candidate = zone[inner ? k : zone.Count - 1 - k];
            if (DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
            cell = candidate;
            return true;
        }
        return false;
    }
}
