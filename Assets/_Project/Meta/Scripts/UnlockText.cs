/// <summary>해금 조건 글(기획서 12.3 — 잠긴 캐릭터 칸 아래 "웨이브 15 클리어 (12/15)").</summary>
public static class UnlockText
{
    public static string Describe(UnlockRule rule)
    {
        switch (rule.Kind)
        {
            case UnlockKind.BestWave: return App.Localize("웨이브 {0} 클리어", rule.Target);
            case UnlockKind.Awakenings: return App.Localize("주사위 각성 {0}회", rule.Target);
            case UnlockKind.JackpotKills: return App.Localize("잭팟 보스 {0}마리 처치", rule.Target);
            case UnlockKind.RunCount: return App.Localize("{0}판 플레이", rule.Target);
            case UnlockKind.DicePlaced: return App.Localize("주사위 {0}개 배치", rule.Target);
            default: return string.Empty;
        }
    }

    /// <summary>조건 + 진행도. 진행도는 목표에서 멈춘다(넘친 수는 보이지 않는다).</summary>
    public static string WithProgress(UnlockRule rule, int progress) =>
        App.Localize("{0} ({1}/{2})", Describe(rule), progress < rule.Target ? progress : rule.Target, rule.Target);
}
