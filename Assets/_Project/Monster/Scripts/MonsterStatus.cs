using UnityEngine;

/// <summary>
/// 몬스터 상태 이상(화상·둔화·얼림·취약) 규칙. MonsterState 값만 바꾸는 정적 함수로 둔다(MonsterMovement와 같은 이유 — ECS로 옮기기 쉽게).
/// 같은 상태가 다시 걸리면 더 센 쪽 세기를 남기고 시간은 처음부터 다시 센다 — 약한 주사위가 센 화상·둔화를 덮어 약하게 만들지 않게.
/// </summary>
public static class MonsterStatus
{
    /// <summary>화상(기획서 7): 초당 최대 체력의 ratio만큼. 체력 비율이라 후반 체력에도 묻히지 않는다.</summary>
    public static void Burn(ref MonsterState state, float ratio, float duration)
    {
        if (ratio <= 0f || duration <= 0f) return;
        if (!state.IsBurning) state.BurnTick = 0f;
        if (ratio >= state.BurnRatio || !state.IsBurning) state.BurnRatio = ratio;
        state.BurnTime = Mathf.Max(state.BurnTime, duration);
    }

    /// <summary>취약(방깍, 기획서 7): 받는 피해를 ratio만큼 늘린다. 몬스터에 거는 상태라 누가 때려도 적용된다. 겹치지 않고 센 쪽이 남는다.</summary>
    public static void Vulnerable(ref MonsterState state, float ratio, float duration)
    {
        if (ratio <= 0f || duration <= 0f) return;
        if (ratio >= state.VulnerableRatio || !state.IsVulnerable) state.VulnerableRatio = ratio;
        state.VulnerableTime = Mathf.Max(state.VulnerableTime, duration);
    }

    /// <summary>
    /// 얼린다(이전 파랑 각성 — 지금은 거는 곳이 없고 강화 카드에서 다시 쓴다, 기획서 7). 이미 얼었거나 풀린 직후 면역 중이면 무시한다 — 계속 다시 얼려 영원히 세워 두지 못하게(6.5).
    /// immunity는 풀린 순간부터 센다.
    /// </summary>
    public static bool Freeze(ref MonsterState state, float duration, float immunity)
    {
        if (duration <= 0f || state.IsFrozen || state.FreezeImmuneTime > 0f) return false;
        state.FreezeTime = duration;
        state.FreezeImmunity = immunity;
        return true;
    }

    /// <summary>
    /// 멈춘다(스킬 체크). 얼림과 같은 상태라 표시·이동이 그대로 따르지만, 면역을 무시하고 걸며 끝나도 면역을 남기지 않는다
    /// — 칩 얼림의 면역은 칩이 같은 적을 계속 세워 두지 못하게 하려는 것이라 카드 한 장의 멈춤과는 상관없다.
    /// 이미 얼어 있으면 더 긴 쪽을 남기고, 그 얼림이 끝나며 남길 면역은 그대로 둔다.
    /// </summary>
    public static void Hold(ref MonsterState state, float duration)
    {
        if (duration <= 0f) return;
        if (!state.IsFrozen) state.FreezeImmunity = 0f;
        state.FreezeTime = Mathf.Max(state.FreezeTime, duration);
        state.FreezeImmuneTime = 0f;
    }

    public static void Slow(ref MonsterState state, float ratio, float duration)
    {
        if (ratio <= 0f || duration <= 0f) return;
        if (ratio >= state.SlowRatio || !state.IsSlowed) state.SlowRatio = ratio;
        state.SlowTime = Mathf.Max(state.SlowTime, duration);
    }

    /// <summary>
    /// 시간을 흘린다. 화상은 tickInterval마다 한 번씩 피해를 낸다(매 프레임 조금씩 주면 피해 숫자가 매 프레임 뜨고
    /// 프레임 속도에 따라 숫자 수가 달라진다). 돌려준 값은 이번에 줄 화상 피해다(방어력을 받지 않는다).
    /// </summary>
    public static float Tick(ref MonsterState state, float deltaTime, float tickInterval)
    {
        float poisonDamage = 0f;
        if (state.IsBurning)
        {
            // 남은 시간보다 길게 쌓이지 않게 해서, 끝나는 순간 남은 몫까지 정확히 준다.
            float elapsed = Mathf.Min(deltaTime, state.BurnTime);
            state.BurnTime -= deltaTime;
            state.BurnTick += elapsed;
            while (state.BurnTick >= tickInterval)
            {
                state.BurnTick -= tickInterval;
                poisonDamage += state.BurnRatio * state.MaxHealth * tickInterval;
            }
            if (!state.IsBurning)
            {
                state.BurnTime = 0f;
                state.BurnRatio = 0f;
                state.BurnTick = 0f;
            }
        }

        if (state.IsSlowed)
        {
            state.SlowTime -= deltaTime;
            if (!state.IsSlowed)
            {
                state.SlowTime = 0f;
                state.SlowRatio = 0f;
            }
        }
        if (state.IsVulnerable)
        {
            state.VulnerableTime -= deltaTime;
            if (!state.IsVulnerable)
            {
                state.VulnerableTime = 0f;
                state.VulnerableRatio = 0f;
            }
        }
        if (state.IsFrozen)
        {
            state.FreezeTime -= deltaTime;
            if (!state.IsFrozen)
            {
                state.FreezeTime = 0f;
                state.FreezeImmuneTime = state.FreezeImmunity;
            }
        }
        else if (state.FreezeImmuneTime > 0f)
        {
            state.FreezeImmuneTime = Mathf.Max(state.FreezeImmuneTime - deltaTime, 0f);
        }
        return poisonDamage;
    }

    /// <summary>둔화·얼림을 반영한 이동 속도 배율.</summary>
    public static float GetSpeedScale(in MonsterState state) =>
        state.IsFrozen ? 0f : state.IsSlowed ? 1f - state.SlowRatio : 1f;
}
