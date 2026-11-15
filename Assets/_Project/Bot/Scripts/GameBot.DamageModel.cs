using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameBot의 딜 점수 — "디버프를 묻힌 적에게 온전하게 많은 딜을 최대한 박을 수 있는지"(2026-11-03 사용자)를 숫자로 옮긴 것.
/// 출현 차례마다 적이 걷는 길(4방향 가장 짧은 길)을 따라가며, 칸마다 그 칸을 사거리에 넣는 주사위들의 초당 피해를 더한다.
/// 취약 칩 주사위의 사거리를 지나면 그 뒤 몇 칸(칩 지속 시간 × 적 걸음)은 피해 ×(1 + 취약), 느림·얼림이면 그 칸에 머무는 시간이 늘어 그만큼 곱한다.
/// 길이 길든 짧든 상관없다 — 디버프 걸린 적이 딜 사거리 안에 오래, 많은 주사위에 맞으며 지나갈수록 크다.
/// 놓기 후보마다 "놓았다고 치고" 다시 매겨 가장 많이 오르는 곳을 고른다(TryPlaceByScore). 판이 작아(15×8, 주사위 수십 개) 후보마다 다시 매겨도 가볍다.
/// </summary>
public sealed partial class GameBot
{
    private struct Tower
    {
        public Vector2Int Cell;
        public float Range;
        public float Power;
        public float Vulnerable, VulnerableCells;
        public float Slow, SlowCells;
        public float Freeze;
    }

    private readonly List<Tower> _towers = new List<Tower>();
    private readonly List<Vector2Int> _noBlock = new List<Vector2Int>();

    /// <summary>
    /// 지금 판 + 가정(extraCell에 주사위 extraDice를 놓거나 얹었다, extraChip을 깔았다)의 딜 점수. 길이 막히면 -1.
    /// </summary>
    private float DamageScore(MapData map, Vector2Int? extraCell, DiceState? extraDice, SO_ChipCard extraChip)
    {
        BuildTowers(extraCell, extraDice, extraChip);
        // 새로 놓는 칸(빈 칸)만 길을 막는다 — 얹기·칩(주사위 밑)은 벽이 그대로다.
        bool newWall = extraCell.HasValue && !_diceField.TryGetPlaced(extraCell.Value, out _);
        int[] distance = BotKillBox.DistanceFieldOf(map, _noBlock, newWall ? extraCell : null);
        float total = 0f;
        float enemyCellsPerSecond = Mathf.Max(_config.ModelEnemySpeed, 0.1f);
        for (int g = 0; g < map.SpawnGates.Count; g++)
        {
            Vector2Int cell = map.SpawnGates[g];
            int d = distance[map.ToIndex(cell.x, cell.y)];
            if (d < 0) return -1f;
            float vulnerableLeft = 0f, vulnerable = 0f, slowLeft = 0f, slow = 0f;
            while (d > 0)
            {
                float power = 0f;
                float freeze = 1f;
                for (int t = 0; t < _towers.Count; t++)
                {
                    Tower tower = _towers[t];
                    if (((Vector2)(cell - tower.Cell)).sqrMagnitude > tower.Range * tower.Range) continue;
                    power += tower.Power;
                    if (tower.Vulnerable > 0f) { vulnerable = Mathf.Max(vulnerable, tower.Vulnerable); vulnerableLeft = Mathf.Max(vulnerableLeft, tower.VulnerableCells); }
                    if (tower.Slow > 0f) { slow = Mathf.Max(slow, tower.Slow); slowLeft = Mathf.Max(slowLeft, tower.SlowCells); }
                    if (tower.Freeze > 1f) freeze = Mathf.Max(freeze, tower.Freeze);
                }
                // 이 칸에 머무는 시간(초): 한 칸을 걷는 시간 × 느림·얼림 배율.
                float seconds = 1f / enemyCellsPerSecond * (slowLeft > 0f ? 1f / Mathf.Max(1f - slow, 0.1f) : 1f) * freeze;
                total += power * seconds * (vulnerableLeft > 0f ? 1f + vulnerable : 1f);
                if (vulnerableLeft > 0f) vulnerableLeft -= 1f;
                if (slowLeft > 0f) slowLeft -= 1f;

                Vector2Int next = cell;
                for (int k = 0; k < Directions4.Length; k++)
                {
                    Vector2Int candidate = cell + Directions4[k];
                    if (!map.InBounds(candidate.x, candidate.y) || distance[map.ToIndex(candidate.x, candidate.y)] != d - 1) continue;
                    next = candidate;
                    break;
                }
                if (next == cell) break;
                cell = next;
                d--;
            }
        }
        return total;
    }

    private static readonly Vector2Int[] Directions4 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    private void BuildTowers(Vector2Int? extraCell, DiceState? extraDice, SO_ChipCard extraChip)
    {
        _towers.Clear();
        bool extraUsed = false;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            DiceState dice = placed[i].Dice;
            SO_ChipCard chip = _chips.Get(at);
            float strength = chip != null ? _chips.GetSet(at).Strength : 1f;
            if (extraCell.HasValue && extraCell.Value == at)
            {
                extraUsed = true;
                if (extraDice.HasValue) dice = extraDice.Value;
                if (extraChip != null) { chip = extraChip; strength = 1f; }
            }
            if (dice.IsSpent) continue;
            _towers.Add(MakeTower(at, dice, chip, strength));
        }
        if (!extraUsed && extraCell.HasValue && extraDice.HasValue)
            _towers.Add(MakeTower(extraCell.Value, extraDice.Value, null, 1f));
    }

    // 주사위 하나의 초당 피해(고정 + 최대 체력 피해 × 기준 체력) — 칩(공격력·공속)·빨강 확정 치명(각성 배율 포함)·초록 각성 공속 포함. 디버프 칩이면 디버프 값도 담는다.
    private Tower MakeTower(Vector2Int cell, DiceState dice, SO_ChipCard chip, float strength)
    {
        Tower tower = new Tower { Cell = cell };
        SO_DiceType type = _diceConfig.GetDiceType(dice.Colors);
        if (type == null) return tower;
        DiceStats stats = DiceStats.Compute(dice, type, _statsConfig);
        float rangeBonus = 0f, damageBonus = 0f, speedBonus = 0f;
        if (chip != null)
        {
            rangeBonus = ChipEffects.RangeBonus(chip) * strength;
            damageBonus = ChipEffects.DamageBonus(chip) * strength;
            speedBonus = ChipEffects.AttackSpeedBonus(chip) * strength;
            float cells = Mathf.Max(_config.ModelEnemySpeed, 0.1f);
            if (chip is SO_ChipCard_Vulnerable vulnerable) { tower.Vulnerable = vulnerable.Ratio * strength; tower.VulnerableCells = vulnerable.Duration * cells; }
            else if (chip is SO_ChipCard_Slow slow) { tower.Slow = Mathf.Clamp01(slow.Ratio * strength); tower.SlowCells = slow.Duration * cells; }
            else if (chip is SO_ChipCard_Freeze freeze) tower.Freeze = 1f + freeze.Duration * strength / Mathf.Max(freeze.Duration + freeze.Immunity, 0.1f);
        }
        tower.Range = stats.GetRange(rangeBonus);
        float hit = (stats.Damage + stats.MaxHealthRatio * _config.ModelRefHealth) * (1f + damageBonus);
        if (type is SO_DiceType_Snipe snipe) hit *= _statsConfig.CritMultiplier + (_statsConfig.IsAwakened(dice) ? snipe.AwakenCritBonus : 0f);
        float interval = stats.AttackInterval / (1f + speedBonus);
        tower.Power = interval > 0f ? hit / interval : 0f;
        return tower;
    }

    // 놓기 후보 중 딜 점수를 가장 많이 올리는 자리. 오르지 않으면 false.
    // 새 칸 후보는 딜 구역 중심에서 SearchRadius 안(그 밖은 딜 구역 몰빵과 상관이 적다), 얹기 후보는 판 위 모든 주사위.
    private bool TryPlaceByScore(MapData map, HandCard card, out Vector2Int cell)
    {
        cell = default;
        float now = DamageScore(map, null, null, null);
        if (now < 0f) return false;
        float best = now;
        float radius = _config.KillBoxRadius + _config.ModelSearchMargin;
        int r = Mathf.CeilToInt(radius);
        Vector2Int center = _killBox.Center;
        if (card.IsDice)
        {
            DiceCard dice = card.Dice;
            int roll = (dice.Faces + 1) / 2;
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    Vector2Int candidate = center + new Vector2Int(x, y);
                    if (x * x + y * y > radius * radius || !map.InBounds(candidate.x, candidate.y)) continue;
                    if (DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
                    DiceState created = DiceRules.Create(dice, roll);
                    float score = DamageScore(map, candidate, created, null);
                    if (score <= best) continue;
                    best = score;
                    cell = candidate;
                }
            }
            IReadOnlyList<PlacedDice> placed = _diceField.Placed;
            for (int i = 0; i < placed.Count; i++)
            {
                DiceState after = placed[i].Dice;
                if (!DiceRules.CanStack(after, dice)) continue;
                DiceRules.Stack(ref after, dice, roll);
                float score = DamageScore(map, placed[i].Cell, after, null);
                if (score <= best) continue;
                best = score;
                cell = placed[i].Cell;
            }
        }
        else if (card.IsChip)
        {
            // 칩은 주사위 밑에만(빈 칸 칩은 길을 막는 벽이라 점수 계산과 맞지 않는다).
            IReadOnlyList<PlacedDice> placed = _diceField.Placed;
            for (int i = 0; i < placed.Count; i++)
            {
                Vector2Int at = placed[i].Cell;
                if (!DicePlacement.IsAccepted(_placement.Check(map, at, card))) continue;
                float score = DamageScore(map, at, null, card.Chip);
                if (score <= best) continue;
                best = score;
                cell = at;
            }
        }
        return best > now;
    }
}
