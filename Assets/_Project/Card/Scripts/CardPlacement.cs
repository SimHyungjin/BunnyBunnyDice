using UnityEngine;

/// <summary>
/// 주사위·칩 카드를 칸에 놓는다 — 판정과 놓기. 카드를 떼는 순간 바로 놓인다
/// (영웅이 걸어가서 놓던 방식(기획서 4)은 결정과 결과 사이가 멀어 불쾌해 되돌렸다, 2026-10-26).
/// 봇도 이것을 부른다(UI를 거치지 않는 게임 행동, 기획서 11.2).
/// </summary>
public sealed class CardPlacement
{
    private readonly DiceField _diceField;
    private readonly ChipField _chips;

    public CardPlacement(DiceField diceField, ChipField chips)
    {
        _diceField = diceField;
        _chips = chips;
    }

    /// <summary>card(주사위·칩)를 cell에 놓을 수 있는지. 끄는 동안의 표시(초록/빨강)도 이 판정을 쓴다 — 놓는 순간의 판정과 어긋나지 않게.</summary>
    public PlacementResult Check(MapData map, Vector2Int cell, HandCard card)
    {
        if (map == null || !(card.IsDice || card.IsChip)) return PlacementResult.NotBuildable;
        if (card.IsChip) return DicePlacement.CheckChip(map, cell);
        PlacementResult result = DicePlacement.Check(map, cell);
        if (result == PlacementResult.Occupied && _diceField.TryGetPlaced(cell, out PlacedDice placed))
            result = DicePlacement.CheckStack(placed.Dice, card.Dice);
        return result;
    }

    /// <summary>판정을 통과하면 놓는다(빈 칸이면 새로, 주사위가 있으면 얹기, 칩은 빈 칸이나 주사위 밑). 카드를 손패에서 빼는 것은 부른 쪽이 한다.</summary>
    public bool TryPlace(MapData map, Vector2Int cell, HandCard card)
    {
        PlacementResult result = Check(map, cell, card);
        if (!DicePlacement.IsAccepted(result)) return false;
        if (card.IsChip) _chips.Place(cell, card.Chip);
        else if (result == PlacementResult.Stack) _diceField.Stack(cell, card.Dice);
        else _diceField.Place(map, cell, card.Dice);
        return true;
    }
}
