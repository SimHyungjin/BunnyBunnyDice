using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 미로 계획(기획서 11.4). 목표 배치(주사위를 놓을 칸들)를 들고, 봇이 그 칸부터 짓게 한다.
/// 봇과 어시스트가 같은 계산을 쓴다 — 봇의 실력과 어시스트의 추천이 함께 오른다.
///
/// 첫 계획은 맵 생성 검사가 지은 미로(MapData.MazePlan)다 — 검사가 "이 미로로 풀린다"고 보고 판을 통과시켰으니 그 미로를 짓는 것이 일관된다.
/// 다시 찾는 것은 벽이 계획과 다르게 바뀔 때뿐이다: 계획 밖 칸에 벽이 생기거나 벽이 없어질 때. 계획한 칸에 벽이 생긴 것은 계획대로 지은 것이라
/// 최선이 그대로다(매번 다시 찾으면 탐색 대부분이 이미 아는 답을 다시 찾는다). 벽은 주사위·칩을 가리지 않는다 — 미로에서는 둘 다 막는 칸이다.
/// 다시 찾을 때는 담금질로 받은 계획에서 이어 찾는다 — 계획한 칸 하나를 다른 빈 칸으로 옮겨 보고 점수(MazeEvaluator)가 오르면 받아들이고,
/// 내려가도 처음엔 가끔 받아들여 국소 최선에 갇히지 않게 한다.
///
/// 탐색은 무거워(걸음마다 길 전체 계산, 끝까지 약 1.6초) 판 사본을 워커 스레드에 넘겨 돌린다.
/// 결과는 끝난 순간이 아니라 시작하고 정해진 게임 시간(thinkTime)이 지난 걸음에 받는다 — 끝나는 순간은 실제 시간이라
/// 그때 받으면 같은 시드·같은 선택이라도 기기·프레임에 따라 받는 걸음이 달라 판이 갈라진다(시뮬레이션 재현, 기획서 11.2).
/// 그 걸음까지 안 끝났으면 그 자리에서 기다린다(배속이 생각 시간보다 빠를 때만 멈춘다).
/// 시작·받기는 게임 걸음 안(Advance)에서만 하므로 언제 무엇을 계획하는지가 게임 상태만으로 정해진다.
/// </summary>
public sealed class MazePlanner : IDisposable
{
    private readonly SO_PlacementPreviewConfig _config;
    private readonly Func<MapData> _getMap;
    private readonly DiceField _diceField;
    private readonly MazeEvaluator _evaluator = new MazeEvaluator();
    // 받은 계획(아직 놓이지 않은 칸들). 탐색 중에도 직전에 받은 것을 그대로 보여 준다.
    private readonly List<Vector2Int> _best = new List<Vector2Int>();
    private readonly List<Vector2Int> _towers = new List<Vector2Int>();
    private readonly List<Vector2Int> _single = new List<Vector2Int>(1);
    // 지금 계획(_best)이 바탕으로 한 판과, 마지막으로 본 칸마다의 벽(놓인 주사위·칩) — 계획 밖에서 바뀌었는지 볼 때 비교한다.
    private MapData _map;
    private bool[] _walls = new bool[0];
    private Search _search;
    private float _searchAge;

    /// <summary>받은 목표 배치(아직 놓이지 않은 칸들). 탐색 중이면 직전에 받은 것.</summary>
    public IReadOnlyList<Vector2Int> Plan => _best;

    /// <summary>탐색 중이라 지금 계획이 판과 맞지 않는지. 봇은 이 동안 새 주사위를 놓지 않는다(생각 중).</summary>
    public bool Busy => _search != null;

    /// <summary>받은 탐색 수와, 생각 시간 안에 못 끝나 기다린 실제 시간의 합(초) — 배속·걸음 수를 정할 때 본다.</summary>
    public int AdoptCount { get; private set; }
    public double WaitSeconds { get; private set; }

    public MazePlanner(SO_PlacementPreviewConfig config, Func<MapData> getMap, DiceField diceField)
    {
        _config = config;
        _getMap = getMap;
        _diceField = diceField;
    }

    public void Dispose() => Cancel();

    /// <summary>
    /// 게임 걸음마다 부른다(쓰는 쪽 — 봇 — 의 OnGameUpdate 안). 새 판이면 검사가 지은 미로를 계획으로 받고,
    /// 벽이 계획 밖에서 바뀌었으면 새 탐색을 시작하고, 탐색을 시작한 지 thinkTime(게임 시간)이 지났으면 결과를 받는다(안 끝났으면 기다린다).
    /// </summary>
    public void Advance(float deltaTime, float thinkTime)
    {
        MapData map = _getMap();
        if (map == null) return;

        if (_search != null)
        {
            _searchAge += deltaTime;
            if (_searchAge >= thinkTime) Adopt();
        }

        if (map != _map)
        {
            // 새 판: 지난 판의 계획·탐색은 버린다 — 이어 쓰면 같은 시드라도 지난 판이 어떻게 끝났는지에 따라 계획이 달라진다.
            Cancel();
            _map = map;
            _best.Clear();
            _best.AddRange(map.MazePlan);
            ReadWalls(map);
            // 검사가 없던 판(검사를 끈 설정)만 처음부터 찾는다.
            if (_best.Count == 0) Start(map);
            return;
        }
        if (ReadWalls(map)) Start(map);
    }

    // 칸마다 벽(놓인 주사위·칩)을 읽어 지난번과 비교한다. 계획 밖에 벽이 생겼거나 벽이 없어졌으면 true(다시 찾는다).
    // 계획한 칸에 벽이 생긴 것은 계획대로 지은 것이라 넘어간다. 얹기·주사위 위 칩은 벽이 그대로라 아무 일도 없다.
    private bool ReadWalls(MapData map)
    {
        int count = map.Width * map.Height;
        bool fresh = _walls.Length != count;
        if (fresh) _walls = new bool[count];
        bool offPlan = false;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int index = map.ToIndex(x, y);
                bool wall = map.HasDice(x, y) || map.HasChip(x, y);
                if (wall == _walls[index]) continue;
                _walls[index] = wall;
                if (fresh || (wall && _best.Contains(new Vector2Int(x, y)))) continue;
                offPlan = true;
            }
        }
        return offPlan;
    }

    /// <summary>
    /// 다음에 놓을 새 칸: 받은 목표 배치 중 지금 놓았을 때 점수가 가장 높은 칸. allow가 있으면 그것이 허락하는 칸만 본다
    /// (봇이 메인 딜 구간 칸을 d20 자리로 남길 때). 놓을 칸이 없으면(목표 배치를 다 지었거나 길을 막는 칸뿐) false.
    /// </summary>
    public bool TryGetNext(Func<Vector2Int, bool> allow, out Vector2Int cell)
    {
        cell = default;
        MapData map = _getMap();
        if (map == null) return false;
        CollectTowers();
        float best = float.MinValue;
        for (int i = 0; i < _best.Count; i++)
        {
            if (allow != null && !allow(_best[i])) continue;
            Consider(map, _best[i], ref best, ref cell);
        }
        return best > float.MinValue;
    }

    /// <summary>목표 배치 중 아직 놓을 수 있는 칸이 남았는지(내다보는 봇이 새 칸 대신 얹기로 넘어가는 기준).</summary>
    public bool HasPlannedCell()
    {
        MapData map = _getMap();
        if (map == null) return false;
        for (int i = 0; i < _best.Count; i++)
        {
            if (DicePlacement.Check(map, _best[i]) == PlacementResult.Ok) return true;
        }
        return false;
    }

    // 탐색 시작: 판 사본·놓인 주사위·지금 계획·설정 값을 넘긴다(워커는 Unity 객체와 판 원본을 읽지 않는다).
    // 판이 또 바뀌어 새로 시작하면 하던 탐색은 버린다(결과를 받지 않고 멈추라고 알린다).
    private void Start(MapData map)
    {
        Cancel();
        CollectTowers();
        Search search = new Search(map.Clone(), new List<Vector2Int>(_towers), new List<Vector2Int>(_best), Signature(map),
            Mathf.Max(_config.MazeTargetDice - _towers.Count, 0), _config.MazeIterations, _config.MazeAssumedRange,
            _config.MazeCoverageWeight, _config.MazeBossWeight, _config.MazeStartTemperature, _config.MazeEndTemperature);
        search.Task = Task.Run(search.Run);
        _search = search;
        _searchAge = 0f;
    }

    private void Adopt()
    {
        Search search = _search;
        _search = null;
        // 생각 시간 안에 못 끝냈으면 여기서 기다린다 — 기다리는 동안 게임이 멈출 뿐 결과는 같다.
        if (!search.Task.IsCompleted)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            search.Task.Wait();
            WaitSeconds += watch.Elapsed.TotalSeconds;
        }
        AdoptCount++;
        _best.Clear();
        _best.AddRange(search.Best);
    }

    private void Cancel()
    {
        if (_search == null) return;
        _search.Cancelled = true;
        _search = null;
    }

    private void Consider(MapData map, Vector2Int candidate, ref float best, ref Vector2Int cell)
    {
        if (DicePlacement.Check(map, candidate) != PlacementResult.Ok) return;
        _single.Clear();
        _single.Add(candidate);
        _towers.Add(candidate);
        float score = _evaluator.Evaluate(map, _single, _towers, _config.MazeAssumedRange, _config.MazeCoverageWeight,
            _config.MazeBossWeight);
        _towers.RemoveAt(_towers.Count - 1);
        if (score <= best) return;
        best = score;
        cell = candidate;
    }

    private void CollectTowers()
    {
        _towers.Clear();
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++) _towers.Add(placed[i].Cell);
    }

    private static bool IsFree(MapData map, Vector2Int cell) =>
        map.IsBuildable(cell.x, cell.y) && !map.HasDice(cell.x, cell.y) && !map.HasChip(cell.x, cell.y);

    // 판의 벽 모양 — 탐색 난수의 시드다. 같은 판이면 같은 수라 같은 계획이 나온다(시드 재현). 주사위·칩은 같은 벽으로 센다.
    private static int Signature(MapData map)
    {
        int hash = map.Width * 31 + map.Height;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int wall = map.HasDice(x, y) || map.HasChip(x, y) ? 1 : map.IsBuildable(x, y) ? 0 : 3;
                hash = hash * 31 + wall;
                for (int i = 0; i < map.Spawns.Count; i++) if (map.Spawns[i].x == x && map.Spawns[i].y == y) hash = hash * 7 + 5;
            }
        }
        return hash;
    }

    /// <summary>
    /// 탐색 하나(워커 스레드). 받은 사본만 읽고 결과는 Best에 남긴다. 난수는 판 모양(signature)으로 정해 같은 판이면 같은 계획이 나온다.
    /// Unity API를 쓰지 않는다(Mathf·Vector2Int 같은 값 계산만 — MazeRouteProbe와 같다).
    /// </summary>
    private sealed class Search
    {
        private readonly MapData _map;
        private readonly List<Vector2Int> _towers;
        private readonly List<Vector2Int> _plan;
        private readonly List<Vector2Int> _free = new List<Vector2Int>();
        private readonly List<Vector2Int> _scoreTowers = new List<Vector2Int>();
        private readonly MazeEvaluator _evaluator = new MazeEvaluator();
        private readonly System.Random _random;
        private readonly int _target;
        private readonly int _iterations;
        private readonly float _range;
        private readonly float _coverageWeight;
        private readonly float _bossWeight;
        private readonly float _startTemperature;
        private readonly float _endTemperature;

        public readonly List<Vector2Int> Best = new List<Vector2Int>();
        public Task Task;
        public volatile bool Cancelled;

        public Search(MapData map, List<Vector2Int> towers, List<Vector2Int> previous, int signature, int target, int iterations,
            float range, float coverageWeight, float bossWeight, float startTemperature, float endTemperature)
        {
            _map = map;
            _towers = towers;
            _plan = previous;
            _random = new System.Random(signature);
            _target = target;
            _iterations = iterations;
            _range = range;
            _coverageWeight = coverageWeight;
            _bossWeight = bossWeight;
            _startTemperature = startTemperature;
            _endTemperature = endTemperature;
        }

        public void Run()
        {
            // 받은 계획에서 이미 막힌 칸(놓인 주사위·칩)을 빼고, 남은 빈 칸을 모은다.
            for (int i = _plan.Count - 1; i >= 0; i--)
            {
                if (!_map.InBounds(_plan[i].x, _plan[i].y) || !IsFree(_map, _plan[i])) _plan.RemoveAt(i);
            }
            for (int y = 0; y < _map.Height; y++)
            {
                for (int x = 0; x < _map.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (IsFree(_map, cell) && !_plan.Contains(cell)) _free.Add(cell);
                }
            }

            while (_plan.Count > _target) _plan.RemoveAt(_plan.Count - 1);
            float score = Score();
            // 길을 막지 않는 칸으로만 하나씩 채운다.
            int guard = _free.Count * 2;
            while (_plan.Count < _target && _free.Count > 0 && guard-- > 0)
            {
                int index = _random.Next(_free.Count);
                _plan.Add(_free[index]);
                float next = Score();
                if (next == float.MinValue)
                {
                    _plan.RemoveAt(_plan.Count - 1);
                    continue;
                }
                _free.RemoveAt(index);
                score = next;
            }
            if (score == float.MinValue) _plan.Clear();
            Best.AddRange(_plan);
            float bestScore = score;

            // 담금질: 계획한 칸 하나를 빈 칸으로 옮겨 본다.
            for (int i = 1; i <= _iterations && !Cancelled; i++)
            {
                if (_plan.Count == 0 || _free.Count == 0) break;
                int planIndex = _random.Next(_plan.Count);
                int freeIndex = _random.Next(_free.Count);
                Vector2Int from = _plan[planIndex];
                _plan[planIndex] = _free[freeIndex];
                float next = Score();
                float t = i / (float)Mathf.Max(_iterations, 1);
                float temperature = Mathf.Lerp(_startTemperature, _endTemperature, t);
                bool accept = next > float.MinValue
                    && (next >= score || _random.NextDouble() < Math.Exp((next - score) / temperature));
                if (!accept)
                {
                    _plan[planIndex] = from;
                    continue;
                }
                _free[freeIndex] = from;
                score = next;
                if (score <= bestScore) continue;
                bestScore = score;
                Best.Clear();
                Best.AddRange(_plan);
            }
        }

        // 사거리로 덮는 것은 놓인 주사위 + 계획한 칸(칩은 쏘지 않는다).
        private float Score()
        {
            _scoreTowers.Clear();
            _scoreTowers.AddRange(_towers);
            _scoreTowers.AddRange(_plan);
            return _evaluator.Evaluate(_map, _plan, _scoreTowers, _range, _coverageWeight, _bossWeight);
        }
    }
}
