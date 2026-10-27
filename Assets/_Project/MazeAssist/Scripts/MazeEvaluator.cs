using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미로 점수(기획서 11.4). 적이 주사위 사거리 안에 머무는 양을 잰다 — 길 길이(출현 구역마다 목표물까지 걷는 거리의 합)
/// + 길 칸마다 사거리 안에 든 주사위 수의 합 × 가중치. 길이 하나로 합쳐질수록 한 주사위가 여러 무리를 때려 둘째 항이 커진다.
/// 구멍마다 출현 몫(그 구멍에 들어간 판 주사위 수, 보스 구멍은 bossWeight를 더)을 곱한다 — ×2·보스 구멍을 희생하는 미로는 그 판의 좋은 답이 아니다
/// (주사위가 문제를 내고 플레이어가 푼다, 기획서 3.3). 몫을 모르면 보스·×2 구멍 쪽을 버리는 미로가 최선으로 나왔다.
///
/// 거리는 8방향(대각선은 양옆이 비어 있을 때만 — 몬스터 길과 같은 규칙)으로 잰다. 몬스터가 실제로 쓰는 길(GridRouteField, 곧게 걷기)은
/// 수만 번 돌리기엔 무거워, 탐색 중에는 그와 거의 같은 이 거리로 평가한다. 칸 수(4방향)로 재면 몬스터가 가로지르는 열린 곳까지
/// 길게 쳐서 벽이 듬성듬성한 엉터리 배치가 최선으로 나온다.
/// </summary>
public sealed class MazeEvaluator
{
    private const float Diagonal = 1.41421356f;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    private float[] _distance = new float[0];
    private bool[] _closed = new bool[0];
    private bool[] _wall = new bool[0];
    // 거리 계산용 이진 힙(칸 번호, 넣을 때의 거리). 같은 칸이 더 짧은 거리로 다시 들어오면 옛것은 꺼낼 때 버린다. 할당 없이 재사용한다.
    private int[] _heap = new int[0];
    private float[] _heapKey = new float[0];
    private int _heapCount;
    private readonly List<Vector2Int> _route = new List<Vector2Int>();
    // _route 칸마다 그 칸을 지나는 구멍의 출현 몫.
    private readonly List<float> _routeWeight = new List<float>();

    /// <summary>마지막 평가의 길 길이 합(출현 몫을 곱한 값).</summary>
    public float RouteLength { get; private set; }

    /// <summary>마지막 평가에서 cell부터 목표물까지의 거리(못 가면 -1). 길 길이 합(RouteLength)을 구멍마다 나눠 볼 때 쓴다.</summary>
    public float DistanceAt(MapData map, Vector2Int cell) => _distance[map.ToIndex(cell.x, cell.y)];

    /// <summary>
    /// map(놓인 주사위·칩 그대로) 위에 planned 칸들을 벽으로 더 세웠다고 치고 점수를 낸다. 길이 막히면 float.MinValue.
    /// towers는 사거리로 길을 덮는 칸(놓인 주사위 + 계획한 칸) — 칩은 벽이지만 쏘지 않는다.
    /// </summary>
    public float Evaluate(MapData map, IReadOnlyList<Vector2Int> planned, IReadOnlyList<Vector2Int> towers, float range, float coverageWeight,
        float bossWeight)
    {
        int count = map.Width * map.Height;
        if (_distance.Length != count)
        {
            _distance = new float[count];
            _closed = new bool[count];
            _wall = new bool[count];
            _heap = new int[count * 8];
            _heapKey = new float[count * 8];
        }
        for (int i = 0; i < count; i++)
        {
            int x = i % map.Width;
            int y = i / map.Width;
            _wall[i] = !map.IsWalkable(x, y);
        }
        for (int i = 0; i < planned.Count; i++) _wall[map.ToIndex(planned[i].x, planned[i].y)] = true;

        BuildDistance(map);

        RouteLength = 0f;
        _route.Clear();
        _routeWeight.Clear();
        for (int i = 0; i < map.Spawns.Count; i++)
        {
            Vector2Int spawn = map.Spawns[i];
            float distance = _distance[map.ToIndex(spawn.x, spawn.y)];
            if (distance < 0f) return float.MinValue;
            float weight = SpawnWeight(map, spawn, bossWeight);
            RouteLength += distance * weight;
            TraceRoute(map, spawn, weight);
        }

        // 길 칸마다 사거리 안의 주사위 수 — 같은 길을 여러 무리가 지나면 그 칸이 여러 번 세진다(출현 구역마다 따로 따라갔으므로).
        float sqr = range * range;
        float covered = 0f;
        for (int r = 0; r < _route.Count; r++)
        {
            for (int t = 0; t < towers.Count; t++)
            {
                if (((Vector2)(towers[t] - _route[r])).sqrMagnitude <= sqr) covered += _routeWeight[r];
            }
        }
        return RouteLength + covered * coverageWeight;
    }

    // 출현 몫 = 출현 차례(SpawnGates)에 든 횟수(판 주사위 수), 보스 구멍은 bossWeight를 더한다. 차례가 없는 맵(테스트)은 1.
    private static float SpawnWeight(MapData map, Vector2Int spawn, float bossWeight)
    {
        int gates = 0;
        for (int g = 0; g < map.SpawnGates.Count; g++)
            if (map.SpawnGates[g] == spawn) gates++;
        float weight = Mathf.Max(gates, 1);
        if (spawn == map.BossPocket) weight += bossWeight;
        return weight;
    }

    // 목표물 칸에서 퍼뜨린 8방향 거리(다익스트라). 못 가는 칸은 -1.
    private void BuildDistance(MapData map)
    {
        for (int i = 0; i < _distance.Length; i++)
        {
            _distance[i] = -1f;
            _closed[i] = false;
        }
        _heapCount = 0;
        RectInt goal = map.GoalRect;
        for (int y = goal.yMin; y < goal.yMax; y++)
        {
            for (int x = goal.xMin; x < goal.xMax; x++)
            {
                int index = map.ToIndex(x, y);
                _distance[index] = 0f;
                Push(index, 0f);
            }
        }

        while (_heapCount > 0)
        {
            int current = Pop();
            if (_closed[current]) continue;
            _closed[current] = true;
            int cx = current % map.Width;
            int cy = current / map.Width;
            for (int d = 0; d < Directions.Length; d++)
            {
                int nx = cx + Directions[d].x;
                int ny = cy + Directions[d].y;
                if (!Open(map, nx, ny)) continue;
                bool diagonal = d >= 4;
                // 대각선은 양옆이 모두 비어 있을 때만 — 막힌 칸 두 개의 모서리 틈으로 끼어들지 않게(GridRouteField와 같다).
                if (diagonal && (!Open(map, cx + Directions[d].x, cy) || !Open(map, cx, cy + Directions[d].y))) continue;
                int next = map.ToIndex(nx, ny);
                if (_closed[next]) continue;
                float distance = _distance[current] + (diagonal ? Diagonal : 1f);
                if (_distance[next] >= 0f && _distance[next] <= distance) continue;
                _distance[next] = distance;
                Push(next, distance);
            }
        }
    }

    private bool Open(MapData map, int x, int y) => map.InBounds(x, y) && !_wall[map.ToIndex(x, y)];

    // 거리가 줄어드는 이웃을 따라 목표물까지 — 출현 구역마다 따로 세므로 합쳐진 길은 여러 번 들어간다.
    private void TraceRoute(MapData map, Vector2Int cell, float weight)
    {
        int guard = _distance.Length;
        while (guard-- > 0)
        {
            int index = map.ToIndex(cell.x, cell.y);
            if (_distance[index] <= 0f) return;
            _route.Add(cell);
            _routeWeight.Add(weight);
            Vector2Int best = cell;
            float bestDistance = _distance[index];
            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int next = cell + Directions[d];
                if (!map.InBounds(next.x, next.y)) continue;
                float distance = _distance[map.ToIndex(next.x, next.y)];
                if (distance < 0f || distance >= bestDistance) continue;
                bestDistance = distance;
                best = next;
            }
            if (best == cell) return;
            cell = best;
        }
    }

    private void Push(int index, float key)
    {
        int i = _heapCount++;
        _heap[i] = index;
        _heapKey[i] = key;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_heapKey[parent] <= _heapKey[i]) break;
            Swap(parent, i);
            i = parent;
        }
    }

    private void Swap(int a, int b)
    {
        (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
        (_heapKey[a], _heapKey[b]) = (_heapKey[b], _heapKey[a]);
    }

    private int Pop()
    {
        int top = _heap[0];
        _heapCount--;
        _heap[0] = _heap[_heapCount];
        _heapKey[0] = _heapKey[_heapCount];
        int i = 0;
        while (true)
        {
            int left = i * 2 + 1;
            if (left >= _heapCount) break;
            int smallest = left + 1 < _heapCount && _heapKey[left + 1] < _heapKey[left] ? left + 1 : left;
            if (_heapKey[i] <= _heapKey[smallest]) break;
            Swap(i, smallest);
            i = smallest;
        }
        return top;
    }
}
