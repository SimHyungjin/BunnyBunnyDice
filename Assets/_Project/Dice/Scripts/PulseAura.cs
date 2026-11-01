using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파동 주사위(하양)의 오라 — 놓여 있는 동안 사거리에 연한 고리를 띄운다(표시만). 쏠 때는 DiceAttack이 고리를 잠깐 밝힌다.
/// 파동은 공속이 빨라(1.5) 쏠 때마다 퍼지는 고리가 여러 개 겹쳐 화면을 난잡하게 덮었다(2026-11-01 사용자) — 퍼지는 고리는 공속이 느린 공격에 어울린다.
/// 사거리 칩·각성으로 바뀐 사거리를 따라가게 매 프레임 다시 그린다. 실제 시간을 따른다 — 멈춰도 놓은 주사위의 오라는 보여야 한다.
/// </summary>
public sealed class PulseAura : IDisposable
{
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly DiceField _field;
    private readonly DiceCombat _combat;
    private readonly AttackEffects _effects;

    public PulseAura(SO_DiceConfig diceConfig, SO_MapViewConfig viewConfig, DiceField field, DiceCombat combat, AttackEffects effects)
    {
        _diceConfig = diceConfig;
        _viewConfig = viewConfig;
        _field = field;
        _combat = combat;
        _effects = effects;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
    }

    private void Tick(float deltaTime)
    {
        IReadOnlyList<PlacedDice> placed = _field.Placed;
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        for (int i = 0; i < placed.Count; i++)
        {
            PlacedDice dice = placed[i];
            // 눈을 다 태운 주사위는 쏘지 않으므로 오라도 없다.
            if (dice.Dice.IsSpent) continue;
            SO_DiceType type = _diceConfig.GetDiceType(dice.Dice.Colors);
            if (!(type is SO_DiceType_Pulse)) continue;
            Vector2Int cell = dice.Cell;
            _effects.ShowAura(cell, new Vector3(cell.x, ground, cell.y), _combat.GetRange(cell), type.Color);
        }
        _effects.EndAuras();
    }
}
