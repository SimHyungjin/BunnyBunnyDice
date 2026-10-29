using System.Collections.Generic;
using UnityEngine;

/// <summary>꺾인 선 하나. 처음부터 잰 거리로 선 위의 점을 구한다(경로 표시의 빛들이 같은 선을 나눠 쓴다).</summary>
public sealed class PathPolyline
{
    private readonly List<Vector3> _points = new List<Vector3>();
    // 처음부터 각 점까지의 누적 거리.
    private readonly List<float> _distances = new List<float>();

    public float Length => _distances.Count > 0 ? _distances[_distances.Count - 1] : 0f;

    public int PointCount => _points.Count;

    public Vector3 GetPoint(int index) => _points[index];

    /// <summary>처음부터 index번째 점까지의 거리.</summary>
    public float DistanceAt(int index) => _distances[index];

    /// <summary>start에서 path(start 제외)를 잇는 선. 점은 바닥(x, z)에 offset을 더한 부모 로컬 좌표다.</summary>
    public PathPolyline(Vector2 start, List<Vector2> path, Vector3 offset)
    {
        _points.Add(new Vector3(start.x, 0f, start.y) + offset);
        _distances.Add(0f);
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = new Vector3(path[i].x, 0f, path[i].y) + offset;
            _distances.Add(Length + Vector3.Distance(_points[_points.Count - 1], point));
            _points.Add(point);
        }
    }

    public Vector3 PointAt(float distance)
    {
        for (int i = 1; i < _points.Count; i++)
        {
            if (distance > _distances[i]) continue;
            float segment = _distances[i] - _distances[i - 1];
            return segment > 0f ? Vector3.Lerp(_points[i - 1], _points[i], (distance - _distances[i - 1]) / segment) : _points[i];
        }
        return _points[_points.Count - 1];
    }
}
