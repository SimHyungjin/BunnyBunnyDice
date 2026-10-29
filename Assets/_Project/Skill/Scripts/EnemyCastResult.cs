/// <summary>적 대상 스킬 한 번의 결과(SkillCaster.EnemyCast). 측정용이다.</summary>
public struct EnemyCastResult
{
    /// <summary>원 안에 있던 적 수.</summary>
    public int Targets;
    /// <summary>실제로 깎은 체력(포켓 처치 몫 포함).</summary>
    public float Dealt;
    /// <summary>죽은 적에게 남은 체력을 넘어 버려진 피해(방어력을 지난 뒤).</summary>
    public float Overkill;
    public int Kills;
}
