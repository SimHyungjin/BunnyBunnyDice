using System;

/// <summary>이번 판의 경험치와 레벨. 판이 끝나면 버린다(메타 성장과 별개).</summary>
public sealed class PlayerLevel
{
    private readonly SO_LevelUpConfig _config;

    public int Level { get; private set; } = 1;
    /// <summary>지금 레벨에서 모은 경험치(레벨이 오르면 넘친 만큼만 남는다).</summary>
    public int Exp { get; private set; }
    public int ExpToNext => _config.GetExpToNext(Level);

    public event Action Changed;
    /// <summary>레벨이 올랐다. 한 번에 여러 레벨이 오르면 그 수만큼 불린다 — 오른 레벨마다 카드를 한 번씩 고른다.</summary>
    public event Action LeveledUp;

    public PlayerLevel(SO_LevelUpConfig config) => _config = config;

    public void Reset()
    {
        Level = 1;
        Exp = 0;
        Changed?.Invoke();
    }

    public void AddExp(int amount)
    {
        if (amount <= 0) return;
        Exp += amount;
        int levels = 0;
        while (Exp >= ExpToNext)
        {
            Exp -= ExpToNext;
            Level++;
            levels++;
        }

        Changed?.Invoke();
        for (int i = 0; i < levels; i++) LeveledUp?.Invoke();
    }
}
