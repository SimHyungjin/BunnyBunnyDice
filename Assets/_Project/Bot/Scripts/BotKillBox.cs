using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 딜 몰빵 구역 계획(2026-11-03 사용자 운영). 사용자는 미로를 신경 쓰지 않는다 — 어떻게든 길을 하나로 모은 뒤 그 둘레에 딜을 몰빵한다.
/// 자리는 "길을 가장 적은 주사위로 모을 수 있고, 그 둘레에 주사위를 많이 놓을 수 있는 곳"이다(사용자).
///
/// 판 시작 때 한 번 정한다: 길 칸 후보 M마다 —
///  - 모으기 비용 = 출현 구멍에서 목표물까지 M을 거치지 않는 길을 모두 끊는 최소 칸 수(정점 최소 절단, 4방향 — 대각은 양옆이 비어야 지나므로
///    4방향으로 끊으면 대각으로도 못 지난다). 끊은 뒤에도 모든 구멍이 M을 거쳐 목표물에 닿아야 한다(길을 완전히 막으면 놓을 수 없다).
///  - 둘레 = M에서 반경 안의 지을 수 있는 빈 칸 수(막을 칸·M 빼고).
///  - 머무는 길 = 길을 모은 뒤 적이 중심 둘레(반경) 안에서 걷는 칸 수 — 모은 길이 딜 구역을 스치기만 하면 딜할 시간이 없다(2026-11-03 사용자).
///  점수 = 둘레 + 머무는 길 × dwellWeight − 비용 × cutCost. 같으면 목표물에 가까운 쪽(뚫려도 덜 아프다).
/// 계산은 판(15×8 안팎)이 작아 칸마다 최대 유량을 돌려도 가볍다.
/// </summary>
public sealed class BotKillBox
{
    private const int Infinite = 1 << 20;
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    /// <summary>계획이 있는지(모든 후보가 안 되면 false — 봇은 원래 방식으로 돈다).</summary>
    public bool Planned { get; private set; }
    /// <summary>딜 구역 중심(모든 길이 지나게 할 칸).</summary>
    public Vector2Int Center { get; private set; }
    /// <summary>길을 모으려고 막을 칸들.</summary>
    public readonly List<Vector2Int> Cut = new List<Vector2Int>();
    /// <summary>딜을 몰빵할 둘레 칸들(중심에 가까운 순).</summary>
    public readonly List<Vector2Int> Zone = new List<Vector2Int>();
    /// <summary>계획한 판(다른 판이면 다시 계획한다).</summary>
    public MapData Map { get; private set; }

    private readonly List<Vector2Int> _cutTry = new List<Vector2Int>();
    private int[] _capacity = new int[0];
    private int[][] _adjacent;
    private int[] _flow = new int[0];
    private int[] _cap = new int[0];
    private int[] _to = new int[0];
    private int _edges;

    public void Plan(MapData map, float radius, float cutCost, float dwellWeight)
    {
        Map = map;
        Planned = false;
        Cut.Clear();
        Zone.Clear();
        float best = float.MinValue;
        int bestDistance = int.MaxValue;
        int[] goalField = MapPathfinder.BuildDistanceField(map);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int center = new Vector2Int(x, y);
                if (!map.IsWalkable(x, y) || IsSpawn(map, center) || map.GoalRect.Contains(center)) continue;
                int distance = goalField[map.ToIndex(x, y)];
                if (distance == MapPathfinder.Unreachable) continue;
                if (!TryCut(map, center, _cutTry)) continue;
                int room = CountRoom(map, center, radius, _cutTry);
                float score = room + Dwell(map, _cutTry, null, center, radius) * dwellWeight - _cutTry.Count * cutCost;
                if (score < best || (score == best && distance >= bestDistance)) continue;
                best = score;
                bestDistance = distance;
                Center = center;
                Cut.Clear();
                Cut.AddRange(_cutTry);
                Planned = true;
            }
        }
        if (!Planned) return;
        CollectZone(map, Center, radius, Cut, Zone);
    }

    /// <summary>
    /// 머무는 길: blocked(와 extra)를 막은 판에서 출현 차례(SpawnGates)마다 목표물까지 가장 짧은 길(4방향)을 따라가며
    /// center에서 radius 안에 든 칸 수를 더한다. 못 가는 구멍이 있으면 -1(그렇게 막을 수 없다).
    /// </summary>
    public static int Dwell(MapData map, List<Vector2Int> blocked, Vector2Int? extra, Vector2Int center, float radius)
    {
        int[] distance = DistanceField(map, blocked, extra);
        float sqr = radius * radius;
        int total = 0;
        for (int g = 0; g < map.SpawnGates.Count; g++)
        {
            Vector2Int cell = map.SpawnGates[g];
            int d = distance[map.ToIndex(cell.x, cell.y)];
            if (d < 0) return -1;
            while (d > 0)
            {
                if (((Vector2)(cell - center)).sqrMagnitude <= sqr) total++;
                Vector2Int next = cell;
                for (int k = 0; k < Directions.Length; k++)
                {
                    Vector2Int candidate = cell + Directions[k];
                    if (!map.InBounds(candidate.x, candidate.y) || distance[map.ToIndex(candidate.x, candidate.y)] != d - 1) continue;
                    next = candidate;
                    break;
                }
                if (next == cell) break;
                cell = next;
                d--;
            }
        }
        return total;
    }

    /// <summary>목표물에서 거꾸로 잰 걸음 수(4방향, blocked와 extra를 더 막은 판). 못 가는 칸은 -1. 딜 점수(GameBot.DamageScore)도 쓴다.</summary>
    public static int[] DistanceFieldOf(MapData map, List<Vector2Int> blocked, Vector2Int? extra) => DistanceField(map, blocked, extra);

    // 목표물에서 거꾸로 잰 걸음 수(4방향). 못 가는 칸은 -1.
    private static int[] DistanceField(MapData map, List<Vector2Int> blocked, Vector2Int? extra)
    {
        int[] distance = new int[map.Width * map.Height];
        for (int i = 0; i < distance.Length; i++) distance[i] = -1;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        RectInt goal = map.GoalRect;
        for (int y = goal.yMin; y < goal.yMax; y++)
            for (int x = goal.xMin; x < goal.xMax; x++)
            {
                distance[map.ToIndex(x, y)] = 0;
                queue.Enqueue(new Vector2Int(x, y));
            }
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            int d = distance[map.ToIndex(cell.x, cell.y)];
            for (int k = 0; k < Directions.Length; k++)
            {
                Vector2Int next = cell + Directions[k];
                if (!map.InBounds(next.x, next.y)) continue;
                int i = map.ToIndex(next.x, next.y);
                if (distance[i] >= 0 || !map.IsWalkable(next.x, next.y)) continue;
                if ((blocked != null && blocked.Contains(next)) || (extra.HasValue && next == extra.Value)) continue;
                distance[i] = d + 1;
                queue.Enqueue(next);
            }
        }
        return distance;
    }

    private static bool IsSpawn(MapData map, Vector2Int cell)
    {
        for (int i = 0; i < map.Spawns.Count; i++)
            if (map.Spawns[i] == cell) return true;
        return false;
    }

    // 둘레: 반경 안의 지을 수 있는 칸(이미 놓인 주사위 칸도 센다 — 거기에 얹으면 된다). 막을 칸·중심·출현·목표물은 뺀다.
    private static int CountRoom(MapData map, Vector2Int center, float radius, List<Vector2Int> cut)
    {
        int count = 0;
        int r = Mathf.CeilToInt(radius);
        float sqr = radius * radius;
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                Vector2Int cell = center + new Vector2Int(dx, dy);
                if (dx * dx + dy * dy > sqr || !IsRoom(map, cell, center, cut)) continue;
                count++;
            }
        }
        return count;
    }

    private static void CollectZone(MapData map, Vector2Int center, float radius, List<Vector2Int> cut, List<Vector2Int> zone)
    {
        int r = Mathf.CeilToInt(radius);
        float sqr = radius * radius;
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                Vector2Int cell = center + new Vector2Int(dx, dy);
                if (dx * dx + dy * dy > sqr || !IsRoom(map, cell, center, cut)) continue;
                zone.Add(cell);
            }
        }
        zone.Sort((a, b) => ((Vector2)(a - center)).sqrMagnitude.CompareTo(((Vector2)(b - center)).sqrMagnitude));
    }

    private static bool IsRoom(MapData map, Vector2Int cell, Vector2Int center, List<Vector2Int> cut)
    {
        if (cell == center || !map.InBounds(cell.x, cell.y) || !map.IsBuildable(cell.x, cell.y) || map.HasChip(cell.x, cell.y)) return false;
        if (IsSpawn(map, cell) || map.GoalRect.Contains(cell)) return false;
        return !cut.Contains(cell);
    }

    // center를 뺀 판에서 출현 구멍 → 목표물의 정점 최소 절단을 cut에 채운다. 끊은 뒤 모든 구멍이 center를 거쳐 목표물에 닿아야 한다.
    // 그냥 끊으면 구멍 둘레를 통째로 막는 답(구멍이 갇힘)이 가장 싸서, center의 이웃을 구멍 쪽·목표물 쪽으로 나누는 경우를 모두 해 본다 —
    // 구멍 쪽 이웃은 출발점, 목표물 쪽 이웃은 도착점에 붙여 끊으면 center가 두 쪽을 잇는 유일한 다리가 된다. 되는 답 중 가장 적게 막는 것.
    private readonly List<Vector2Int> _cutBest = new List<Vector2Int>();
    private readonly List<int> _sides = new List<int>(4);

    private bool TryCut(MapData map, Vector2Int center, List<Vector2Int> cut)
    {
        cut.Clear();
        _cutBest.Clear();
        int cells = map.Width * map.Height;
        _sides.Clear();
        for (int d = 0; d < Directions.Length; d++)
        {
            Vector2Int next = center + Directions[d];
            if (map.InBounds(next.x, next.y) && map.IsWalkable(next.x, next.y)) _sides.Add(map.ToIndex(next.x, next.y));
        }
        if (_sides.Count < 2) return false;
        bool found = false;
        int combos = 1 << _sides.Count;
        for (int mask = 1; mask < combos - 1; mask++)
        {
            BuildGraph(map, center, cells, mask);
            int source = cells * 2;
            int sink = source + 1;
            if (MaxFlow(source, sink) >= Infinite) continue;
            // 남은 용량으로 출발점에서 닿는 쪽: 들어가는 쪽은 닿고 나가는 쪽은 못 닿는 칸이 절단이다.
            bool[] reached = Reach(source, cells * 2 + 2);
            cut.Clear();
            for (int i = 0; i < cells; i++)
            {
                if (reached[i * 2] && !reached[i * 2 + 1] && _capacity[i] == 1)
                    cut.Add(new Vector2Int(i % map.Width, i / map.Width));
            }
            if (found && cut.Count >= _cutBest.Count) continue;
            if (!AllReachThrough(map, center, cut)) continue;
            found = true;
            _cutBest.Clear();
            _cutBest.AddRange(cut);
        }
        cut.Clear();
        cut.AddRange(_cutBest);
        return found;
    }

    // mask: center 이웃(_sides) 중 비트가 선 것은 구멍 쪽(출발점에 붙임), 아닌 것은 목표물 쪽(도착점에 붙임).
    private void BuildGraph(MapData map, Vector2Int center, int cells, int mask)
    {
        if (_capacity.Length != cells) _capacity = new int[cells];
        int nodes = cells * 2 + 2;
        int maxEdges = (cells * 6 + map.Spawns.Count + 8) * 2;
        if (_cap.Length < maxEdges)
        {
            _cap = new int[maxEdges];
            _flow = new int[maxEdges];
            _to = new int[maxEdges];
        }
        if (_adjacent == null || _adjacent.Length != nodes) _adjacent = new int[nodes][];
        _edges = 0;
        List<int>[] lists = new List<int>[nodes];
        for (int i = 0; i < nodes; i++) lists[i] = new List<int>(6);

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.ToIndex(x, y);
                Vector2Int cell = new Vector2Int(x, y);
                bool walkable = map.IsWalkable(x, y) && cell != center;
                // 막을 수 있는 칸 = 지을 수 있는 빈 칸(출현·목표물 빼고). 못 막는 길(출현·목표물·지을 수 없는 길)은 무한.
                bool blockable = walkable && map.IsBuildable(x, y) && !IsSpawn(map, cell) && !map.GoalRect.Contains(cell);
                _capacity[i] = !walkable ? 0 : blockable ? 1 : Infinite;
                if (_capacity[i] > 0) AddEdge(lists, i * 2, i * 2 + 1, _capacity[i]);
            }
        }
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.ToIndex(x, y);
                if (_capacity[i] == 0) continue;
                for (int d = 0; d < Directions.Length; d++)
                {
                    int nx = x + Directions[d].x, ny = y + Directions[d].y;
                    if (!map.InBounds(nx, ny)) continue;
                    int j = map.ToIndex(nx, ny);
                    if (_capacity[j] == 0) continue;
                    AddEdge(lists, i * 2 + 1, j * 2, Infinite);
                }
            }
        }
        int source = cells * 2, sink = source + 1;
        for (int s = 0; s < map.Spawns.Count; s++)
        {
            int i = map.ToIndex(map.Spawns[s].x, map.Spawns[s].y);
            if (_capacity[i] > 0) AddEdge(lists, source, i * 2, Infinite);
        }
        RectInt goal = map.GoalRect;
        for (int gy = goal.yMin; gy < goal.yMax; gy++)
            for (int gx = goal.xMin; gx < goal.xMax; gx++)
            {
                int i = map.ToIndex(gx, gy);
                if (_capacity[i] > 0) AddEdge(lists, i * 2 + 1, sink, Infinite);
            }
        for (int k = 0; k < _sides.Count; k++)
        {
            int i = _sides[k];
            if (_capacity[i] == 0) continue;
            if ((mask & (1 << k)) != 0) AddEdge(lists, source, i * 2, Infinite);
            else AddEdge(lists, i * 2 + 1, sink, Infinite);
        }
        for (int i = 0; i < nodes; i++) _adjacent[i] = lists[i].ToArray();
    }

    private void AddEdge(List<int>[] lists, int from, int to, int capacity)
    {
        _to[_edges] = to; _cap[_edges] = capacity; _flow[_edges] = 0; lists[from].Add(_edges); _edges++;
        _to[_edges] = from; _cap[_edges] = 0; _flow[_edges] = 0; lists[to].Add(_edges); _edges++;
    }

    // 에드먼즈-카프. 무한을 넘으면 거기서 멈춘다(끊을 수 없는 판).
    private int MaxFlow(int source, int sink)
    {
        int total = 0;
        int nodes = _adjacent.Length;
        int[] parentEdge = new int[nodes];
        Queue<int> queue = new Queue<int>();
        while (total < Infinite)
        {
            for (int i = 0; i < nodes; i++) parentEdge[i] = -1;
            queue.Clear();
            queue.Enqueue(source);
            parentEdge[source] = -2;
            while (queue.Count > 0 && parentEdge[sink] == -1)
            {
                int node = queue.Dequeue();
                int[] edges = _adjacent[node];
                for (int k = 0; k < edges.Length; k++)
                {
                    int e = edges[k];
                    int next = _to[e];
                    if (parentEdge[next] != -1 || _cap[e] - _flow[e] <= 0) continue;
                    parentEdge[next] = e;
                    queue.Enqueue(next);
                }
            }
            if (parentEdge[sink] == -1) break;
            int push = Infinite;
            for (int node = sink; node != source; node = _to[parentEdge[node] ^ 1])
                push = Mathf.Min(push, _cap[parentEdge[node]] - _flow[parentEdge[node]]);
            for (int node = sink; node != source; node = _to[parentEdge[node] ^ 1])
            {
                _flow[parentEdge[node]] += push;
                _flow[parentEdge[node] ^ 1] -= push;
            }
            total += push;
        }
        return total;
    }

    private bool[] Reach(int source, int nodes)
    {
        bool[] seen = new bool[nodes];
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(source);
        seen[source] = true;
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            int[] edges = _adjacent[node];
            for (int k = 0; k < edges.Length; k++)
            {
                int e = edges[k];
                int next = _to[e];
                if (seen[next] || _cap[e] - _flow[e] <= 0) continue;
                seen[next] = true;
                queue.Enqueue(next);
            }
        }
        return seen;
    }

    // 막을 칸을 막은 판에서 모든 출현 구멍이 목표물에 닿고, center를 빼면 아무도 못 닿는지(모든 길이 center를 지나는지).
    private static bool AllReachThrough(MapData map, Vector2Int center, List<Vector2Int> cut)
    {
        bool[] withCenter = Flood(map, cut, null);
        bool[] withoutCenter = Flood(map, cut, center);
        for (int s = 0; s < map.Spawns.Count; s++)
        {
            int i = map.ToIndex(map.Spawns[s].x, map.Spawns[s].y);
            if (!withCenter[i] || withoutCenter[i]) return false;
        }
        return true;
    }

    // 목표물에서 거꾸로 걸어 닿는 칸(4방향). blocked는 막은 칸, skip은 더 막을 칸.
    private static bool[] Flood(MapData map, List<Vector2Int> blocked, Vector2Int? skip)
    {
        bool[] seen = new bool[map.Width * map.Height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        RectInt goal = map.GoalRect;
        for (int y = goal.yMin; y < goal.yMax; y++)
            for (int x = goal.xMin; x < goal.xMax; x++)
            {
                seen[map.ToIndex(x, y)] = true;
                queue.Enqueue(new Vector2Int(x, y));
            }
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int next = cell + Directions[d];
                if (!map.InBounds(next.x, next.y)) continue;
                int i = map.ToIndex(next.x, next.y);
                if (seen[i] || !map.IsWalkable(next.x, next.y) || blocked.Contains(next) || (skip.HasValue && next == skip.Value)) continue;
                seen[i] = true;
                queue.Enqueue(next);
            }
        }
        return seen;
    }
}
