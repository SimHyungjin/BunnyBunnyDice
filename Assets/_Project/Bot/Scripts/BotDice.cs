/// <summary>봇이 주사위를 나눠 보는 기준. 레벨업(BotCardChoice)과 놓기(GameBot)가 같은 기준을 쓰게 한곳에 둔다.</summary>
public static class BotDice
{
    /// <summary>범위 주사위 = 색이 둘 이상(노랑·자홍·하늘·하양). 단색(빨강·초록·파랑)과 검정은 단일.</summary>
    public static bool IsArea(DiceColor colors) => ColorCount(colors) >= 2;

    public static int ColorCount(DiceColor colors)
    {
        int count = 0;
        if ((colors & DiceColor.Red) != 0) count++;
        if ((colors & DiceColor.Green) != 0) count++;
        if ((colors & DiceColor.Blue) != 0) count++;
        return count;
    }
}
