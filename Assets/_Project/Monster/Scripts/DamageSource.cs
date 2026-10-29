/// <summary>
/// 피해를 누가 냈는지(기획서 11.2의 4번 — 측정). 주사위는 색(8가지)까지 나눠, 어느 색이 판을 끌고 가는지 보이게 한다.
/// 번호(Index)로 고정 길이 배열에 모을 수 있게 둔다 — 피해는 매 프레임 많이 들어오므로 사전·문자열 없이 센다.
/// 오버드라이브(Boost)는 색과 겹쳐 따로 센다 — 색 몫은 그대로 두고, 그중 오버드라이브 상태에서 낸 몫이 얼마인지 보려고
/// (오버드라이브로 늘어난 피해가 주사위 몫에 묻혀 영웅의 실제 몫이 안 보였다).
/// </summary>
public readonly struct DamageSource
{
    // 0~7 = 주사위 색(DiceColor 비트 그대로), 그 뒤가 나머지.
    private const int DiceCount = 8;
    private const int HeroIndex = DiceCount;
    private const int SkillIndex = DiceCount + 1;
    private const int BurnIndex = DiceCount + 2;
    public const int Count = DiceCount + 3;

    // 기록(CSV) 머리글에 쓴다 — 번역하지 않는다(테스트·분석용).
    private static readonly string[] Names =
    {
        "Black", "Red", "Green", "Yellow", "Blue", "Magenta", "Cyan", "White",
        "Hero", "Skill", "Burn",
    };

    public readonly int Index;
    /// <summary>오버드라이브 상태의 주사위가 낸 피해인지(주사위만). 측정 기록이 색과 따로 한 번 더 센다.</summary>
    public readonly DamageBoost Boost;

    private DamageSource(int index, DamageBoost boost = DamageBoost.None)
    {
        Index = index;
        Boost = boost;
    }

    public static DamageSource Dice(DiceColor colors, DamageBoost boost = DamageBoost.None) =>
        new DamageSource((int)DiceColors.Normalize(colors), boost);
    public static readonly DamageSource Hero = new DamageSource(HeroIndex);
    public static readonly DamageSource Skill = new DamageSource(SkillIndex);
    /// <summary>화상(지속 피해 — 화상 칩·틸트 카드). 주사위 색과 따로 센다 — 화상이 얼마나 일하는지 보려고.</summary>
    public static readonly DamageSource Burn = new DamageSource(BurnIndex);

    public static string NameOf(int index) => Names[index];
}

/// <summary>주사위가 어떤 오버드라이브로 쐈는지. 카드 시간이 남아 있으면 영웅이 올라타 있어도 카드 몫이다(카드 시간을 먼저 쓴다).</summary>
public enum DamageBoost
{
    None,
    /// <summary>영웅이 올라탄 주사위(영웅 오버드라이브, 기획서 10.9).</summary>
    Hero,
    /// <summary>오버드라이브 스킬 카드.</summary>
    Card,
}
