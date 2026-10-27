using System;
using UnityEngine;

/// <summary>영웅이 설 수 있는 곳의 판정. 몬스터 길(MapData.IsWalkable)과 규칙이 달라 따로 둔다.</summary>
public static class HeroTerrain
{
    /// <summary>FindNearest에 넘길 판정. 메서드를 넘길 때마다 대리자가 새로 만들어지지 않게 하나를 둔다.</summary>
    public static readonly Func<MapData, int, int, bool> Passable = IsPassable;

    /// <summary>판 시작 자리 고르기용 — 바닥 칸만(목표물 위에서 시작하지 않는다).</summary>
    public static readonly Func<MapData, int, int, bool> Ground = IsGround;

    // 판 안은 어디든 밟는다(기획서 10.8) — 주사위·칩·목표물·출현 구역 모두. 영웅이 걸어가서 놓으므로 막히면 벽에 둘러싸인 안쪽 칸에 못 가고
    // 자기가 놓은 것에 갇히며, 목표물을 돌아 걸으면 길이 부자연스럽게 꺾인다. 출현 구역을 막던 이유(문을 틀어막아 출현 봉쇄)는
    // 몬스터가 영웅을 통과하므로 없다. 몬스터만 막힌다(MapData.IsWalkable). 판 안 벽(Wall)은 지금 생성기가 만들지 않는다.
    public static bool IsPassable(MapData map, int x, int y) => map.InBounds(x, y) && map.Get(x, y) != TileType.Wall;

    private static bool IsGround(MapData map, int x, int y) => map.InBounds(x, y) && map.Get(x, y) == TileType.Ground;

    /// <summary>
    /// position에서 가장 가까운 설 수 있는 칸의 중심. 설 수 없는 칸에 걸쳤을 때 빼내는 데 쓴다.
    /// 설 곳이 하나도 없으면 position을 그대로 돌려준다.
    /// </summary>
    public static Vector2 FindNearestPassable(MapData map, Vector2 position) => FindNearest(map, position, Passable);

    /// <summary>position에서 가장 가까운, accept가 참인 칸의 중심. 없으면 position 그대로.</summary>
    public static Vector2 FindNearest(MapData map, Vector2 position, Func<MapData, int, int, bool> accept)
    {
        Vector2 best = position;
        float bestSqr = float.MaxValue;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (!accept(map, x, y)) continue;
                float sqr = (new Vector2(x, y) - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = new Vector2(x, y);
            }
        }
        return best;
    }
}
