using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 판 시작 시 맵 생성: 중앙 목표물 → 판 주사위 굴림 → 출현 구역(나온 눈의 포켓) → 시작 칩(눈의 합만큼, 경로가 끊기지 않는 것만).
/// 칩 자리만 정한다 — 칩 종류(어떤 강화인지)는 ChipField가 뽑는다(맵은 길을 막는지만 안다).
/// 맵 = 플레이 영역이다. 둘레 벽 칸은 두지 않고 맵 밖은 경계 검사(InBounds)가 벽처럼 막는다.
/// 같은 시드면 같은 맵이 나오도록 UnityEngine.Random 전역 상태 대신 System.Random을 쓴다.
///
/// 판 주사위(기획서 3.3): d6 몇 개를 굴려 눈 n이 나오면 n번 포켓이 열리고(같은 눈이 겹치면 그 포켓에서 그만큼 더 나온다),
/// 눈의 합이 시작 칩 수다. 결과를 먼저 정하고 연출은 그 결과를 보여 줄 뿐이다 — 합만큼 칩을 놓지 못하면(길이 끊기거나 자리가 모자람)
/// 안에서 다시 굴린다. 플레이어가 본 합과 실제 칸 수가 늘 같아야 규칙을 믿는다.
///
/// 미로 검사(probe가 있을 때): 주사위가 문제(열린 구멍·×2·보스)를 내고, 검사는 그 문제를 잘 풀 수 있는지만 본다(2026-10-27 사용자 결정, 기획서 3.3).
/// 보스·×2 구멍을 더 쳐 주며 미로를 지어 보고(봇과 같은 점수), 가장 짧은 구멍이 MinShortestRoute보다 짧으면 굴림은 두고 칩만 다시 놓는다
/// — 주사위가 모자라 한 구멍을 희생해야 하는 맵이 판 결과를 크게 갈랐다. 좋은 미로가 여럿이라 한 번 지은 결과는 크게 흔들려(같은 맵이 9~50칸)
/// ProbeTries번까지 지어 보고 한 번이라도 넘으면 통과다("좋은 답이 하나라도 있는가"). 보스 구멍을 길이로 옮기지 않는다 — 답을 정해 보여 주는 셈이고 답도 하나가 아니다.
///
/// 두 단계로 나눈다: Roll(굴림 + 첫 칩 배치, 수 ms — 그 자리에서)과 Refine(미로 검사·칩 다시 놓기, 0.2~2초 — 워커 스레드).
/// 굴림은 Roll에서 확정되고 Refine은 칩 자리만 바꾸므로, 판 주사위 연출은 Roll 결과로 바로 시작하고 칩이 떨어지기 전까지만 Refine을 기다린다.
/// Refine은 Unity API를 쓰지 않는다(워커 스레드).
/// </summary>
public static class MapGenerator
{
    // Refine이 칩을 다시 놓을 난수를 굴림과 따로 두려고 시드에 섞는 값(조정 대상이 아니다).
    private const int RefineSeedSalt = 0x5f3759df;

    /// <summary>Roll + Refine을 한 번에(그 자리에서). 첫 맵·테스트용. 검사가 지은 미로는 맵의 MazePlan에 붙인다.</summary>
    public static MapData Generate(SO_MapGenConfig settings, int seed, IMapRouteProbe probe = null)
    {
        List<Vector2Int> plan = new List<Vector2Int>();
        MapData map = Refine(settings, seed, Roll(settings, seed), probe, plan);
        map.MazePlan.Clear();
        map.MazePlan.AddRange(plan);
        return map;
    }

    /// <summary>
    /// 판 주사위를 굴리고 합만큼 칩을 놓는다. 못 놓으면(길이 끊기거나 자리가 모자람) 주사위부터 다시 — 여기서 통과한 굴림은 연출에 그대로 나간다.
    /// </summary>
    public static MapData Roll(SO_MapGenConfig settings, int seed)
    {
        System.Random random = new System.Random(seed);
        MapData map = null;
        int rerolls = Mathf.Max(0, settings.MaxRerolls);
        for (int attempt = 0; attempt <= rerolls; attempt++)
        {
            map = NewMap(settings);
            RollSpawns(map, settings, random);
            int target = ChipTarget(map);
            if (PlaceChips(map, settings, random, target) == target) return map;
        }
        Debug.LogWarning($"[MapGenerator] {rerolls}번 다시 굴려도 눈의 합만큼 칩을 놓지 못했다 — 마지막 결과를 쓴다(합과 칸 수가 다르다).");
        return map;
    }

    /// <summary>
    /// 미로 검사를 넘을 때까지 굴림은 두고 칩만 다시 놓는다(rolled 그대로가 첫 후보). 못 넘으면 가장 짧은 구멍이 가장 길었던 맵.
    /// 워커 스레드에서 불린다 — rolled를 고치지 않고 새 맵을 만든다.
    /// plan에는 돌려준 맵에서 검사가 지은 미로를 담는다(검사가 없으면 비운다). 맵에 바로 붙이지 않는 이유: 통과한 맵이 rolled 그대로면
    /// 메인 스레드가 이미 쓰는 맵이라, 붙이는 일은 결과를 받은 메인 스레드가 한다.
    /// </summary>
    public static MapData Refine(SO_MapGenConfig settings, int seed, MapData rolled, IMapRouteProbe probe, List<Vector2Int> plan)
    {
        plan.Clear();
        if (probe == null || settings.MinShortestRoute <= 0f) return rolled;
        List<Vector2Int> tried = new List<Vector2Int>();

        System.Random random = new System.Random(seed ^ RefineSeedSalt);
        List<float> lengths = new List<float>();
        MapData fallback = rolled;
        float fallbackRoute = float.MinValue;
        int rerolls = Mathf.Max(0, settings.MaxRerolls);
        int tries = Mathf.Max(1, settings.ProbeTries);
        for (int attempt = 0; attempt <= rerolls; attempt++)
        {
            MapData map = rolled;
            if (attempt > 0)
            {
                map = NewMap(settings);
                CopyRoll(rolled, map);
                int target = ChipTarget(map);
                // 굴림은 Roll이 이미 합만큼 놓아 본 것이라 다시 굴리지 않는다(연출에 나간 눈이 바뀌면 안 된다). 이번 배치만 버린다.
                if (PlaceChips(map, settings, random, target) != target) continue;
            }

            float best = float.MinValue;
            for (int t = 0; t < tries; t++)
            {
                probe.Measure(map, seed + attempt * tries + t, lengths, tried);
                float shortest = Shortest(lengths);
                if (shortest >= settings.MinShortestRoute)
                {
                    plan.Clear();
                    plan.AddRange(tried);
                    return map;
                }
                if (shortest <= best) continue;
                best = shortest;
                if (best <= fallbackRoute) continue;
                // 못 넘으면 쓰는 맵(가장 짧은 구멍이 가장 길었던 것)의 미로를 남긴다.
                fallbackRoute = best;
                fallback = map;
                plan.Clear();
                plan.AddRange(tried);
            }
        }
        Debug.LogWarning($"[MapGenerator] {rerolls}번 다시 놓아도 가장 짧은 구멍이 {settings.MinShortestRoute}칸에 못 미쳤다 — 가장 나은 맵({fallbackRoute:0.0}칸)을 쓴다.");
        return fallback;
    }

    private static MapData NewMap(SO_MapGenConfig settings)
    {
        MapData map = new MapData(settings.Width, settings.Height);
        PlaceGoal(map, settings);
        PlacePockets(map);
        return map;
    }

    // 시작 칩 수 = 판 주사위 눈의 합.
    private static int ChipTarget(MapData map)
    {
        int target = 0;
        for (int i = 0; i < map.Roll.Count; i++) target += map.Roll[i];
        return target;
    }

    private static float Shortest(List<float> lengths)
    {
        float shortest = float.MaxValue;
        for (int i = 0; i < lengths.Count; i++)
            if (lengths[i] < shortest) shortest = lengths[i];
        return lengths.Count > 0 ? shortest : 0f;
    }

    // 굴림 결과(눈·열린 구멍·출현 차례·보스 구멍)를 새 맵에 옮긴다 — 칩만 다시 놓을 때 문제(주사위)는 그대로 두려고.
    private static void CopyRoll(MapData from, MapData to)
    {
        to.Roll.AddRange(from.Roll);
        for (int i = 0; i < from.Spawns.Count; i++)
        {
            Vector2Int cell = from.Spawns[i];
            to.Set(cell.x, cell.y, TileType.Spawn);
            to.Spawns.Add(cell);
        }
        to.SpawnGates.AddRange(from.SpawnGates);
        to.BossPocket = from.BossPocket;
    }

    private static void PlaceGoal(MapData map, SO_MapGenConfig settings)
    {
        int w = Mathf.Clamp(settings.GoalSize.x, 1, settings.Width - 2);
        int h = Mathf.Clamp(settings.GoalSize.y, 1, settings.Height - 2);
        map.GoalRect = new RectInt((map.Width - w) / 2, (map.Height - h) / 2, w, h);

        for (int y = map.GoalRect.yMin; y < map.GoalRect.yMax; y++)
            for (int x = map.GoalRect.xMin; x < map.GoalRect.xMax; x++)
                map.Set(x, y, TileType.Goal);
    }

    // 당구대처럼 포켓은 네 모서리 + 긴 변 가운데 둘로 고정이고, 번호(1~6)는 주사위 눈과 짝이 된다.
    // 번호는 앞쪽 변(y = 0) 왼쪽부터 1·2·3, 뒤쪽 변 왼쪽부터 4·5·6 — 주사위 6의 눈처럼 두 줄 셋씩이라 읽기 쉽다.
    private static void PlacePockets(MapData map)
    {
        int right = map.Width - 1, top = map.Height - 1;
        if (map.Width >= map.Height)
        {
            int middle = right / 2;
            map.Pockets.Add(new Vector2Int(0, 0));
            map.Pockets.Add(new Vector2Int(middle, 0));
            map.Pockets.Add(new Vector2Int(right, 0));
            map.Pockets.Add(new Vector2Int(0, top));
            map.Pockets.Add(new Vector2Int(middle, top));
            map.Pockets.Add(new Vector2Int(right, top));
        }
        else
        {
            int middle = top / 2;
            map.Pockets.Add(new Vector2Int(0, 0));
            map.Pockets.Add(new Vector2Int(0, middle));
            map.Pockets.Add(new Vector2Int(0, top));
            map.Pockets.Add(new Vector2Int(right, 0));
            map.Pockets.Add(new Vector2Int(right, middle));
            map.Pockets.Add(new Vector2Int(right, top));
        }
    }

    // 주사위를 굴려 나온 눈의 포켓을 연다. 출현 순서(SpawnGates)는 포켓당 주사위 수만큼 넣되 돌아가며 섞는다
    // — 눈 1·1·3·5면 1·3·5·1 순서라, 두 번 나온 1번 포켓이 몰아서가 아니라 고르게 두 배로 나온다.
    private static void RollSpawns(MapData map, SO_MapGenConfig settings, System.Random random)
    {
        int faces = map.Pockets.Count;
        int[] counts = new int[faces];
        int dice = Mathf.Max(1, settings.SpawnDiceCount);
        for (int i = 0; i < dice; i++)
        {
            int face = random.Next(1, faces + 1);
            map.Roll.Add(face);
            counts[face - 1]++;
        }

        int most = 0;
        for (int i = 0; i < faces; i++)
        {
            if (counts[i] == 0) continue;
            Vector2Int cell = map.Pockets[i];
            map.Set(cell.x, cell.y, TileType.Spawn);
            map.Spawns.Add(cell);
            if (counts[i] > most) most = counts[i];
        }
        for (int round = 1; round <= most; round++)
            for (int i = 0; i < faces; i++)
                if (counts[i] >= round) map.SpawnGates.Add(map.Pockets[i]);

        // 보스 포켓: 주사위가 가장 많이 들어간 포켓 중 무작위(같은 시드면 같은 포켓).
        int ties = 0;
        for (int i = 0; i < faces; i++)
            if (counts[i] == most) ties++;
        int pick = random.Next(ties);
        for (int i = 0; i < faces; i++)
        {
            if (counts[i] != most) continue;
            if (pick-- == 0)
            {
                map.BossPocket = map.Pockets[i];
                break;
            }
        }
    }

    /// <summary>
    /// target칸까지 칩을 작은 무리로 놓고 실제로 놓은 칸 수를 돌려준다. 무리의 첫 칩은 후보를 ChipSpreadCandidates곳 뽑아 이미 놓인 칩에서
    /// 가장 먼 곳에 놓고(판 전체에 고르게), 나머지는 ChipGroupSize만큼 그 무리 옆에 붙인다 — 다른 무리에 닿는 칸은 건너뛰어 엉기지 않게.
    /// 이전 1~3칸 줄은 퍼뜨리는 규칙이 없어 덩어리지고 쏠렸고(10개 중 8개가 붙음), 낱개 배치는 고르지만 보기에 심심했다(2026-10-28, 기획서 3.3).
    /// </summary>
    private static int PlaceChips(MapData map, SO_MapGenConfig settings, System.Random random, int target)
    {
        List<Vector2Int> placed = new List<Vector2Int>();
        int candidates = Mathf.Max(1, settings.ChipSpreadCandidates);

        // 조건에 맞는 자리가 없을 수도 있으니 시도 횟수로 끝을 보장한다.
        int attempts = target * settings.PlaceAttemptsPerTile;
        while (placed.Count < target && attempts-- > 0)
        {
            bool found = false;
            Vector2Int best = default;
            int bestDistance = -1;
            for (int c = 0; c < candidates; c++)
            {
                Vector2Int pos = new Vector2Int(random.Next(map.Width), random.Next(map.Height));
                if (!CanPlaceChip(map, settings, pos)) continue;
                int distance = NearestSqrDistance(placed, pos);
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                best = pos;
                found = true;
            }
            if (!found) continue;

            int groupStart = placed.Count;
            if (!TryPlaceChip(map, placed, best)) continue;

            int minSize = Mathf.Max(1, settings.ChipGroupSize.x);
            int size = random.Next(minSize, Mathf.Max(minSize, settings.ChipGroupSize.y) + 1);
            for (int k = 1; k < size && placed.Count < target; k++)
                if (!GrowGroup(map, settings, random, placed, groupStart)) break;
        }
        return placed.Count;
    }

    private static readonly Vector2Int[] GroupDirections = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // 무리(placed[groupStart..])의 칩 하나 옆 칸에 칩을 붙인다. 다른 무리 칩에 닿는(대각선 포함) 칸은 건너뛴다. 못 붙이면 false.
    private static bool GrowGroup(MapData map, SO_MapGenConfig settings, System.Random random, List<Vector2Int> placed, int groupStart)
    {
        int groupCount = placed.Count - groupStart;
        // 옆 칸이 다 막혔을 수 있으니 시도 횟수로 끝을 보장한다.
        for (int attempt = 0; attempt < settings.PlaceAttemptsPerTile; attempt++)
        {
            Vector2Int from = placed[groupStart + random.Next(groupCount)];
            Vector2Int pos = from + GroupDirections[random.Next(GroupDirections.Length)];
            if (!CanPlaceChip(map, settings, pos) || TouchesOtherGroup(placed, groupStart, pos)) continue;
            if (TryPlaceChip(map, placed, pos)) return true;
        }
        return false;
    }

    private static bool TouchesOtherGroup(List<Vector2Int> placed, int groupStart, Vector2Int pos)
    {
        for (int i = 0; i < groupStart; i++)
        {
            Vector2Int d = placed[i] - pos;
            if (Mathf.Abs(d.x) <= 1 && Mathf.Abs(d.y) <= 1) return true;
        }
        return false;
    }

    // 길이 끊기면 없던 일로 한다. 처음부터 막힌 맵은 플레이어가 풀 수 없다.
    private static bool TryPlaceChip(MapData map, List<Vector2Int> placed, Vector2Int pos)
    {
        map.SetChip(pos.x, pos.y, true);
        if (!MapPathfinder.AllSpawnsReachable(map, MapPathfinder.BuildDistanceField(map)))
        {
            map.SetChip(pos.x, pos.y, false);
            return false;
        }
        placed.Add(pos);
        return true;
    }

    // 놓인 칩 중 가장 가까운 것까지의 거리 제곱(놓인 칩이 없으면 int.MaxValue).
    private static int NearestSqrDistance(List<Vector2Int> placed, Vector2Int pos)
    {
        int nearest = int.MaxValue;
        for (int i = 0; i < placed.Count; i++)
        {
            int distance = (placed[i] - pos).sqrMagnitude;
            if (distance < nearest) nearest = distance;
        }
        return nearest;
    }

    private static bool CanPlaceChip(MapData map, SO_MapGenConfig settings, Vector2Int pos)
    {
        if (!map.InBounds(pos.x, pos.y) || map.Get(pos.x, pos.y) != TileType.Ground || map.HasChip(pos.x, pos.y)) return false;
        if (DistanceToRect(pos, map.GoalRect) <= settings.GoalClearance) return false;

        for (int i = 0; i < map.Spawns.Count; i++)
        {
            Vector2Int s = map.Spawns[i];
            int chebyshev = Mathf.Max(Mathf.Abs(pos.x - s.x), Mathf.Abs(pos.y - s.y));
            if (chebyshev <= settings.SpawnClearance) return false;
        }
        return true;
    }

    // 사각형 바깥 칸까지의 체비셰프 거리(사각형 안이면 0).
    private static int DistanceToRect(Vector2Int pos, RectInt rect)
    {
        int dx = Mathf.Max(rect.xMin - pos.x, 0, pos.x - (rect.xMax - 1));
        int dy = Mathf.Max(rect.yMin - pos.y, 0, pos.y - (rect.yMax - 1));
        return Mathf.Max(dx, dy);
    }
}
