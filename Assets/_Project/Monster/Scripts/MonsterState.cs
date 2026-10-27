using UnityEngine;

/// <summary>
/// 몬스터 한 마리의 상태. 클래스·GameObject·목록 없이 값만 둔다
/// — 수가 많아지면 ECS·GPU로 옮길 예정이라, 관리 참조가 없는 구조체로 두어 그대로 옮겨 갈 수 있게 한다.
/// 상태를 바꾸는 규칙은 MonsterMovement·MonsterTargeting·MonsterStatus, 표시는 MonsterRenderer가 맡는다.
/// </summary>
public struct MonsterState
{
    /// <summary>MonsterField 모양(MonsterKind) 목록의 위치. 관리 참조를 들지 않으려고 번호로 든다.</summary>
    public int TypeIndex;
    /// <summary>나올 때 붙는 번호. 슬롯은 지울 때 바뀌므로, 같은 몬스터인지는 이 번호로 본다.</summary>
    public int Id;
    public Vector2 Position;
    /// <summary>지금 곧장 향하는 경유점. 닿으면 걷는 길(GridRouteField)에 다음 경유점을 묻는다.</summary>
    public Vector2 Waypoint;
    /// <summary>마지막으로 움직인 방향. 표시가 몸을 돌릴 때 쓴다.</summary>
    public Vector2 Heading;
    /// <summary>목표물까지 길이 없다(주사위에 갇힘). 갇히면 MonsterField가 그 자리에서 죽인다(기획서 3.5).</summary>
    public bool Trapped;
    /// <summary>0 이하면 죽은 것. 맞은 그 자리에서 지우지 않고 MonsterField가 다음 갱신 때 한꺼번에 치운다
    /// — 공격하는 쪽이 같은 프레임에 슬롯 번호를 들고 있어도 번호가 바뀌지 않게.</summary>
    public float Health;
    /// <summary>나온 때의 체력(웨이브 배율 포함). 재생은 이 비율로 회복하고, 분열한 조각의 체력도 이것을 나눈다.</summary>
    public float MaxHealth;

    // 상태 이상(MonsterStatus). 남은 시간이 0이면 걸려 있지 않은 것이다.
    /// <summary>독: 초당 최대 체력에 대한 피해 비율, 남은 시간, 다음 피해까지 쌓인 시간.</summary>
    public float PoisonRatio;
    public float PoisonTime;
    public float PoisonTick;
    /// <summary>독 피해의 1 미만 나머지. 피해는 정수로 들어가므로(기획서 9.11) 작은 독 피해를 모았다가 1이 넘을 때 준다.</summary>
    public float PoisonCarry;
    /// <summary>둔화: 이동 속도를 줄이는 비율(0~1)과 남은 시간.</summary>
    public float SlowRatio;
    public float SlowTime;
    /// <summary>얼림: 남은 시간, 풀린 뒤 줄 면역 시간, 남은 면역 시간.</summary>
    public float FreezeTime;
    public float FreezeImmunity;
    public float FreezeImmuneTime;
    /// <summary>취약(방깍): 받는 피해에 더하는 비율(0.3 = 1.3배)과 남은 시간.</summary>
    public float VulnerableRatio;
    public float VulnerableTime;

    public bool IsAlive => Health > 0f;
    public bool IsPoisoned => PoisonTime > 0f;
    public bool IsSlowed => SlowTime > 0f;
    public bool IsFrozen => FreezeTime > 0f;
    public bool IsVulnerable => VulnerableTime > 0f;
}
