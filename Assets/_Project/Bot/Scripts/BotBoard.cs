using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 봇이 판을 읽는 계산(기획서 11.2). 적이 걷는 길과, 칸마다 사거리 안에 들어오는 길 칸 수(덮는 길)를 센다.
/// 사람이 "길을 길게, 주사위는 길이 많이 지나는 곳에"로 판단하는 것을 숫자로 옮긴 것이다.
/// 몰림(Traffic)은 길 칸마다 지나는 적의 몫을 센다 — 여러 포켓의 길이 합쳐지는 곳, 주사위가 많이 들어간 포켓의 길일수록 크다.
/// 사용자: 적이 몰리는 곳에 디버프를 깔고 범위 주사위를 키우는 것이 중요했다.
/// 메인 딜 구간(KillZone)은 길 위에서 둘레 반경 안의 몰림 합이 가장 큰 칸이다 — 사용자는 미로를 지을 때 "주 딜 구간은 여기"라고 정하고
/// 범위 주사위·칩·사거리를 거기로 모으고, 적이 그 구간을 뚫을 것 같을 때만 레이즈·스킬을 썼다.
/// </summary>
public sealed class BotBoard
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    private readonly List<Vector2Int> _route = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> _routeSet = new HashSet<Vector2Int>();
    // 길 칸마다 지나는 적의 몫(출현 차례 수 — 판 주사위가 겹친 포켓은 그만큼 많이 나온다).
    private readonly Dictionary<Vector2Int, int> _traffic = new Dictionary<Vector2Int, int>();
    private int[] _distance;
    private int _width;

    /// <summary>메인 딜 구간의 중심 칸(길 위). 길이 없으면 HasKillZone이 false.</summary>
    public Vector2Int KillZone { get; private set; }
    public bool HasKillZone { get; private set; }
    /// <summary>메인 딜 구간 중심에서 목표물까지의 칸 수 — 이보다 가까운 적은 구간을 넘어왔다.</summary>
    public int KillZoneDistance { get; private set; }

    /// <summary>마지막으로 읽은 길(출현 구역마다 목표물까지 가장 짧게 걷는 칸들).</summary>
    public IReadOnlyList<Vector2Int> Route => _route;

    /// <summary>마지막으로 읽은 길의 길이 합(출현 구역마다 목표물까지 칸 수). 길을 막으면 길어진다.</summary>
    public int RouteLength { get; private set; }

    /// <summary>지금 판으로 길을 다시 읽는다. 모든 출현 구역이 목표물에 닿지 못하면 false.</summary>
    /// <param name="killZoneRadius">메인 딜 구간의 반경(칸) — 그 안의 몰림 합이 가장 큰 길 칸을 고른다.</param>
    public bool Read(MapData map, float killZoneRadius)
    {
        int[] field = MapPathfinder.BuildDistanceField(map);
        _distance = field;
        _width = map.Width;
        HasKillZone = false;
        _route.Clear();
        _routeSet.Clear();
        _traffic.Clear();
        RouteLength = 0;
        for (int i = 0; i < map.Spawns.Count; i++)
        {
            Vector2Int cell = map.Spawns[i];
            int distance = field[map.ToIndex(cell.x, cell.y)];
            if (distance == MapPathfinder.Unreachable) return false;
            RouteLength += distance;
            int weight = 0;
            for (int g = 0; g < map.SpawnGates.Count; g++)
                if (map.SpawnGates[g] == cell) weight++;
            // 거리가 하나씩 줄어드는 이웃을 따라 내려가면 가장 짧은 길이다.
            while (distance > 0)
            {
                if (_routeSet.Add(cell)) _route.Add(cell);
                _traffic[cell] = (_traffic.TryGetValue(cell, out int passing) ? passing : 0) + Mathf.Max(weight, 1);
                Vector2Int next = cell;
                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector2Int candidate = cell + Directions[d];
                    if (!map.InBounds(candidate.x, candidate.y)) continue;
                    if (field[map.ToIndex(candidate.x, candidate.y)] != distance - 1) continue;
                    next = candidate;
                    break;
                }
                if (next == cell) break;
                cell = next;
                distance--;
            }
        }
        FindKillZone(killZoneRadius);
        return true;
    }

    /// <summary>cell에서 목표물까지의 칸 수(4방향). 판 밖·못 가는 칸은 -1.</summary>
    public int DistanceAt(Vector2Int cell)
    {
        if (_distance == null || cell.x < 0 || cell.y < 0 || cell.x >= _width) return -1;
        int index = cell.y * _width + cell.x;
        return index < _distance.Length ? _distance[index] : -1;
    }

    // 길 칸마다 반경 안 몰림 합을 세어 가장 큰 칸. 같으면 먼저 본(출현 구역 쪽) 칸 — 앞쪽에서 막을수록 뒤에 여유가 생긴다.
    private void FindKillZone(float radius)
    {
        float sqr = radius * radius;
        int best = 0;
        for (int i = 0; i < _route.Count; i++)
        {
            int sum = 0;
            for (int j = 0; j < _route.Count; j++)
                if (((Vector2)(_route[j] - _route[i])).sqrMagnitude <= sqr) sum += _traffic[_route[j]];
            if (sum <= best) continue;
            best = sum;
            KillZone = _route[i];
            HasKillZone = true;
        }
        if (HasKillZone) KillZoneDistance = DistanceAt(KillZone);
    }

    /// <summary>cell에서 range(칸) 안에 들어오는 길 칸의 몰림 합 — 그 자리를 지나는 적의 양.</summary>
    public int Traffic(Vector2Int cell, float range)
    {
        float sqr = range * range;
        int count = 0;
        for (int i = 0; i < _route.Count; i++)
        {
            if (((Vector2)(_route[i] - cell)).sqrMagnitude <= sqr) count += _traffic[_route[i]];
        }
        return count;
    }

    /// <summary>cell에서 목표물까지 가장 짧게 걷는 칸들(cell 포함, 목표물 칸 앞까지)을 path에 채운다. 못 가는 칸이면 비운다.</summary>
    public void PathFrom(Vector2Int cell, List<Vector2Int> path)
    {
        path.Clear();
        int distance = DistanceAt(cell);
        if (distance < 0) return;
        while (distance > 0)
        {
            path.Add(cell);
            Vector2Int next = cell;
            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int candidate = cell + Directions[d];
                if (DistanceAt(candidate) != distance - 1) continue;
                next = candidate;
                break;
            }
            if (next == cell) break;
            cell = next;
            distance--;
        }
    }
}
