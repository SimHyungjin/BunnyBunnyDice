using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅 오버드라이브(기획서 10.9). 영웅이 올라타 있는 주사위가 오버드라이브 상태가 되고(스킬 카드는 이것을 멀리서 쓰는 것 — 같은 배율, 겹치지 않음), 그 대가로 머문 시간이 쌓일 때마다 눈이 1씩 영구로 깎인다.
/// 눈이 0이면 공격을 멈추지만 벽으로는 남는다(미로가 무너지지 않게). 얹기·다시 굴리기로 다시 살아난다.
/// 각성 주사위(눈 20)는 첫 눈이 더 오래 걸려 깎인다 — 깎이면 각성이 꺼지므로, 3초 만에 각성을 잃으면 아무도 각성 주사위에 올라타지 않는다.
/// 깎인 눈이 돌아오지 않는 이유: 돌아오면 주사위 몇 개를 돌려쓰며 끝까지 버텨 주사위 카드가 필요 없어진다.
/// 머문 시간은 주사위마다 쌓는다 — 깎이기 직전에 내렸다 다시 올라타 버프만 받는 일이 없게.
/// 오버드라이브 카드 시간이 남은 주사위는 그 시간을 먼저 쓴다 — 카드가 걸린 동안은 눈이 깎이지 않는다(같은 상태라 둘이 겹치지 않으니 카드가 헛되지 않게).
/// 시간은 적이 판에 있을 때만 흐른다 — 쉬는 시간에 서 있다가 쏠 것도 없이 눈을 잃지 않게.
/// </summary>
public sealed class HeroOverdrive : IDisposable
{
    private readonly SO_HeroConfig _config;
    private readonly Hero _hero;
    private readonly DiceField _diceField;
    private readonly DiceCombat _diceCombat;
    private readonly MonsterField _monsters;
    // 주사위(칸)마다 쌓인 머문 시간. 철거되면 지우고 새 판이면 비운다.
    private readonly Dictionary<Vector2Int, float> _elapsed = new Dictionary<Vector2Int, float>();

    /// <summary>버프를 받는 주사위가 있는지와 그 칸. 표시(HeroOverdriveView)가 쓴다.</summary>
    public bool Active { get; private set; }
    public Vector2Int Cell { get; private set; }

    /// <summary>눈이 하나 깎였다. 측정용(어느 주사위를 얼마나 태웠는지).</summary>
    public event Action<Vector2Int> PipDrained;

    public HeroOverdrive(SO_HeroConfig config, Hero hero, DiceField diceField, DiceCombat diceCombat, MonsterField monsters)
    {
        _config = config;
        _hero = hero;
        _diceField = diceField;
        _diceCombat = diceCombat;
        _monsters = monsters;
        _diceField.DiceRemoved += OnDiceRemoved;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose()
    {
        _diceField.DiceRemoved -= OnDiceRemoved;
        Core.Loop.OnGameUpdate -= Tick;
        _diceCombat.ClearBoost();
    }

    /// <summary>새 판: 쌓인 시간을 비운다.</summary>
    public void Clear()
    {
        _elapsed.Clear();
        SetInactive();
    }

    private void OnDiceRemoved(int index, Vector2Int cell) => _elapsed.Remove(cell);

    // 게임 시간을 따른다 — 카드를 집는 동안의 감속을 똑같이 받는다(DiceCombat과 같다).
    private void Tick(float deltaTime)
    {
        // 뛰어오르는 중에는 아직 올라탄 게 아니다 — 지나가며 깡총 밟은 주사위가 깎이지 않게.
        Vector2Int cell = GridCircle.CellOf(_hero.Position);
        bool onDice = _diceField.TryGetPlaced(cell, out PlacedDice placed);
        SO_SkillCard_Overdrive overdrive = _config.Overdrive;
        if (overdrive == null || _hero.Held || _hero.IsHopping || !onDice || placed.Dice.IsSpent)
        {
            SetInactive();
            return;
        }

        Active = true;
        Cell = cell;
        _diceCombat.SetBoost(cell, overdrive.DamageScale, overdrive.SpeedScale);

        _elapsed.TryGetValue(cell, out float elapsed);
        if (!_monsters.Paused && _monsters.Count > 0 && !_diceCombat.IsOverdriven(cell))
        {
            elapsed += deltaTime;
            while (true)
            {
                bool awakened = _diceField.IsAwakened(cell);
                float interval = awakened ? _config.OverdriveAwakenPipInterval : _config.OverdrivePipInterval;
                if (elapsed < interval || !_diceField.DrainPip(cell)) break;
                elapsed -= interval;
                PipDrained?.Invoke(cell);
            }
        }
        _elapsed[cell] = elapsed;
    }

    private void SetInactive()
    {
        Active = false;
        _diceCombat.ClearBoost();
    }
}
