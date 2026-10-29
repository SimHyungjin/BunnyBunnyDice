using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>GameBot의 주사위 카드 놓기 — 미로(계획 칸)와 메인 딜 구간(d20), 얹기 자리 고르기.</summary>
public sealed partial class GameBot
{
    // d20은 메인 딜 구간 쪽, d6·d12는 미로(사용자). 생각 중(판이 바뀐 뒤 새 계획을 아직 받지 않음)이면 주사위는 쥐고 있는다 — 칩·스킬은 그대로 쓴다.
    private bool TryPlaceDice(MapData map, HandCard card)
    {
        if (_planner.Busy) return false;
        DiceCard dice = card.Dice;
        if (dice.Faces >= 20)
        {
            // 메인 딜 구간의 빈 칸 → 구간 딜링 주사위에 얹기 → 그것도 없으면 미로 계획 칸.
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
        // 미로 칸은 메인 딜 구간 밖부터 — 구간 칸은 d20 자리로 남긴다. 밖에 남은 계획 칸이 없으면 아무 계획 칸.
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
            float range = RangeAt(at, after.Colors, _chips.Get(at));
            if (zoneOnly && !CoversKillZone(at, range)) continue;
            float score = _board.Traffic(at, range) * (1f + after.Pips)
                * (BotDice.IsArea(after.Colors) ? _config.AreaStackWeight : 1f)
                * (CoversKillZone(at, range) ? _config.KillZoneStackWeight : 1f);
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best > 0f;
    }
}
