using UnityEngine;

/// <summary>
/// 공격할 몬스터 고르기. 상태 배열을 훑는 정적 함수로 둔다(MonsterMovement와 같은 이유 — ECS·Job으로 옮기기 쉽게).
/// 고른 결과는 슬롯 번호이고, 죽은 몬스터는 치우기 전까지 배열에 남아 있으므로 여기서 건너뛴다.
/// </summary>
public static class MonsterTargeting
{
    /// <summary>
    /// 사거리 안에서 목표물에 가장 가까운(남은 길이 가장 짧은) 몬스터. 없으면 -1.
    /// 주사위가 쓴다 — 미로 끝에 다다른 몬스터부터 잡아야 새는 몬스터가 줄어든다(타워 디펜스의 "선두 우선").
    /// </summary>
    public static int FindFirst(MonsterState[] states, int count, Vector2 center, float range, GridRouteField route)
    {
        float rangeSqr = range * range;
        int best = -1;
        float bestRemaining = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            ref MonsterState state = ref states[i];
            if (!state.IsAlive || (state.Position - center).sqrMagnitude > rangeSqr) continue;

            float remaining = GetRemainingDistance(state, route);
            if (remaining >= bestRemaining) continue;
            bestRemaining = remaining;
            best = i;
        }
        return best;
    }

    /// <summary>
    /// 사거리 안에서 최대 체력이 가장 큰 몬스터(보스 등), 같으면 선두. 없으면 -1.
    /// 단일형 주사위(SO_DiceType.PreferToughest)가 쓴다 — 선두만 노리면 떼 속 보스를 거의 맞히지 못한다.
    /// </summary>
    public static int FindToughest(MonsterState[] states, int count, Vector2 center, float range, GridRouteField route)
    {
        float rangeSqr = range * range;
        int best = -1;
        float bestHealth = 0f;
        float bestRemaining = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            ref MonsterState state = ref states[i];
            if (!state.IsAlive || (state.Position - center).sqrMagnitude > rangeSqr) continue;
            if (state.MaxHealth < bestHealth) continue;

            float remaining = GetRemainingDistance(state, route);
            if (state.MaxHealth == bestHealth && remaining >= bestRemaining) continue;
            bestHealth = state.MaxHealth;
            bestRemaining = remaining;
            best = i;
        }
        return best;
    }

    /// <summary>
    /// 사거리 안에서 선두부터 result 길이만큼 고른다(다중 타겟). 고른 수를 돌려주고, result 앞에서부터 선두 순서로 채운다.
    /// remaining은 result와 같은 길이의 작업 공간이다(호출하는 쪽이 들고 있어 할당 없이 쓰게).
    /// </summary>
    public static int FindFirst(MonsterState[] states, int count, Vector2 center, float range, GridRouteField route,
        int[] result, float[] remaining, int max)
    {
        float rangeSqr = range * range;
        int found = 0;
        for (int i = 0; i < count; i++)
        {
            ref MonsterState state = ref states[i];
            if (!state.IsAlive || (state.Position - center).sqrMagnitude > rangeSqr) continue;

            // 고를 수(타겟 수)가 몇 개뿐이라 정렬된 칸에 끼워 넣는다.
            float distance = GetRemainingDistance(state, route);
            int slot = found < max ? found : max;
            while (slot > 0 && remaining[slot - 1] > distance) slot--;
            if (slot >= max) continue;
            int last = found < max ? found : max - 1;
            for (int k = last; k > slot; k--)
            {
                result[k] = result[k - 1];
                remaining[k] = remaining[k - 1];
            }
            result[slot] = i;
            remaining[slot] = distance;
            if (found < max) found++;
        }
        return found;
    }

    /// <summary>center에서 range 안의 가장 가까운 몬스터 중 exclude 앞 excludeCount개에 없는 것. 없으면 -1. 체인이 다음 대상을 고를 때 쓴다.</summary>
    public static int FindNearestExcept(MonsterState[] states, int count, Vector2 center, float range, int[] exclude, int excludeCount)
    {
        int best = -1;
        float bestSqr = range * range;
        for (int i = 0; i < count; i++)
        {
            ref MonsterState state = ref states[i];
            if (!state.IsAlive) continue;

            float sqr = (state.Position - center).sqrMagnitude;
            if (sqr > bestSqr || Contains(exclude, excludeCount, i)) continue;
            bestSqr = sqr;
            best = i;
        }
        return best;
    }

    /// <summary>사거리 안에서 가장 가까운 몬스터. 없으면 -1. 영웅이 쓴다 — 데려간 자리의 적을 바로 치게.</summary>
    public static int FindNearest(MonsterState[] states, int count, Vector2 center, float range)
    {
        int best = -1;
        float bestSqr = range * range;
        for (int i = 0; i < count; i++)
        {
            ref MonsterState state = ref states[i];
            if (!state.IsAlive) continue;

            float sqr = (state.Position - center).sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            best = i;
        }
        return best;
    }

    private static bool Contains(int[] values, int count, int value)
    {
        for (int i = 0; i < count; i++)
        {
            if (values[i] == value) return true;
        }
        return false;
    }

    // 지금 경유점까지 + 경유점에서 목표물까지의 길. 경유점은 칸 중심이라 그 칸의 거리장 값을 그대로 쓴다.
    // 갇힌 몬스터는 길이 없으므로 가장 멀다고 본다 — 목표물을 위협하지 않으니 마지막에 잡는다.
    private static float GetRemainingDistance(in MonsterState state, GridRouteField route)
    {
        if (state.Trapped) return float.MaxValue;
        float rest = route.GetDistance(GridCircle.CellOf(state.Waypoint));
        if (rest == GridRouteField.Unreachable) return float.MaxValue;
        return Vector2.Distance(state.Position, state.Waypoint) + rest;
    }
}
