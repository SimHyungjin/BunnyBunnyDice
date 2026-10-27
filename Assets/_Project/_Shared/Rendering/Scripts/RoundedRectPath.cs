using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 둥근 사각형 경로(위에서 볼 때 반시계 방향). 점과 바깥 방향 법선을 함께 낸다. SweepMesh가 이 경로를 따라 단면을 돌린다.
/// 테이블 펠트 가장자리(레일·인쇄 선은 이 경로를 바깥(+)·안쪽(−)으로 민 것 — 둥근 사각형은 법선 방향으로 밀어도 둥근 사각형이라 모서리가 꼬이지 않는다)와,
/// 반지름을 짧은 변의 절반으로 둔 원(칩)에 쓴다. 좌표는 XZ 평면(y = 0)이다.
/// </summary>
public sealed class RoundedRectPath
{
    public readonly List<Vector3> Points = new List<Vector3>();
    public readonly List<Vector3> Normals = new List<Vector3>();

    public int Count => Points.Count;

    /// <param name="min">펠트 영역의 왼쪽 아래(x, z).</param>
    /// <param name="max">펠트 영역의 오른쪽 위(x, z).</param>
    /// <param name="radius">모서리 반지름. 짧은 변의 절반을 넘으면 줄인다(그때 양 끝이 반원인 경기장 모양).</param>
    /// <param name="cornerSegments">모서리 하나를 나누는 수. 클수록 매끈하다.</param>
    public void Build(Vector2 min, Vector2 max, float radius, int cornerSegments)
    {
        Points.Clear();
        Normals.Clear();
        float r = Mathf.Clamp(radius, 0f, Mathf.Min(max.x - min.x, max.y - min.y) * 0.5f);
        int segments = Mathf.Max(1, cornerSegments);

        // 오른쪽 아래 → 오른쪽 위 → 왼쪽 위 → 왼쪽 아래. 모서리마다 90°씩 돈다.
        AddCorner(new Vector2(max.x - r, min.y + r), -90f, r, segments);
        AddCorner(new Vector2(max.x - r, max.y - r), 0f, r, segments);
        AddCorner(new Vector2(min.x + r, max.y - r), 90f, r, segments);
        AddCorner(new Vector2(min.x + r, min.y + r), 180f, r, segments);
    }

    private void AddCorner(Vector2 center, float startDegrees, float r, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float a = (startDegrees + 90f * i / segments) * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Points.Add(new Vector3(center.x, 0f, center.y) + normal * r);
            Normals.Add(normal);
        }
    }
}
