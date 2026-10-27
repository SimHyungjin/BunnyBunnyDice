using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 놓인 주사위들의 공격 주기. 간격마다 쏠 수 있는 주사위를 골라 DiceAttack에 넘긴다(투사체 없음).
/// 누구를 어떻게 맞힐지는 공격 방식(종류 SO)마다 다르므로 DiceAttack이 정하고, 여기서는 언제 쏠지만 정한다.
/// 배치·표시(DiceField)와 떼어 둔 이유: 공격은 몬스터 상태 배열을 훑는 일이라 몬스터와 함께 ECS로 옮겨 갈 수 있게.
/// </summary>
public sealed class DiceCombat : IDisposable
{
    private readonly SO_MapViewConfig _viewConfig;
    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly SO_DiceConfig _diceConfig;
    private readonly DiceField _diceField;
    private readonly ChipField _chips;
    private readonly MonsterField _monsters;
    private readonly DiceAttack _attack;
    // 모두 DiceField.Placed와 같은 번호. 철거(폴드)되면 같은 자리를 지우고(OnDiceRemoved), 판이 바뀌어 비면 같이 비운다.
    private readonly List<float> _cooldowns = new List<float>();
    // 지금까지 쏜 횟수(저격 각성의 "N번째마다").
    private readonly List<int> _shotCounts = new List<int>();
    // 오버드라이브 스킬: 남은 시간과 공격력·공격 속도 배율(기획서 5.3).
    private readonly List<float> _overdriveTimes = new List<float>();
    private readonly List<float> _overdriveDamage = new List<float>();
    private readonly List<float> _overdriveSpeed = new List<float>();
    // 영웅 오버드라이브(영웅이 올라탄 주사위, 기획서 10.9) — 카드와 같은 상태다. 한 번에 하나뿐이라 칸으로 들고 있는다.
    private bool _hasBoost;
    private Vector2Int _boostCell;
    private float _boostDamage = 1f;
    private float _boostSpeed = 1f;

    public DiceCombat(SO_MapViewConfig viewConfig, SO_DiceStatsConfig statsConfig,
        SO_DiceConfig diceConfig, DiceField diceField, ChipField chips, MonsterField monsters, DiceAttack attack)
    {
        _viewConfig = viewConfig;
        _statsConfig = statsConfig;
        _diceConfig = diceConfig;
        _diceField = diceField;
        _chips = chips;
        _monsters = monsters;
        _attack = attack;
        _diceField.DiceRemoved += OnDiceRemoved;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        _diceField.DiceRemoved -= OnDiceRemoved;
        Core.Loop.OnUpdate -= Tick;
    }

    // 주사위 번호가 앞으로 당겨지므로 붙여 둔 상태도 같은 자리를 빼서 맞춘다.
    private void OnDiceRemoved(int index, Vector2Int cell)
    {
        if (index < 0 || index >= _cooldowns.Count) return;
        _cooldowns.RemoveAt(index);
        _shotCounts.RemoveAt(index);
        _overdriveTimes.RemoveAt(index);
        _overdriveDamage.RemoveAt(index);
        _overdriveSpeed.RemoveAt(index);
    }

    /// <summary>
    /// 오버드라이브: index번째 주사위가 duration초 동안 공격력 damageScale배·공속 speedScale배로 쏜다. 다시 걸면 시간이 새로 시작한다(겹쳐 세지 않음).
    /// 지금 기다리던 대기 시간도 빨라진 간격까지 줄인다 — 쓰자마자 빨라진 것이 보이게.
    /// </summary>
    public void Overdrive(int index, float damageScale, float speedScale, float duration)
    {
        SyncSlots(_diceField.Placed.Count);
        if (index < 0 || index >= _overdriveTimes.Count || speedScale <= 0f) return;
        _overdriveTimes[index] = duration;
        _overdriveDamage[index] = damageScale;
        _overdriveSpeed[index] = speedScale;
        PlacedDice dice = _diceField.Placed[index];
        DiceStats stats = DiceStats.Compute(dice.Dice, _diceConfig.GetDiceType(dice.Dice.Colors), _statsConfig);
        _cooldowns[index] = Mathf.Min(_cooldowns[index], stats.AttackInterval / speedScale);
    }

    /// <summary>cell에 놓인 주사위의 사거리(칩 보너스 포함). 주사위가 없으면 0. 영웅이 올라타 있으면 이 사거리로 친다(HeroAttack).</summary>
    public float GetRange(Vector2Int cell)
    {
        if (!_diceField.TryGetPlaced(cell, out PlacedDice dice)) return 0f;
        DiceStats stats = DiceStats.Compute(dice.Dice, _diceConfig.GetDiceType(dice.Dice.Colors), _statsConfig);
        return stats.GetRange(ChipEffects.RangeBonus(_chips.Get(cell)));
    }

    /// <summary>cell의 주사위에 오버드라이브 카드 시간이 남아 있는지. 영웅 오버드라이브는 이 시간을 먼저 쓴다(그동안 눈이 깎이지 않는다).</summary>
    public bool IsOverdriven(Vector2Int cell)
    {
        int index = _diceField.IndexOf(cell);
        return index >= 0 && index < _overdriveTimes.Count && _overdriveTimes[index] > 0f;
    }

    /// <summary>cell의 주사위가 오버드라이브 상태로 쏜다(영웅 오버드라이브 — 영웅이 올라탄 동안). 다른 칸을 부르면 그쪽으로 옮겨 간다.</summary>
    public void SetBoost(Vector2Int cell, float damageScale, float speedScale)
    {
        _hasBoost = true;
        _boostCell = cell;
        _boostDamage = damageScale;
        _boostSpeed = speedScale;
    }

    public void ClearBoost() => _hasBoost = false;

    // 주사위 번호마다 붙여 둔 상태를 놓인 주사위 수에 맞춘다.
    private void SyncSlots(int count)
    {
        // 판이 바뀌어 줄었으면 비운다. 새로 놓인 주사위는 대기 없이 바로 쏠 수 있게 0으로 붙인다.
        if (_cooldowns.Count > count)
        {
            _cooldowns.Clear();
            _shotCounts.Clear();
            _overdriveTimes.Clear();
            _overdriveDamage.Clear();
            _overdriveSpeed.Clear();
        }
        while (_cooldowns.Count < count)
        {
            _cooldowns.Add(0f);
            _shotCounts.Add(0);
            _overdriveTimes.Add(0f);
            _overdriveDamage.Add(1f);
            _overdriveSpeed.Add(1f);
        }
    }

    // 게임 시간을 따른다 — 카드를 집는 동안의 감속(게임 속도 50%)을 공격도 똑같이 받는다.
    private void Tick(float _)
    {
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        SyncSlots(placed.Count);

        // 게임 오버로 몬스터가 멈추면 공격도 멈춘다(뚫린 자리를 그대로 보여 주게).
        if (_monsters.Paused || _monsters.Route == null) return;
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < placed.Count; i++)
        {
            _overdriveTimes[i] = Mathf.Max(_overdriveTimes[i] - deltaTime, 0f);
            // 적이 없으면 준비된 채로 기다릴 뿐 쌓아 두지 않는다 — 적이 들어오자마자 몰아 쏘지 않게.
            _cooldowns[i] = Mathf.Max(_cooldowns[i] - deltaTime, 0f);
            if (_cooldowns[i] > 0f) continue;

            PlacedDice dice = placed[i];
            // 눈을 다 태운 주사위는 쏘지 않는다(벽으로만 남는다).
            if (dice.Dice.IsSpent) continue;
            SO_DiceType type = _diceConfig.GetDiceType(dice.Dice.Colors);
            DiceStats stats = DiceStats.Compute(dice.Dice, type, _statsConfig);
            if (stats.AttackInterval <= 0f) continue;
            float damageScale = 1f;
            float speedScale = 1f;
            DamageBoost boost = DamageBoost.None;
            if (_overdriveTimes[i] > 0f)
            {
                damageScale = _overdriveDamage[i];
                speedScale = _overdriveSpeed[i];
                boost = DamageBoost.Card;
            }
            // 영웅이 올라탄 주사위도 같은 오버드라이브 상태라 겹치지 않는다 — 곱하지 않고 큰 쪽을 쓴다.
            // 카드 시간이 남았으면 카드 몫으로 센다(영웅 오버드라이브가 카드 시간을 먼저 쓴다).
            if (_hasBoost && dice.Cell == _boostCell)
            {
                damageScale = Mathf.Max(damageScale, _boostDamage);
                speedScale = Mathf.Max(speedScale, _boostSpeed);
                if (boost == DamageBoost.None) boost = DamageBoost.Hero;
            }
            stats.Damage *= damageScale;
            if (speedScale > 0f) stats.AttackInterval /= speedScale;

            // 붙은 강화는 그 칸의 칩이다(칩 위에 놓인 주사위가 받는다). 칩 위 주사위는 칩 두께만큼 올라앉아 있다.
            SO_ChipCard chip = _chips.Get(dice.Cell);
            stats.Damage *= 1f + ChipEffects.DamageBonus(chip);
            stats.AttackInterval /= 1f + ChipEffects.AttackSpeedBonus(chip);
            float top = chip != null ? _viewConfig.ChipHeight : _viewConfig.GetTileTop(TileType.Ground);
            DiceAttack.Shot shot = new DiceAttack.Shot
            {
                Dice = dice.Dice,
                Type = type,
                Stats = stats,
                Origin = dice.Cell,
                Muzzle = new Vector3(dice.Cell.x, top + _diceConfig.MuzzleHeight, dice.Cell.y),
                Range = stats.GetRange(ChipEffects.RangeBonus(chip)),
                ShotNumber = _shotCounts[i] + 1,
                Attachment = chip,
                Boost = boost,
            };
            if (!_attack.Fire(shot)) continue;
            _cooldowns[i] = stats.AttackInterval;
            _shotCounts[i] = shot.ShotNumber;
        }
    }
}
