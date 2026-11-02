/// <summary>봇이 주사위를 나눠 보는 기준. 레벨업(BotCardChoice)과 놓기(GameBot)가 같은 기준을 쓰게 한곳에 둔다.</summary>
public static class BotDice
{
    /// <summary>범위 주사위 = 색이 둘 이상(노랑·자홍·하늘·하양). 단색(빨강·초록·파랑)과 검정은 단일.</summary>
    public static bool IsArea(DiceColor colors) => ColorCount(colors) >= 2;

    /// <summary>
    /// 초반 판을 가르는 각성 색 — 빨강(모든 공격 치명)·초록(이어 맞힐수록 세짐)·하양(파동 두 겹, 큰 고정 피해)(2026-11-02 사용자).
    /// 파랑은 보스를 거의 못 잡고, 두 색 섞임(노랑·자홍·하늘)은 이들보다 늦게 본다.
    /// </summary>
    public static bool IsCoreAwaken(DiceColor colors) =>
        colors == DiceColor.Red || colors == DiceColor.Green || colors == DiceColors.All;

    public static int ColorCount(DiceColor colors)
    {
        int count = 0;
        if ((colors & DiceColor.Red) != 0) count++;
        if ((colors & DiceColor.Green) != 0) count++;
        if ((colors & DiceColor.Blue) != 0) count++;
        return count;
    }
}
