using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 걷는 길 거리장(Lazy Theta*). 출발점들(목표물 칸, 또는 영웅의 목적지)에서 퍼뜨려, 각 칸에서 출발점까지 곧게 가는 길을 구해 둔다.
/// 칸마다 "곧장 보이는 다음 경유점(부모)"을 기억하므로, 어느 칸에서든 부모를 따라가면 장애물 모서리에서만 꺾이는 길이 나온다.
///
/// 칸 경로를 구한 뒤 펴는 방식을 쓰지 않는 이유: 펴기는 칸 경로 위의 칸 중심만 경유점으로 쓸 수 있어서,
/// 칸 경로가 어느 쪽으로 붙어 가느냐에 따라 장애물과 무관한 곳에서 꺾인다. 여기서는 퍼뜨리는 동안 직접 보이는 칸을 부모로 삼는다.
///
/// 시야는 반지름 radius의 원이 지나가는지로 본다(PathStraightener.HasClearLine). 그래서 모서리끼리 붙은 막힌 칸 사이로는 지나가지 않고,
/// 이웃 칸으로의 이동도 대각선은 양옆이 비어 있을 때만 하므로 갈 수 있는 곳은 4방향과 같다(미로가 새지 않는다).
/// </summary>
public sealed class GridRouteField
{
    public const float Unreachable = -1f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    private MapData _map;
    private Func<MapData, int, int, bool> _passable;
    private float _radius;
    // 칸에서 출발점까지의 거리, 곧장 보이는 다음 경유점(출발점은 자기 자신), 거리 확정 여부.
    private float[] _distance = new float[0];
    private int[] _parent = new int[0];
    private bool[] _closed = new bool[0];
    private readonly List<Vector2> _chain = new List<Vector2>();

    public void Build(MapData map, Func<MapData, int, int, bool> passable, List<Vector2Int> sources, float radius)
    {
        _map = map;
        _passable = passable;
        _radius = radius;

        int count = map.Width * map.Height;
        if (_distance.Length != count)
        {
            _distance = new float[count];
            _parent = new int[count];
            _closed = new bool[count];
        }
        for (int i = 0; i < count; i++)
        {
            _distance[i] = Unreachable;
            _parent[i] = -1;
            _closed[i] = false;
        }
        for (int i = 0; i < sources.Count; i++)
        {
            int index = map.ToIndex(sources[i].x, sources[i].y);
            _distance[index] = 0f;
            _parent[index] = index;
        }

        // 칸이 백몇십 개뿐이라 우선순위 큐 없이 매번 가장 가까운 칸을 훑어 고른다.
        while (true)
        {
            int current = -1;
            for (int i = 0; i < count; i++)
            {
                if (_closed[i] || _distance[i] == Unreachable) continue;
                if (current < 0 || _distance[i] < _distance[current]) current = i;
            }
            if (current < 0) break;
            _closed[current] = true;

            // 이웃을 넓힐 때는 "부모에서 곧장 보인다"고 가정해 두고, 그 칸을 꺼낼 때 한 번만 확인한다(Lazy) — 시야 검사가 칸 수만큼으로 줄어든다.
            if (_parent[current] != current && !HasClearLine(_parent[current], current)) RepairParent(current);

            int cx = current % map.Width;
            int cy = current / map.Width;
            int parent = _parent[current];
            for (int d = 0; d < Directions.Length; d++)
            {
                if (!CanStep(cx, cy, Directions[d])) continue;
                int next = map.ToIndex(cx + Directions[d].x, cy + Directions[d].y);
                if (_closed[next]) continue;

                float distance = _distance[parent] + Vector2.Distance(Center(parent), Center(next));
                if (_distance[next] != Unreachable && distance >= _distance[next]) continue;
                _distance[next] = distance;
                _parent[next] = parent;
            }
        }
    }

    /// <summary>
    /// from에서 출발점까지 걸을 경유점들(from 제외, 마지막이 출발점 칸 중심)을 result에 채운다. 길이 없으면 false.
    /// from이 칸 중심이 아니어도(걷는 중인 몬스터) 곧장 갈 수 있으면 자기 칸 중심을 건너뛴다.
    /// </summary>
    public bool TryGetPath(Vector2 from, List<Vector2> result)
    {
        result.Clear();
        Vector2Int start = GridCircle.CellOf(from);
        if (!_map.InBounds(start.x, start.y)) return false;
        int index = _map.ToIndex(start.x, start.y);
        if (_distance[index] == Unreachable) return false;

        _chain.Clear();
        _chain.Add(Center(index));
        while (_parent[index] != index)
        {
            index = _parent[index];
            _chain.Add(Center(index));
        }
        PathStraightener.Straighten(_map, _passable, from, _chain, _radius, result);
        return true;
    }

    /// <summary>
    /// from에서 곧장 갈 다음 경유점 하나(TryGetPath가 채우는 첫 점과 같다). 길이 없으면 false.
    /// 몬스터는 경로 전체를 들고 다니지 않고 경유점에 닿을 때마다 이것만 묻는다 — 몬스터마다 목록을 두지 않아 수가 많아져도 값만으로 상태를 담을 수 있다.
    /// </summary>
    public bool TryGetNextWaypoint(Vector2 from, out Vector2 waypoint)
    {
        waypoint = from;
        Vector2Int start = GridCircle.CellOf(from);
        if (!_map.InBounds(start.x, start.y)) return false;
        int index = _map.ToIndex(start.x, start.y);
        if (_distance[index] == Unreachable) return false;

        // 부모 사슬을 따라 from에서 곧장 보이는 데까지 간다(PathStraightener.Straighten의 첫 걸음과 같다).
        waypoint = Center(index);
        while (_parent[index] != index)
        {
            int next = _parent[index];
            // 칸 중심에 서 있으면(앞 경유점에 막 닿음) 부모는 퍼뜨릴 때 곧장 보이는 것을 이미 확인했으므로 검사 없이 간다.
            bool atCenter = waypoint == from;
            if (!atCenter && !PathStraightener.HasClearLine(_map, _passable, from, Center(next), _radius)) break;
            index = next;
            waypoint = Center(index);
        }
        return true;
    }

    /// <summary>칸에서 출발점까지 걷는 거리. 맵 밖이거나 갈 수 없으면 Unreachable.</summary>
    public float GetDistance(Vector2Int cell) =>
        _map.InBounds(cell.x, cell.y) ? _distance[_map.ToIndex(cell.x, cell.y)] : Unreachable;

    // 부모까지 보이지 않으면, 이미 확정된 이웃 중 거쳐 오는 거리가 가장 짧은 칸을 부모로 삼는다(이웃 칸으로 한 칸 이동으로 되돌림).
    // 이 칸을 넓혀 준 이웃이 이미 확정돼 있으므로 고를 칸은 반드시 있다.
    private void RepairParent(int index)
    {
        int x = index % _map.Width;
        int y = index / _map.Width;
        float best = float.MaxValue;
        for (int d = 0; d < Directions.Length; d++)
        {
            if (!CanStep(x, y, Directions[d])) continue;
            int neighbor = _map.ToIndex(x + Directions[d].x, y + Directions[d].y);
            if (!_closed[neighbor]) continue;

            float distance = _distance[neighbor] + Vector2.Distance(Center(neighbor), Center(index));
            if (distance >= best) continue;
            best = distance;
            _distance[index] = distance;
            _parent[index] = neighbor;
        }
    }

    private bool HasClearLine(int a, int b) => PathStraightener.HasClearLine(_map, _passable, Center(a), Center(b), _radius);

    private bool CanStep(int x, int y, Vector2Int direction)
    {
        if (!_passable(_map, x + direction.x, y + direction.y)) return false;
        // 대각선은 양옆이 모두 비어 있을 때만 — 막힌 칸 두 개의 모서리 틈으로 끼어들지 않게.
        if (direction.x == 0 || direction.y == 0) return true;
        return _passable(_map, x + direction.x, y) && _passable(_map, x, y + direction.y);
    }

    private Vector2 Center(int index) => new Vector2(index % _map.Width, index / _map.Width);
}
