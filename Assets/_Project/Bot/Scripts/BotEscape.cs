using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레이즈를 쓸지 정하는 어림(기획서 11.4 — 사용자 판 교본 2차). 사용자: "메인 딜 구간을 벗어날 것 같고, 목표물에 닿기 전에
/// 레이즈를 걸면 남은 주사위로 잡을 수 있을 것 같을 때" 쓴다 — 수치가 아니라 판 상태로 판단하므로 그 판단을 계산으로 옮겼다.
///
/// 적마다: 목표물까지 남은 길에서 받을 피해 = 주사위마다 (초당 피해 × 그 주사위 사거리 안 남은 길 칸 수 ÷ 적 이속)의 합.
/// 이것이 남은 체력보다 작으면 빠져나갈 적이다. 빠져나갈 적 중, 어떤 주사위에 레이즈를 걸면 늘어나는 피해까지 더해 잡히는 적이 있으면
/// 그 주사위가 후보다 — 어차피 못 잡을 적이나 걸지 않아도 죽을 적에게는 쓰지 않는다(사용자: 레이즈를 과하게 쓴다).
/// 가장 많은 적을 살리는(잡는) 주사위를 고른다.
///
/// 어림이라 빠진 것: 방어력·치명타·취약·화상·영웅 공격·둔화로 길어지는 시간. 모두 같은 쪽으로 틀리지 않아(방어력은 낮추고 나머지는 높인다)
/// 대략 맞는 것으로 보고, 여유는 SO_BotConfig.RaiseMargin으로 맞춘다.
/// </summary>
public sealed class BotEscape
{
    private readonly List<Vector2Int> _path = new List<Vector2Int>();
    // 주사위마다 초당 피해·사거리(칩 포함) — 적마다 다시 계산하지 않게 한 번 모은다.
    private readonly List<float> _dps = new List<float>();
    private readonly List<float> _range = new List<float>();
    private readonly List<float> _time = new List<float>();
    private readonly List<int> _saved = new List<int>();

    /// <summary>
    /// 레이즈를 걸 주사위 칸. 걸어서 살릴(잡을) 적이 없으면 false.
    /// </summary>
    /// <param name="zoneDistance">이 길 거리(목표물까지 칸 수)보다 가까운 적만 본다 — 메인 딜 구간에 들어왔거나 지난 적.</param>
    /// <param name="damageScale">레이즈 공격력·공격 속도 배율.</param>
    /// <param name="duration">레이즈 지속 시간(초). 영웅이 올라타는 것은 내릴 때까지라 큰 값을 넘긴다.</param>
    /// <param name="exclude">이미 레이즈인 주사위 등 뺄 칸(없으면 null).</param>
    public bool FindTarget(BotBoard board, MonsterField monsters, DiceField diceField, ChipField chips, SO_DiceConfig diceConfig,
        SO_DiceStatsConfig statsConfig, int zoneDistance, float damageScale, float speedScale, float duration, float margin,
        Vector2Int? exclude, out Vector2Int cell)
    {
        cell = default;
        IReadOnlyList<PlacedDice> placed = diceField.Placed;
        CollectDice(placed, chips, diceConfig, statsConfig);
        _saved.Clear();
        for (int d = 0; d < placed.Count; d++) _saved.Add(0);
        float boost = damageScale * speedScale - 1f;

        MonsterState[] states = monsters.States;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            Vector2Int at = Vector2Int.RoundToInt(states[i].Position);
            int distance = board.DistanceAt(at);
            if (distance < 0 || distance > zoneDistance) continue;
            float speed = monsters.KindOf(i).MoveSpeed;
            if (speed <= 0f) continue;
            board.PathFrom(at, _path);

            float remaining = 0f;
            _time.Clear();
            for (int d = 0; d < placed.Count; d++)
            {
                float time = _dps[d] > 0f ? CellsInRange(placed[d].Cell, _range[d]) / speed : 0f;
                _time.Add(time);
                remaining += _dps[d] * time;
            }
            double health = states[i].Health;
            // 걸지 않아도 죽는다.
            if (health <= remaining * margin) continue;
            for (int d = 0; d < placed.Count; d++)
            {
                if (_time[d] <= 0f || (exclude.HasValue && placed[d].Cell == exclude.Value)) continue;
                float extra = _dps[d] * boost * Mathf.Min(_time[d], duration);
                if (health <= (remaining + extra) * margin) _saved[d]++;
            }
        }

        int best = 0;
        for (int d = 0; d < placed.Count; d++)
        {
            if (_saved[d] <= best) continue;
            best = _saved[d];
            cell = placed[d].Cell;
        }
        return best > 0;
    }

    // 주사위마다 초당 피해(공격력 × 칩 ÷ 간격 × 칩)와 사거리(칩 포함). 눈이 다 깎인 주사위는 쏘지 않는다.
    private void CollectDice(IReadOnlyList<PlacedDice> placed, ChipField chips, SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig)
    {
        _dps.Clear();
        _range.Clear();
        for (int d = 0; d < placed.Count; d++)
        {
            DiceState dice = placed[d].Dice;
            SO_DiceType type = diceConfig.GetDiceType(dice.Colors);
            DiceStats stats = DiceStats.Compute(dice, type, statsConfig);
            SO_ChipCard chip = chips.Get(placed[d].Cell);
            bool shoots = !dice.IsSpent && stats.AttackInterval > 0f;
            _dps.Add(shoots ? stats.Damage * (1f + ChipEffects.DamageBonus(chip)) * (1f + ChipEffects.AttackSpeedBonus(chip)) / stats.AttackInterval : 0f);
            _range.Add(stats.GetRange(ChipEffects.RangeBonus(chip)));
        }
    }

    private int CellsInRange(Vector2Int center, float range)
    {
        float sqr = range * range;
        int count = 0;
        for (int i = 0; i < _path.Count; i++)
            if (((Vector2)(_path[i] - center)).sqrMagnitude <= sqr) count++;
        return count;
    }
}
