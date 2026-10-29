/// <summary>
/// 적 대상 스킬 종류별 측정 칸(WaveRecord·BotBatchReport). 스킬마다 배율이 남아도는지·몇 마리를 잡는지 따로 보려고 종류를 여기서만 나눈다.
/// 적 대상 스킬이 늘면 Names와 IndexOf에 함께 더한다.
/// </summary>
public static class EnemySkillStats
{
    public static readonly string[] Names = { "pocket", "rake", "backspin", "checkmate" };

    public static int Count => Names.Length;

    public static int IndexOf(SO_EnemySkillCard card)
    {
        switch (card)
        {
            case SO_SkillCard_Pocket _: return 0;
            case SO_SkillCard_Rack _: return 1;
            case SO_SkillCard_Backspin _: return 2;
            case SO_SkillCard_Checkmate _: return 3;
            default: return -1;
        }
    }
}
