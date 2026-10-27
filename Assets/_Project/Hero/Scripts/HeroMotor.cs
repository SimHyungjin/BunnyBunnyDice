using UnityEngine;

/// <summary>
/// 영웅의 위치와 충돌. 영웅을 원으로 보고 막힌 칸(사각형)과 겹친 만큼 밀어낸다
/// — 벽에 비스듬히 부딪히면 벽에 수직인 성분만 깎여 벽을 타고 미끄러지므로, 좁은 길에서 모서리를 스칠 때 걸리지 않는다.
/// 막는 것은 판 밖뿐이다(판 안은 모두 밟는다 — HeroTerrain). 물리 엔진 없이 맵 데이터를 직접 본다.
/// </summary>
public sealed class HeroMotor
{
    private readonly SO_HeroConfig _config;

    public Vector2 Position { get; private set; }

    public HeroMotor(SO_HeroConfig config)
    {
        _config = config;
    }

    public void Teleport(Vector2 position) => Position = position;

    public void Move(MapData map, Vector2 delta)
    {
        float radius = _config.Radius;
        Vector2 position = Position;

        // 막힌 칸 안에 있으면 어느 쪽으로 밀어야 할지 정할 수 없으므로 가장 가까운 설 수 있는 칸으로 옮긴다.
        Vector2Int cell = GridCircle.CellOf(position);
        if (!HeroTerrain.IsPassable(map, cell.x, cell.y)) position = HeroTerrain.FindNearestPassable(map, position);

        // 한 번에 반지름보다 크게 움직이면 판 가장자리를 뚫고 나갈 수 있어 나눠 움직인다.
        float maxStep = radius * 0.5f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / maxStep));
        Vector2 step = delta / steps;
        for (int i = 0; i < steps; i++) position = PushOut(map, position + step, radius);

        Position = position;
    }

    // 걸친 막힌 칸마다 차례로 밀어낸다. 모서리에서는 두 칸에 동시에 걸리지만 한 칸씩 밀면 둘 다 풀린다.
    private static Vector2 PushOut(MapData map, Vector2 position, float radius)
    {
        GridCircle.GetCellRange(position, radius, out Vector2Int min, out Vector2Int max);
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                if (HeroTerrain.IsPassable(map, x, y)) continue;

                Vector2 closest = GridCircle.ClosestPointInCell(x, y, position);
                Vector2 away = position - closest;
                float sqr = away.sqrMagnitude;
                if (sqr >= radius * radius) continue;

                // 중심이 칸 경계에 정확히 걸리면 밀 방향이 없으므로 칸 중심의 반대쪽으로 민다.
                if (sqr <= 0f) away = position - new Vector2(x, y);
                position = closest + away.normalized * radius;
            }
        }
        return position;
    }
}
