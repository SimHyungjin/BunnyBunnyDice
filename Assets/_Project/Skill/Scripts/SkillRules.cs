using UnityEngine;

/// <summary>
/// 스킬 판정·계산 규칙(기획서 5.3). DiceRules·MonsterMovement처럼 값만 다루는 정적 함수로 둔다
/// — 드래그 미리보기가 같은 함수로 결과를 미리 계산하고, 몬스터 쪽 계산은 ECS로 옮겨 가기 쉽게.
/// 적용(몬스터·주사위를 실제로 바꾸고 연출하기)은 SkillCaster가 맡는다.
/// </summary>
public static class SkillRules
{
    // 백스핀이 밀리는 길을 끊어 보는 간격(칸). 조정 값이 아니라 칸(1)보다 충분히 작아 벽을 건너뛰지 않기 위한 정밀도다.
    private const float KnockbackStep = 0.1f;

    /// <summary>주사위 대상 스킬을 이 주사위에 쓸 수 있는지.</summary>
    public static PlacementResult CheckDice(in DiceState dice, SO_SkillCard card)
    {
        if (card == null) return PlacementResult.NoEffect;

        switch (card)
        {
            // 같은 색으로 칠하거나 이미 상한인 주사위에 잭팟을 쓰면 카드만 버려진다 — 실수로 날리지 않게 막는다.
            case SO_SkillCard_Paint paint:
                DiceColor colors = DiceColors.Normalize(paint.Colors);
                return colors == DiceColor.Clear || colors == dice.Colors ? PlacementResult.NoEffect : PlacementResult.Upgrade;
            case SO_SkillCard_Jackpot _:
                return dice.IsMaxed ? PlacementResult.NoEffect : PlacementResult.Upgrade;
            // 리롤은 상한에 닿은 주사위에도 막지 않는다(판단에 맡긴다). 오버드라이브는 늘 된다.
            default:
                return PlacementResult.Upgrade;
        }
    }

    /// <summary>칸 대상 스킬(캐시 아웃)을 이 칸에 쓸 수 있는지 — 거둘 주사위나 칩이 있어야 한다.</summary>
    public static PlacementResult CheckCell(bool hasDice, bool hasChip) =>
        hasDice || hasChip ? PlacementResult.Upgrade : PlacementResult.NoEffect;

    /// <summary>
    /// 주사위 상태를 바꾸는 스킬을 적용한다(페인트·리롤·잭팟). 상태를 바꾸지 않는 스킬(오버드라이브)은 아무 일도 없다.
    /// roll은 리롤의 새 눈 — 미리보기에서는 지금 눈을 넘겨 그대로 둔다. 리롤의 새 면체는 부르는 쪽(DiceField)이 먼저 넣는다.
    /// </summary>
    public static void ApplyDice(ref DiceState dice, SO_SkillCard card, int roll)
    {
        switch (card)
        {
            case SO_SkillCard_Paint paint:
                dice.Colors = DiceColors.Normalize(paint.Colors);
                break;
            case SO_SkillCard_Reroll _:
                dice.Pips = Mathf.Clamp(roll, 1, dice.Faces);
                break;
            case SO_SkillCard_Jackpot _:
                dice.Pips = dice.Faces;
                break;
        }
    }

    /// <summary>눈을 새로 굴리는 스킬인지(굴리는 연출을 보일지).</summary>
    public static bool Rolls(SO_SkillCard card) => card is SO_SkillCard_Reroll || card is SO_SkillCard_Jackpot;

    /// <summary>적이 설 수 있는 칸인지 — 목표물까지 길이 있는 칸이다(주사위·칩·벽 칸과 갇힌 구역은 아니다).</summary>
    public static bool IsWalkable(GridRouteField route, Vector2 position) =>
        route.GetDistance(GridCircle.CellOf(position)) != GridRouteField.Unreachable;

    /// <summary>
    /// 원 안에서 적을 모을 자리(레이크). 원 중심 칸에 설 수 있으면 그 칸 중심, 아니면 원 안에서 중심에 가장 가까운 설 수 있는 칸의 중심.
    /// 칸 중심으로 맞추는 이유: 모인 적이 걷는 길에 바로 올라서게(칸 가장자리면 벽에 걸친 채 경유점을 찾는다). 원 안에 설 칸이 없으면 false.
    /// </summary>
    public static bool TryGetGatherPoint(GridRouteField route, Vector2 center, float radius, out Vector2 gather)
    {
        gather = center;
        GridCircle.GetCellRange(center, radius, out Vector2Int min, out Vector2Int max);
        float best = float.MaxValue;
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2 cell = new Vector2(x, y);
                float sqr = (cell - center).sqrMagnitude;
                if (sqr > radius * radius || sqr >= best) continue;
                if (route.GetDistance(new Vector2Int(x, y)) == GridRouteField.Unreachable) continue;
                best = sqr;
                gather = cell;
            }
        }
        return best < float.MaxValue;
    }

    /// <summary>
    /// 백스핀: from에서 direction(단위 벡터) 쪽으로 곧게 distance만큼 밀린 자리. 설 수 없는 칸(벽·주사위·칩)에 닿으면 그 앞에서 멈춘다.
    /// 칸을 건너뛰어 벽 너머로 넘어가지 않게 KnockbackStep 간격으로 끊어 본다.
    /// </summary>
    public static Vector2 Knockback(GridRouteField route, Vector2 from, Vector2 direction, float distance)
    {
        Vector2 position = from;
        float moved = 0f;
        while (moved < distance)
        {
            float step = Mathf.Min(KnockbackStep, distance - moved);
            Vector2 next = position + direction * step;
            if (!IsWalkable(route, next)) break;
            position = next;
            moved += step;
        }
        return position;
    }
}
