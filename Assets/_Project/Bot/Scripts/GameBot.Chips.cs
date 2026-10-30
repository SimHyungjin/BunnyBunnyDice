using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>GameBot의 칩 카드 놓기 — 종류마다 자리가 다르다(디버프·강화·화상·사거리).</summary>
public sealed partial class GameBot
{
    // 칩은 종류마다 자리가 다르다(사용자 판 교본 2차). 모두 칩이 없는 주사위 밑에만 놓는다(빈 칸에는 놓지 않는다).
    // - 디버프(둔화·얼림·취약): 메인 딜 구간을 가장 넓게 덮는 범위 주사위 밑 — 눈은 상관없고 맞히기만 하면 된다.
    // - 빨강·초록: 메인 딜 구간 딜링 주사위 밑(몰림 × 눈). 빨강 단색 주사위엔 초록만(빨강 칩은 겹쳐 봐야 덜 는다).
    // - 화상: 미로 쪽 범위 주사위 밑 — 아직 화상이 안 닿는 길을 가장 많이 새로 덮는 곳 × 공격 속도(4초 안에 다시 맞혀야 끊기지 않는다).
    // - 사거리: 받으면 메인 딜 구간에 새로 닿는 주사위.
    private bool TryPlaceChip(MapData map, HandCard card)
    {
        SO_ChipCard chip = card.Chip;
        Vector2Int target;
        bool found;
        if (chip is SO_ChipCard_Burn) found = TryFindBurnCell(out target);
        else if (chip is SO_ChipCard_Slow || chip is SO_ChipCard_Freeze || chip is SO_ChipCard_Vulnerable) found = TryFindDebuffCell(out target);
        else if (chip is SO_ChipCard_Damage || chip is SO_ChipCard_AttackSpeed) found = TryFindBoostCell(chip, out target);
        else found = TryFindRangeCell(chip, out target);
        return found && _placement.TryPlace(map, target, card);
    }

    // 디버프: 메인 딜 구간에 닿는 범위 주사위 > 구간에 닿는 주사위 > 그 밖. 같은 등급이면 덮는 몰림이 큰 곳.
    private bool TryFindDebuffCell(out Vector2Int cell)
    {
        cell = default;
        int bestTier = -1;
        float best = -1f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            if (_chips.Get(at) != null) continue;
            float range = RangeAt(at, placed[i].Dice.Colors, default);
            int tier = CoversKillZone(at, range) ? (BotDice.IsArea(placed[i].Dice.Colors) ? 2 : 1) : 0;
            float score = _board.Traffic(at, range);
            if (tier < bestTier || (tier == bestTier && score <= best)) continue;
            bestTier = tier;
            best = score;
            cell = at;
        }
        return bestTier >= 0;
    }

    // 빨강·초록: 메인 딜 구간에 닿는 주사위 > 그 밖. 같은 등급이면 몰림 × (1 + 눈), 범위 주사위면 더.
    private bool TryFindBoostCell(SO_ChipCard chip, out Vector2Int cell)
    {
        cell = default;
        int bestTier = -1;
        float best = -1f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            DiceState dice = placed[i].Dice;
            if (_chips.Get(at) != null) continue;
            if (chip is SO_ChipCard_Damage && dice.Colors == DiceColor.Red) continue;
            float range = RangeAt(at, dice.Colors, default);
            int tier = CoversKillZone(at, range) ? 1 : 0;
            float score = _board.Traffic(at, range) * (1f + dice.Pips) * (BotDice.IsArea(dice.Colors) ? _config.AreaStackWeight : 1f);
            if (tier < bestTier || (tier == bestTier && score <= best)) continue;
            bestTier = tier;
            best = score;
            cell = at;
        }
        return bestTier >= 0;
    }

    // 화상: 범위 주사위 > 단일, 메인 딜 구간 밖 > 안(사용자: 화상은 미로 쪽 — 구간에선 중요하지 않다).
    // 같은 등급이면 아직 화상 주사위가 안 덮는 길 칸 수 × 공격 속도 — 화상 칩이 여럿이면 길 전체에 고르게 퍼진다.
    private bool TryFindBurnCell(out Vector2Int cell)
    {
        cell = default;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        _burned.Clear();
        for (int i = 0; i < placed.Count; i++)
        {
            if (!_chips.GetSet(placed[i].Cell).Contains<SO_ChipCard_Burn>()) continue;
            float range = RangeAt(placed[i].Cell, placed[i].Dice.Colors, _chips.GetSet(placed[i].Cell));
            MarkRoute(placed[i].Cell, range, _burned);
        }

        int bestTier = -1;
        float best = -1f;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            DiceState dice = placed[i].Dice;
            if (_chips.Get(at) != null) continue;
            float range = RangeAt(at, dice.Colors, default);
            int tier = (BotDice.IsArea(dice.Colors) ? 2 : 0) + (CoversKillZone(at, range) ? 0 : 1);
            DiceStats stats = DiceStats.Compute(dice, _diceConfig.GetDiceType(dice.Colors), _statsConfig);
            float speed = stats.AttackInterval > 0f ? 1f / stats.AttackInterval : 0f;
            float score = NewRouteCells(at, range, _burned) * speed;
            if (tier < bestTier || (tier == bestTier && score <= best)) continue;
            bestTier = tier;
            best = score;
            cell = at;
        }
        return bestTier >= 0;
    }

    private readonly HashSet<Vector2Int> _burned = new HashSet<Vector2Int>();

    // 화상 칩을 깔 자리: 칩 없는 범위 주사위 중 아직 화상 주사위가 안 덮는 길을 새로 덮는 것이 있다.
    private bool HasBurnSpot()
    {
        return TryFindBurnCell(out Vector2Int cell) && _diceField.TryGetPlaced(cell, out PlacedDice placed)
            && BotDice.IsArea(placed.Dice.Colors) && NewRouteCells(cell, RangeAt(cell, placed.Dice.Colors, default), _burned) > 0;
    }

    private void MarkRoute(Vector2Int center, float range, HashSet<Vector2Int> marked)
    {
        float sqr = range * range;
        IReadOnlyList<Vector2Int> route = _board.Route;
        for (int i = 0; i < route.Count; i++)
            if (((Vector2)(route[i] - center)).sqrMagnitude <= sqr) marked.Add(route[i]);
    }

    private int NewRouteCells(Vector2Int center, float range, HashSet<Vector2Int> marked)
    {
        float sqr = range * range;
        int count = 0;
        IReadOnlyList<Vector2Int> route = _board.Route;
        for (int i = 0; i < route.Count; i++)
            if (!marked.Contains(route[i]) && ((Vector2)(route[i] - center)).sqrMagnitude <= sqr) count++;
        return count;
    }

    // 사거리: 칩을 받으면 메인 딜 구간에 새로 닿는 주사위를 가장 크게 쳐 준다 — 사거리로 구간을 때리게 유도한다.
    private bool TryFindRangeCell(SO_ChipCard chip, out Vector2Int cell)
    {
        cell = default;
        float bonus = _chips.Preview(chip).RangeBonus;
        float best = -1f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            if (_chips.Get(at) != null) continue;
            float range = RangeAt(at, placed[i].Dice.Colors, default);
            float boosted = range * (1f + bonus);
            float score = _board.Traffic(at, boosted) * (1f + placed[i].Dice.Pips);
            if (CoversKillZone(at, boosted))
                score *= !CoversKillZone(at, range) ? _config.KillZoneChipWeight * 2f : _config.KillZoneChipWeight;
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best >= 0f;
    }
}
