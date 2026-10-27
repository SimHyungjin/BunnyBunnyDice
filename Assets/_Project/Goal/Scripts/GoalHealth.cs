using System;
using UnityEngine;

/// <summary>목표물 체력. 0이 되는 순간 Died를 한 번 알린다(게임 오버). 표시와 게임 오버 처리는 구독하는 쪽이 맡는다.</summary>
public sealed class GoalHealth
{
    private readonly SO_GoalConfig _config;

    public int Current { get; private set; }
    // 값을 복사해 두지 않는다 — 실행 중 조정한 최대 체력이 표시에 바로 반영되게.
    public int Max => _config.MaxHealth;
    public bool IsDead => Current <= 0;

    public event Action Changed;
    public event Action Died;

    public GoalHealth(SO_GoalConfig config)
    {
        _config = config;
        Current = config.MaxHealth;
    }

    /// <summary>새 판: 최대 체력으로 되돌린다.</summary>
    public void Reset()
    {
        Current = _config.MaxHealth;
        Changed?.Invoke();
    }

    public void Damage(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke();
        if (IsDead) Died?.Invoke();
    }
}
