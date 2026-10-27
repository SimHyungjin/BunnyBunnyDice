using System;

/// <summary>
/// 주사위 색(기획서 9.4). 기본 3색을 비트로 두고, 섞인 색은 비트 합(합집합)으로 나타낸다 — 얹을 때 색 합치기가 OR 한 번이 된다.
/// 아무 비트도 없으면 검정이다(빛의 3원색이라 빛이 하나도 없으면 검정 — 색을 더하지 않으므로 0이 자연스럽다).
/// 색 상태마다의 공격 방식·능력치는 종류 SO(SO_DiceType)가 정한다.
/// </summary>
[Flags]
public enum DiceColor
{
    Clear = 0,
    Red = 1,
    Green = 2,
    Blue = 4,
}

public static class DiceColors
{
    public const DiceColor All = DiceColor.Red | DiceColor.Green | DiceColor.Blue;

    /// <summary>
    /// 정의된 세 비트만 남긴다. 인스펙터는 모든 비트가 켜진 플래그 enum을 "Everything"(-1)으로 저장하므로,
    /// 에셋에서 읽은 색은 이걸 거쳐야 하양(7)과 같게 비교된다.
    /// </summary>
    public static DiceColor Normalize(DiceColor colors) => colors & All;
}
