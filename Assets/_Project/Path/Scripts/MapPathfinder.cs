using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 길. 두 가지 거리장을 목표물에서 한 번씩 퍼뜨려 두고 모든 몬스터가 나눠 쓴다(몬스터마다 길을 찾지 않는다).
/// - 막힘 판정(BuildDistanceField): 4방향 칸 수. 배치 금지("길을 완전히 막으면 안 된다")와 맵 생성 검사에 쓴다. 정수 BFS라 가장 싸다.
/// - 걷는 길(BuildRouteField): 장애물 모서리에서만 꺾이는 곧은 길(GridRouteField, Lazy Theta*).
/// 둘이 보는 "갈 수 있는 곳"은 같다 — 걷는 길이 막힘 판정과 어긋나지 않는다.
/// </summary>
public static class MapPathfinder
{
    public const int Unreachable = -1;

    /// <summary>몬스터가 지나갈 수 있는 칸. 맵 밖은 막힌 것으로 본다(GridCircle은 맵 가장자리 밖 칸까지 살핀다).</summary>
    public static readonly Func<MapData, int, int, bool> Passable = (map, x, y) => map.InBounds(x, y) && map.IsWalkable(x, y);

    // 작업 목록(메인 스레드 전용).
    private static readonly List<Vector2Int> GoalCells = new List<Vector2Int>();

    // 막힘 판정은 4방향으로 충분하다(모서리를 깎지 않는 8방향과 갈 수 있는 곳이 같다).
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    /// <summary>각 칸에서 목표물까지의 칸 수. 도달할 수 없으면 Unreachable.</summary>
    public static int[] BuildDistanceField(MapData map)
    {
        int[] field = new int[map.Width * map.Height];
        for (int i = 0; i < field.Length; i++) field[i] = Unreachable;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        RectInt goal = map.GoalRect;
        for (int y = goal.yMin; y < goal.yMax; y++)
        {
            for (int x = goal.xMin; x < goal.xMax; x++)
            {
                field[map.ToIndex(x, y)] = 0;
                queue.Enqueue(new Vector2Int(x, y));
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            int next = field[map.ToIndex(cur.x, cur.y)] + 1;
            for (int d = 0; d < Directions.Length; d++)
            {
                int nx = cur.x + Directions[d].x;
                int ny = cur.y + Directions[d].y;
                if (!map.InBounds(nx, ny) || !map.IsWalkable(nx, ny)) continue;

                int index = map.ToIndex(nx, ny);
                if (field[index] != Unreachable) continue;
                field[index] = next;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return field;
    }

    /// <summary>모든 출현 구역이 목표물까지 갈 수 있는지. 배치 금지 판정("길을 완전히 막으면 안 된다")에 쓴다.</summary>
    public static bool AllSpawnsReachable(MapData map, int[] field)
    {
        for (int i = 0; i < map.Spawns.Count; i++)
        {
            Vector2Int s = map.Spawns[i];
            if (field[map.ToIndex(s.x, s.y)] == Unreachable) return false;
        }
        return true;
    }

    /// <summary>
    /// 목표물까지 걷는 길(GridRouteField). 몬스터 반지름 radius의 원이 지나갈 수 있는 길로 구한다.
    /// 주사위 배치로 길이 바뀔 때마다 다시 만들고, 경로 표시(MapPathView)와 몬스터(MonsterField)가 같이 써서 표시가 실제 움직임과 어긋나지 않게 한다.
    /// </summary>
    public static GridRouteField BuildRouteField(MapData map, float radius)
    {
        GoalCells.Clear();
        RectInt goal = map.GoalRect;
        for (int y = goal.yMin; y < goal.yMax; y++)
            for (int x = goal.xMin; x < goal.xMax; x++)
                GoalCells.Add(new Vector2Int(x, y));

        GridRouteField field = new GridRouteField();
        field.Build(map, Passable, GoalCells, radius);
        return field;
    }
}
