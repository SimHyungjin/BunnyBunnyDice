using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 생성 검사(기획서 3.3): 빈 맵에 미로 계획(MazePlanner와 같은 담금질)을 한 번에 돌려, 미로를 지은 뒤 구멍마다 목표물까지의 길 길이를 낸다.
/// 주사위 수가 모자라 한 구멍을 희생하는 배치가 나오는 맵(그 구멍의 길이 아주 짧다)이 판 결과를 크게 가른다(사용자 관찰, 봇 측정) — 생성기가 이 값으로 칩을 다시 놓는다.
/// 봇·어시스트와 같은 점수(출현 몫을 곱함)로 짓는다 — "이 판의 문제를 잘 푼 미로"에서도 짧은 구멍이 남는지를 본다.
/// 판 시작 전에 한 번 도는 계산이라 걸음 수를 MazeProbeIterations로 따로 줄여 둔다. 같은 맵·시드면 같은 값이다.
/// GameScene이 워커 스레드에서 부르므로 Unity API를 쓰지 않는다(MapGenerator·MazeEvaluator도 같다). 버퍼를 들고 있어 생성 하나에 하나씩 쓴다.
/// </summary>
public sealed class MazeRouteProbe : IMapRouteProbe
{
    private readonly SO_MazePlanConfig _config;
    private readonly MazeEvaluator _evaluator = new MazeEvaluator();
    private readonly List<Vector2Int> _plan = new List<Vector2Int>();
    private readonly List<Vector2Int> _best = new List<Vector2Int>();
    private readonly List<Vector2Int> _free = new List<Vector2Int>();

    public MazeRouteProbe(SO_MazePlanConfig config) => _config = config;

    public void Measure(MapData map, int seed, List<float> lengths, List<Vector2Int> plan)
    {
        lengths.Clear();
        plan.Clear();
        System.Random random = new System.Random(seed);
        _plan.Clear();
        _free.Clear();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.IsBuildable(x, y) && !map.HasChip(x, y)) _free.Add(new Vector2Int(x, y));
            }
        }

        // 길을 막지 않는 칸으로만 하나씩 채운다(MazePlanner.Refresh와 같다).
        float score = Score(map, _plan);
        int guard = _free.Count * 2;
        while (_plan.Count < _config.TargetDice && _free.Count > 0 && guard-- > 0)
        {
            int index = random.Next(_free.Count);
            _plan.Add(_free[index]);
            float next = Score(map, _plan);
            if (next == float.MinValue)
            {
                _plan.RemoveAt(_plan.Count - 1);
                continue;
            }
            _free.RemoveAt(index);
            score = next;
        }
        _best.Clear();
        _best.AddRange(_plan);
        float bestScore = score;

        int iterations = _config.ProbeIterations;
        for (int i = 1; i <= iterations && _plan.Count > 0 && _free.Count > 0; i++)
        {
            int planIndex = random.Next(_plan.Count);
            int freeIndex = random.Next(_free.Count);
            Vector2Int from = _plan[planIndex];
            _plan[planIndex] = _free[freeIndex];
            float next = Score(map, _plan);
            float temperature = Mathf.Lerp(_config.StartTemperature, _config.EndTemperature, i / (float)iterations);
            bool accept = next > float.MinValue
                && (next >= score || random.NextDouble() < System.Math.Exp((next - score) / temperature));
            if (!accept)
            {
                _plan[planIndex] = from;
                continue;
            }
            _free[freeIndex] = from;
            score = next;
            if (next <= bestScore) continue;
            bestScore = next;
            _best.Clear();
            _best.AddRange(_plan);
        }

        Score(map, _best);
        for (int i = 0; i < map.Spawns.Count; i++) lengths.Add(_evaluator.DistanceAt(map, map.Spawns[i]));
        plan.AddRange(_best);
    }

    // 빈 맵이라 사거리로 덮는 칸 = 계획한 칸뿐이다.
    private float Score(MapData map, List<Vector2Int> plan) =>
        _evaluator.Evaluate(map, plan, plan, _config.AssumedRange, _config.CoverageWeight,
            _config.BossWeight);
}
