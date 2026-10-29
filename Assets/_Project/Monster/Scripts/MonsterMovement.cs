using UnityEngine;

/// <summary>
/// 몬스터 이동 규칙. MonsterState 값만 바꾸는 정적 함수로 둔다 — 상태와 규칙을 떼어 두면 나중에 ECS 시스템·Job으로 옮기기 쉽다.
/// 영웅은 몬스터를 막지 않고(기획서 10.1) 몬스터끼리도 겹쳐 지나간다. 몬스터는 지형과 주사위만 본다.
///
/// 갇힘: 주사위 배치는 걷고 있는 몬스터를 막다른 곳에 가둘 수 있다(허용, 기획서 3.5). 여기서는 갇혔다고 표시만 하고,
/// 갇힌 몬스터는 MonsterField가 죽인다 — 보스도 가두면 잡히는 1회용 위기 탈출이 된다.
/// </summary>
public static class MonsterMovement
{
    private static readonly Vector2Int[] Neighbors =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    /// <summary>길이 바뀌었을 때(출현·주사위 배치) 지금 자리에서 경유점을 다시 정한다.</summary>
    public static void Retarget(ref MonsterState state, GridRouteField route)
    {
        state.Trapped = false;
        if (route.TryGetNextWaypoint(state.Position, out state.Waypoint)) return;

        // 발밑 칸에 주사위가 놓이면 그 칸에서는 길이 없다. 목표물에 가장 가까운 이어진 이웃 칸으로 빠져나온다.
        Vector2Int cell = GridCircle.CellOf(state.Position);
        float best = float.MaxValue;
        for (int i = 0; i < Neighbors.Length; i++)
        {
            Vector2Int neighbor = cell + Neighbors[i];
            float distance = route.GetDistance(neighbor);
            if (distance == GridRouteField.Unreachable || distance >= best) continue;
            best = distance;
            state.Waypoint = neighbor;
        }
        if (best < float.MaxValue) return;

        state.Trapped = true;
        state.Waypoint = state.Position;
    }

    /// <summary>경유점을 향해 distance만큼 걷는다. 경유점에 닿으면 다음 경유점을 받아 남은 거리를 이어 걷는다(경유점마다 멈칫하지 않게).</summary>
    public static void Advance(ref MonsterState state, float distance, GridRouteField route)
    {
        if (state.Trapped) return;

        Vector2 start = state.Position;
        while (distance > 0f)
        {
            Vector2 to = state.Waypoint - state.Position;
            float length = to.magnitude;
            if (length > distance)
            {
                state.Position += to / length * distance;
                break;
            }

            state.Position = state.Waypoint;
            distance -= length;
            // 길 끝(목표물 칸 중심)에서는 다음 경유점이 제자리다. 목표물 판정이 그 전에 걸리므로 보통은 오지 않는다.
            if (!route.TryGetNextWaypoint(state.Position, out state.Waypoint) || state.Waypoint == state.Position) break;
        }

        Vector2 moved = state.Position - start;
        if (moved.sqrMagnitude > 0f) state.Heading = moved;
    }
}
