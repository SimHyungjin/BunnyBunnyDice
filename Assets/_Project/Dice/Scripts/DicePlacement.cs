using UnityEngine;

public enum PlacementResult
{
    Ok,
    Stack,        // 놓인 주사위 위에 얹는다(합성, 기획서 9.3)
    OutOfMap,
    NotBuildable, // 목표물·출현 구역
    Occupied,     // 주사위가 있다 — CheckStack으로 얹을 수 있는지 다시 본다
    AtCap,        // 눈이 상한에 닿은 주사위에 같거나 작은 면체를 얹으려 했다
    BlocksPath,   // 어느 출현 구역에서든 목표물까지 길이 끊긴다
    Upgrade,      // 카드를 쓴다 — 주사위 대상 스킬은 놓인 주사위에, 칩 카드는 빈 칸이나 주사위 밑에(기획서 5.3·9.7)
    NoDice,       // 주사위 대상 스킬은 주사위가 있는 칸에만 쓴다
    SlotFull,     // 칸에 이미 칩이 있다(슬롯 1개)
    NoEffect,     // 써도 바뀌는 것이 없다(같은 색 페인트, 상한인 주사위에 잭팟)
}

/// <summary>
/// 주사위·칩 배치 판정. 표시(MapPreview)나 입력(CardHand)과 무관하게 MapData·주사위 상태만으로 결정한다.
/// 걷고 있는 몬스터는 보지 않는다 — 몬스터를 막다른 곳에 가두는 배치는 허용한다(갇힌 몬스터는 MonsterMovement 참고).
/// </summary>
public static class DicePlacement
{
    public static PlacementResult Check(MapData map, Vector2Int cell)
    {
        if (!map.InBounds(cell.x, cell.y)) return PlacementResult.OutOfMap;
        if (!map.IsBuildable(cell.x, cell.y)) return PlacementResult.NotBuildable;
        if (map.HasDice(cell.x, cell.y)) return PlacementResult.Occupied;

        // 칩 칸은 이미 막혀 있어 놓아도 길이 바뀌지 않는다.
        if (map.HasChip(cell.x, cell.y)) return PlacementResult.Ok;

        // 실제로 놓아 보고 길을 다시 계산한 뒤 되돌린다. 120칸 BFS라 드래그 중 매 칸 이동마다 해도 충분히 싸다.
        map.SetDice(cell.x, cell.y, true);
        bool reachable = MapPathfinder.AllSpawnsReachable(map, MapPathfinder.BuildDistanceField(map));
        map.SetDice(cell.x, cell.y, false);
        return reachable ? PlacementResult.Ok : PlacementResult.BlocksPath;
    }

    /// <summary>
    /// 칩 카드를 놓을 때. 칸에 칩이 이미 있으면 막고(슬롯 1개), 주사위 칸이면 주사위 밑으로 들어가므로 길은 보지 않는다.
    /// 빈 칸이면 칩이 새 벽이 되므로 주사위처럼 길이 끊기는지 본다.
    /// </summary>
    public static PlacementResult CheckChip(MapData map, Vector2Int cell)
    {
        if (!map.InBounds(cell.x, cell.y)) return PlacementResult.OutOfMap;
        if (!map.IsBuildable(cell.x, cell.y)) return PlacementResult.NotBuildable;
        if (map.HasChip(cell.x, cell.y)) return PlacementResult.SlotFull;
        if (map.HasDice(cell.x, cell.y)) return PlacementResult.Upgrade;

        map.SetChip(cell.x, cell.y, true);
        bool reachable = MapPathfinder.AllSpawnsReachable(map, MapPathfinder.BuildDistanceField(map));
        map.SetChip(cell.x, cell.y, false);
        return reachable ? PlacementResult.Upgrade : PlacementResult.BlocksPath;
    }

    /// <summary>놓인 주사위 위에 카드를 얹을 때. 얹기는 칸을 새로 막지 않으므로 길은 보지 않는다.</summary>
    public static PlacementResult CheckStack(in DiceState dice, DiceCard card) =>
        DiceRules.CanStack(dice, card) ? PlacementResult.Stack : PlacementResult.AtCap;

    /// <summary>카드를 놓거나 얹거나 쓸 수 있는 결과인지.</summary>
    public static bool IsAccepted(PlacementResult result) =>
        result == PlacementResult.Ok || result == PlacementResult.Stack || result == PlacementResult.Upgrade;
}
