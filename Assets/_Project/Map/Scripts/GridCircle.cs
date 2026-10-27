using System;
using UnityEngine;

/// <summary>
/// 연속 좌표의 원과 격자 칸 사이의 판정. 영웅·몬스터처럼 칸에 묶이지 않고 움직이는 것들이 같이 쓴다.
/// 좌표는 맵 로컬 (x, z)이고 칸 (x, y)는 [x-0.5, x+0.5] × [y-0.5, y+0.5]를 차지한다.
/// 어떤 칸이 막혔는지는 쓰는 쪽마다 다르므로(영웅은 바닥만, 몬스터는 길 전체) 판정 함수를 받는다.
/// </summary>
public static class GridCircle
{
    public static Vector2Int CellOf(Vector2 position) =>
        new Vector2Int(Mathf.FloorToInt(position.x + 0.5f), Mathf.FloorToInt(position.y + 0.5f));

    /// <summary>원이 걸칠 수 있는 칸 범위(양 끝 포함).</summary>
    public static void GetCellRange(Vector2 center, float radius, out Vector2Int min, out Vector2Int max)
    {
        min = CellOf(center - new Vector2(radius, radius));
        max = CellOf(center + new Vector2(radius, radius));
    }

    /// <summary>칸 사각형 위에서 point에 가장 가까운 점.</summary>
    public static Vector2 ClosestPointInCell(int x, int y, Vector2 point) =>
        new Vector2(Mathf.Clamp(point.x, x - 0.5f, x + 0.5f), Mathf.Clamp(point.y, y - 0.5f, y + 0.5f));

    /// <summary>원이 막힌 칸과 겹치는지.</summary>
    public static bool Overlaps(MapData map, Func<MapData, int, int, bool> passable, Vector2 center, float radius)
    {
        GetCellRange(center, radius, out Vector2Int min, out Vector2Int max);
        float sqrRadius = radius * radius;
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                if (passable(map, x, y)) continue;
                if ((center - ClosestPointInCell(x, y, center)).sqrMagnitude < sqrRadius) return true;
            }
        }
        return false;
    }
}
