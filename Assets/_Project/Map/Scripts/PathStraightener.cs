using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 칸 경로 펴기. 칸 중심을 그대로 밟으면 계단처럼 꺾이므로, 지금 자리에서 반지름 radius의 원이 막힘 없이 곧장 갈 수 있는 점까지 건너뛴다.
/// 원으로 검사하므로 모서리끼리 붙은 막힌 칸 두 개 사이로는 지나가지 않는다 — 몬스터 경로에 써도 미로가 새지 않는다(반지름 > 0일 때).
/// </summary>
public static class PathStraightener
{
    /// <summary>from에서 출발해 points(from 제외, 순서대로)를 지나는 경로를 펴서 result에 채운다. 마지막 점은 항상 남는다.</summary>
    public static void Straighten(MapData map, Func<MapData, int, int, bool> passable, Vector2 from, List<Vector2> points,
        float radius, List<Vector2> result)
    {
        result.Clear();
        Vector2 anchor = from;
        int i = 0;
        while (i < points.Count)
        {
            // 앞에서부터 보이는 데까지만 늘린다. 끝에서부터 찾으면 더 멀리 건너뛸 때가 있지만, 미로처럼 꺾인 길에서는
            // 대부분 헛검사라 비용이 경로 길이의 제곱으로 늘어난다(주사위를 놓을 때마다 몬스터 전원이 다시 편다).
            int far = i;
            while (far + 1 < points.Count && HasClearLine(map, passable, anchor, points[far + 1], radius)) far++;

            result.Add(points[far]);
            anchor = points[far];
            i = far + 1;
        }
    }

    public static bool HasClearLine(MapData map, Func<MapData, int, int, bool> passable, Vector2 a, Vector2 b, float radius)
    {
        // 반지름의 절반 간격으로 짚어 보면 칸 모서리를 스치는 경우도 놓치지 않는다.
        float spacing = radius * 0.5f;
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / spacing));
        for (int i = 1; i <= samples; i++)
        {
            if (GridCircle.Overlaps(map, passable, Vector2.Lerp(a, b, (float)i / samples), radius)) return false;
        }
        return true;
    }
}
