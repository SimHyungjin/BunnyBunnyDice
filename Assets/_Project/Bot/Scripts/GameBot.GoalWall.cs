using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameBot의 목표물 벽 — 미로는 목표물의 네 면 중 세 면을 먼저 막고 거기서 뻗어 나간다(2026-11-02 사용자).
/// 열어 둘 한 면은 나머지 세 면을 막았을 때 길이 가장 길어지는 면이다(MazeEvaluator — 막히면 그 면은 뺀다).
/// 모서리는 막지 않는다 — 두 면이 막힌 사이의 모서리는 어차피 대각으로 지나갈 수 없어 주사위만 버린다(사용자).
/// 세 면을 다 막은 뒤엔 미로 계획 칸을 목표물 가까운 순으로 놓되, 목표물 둘레 한 칸 중 벽이 아닌 칸(열어 둔 면·모서리)은 건너뛴다(IsGoalRingGap).
/// </summary>
public sealed partial class GameBot
{
    private readonly MazeEvaluator _wallEvaluator = new MazeEvaluator();
    private readonly List<Vector2Int> _goalWall = new List<Vector2Int>();
    private readonly List<Vector2Int> _wallTry = new List<Vector2Int>();
    private MapData _goalWallMap;

    // 목표물 벽에서 지금 놓을 수 있는 칸(목표물에 붙은 칸부터). 다 막았거나 놓을 수 없으면 false.
    private bool TryFindGoalWallCell(MapData map, out Vector2Int cell)
    {
        cell = default;
        if (map != _goalWallMap) BuildGoalWall(map);
        for (int i = 0; i < _goalWall.Count; i++)
        {
            if (DicePlacement.Check(map, _goalWall[i]) != PlacementResult.Ok) continue;
            cell = _goalWall[i];
            return true;
        }
        return false;
    }

    // 판마다 한 번: 네 면 중 열어 둘 면을 고르고 나머지 세 면(사이 모서리 포함)의 칸을 모은다.
    private void BuildGoalWall(MapData map)
    {
        _goalWallMap = map;
        _goalWall.Clear();
        float best = float.MinValue;
        for (int open = 0; open < 4; open++)
        {
            _wallTry.Clear();
            CollectWall(map, open, _wallTry);
            // 길 길이만 본다(사거리 덮기는 0) — 어느 면을 열어야 적이 목표물을 가장 멀리 돌아오는지.
            float score = _wallEvaluator.Evaluate(map, _wallTry, _wallTry, 0f, 0f, 0f);
            if (score <= best) continue;
            best = score;
            _goalWall.Clear();
            _goalWall.AddRange(_wallTry);
        }
    }

    /// <summary>목표물 둘레 한 칸(모서리 포함) 중 세 면 벽이 아닌 칸 — 열어 둔 면과 모서리. 목표물 가까운 칸부터 놓을 때 건너뛴다.</summary>
    private bool IsGoalRingGap(MapData map, Vector2Int cell)
    {
        if (map != _goalWallMap) BuildGoalWall(map);
        RectInt goal = map.GoalRect;
        bool ring = cell.x >= goal.xMin - 1 && cell.x <= goal.xMax && cell.y >= goal.yMin - 1 && cell.y <= goal.yMax && !goal.Contains(cell);
        return ring && !_goalWall.Contains(cell);
    }

    // 면 번호: 0 왼쪽, 1 오른쪽, 2 아래, 3 위. open 면을 뺀 세 면의 칸. 지을 수 있는 칸만.
    private static void CollectWall(MapData map, int open, List<Vector2Int> cells)
    {
        RectInt goal = map.GoalRect;
        for (int side = 0; side < 4; side++)
        {
            if (side == open) continue;
            if (side < 2)
            {
                int x = side == 0 ? goal.xMin - 1 : goal.xMax;
                for (int y = goal.yMin; y < goal.yMax; y++) AddWall(map, new Vector2Int(x, y), cells);
            }
            else
            {
                int y = side == 2 ? goal.yMin - 1 : goal.yMax;
                for (int x = goal.xMin; x < goal.xMax; x++) AddWall(map, new Vector2Int(x, y), cells);
            }
        }
    }

    private static void AddWall(MapData map, Vector2Int cell, List<Vector2Int> cells)
    {
        if (!map.InBounds(cell.x, cell.y) || !map.IsBuildable(cell.x, cell.y)) return;
        cells.Add(cell);
    }
}
