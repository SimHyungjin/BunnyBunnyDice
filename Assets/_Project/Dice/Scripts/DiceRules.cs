using UnityEngine;

/// <summary>
/// 주사위 굴리기·얹기 규칙(기획서 6.3). DiceState 값만 바꾸는 정적 함수로 둔다 — 표시·입력과 무관하게 판정하고,
/// 드래그 미리보기(6.8)가 같은 함수로 결과를 미리 계산할 수 있게.
/// </summary>
public static class DiceRules
{
    public static int Roll(int faces) => GameRandom.Dice.Range(1, faces + 1);

    /// <summary>빈 칸에 놓을 때. 굴린 눈이 그대로 눈이 된다.</summary>
    public static DiceState Create(DiceCard card, int roll) =>
        new DiceState { Faces = card.Faces, Colors = DiceColors.Normalize(card.Colors), Pips = Mathf.Clamp(roll, 1, card.Faces) };

    /// <summary>
    /// 놓인 주사위 위에 카드를 얹을 수 있는지. 눈이 상한에 닿은 주사위에는 더 큰 면체만 얹을 수 있다
    /// — 같거나 작은 면체는 눈을 하나도 더하지 못하고 색만 바꾸게 되므로 막는다.
    /// </summary>
    public static bool CanStack(in DiceState dice, DiceCard card) => !dice.IsMaxed || card.Faces > dice.Faces;

    /// <summary>
    /// 얹기: 눈은 더하고(상한을 넘친 만큼은 버림), 면체는 큰 쪽, 색은 합집합(검정은 0이라 색을 더하지 않는다).
    /// 면체를 먼저 키워야 큰 면체를 얹을 때 새 상한까지 눈이 오른다.
    /// </summary>
    public static void Stack(ref DiceState dice, DiceCard card, int roll)
    {
        dice.Faces = Mathf.Max(dice.Faces, card.Faces);
        dice.Pips = Mathf.Min(dice.Pips + roll, dice.Faces);
        dice.Colors |= DiceColors.Normalize(card.Colors);
    }

    /// <summary>
    /// 각성: 색이 있는 주사위의 눈이 awakenPips(20)에 닿았다(기획서 6.5) — 눈은 상한(면 수)을 넘지 않으므로 d20을 꽉 채웠을 때만이다.
    /// 작은 면체가 쉽게 상한을 채워 각성을 흔하게 만들지 않게, 세기를 나누지 않고 한 지점에서만 온전히 켠다.
    /// 검정은 극대화할 공격이 없어 각성하지 않는다 — 대신 눈 20에 닿으면 무작위 색으로 바뀌어 바로 각성한다(ResolveBlack).
    /// </summary>
    public static bool IsAwakened(in DiceState dice, int awakenPips) =>
        dice.Pips >= awakenPips && dice.Colors != DiceColor.Clear;

    /// <summary>
    /// 눈 20 검정은 7색 중 하나(같은 확률)로 바뀐다(기획서 6.5). 눈 20은 d20 상한이라 더 얹을 수 없어,
    /// 그대로 두면 페인트 카드가 나오기 전까지 약한 기본 공격으로 굳는다 — 저금통을 끝까지 채운 보상이 무작위 각성이 된다.
    /// 놓거나 얹어 실제로 굴린 뒤에만 부른다(고스트 미리보기에서는 부르지 않는다).
    /// </summary>
    public static void ResolveBlack(ref DiceState dice, int awakenPips)
    {
        if (dice.Colors != DiceColor.Clear || dice.Pips < awakenPips) return;
        dice.Colors = (DiceColor)GameRandom.Dice.Range(1, (int)DiceColors.All + 1);
    }
}
