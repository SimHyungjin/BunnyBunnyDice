using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 미로 계획(기획서 11.4). 판이 바뀔 때마다(맵·주사위·칩) 목표 배치를 담금질로 찾는다 — 계획한 칸 하나를 다른 빈 칸으로 옮겨 보고
/// 점수(MazeEvaluator)가 오르면 받아들이고, 내려가도 처음엔 가끔 받아들여 국소 최선에 갇히지 않게 한다. 한 프레임에 조금씩 돌린다.
/// 봇과 어시스트가 같은 계산을 쓴다 — 봇의 실력과 어시스트의 추천이 함께 오른다.
/// 탐색은 무거워(걸음마다 길 전체 계산) 내다본 최선을 쓰는 쪽이 있을 때만(Searching) 돌리고, 한 프레임에 쓸 시간을 정해 끊는다.
/// </summary>
public sealed class MazePlanner : IDisposable
{
    private readonly SO_PlacementPreviewConfig _config;
    private readonly Func<MapData> _getMap;
    private readonly DiceField _diceField;
    private readonly MazeEvaluator _evaluator = new MazeEvaluator();
    private readonly List<Vector2Int> _plan = new List<Vector2Int>();
    private readonly List<Vector2Int> _best = new List<Vector2Int>();
    private readonly List<Vector2Int> _free = new List<Vector2Int>();
    private readonly List<Vector2Int> _towers = new List<Vector2Int>();
    private readonly List<Vector2Int> _single = new List<Vector2Int>(1);
    private System.Random _random = new System.Random(0);
    private MapData _map;
    private int _signature;
    private int _iteration;
    private float _score;
    private float _bestScore;
    private readonly Stopwatch _watch = new Stopwatch();

    /// <summary>찾아 둔 목표 배치(아직 놓이지 않은 칸들). 탐색 중이면 지금까지 가장 좋았던 것.</summary>
    public IReadOnlyList<Vector2Int> Plan => _best;

    /// <summary>
    /// 목표 배치를 찾아 둘지 — 내다본 최선을 쓰는 쪽(봇·개발용 어시스트)이 켠다. 아무도 안 쓰는데 돌면 놓을 때마다 프레임이 끊긴다.
    /// </summary>
    public bool Searching { get; set; }

    /// <summary>이 판의 탐색을 마쳤는지.</summary>
    public bool Ready => _iteration >= _config.MazeIterations;

    public MazePlanner(SO_PlacementPreviewConfig config, Func<MapData> getMap, DiceField diceField)
    {
        _config = config;
        _getMap = getMap;
        _diceField = diceField;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnUpdate -= Tick;

    /// <summary>
    /// 다음에 놓을 새 칸: 판 전체를 탐색해 둔 목표 배치 중 지금 놓았을 때 점수가 가장 높은 칸.
    /// 놓을 칸이 없으면(목표 배치를 다 지었거나 길을 막는 칸뿐) false.
    /// 한 장만 보고 모든 빈 칸 중 당장 가장 좋은 칸을 고르는 방식(당장의 최선)도 있었는데, 주사위를 한 줄로 늘어놓을 뿐 미로를 거의 못 지어
    /// 기준선으로도 비교군으로도 쓸 데가 없어 지웠다(2026-10-28).
    /// </summary>
    public bool TryGetNext(out Vector2Int cell)
    {
        cell = default;
        MapData map = _getMap();
        if (map == null) return false;
        Refresh(map);
        CollectTowers();
        float best = float.MinValue;
        for (int i = 0; i < _best.Count; i++) Consider(map, _best[i], ref best, ref cell);
        return best > float.MinValue;
    }

    /// <summary>목표 배치 중 아직 놓을 수 있는 칸이 남았는지(내다보는 봇이 새 칸 대신 얹기로 넘어가는 기준).</summary>
    public bool HasPlannedCell()
    {
        MapData map = _getMap();
        if (map == null) return false;
        Refresh(map);
        for (int i = 0; i < _best.Count; i++)
        {
            if (DicePlacement.Check(map, _best[i]) == PlacementResult.Ok) return true;
        }
        return false;
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

    /// <summary>
    /// 지금 판의 탐색을 끝까지(MazeIterations 걸음) 마친다 — 봇이 계획을 읽기 직전에 부른다.
    /// 프레임마다 시간으로 끊어 도는 탐색(Searching)은 읽는 순간 몇 걸음까지 갔는지가 기기·프레임에 따라 달라,
    /// 같은 시드·같은 선택이라도 봇이 다른 칸을 고른다(시뮬레이션 재현이 깨진다). 끝까지 돌리면 계획은 판 상태만으로 정해진다.
    /// 한 번에 몰아 계산하므로 놓을 때마다 잠깐 멈출 수 있다 — 봇(시뮬레이션) 전용이다.
    /// </summary>
    public void CompleteSearch()
    {
        MapData map = _getMap();
        if (map == null) return;
        Refresh(map);
        while (_iteration < _config.MazeIterations) Step(map);
    }

    private void Tick(float _)
    {
        MapData map = _getMap();
        if (!Searching || map == null) return;
        Refresh(map);
        // 걸음 수가 아니라 시간으로 끊는다 — 맵 크기·기기에 따라 한 걸음 비용이 달라 걸음 수로는 렉을 막지 못한다.
        _watch.Restart();
        double budget = _config.MazeFrameBudgetMs;
        while (_iteration < _config.MazeIterations && _watch.Elapsed.TotalMilliseconds < budget) Step(map);
    }

    // 판이 바뀌었으면(맵·주사위·칩) 지금 계획에서 이어 다시 찾는다 — 놓인 칸은 계획에서 빠지고 모자란 만큼 채운다.
    private void Refresh(MapData map)
    {
        int signature = Signature(map);
        if (map == _map && signature == _signature) return;
        // 새 맵(새 판)이면 지난 판의 계획을 이어 쓰지 않는다 — 이어 쓰면 같은 시드라도 지난 판이 어떻게 끝났는지에 따라 계획이 달라진다.
        if (map != _map) _best.Clear();
        _map = map;
        _signature = signature;
        _random = new System.Random(signature);
        _iteration = 0;

        _plan.Clear();
        for (int i = 0; i < _best.Count; i++)
        {
            if (map.InBounds(_best[i].x, _best[i].y) && IsFree(map, _best[i])) _plan.Add(_best[i]);
        }
        _free.Clear();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsFree(map, cell) && !_plan.Contains(cell)) _free.Add(cell);
            }
        }

        CollectTowers();
        int target = Mathf.Max(_config.MazeTargetDice - _diceField.Placed.Count, 0);
        while (_plan.Count > target) _plan.RemoveAt(_plan.Count - 1);
        _score = Score(map);
        // 길을 막지 않는 칸으로만 하나씩 채운다.
        int guard = _free.Count * 2;
        while (_plan.Count < target && _free.Count > 0 && guard-- > 0)
        {
            int index = _random.Next(_free.Count);
            Vector2Int cell = _free[index];
            _plan.Add(cell);
            float score = Score(map);
            if (score == float.MinValue)
            {
                _plan.RemoveAt(_plan.Count - 1);
                continue;
            }
            _free.RemoveAt(index);
            _score = score;
        }
        if (_score == float.MinValue) _plan.Clear();
        _best.Clear();
        _best.AddRange(_plan);
        _bestScore = _score;
    }

    // 담금질 한 걸음: 계획한 칸 하나를 빈 칸으로 옮겨 본다.
    private void Step(MapData map)
    {
        _iteration++;
        if (_plan.Count == 0 || _free.Count == 0) return;
        int planIndex = _random.Next(_plan.Count);
        int freeIndex = _random.Next(_free.Count);
        Vector2Int from = _plan[planIndex];
        Vector2Int to = _free[freeIndex];
        _plan[planIndex] = to;
        float score = Score(map);
        float t = _iteration / (float)Mathf.Max(_config.MazeIterations, 1);
        float temperature = Mathf.Lerp(_config.MazeStartTemperature, _config.MazeEndTemperature, t);
        bool accept = score > float.MinValue
            && (score >= _score || _random.NextDouble() < Math.Exp((score - _score) / temperature));
        if (!accept)
        {
            _plan[planIndex] = from;
            return;
        }
        _free[freeIndex] = from;
        _score = score;
        if (score <= _bestScore) return;
        _bestScore = score;
        _best.Clear();
        _best.AddRange(_plan);
    }

    private float Score(MapData map)
    {
        // 사거리로 덮는 것은 놓인 주사위 + 계획한 칸(칩은 쏘지 않는다).
        CollectTowers();
        _towers.AddRange(_plan);
        return _evaluator.Evaluate(map, _plan, _towers, _config.MazeAssumedRange, _config.MazeCoverageWeight,
            _config.MazeBossWeight);
    }

    private static bool IsFree(MapData map, Vector2Int cell) =>
        map.IsBuildable(cell.x, cell.y) && !map.HasDice(cell.x, cell.y) && !map.HasChip(cell.x, cell.y);

    // 판의 벽 모양 — 바뀌면 다시 찾는다. 같은 판이면 같은 수라 같은 계획이 나온다(시드 재현).
    private static int Signature(MapData map)
    {
        int hash = map.Width * 31 + map.Height;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int wall = map.HasDice(x, y) ? 1 : map.HasChip(x, y) ? 2 : map.IsBuildable(x, y) ? 0 : 3;
                hash = hash * 31 + wall;
                for (int i = 0; i < map.Spawns.Count; i++) if (map.Spawns[i].x == x && map.Spawns[i].y == y) hash = hash * 7 + 5;
            }
        }
        return hash;
    }
}
