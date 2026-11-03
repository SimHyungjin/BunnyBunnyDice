/// <summary>
/// 몬스터가 나올 때 웨이브가 정해 주는 값들. 나온 때 정해 들고 다닌다 — 앞 웨이브 몬스터가 다음 웨이브가 시작됐다고 바뀌지 않게.
/// </summary>
public readonly struct MonsterSpawnScale
{
    /// <summary>기본 체력(SO_MonsterType.Health)에 곱하는 값. 특성의 체력 배율은 모양이 따로 곱한다.</summary>
    public readonly float Health;
    /// <summary>이동 속도 배율(SO_WaveConfig.GetSpeedScale).</summary>
    public readonly float Speed;
    /// <summary>웨이브 방어력(SO_WaveConfig.GetArmor). 특성의 방어력 배율이 여기에 곱해진다(MonsterKind.GetArmor).</summary>
    public readonly float Armor;

    public MonsterSpawnScale(float health, float speed, float armor)
    {
        Health = health;
        Speed = speed;
        Armor = armor;
    }
}
