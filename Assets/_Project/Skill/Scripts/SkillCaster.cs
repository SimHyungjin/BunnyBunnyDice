using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 카드를 실제로 쓴다(기획서 8): 대상을 모으고, 몬스터·주사위를 바꾸고, 선·고리로 보여 준다.
/// 적 대상 스킬은 모두 즉발이고 판 위의 모든 적에게 쓴다(2026-10-29 사용자 결정 — 처음엔 한 점의 원이었다).
/// 적 대상·판 전체 스킬은 대상이 없어도 맵 위면 쓸 수 있다 — 필요 없는 카드를 버리는 방법이기도 하다.
/// 계산은 SkillRules, 끄는 동안의 표시는 SkillPreview — 미리보기도 같은 대상 모으기(CollectTargets)를 지나 실제와 어긋나지 않는다.
/// 적 대상 스킬은 모두 영웅 공격력 비례 피해를 먼저 주고(기획서 10.1) 특징을 더한다. 스킬로 죽은 적은 판 주사위처럼 포켓으로 슝 날아간다(MonsterField.SinkBySkill).
/// 연출: 판정은 즉발이고 몸체만 따라 움직인다 — 레이크는 빨려 들어가고, 백스핀은 튕겨 미끄러진다(MonsterRenderer).
/// 발동 컷인은 캐릭터 작업과 함께 넣는다.
/// </summary>
public sealed class SkillCaster
{
    private readonly MonsterField _monsters;
    private readonly DiceField _diceField;
    private readonly ChipField _chips;
    private readonly DiceCombat _diceCombat;
    private readonly AttackEffects _effects;
    private readonly HeroStats _heroStats;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly Func<MapData> _getMap;
    private readonly Func<GoalHealth> _getGoal;
    private readonly Func<HeroAttack> _getHeroAttack;
    private readonly ExpBoost _expBoost;
    private readonly Func<CardHand> _getHand;
    // 쓸 때 대상을 모아 두는 작업 공간(할당 없이).
    private readonly List<int> _targets = new List<int>();

    /// <summary>스킬 카드를 썼다(어느 입구로든). 측정 기록(WaveLog)이 센다.</summary>
    public event Action<SO_SkillCard> Used;

    /// <summary>적 대상 스킬 한 번의 결과(깎은 체력·넘친 피해·처치). 측정 기록(WaveLog)이 스킬별로 센다 — 배율이 남아도는지 보려고.</summary>
    public event Action<SO_EnemySkillCard, EnemyCastResult> EnemyCast;

    // 쓰는 동안 모으는 결과.
    private EnemyCastResult _result;

    public SkillCaster(MonsterField monsters, DiceField diceField, ChipField chips, DiceCombat diceCombat, AttackEffects effects, HeroStats heroStats,
        SO_MapViewConfig viewConfig, Func<MapData> getMap, Func<GoalHealth> getGoal, Func<HeroAttack> getHeroAttack, ExpBoost expBoost,
        Func<CardHand> getHand)
    {
        _monsters = monsters;
        _diceField = diceField;
        _chips = chips;
        _diceCombat = diceCombat;
        _effects = effects;
        _heroStats = heroStats;
        _viewConfig = viewConfig;
        _getMap = getMap;
        _getGoal = getGoal;
        _getHeroAttack = getHeroAttack;
        _expBoost = expBoost;
        _getHand = getHand;
    }

    /// <summary>판정(SkillRules.CheckDice·CheckCell)을 통과한 칸에만 부른다 — 주사위 대상·칸 대상 스킬.</summary>
    public void CastOnCell(Vector2Int cell, SO_SkillCard card)
    {
        Used?.Invoke(card);
        if (card is SO_SkillCard_CashOut cashOut)
        {
            CashOut(cell, cashOut);
            return;
        }
        if (card is SO_SkillCard_FullHouse fullHouse)
        {
            _diceCombat.FullHouse(_diceField.IndexOf(cell), fullHouse.Duration);
            _effects.ShowBurst(Ground(cell), fullHouse.BurstRadius, card.IconColor);
            _effects.ShowRing(Ground(cell), fullHouse.BurstRadius * 0.5f, card.IconColor, fullHouse.Duration);
            return;
        }
        if (card is SO_SkillCard_Overdrive overdrive)
        {
            _diceCombat.Overdrive(_diceField.IndexOf(cell), overdrive.DamageScale, overdrive.SpeedScale, overdrive.Duration);
            // 쓰는 순간 퍼지는 고리 + 지속되는 동안 주사위 밑에 머무는 고리 — 어느 주사위가 빨라졌는지 보이게.
            _effects.ShowBurst(Ground(cell), overdrive.BurstRadius, card.IconColor);
            _effects.ShowRing(Ground(cell), overdrive.RingRadius, card.IconColor, overdrive.Duration);
            return;
        }
        _diceField.ApplySkill(cell, card);
    }

    /// <summary>쓰면 영향을 받을 몬스터 슬롯을 result에 채운다(판 위의 산 적 모두). 미리보기와 실제 사용이 같은 함수를 지난다.</summary>
    public int CollectTargets(SO_EnemySkillCard card, List<int> result)
    {
        result.Clear();
        if (card == null || _monsters.Route == null) return 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (states[i].IsAlive) result.Add(i);
        }
        return result.Count;
    }

    /// <summary>체크메이트의 왕: targets 중 체력이 가장 많이 남은 슬롯(같으면 먼저 모은 쪽). 체크메이트가 아니거나 대상이 없으면 -1.</summary>
    public int FindKing(SO_EnemySkillCard card, List<int> targets)
    {
        if (!(card is SO_SkillCard_Checkmate) || targets.Count == 0) return -1;
        int best = targets[0];
        for (int i = 1; i < targets.Count; i++)
        {
            if (_monsters.States[targets[i]].Health > _monsters.States[best].Health) best = targets[i];
        }
        return best;
    }

    /// <summary>레이크의 모음점: 놓은 칸(설 수 없으면 가장 가까운 설 수 있는 칸)의 중심. 레이크가 아니거나 판에 설 칸이 없으면 false.</summary>
    public bool TryGetGatherPoint(SO_EnemySkillCard card, Vector2 center, out Vector2 gather)
    {
        gather = center;
        MapData map = _getMap();
        if (!(card is SO_SkillCard_Rake) || _monsters.Route == null || map == null) return false;
        // 판 어디를 짚어도 설 칸을 찾게 판 전체를 덮는 거리로 찾는다.
        return SkillRules.TryGetGatherPoint(_monsters.Route, center, map.Width + map.Height, out gather);
    }

    /// <summary>center에 쓸 수 있는지 — 맵 위면 된다. 대상이 없어도 쓸 수 있다(카드를 버리는 방법).</summary>
    public bool CanCast(SO_EnemySkillCard card, Vector2 center)
    {
        MapData map = _getMap();
        if (card == null || map == null) return false;
        Vector2Int cell = GridCircle.CellOf(center);
        return map.InBounds(cell.x, cell.y);
    }

    /// <summary>CanCast가 참인 자리에만 부른다.</summary>
    public void Cast(SO_EnemySkillCard card, Vector2 center)
    {
        if (_monsters.Route == null) return;
        Used?.Invoke(card);
        _result = new EnemyCastResult { Targets = CollectTargets(card, _targets) };
        DamageTargets(card);
        switch (card)
        {
            case SO_SkillCard_Pocket pocket:
                CastPocket(pocket);
                break;
            case SO_SkillCard_Rake rake:
                CastRake(rake, center);
                break;
            case SO_SkillCard_Backspin backspin:
                CastBackspin(backspin);
                break;
            case SO_SkillCard_Check check:
                // 멈춘 모습은 얼림 표시(체력 바 옆 상태 칸)가 보여 준다.
                for (int i = 0; i < _targets.Count; i++) _monsters.Hold(_targets[i], check.Duration);
                break;
        }
        EnemyCast?.Invoke(card, _result);
    }

    // 모든 적에게 영웅 공격력 비례 피해(체크메이트의 왕은 왕 배율만 받는다). 적마다 영웅 치명타를 따로 굴린다. 죽은 적은 포켓으로 날려 보내고 대상에서 뺀다(특징은 산 적에게만).
    private void DamageTargets(SO_EnemySkillCard card)
    {
        int king = FindKing(card, _targets);
        float kingMultiplier = card is SO_SkillCard_Checkmate checkmate ? checkmate.KingMultiplier : 0f;
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            int slot = _targets[i];
            float multiplier = slot == king ? kingMultiplier : card.DamageMultiplier;
            if (multiplier <= 0f) continue;
            float damage = _heroStats.Attack * multiplier;
            bool critical = GameRandom.Crit.Value < _heroStats.CritChance;
            ref MonsterState state = ref _monsters.States[slot];
            float before = state.Health;
            _monsters.Hit(slot, critical ? damage * _heroStats.CritMultiplier : damage, DamageSource.Skill, 0f, critical);
            _result.Dealt += before - Mathf.Max(state.Health, 0f);
            if (state.IsAlive) continue;
            // 방어력을 지난 뒤 남은 체력을 넘은 몫 — 화력이 남아도는지.
            _result.Overkill -= state.Health;
            _result.Kills++;
            _monsters.SinkBySkill(slot);
            _targets.RemoveAt(i);
        }
    }

    // 피해(DamageTargets) 뒤 처치선 아래로 내려간 적을 가장 가까운 포켓으로 굴려 넣는다(보스 포함).
    private void CastPocket(SO_SkillCard_Pocket card)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            int slot = _targets[i];
            ref MonsterState state = ref _monsters.States[slot];
            if (state.Health > state.MaxHealth * card.HealthThreshold) continue;
            _result.Dealt += state.Health;
            _result.Kills++;
            _monsters.Execute(slot);
        }
    }

    // 모든 적을 모음점 둘레로 끌어온다(보스 포함). 모음점에 고리를 퍼뜨려 어디로 모였는지 보이게.
    private void CastRake(SO_SkillCard_Rake card, Vector2 center)
    {
        if (!TryGetGatherPoint(card, center, out Vector2 gather)) return;
        GridRouteField route = _monsters.Route;
        _effects.ShowBurst(Ground(gather), card.BurstRadius, card.IconColor);
        for (int i = 0; i < _targets.Count; i++)
        {
            int slot = _targets[i];
            // 한 점에 완전히 겹치면 한 마리로 보이므로 둘레에 흩어 세운다. 흩은 자리가 설 수 없는 칸이면 모음점 그대로.
            Vector2 to = gather + GameRandom.Skill.InsideUnitCircle() * card.Scatter;
            if (!SkillRules.IsWalkable(route, to)) to = gather;
            // 끌려 들어가므로 점점 빨라진다.
            _monsters.Displace(slot, to, card.SlideDuration, true);
        }
    }

    // 걷던 방향의 반대쪽으로 곧게 밀어낸다. 저항(거대)이 있으면 거리를 줄인다.
    private void CastBackspin(SO_SkillCard_Backspin card)
    {
        GridRouteField route = _monsters.Route;
        for (int i = 0; i < _targets.Count; i++)
        {
            int slot = _targets[i];
            ref MonsterState state = ref _monsters.States[slot];
            // 막 나와 아직 걷지 않았으면 향하던 경유점의 반대쪽.
            Vector2 forward = state.Heading.sqrMagnitude > 0f ? state.Heading : state.Waypoint - state.Position;
            if (forward.sqrMagnitude <= 0f) continue;
            Vector2 from = state.Position;
            Vector2 to = SkillRules.Knockback(route, from, -forward.normalized, card.Distance * _monsters.KindOf(slot).ImpairScale);
            if (to == from) continue;
            // 맞아서 튕겨 나가므로 점점 느려진다.
            _monsters.Displace(slot, to, card.SlideDuration, false);
        }
    }

    /// <summary>판 전체 스킬은 언제든 쓸 수 있다 — 바꿀 것이 없으면 아무 일도 없이 버려진다(카드를 버리는 방법).</summary>
    public bool CanCastBoard(SO_SkillCard card) => card != null && card.Target == SkillTarget.Board;

    public void CastBoard(SO_SkillCard card)
    {
        Used?.Invoke(card);
        switch (card)
        {
            case SO_SkillCard_AllIn _:
                _getGoal()?.HealFull();
                _diceField.RerollAll();
                break;
            case SO_SkillCard_BreakShot breakShot:
                _getHeroAttack()?.Overdrive(breakShot.DamageScale, breakShot.SpeedScale, breakShot.Duration);
                break;
            case SO_SkillCard_Respin _:
                _diceField.RerollLowest();
                break;
            case SO_SkillCard_HouseEdge houseEdge:
                _expBoost.Activate(houseEdge.ExpScale, houseEdge.Duration);
                break;
            case SO_SkillCard_SevensHeaven sevens:
                _diceCombat.AwakenAll(sevens.Duration);
                break;
        }
    }

    // 칸의 주사위·칩을 거둬 장마다 Copies장씩 손패로 돌려준다. 주사위는 면체·색만 돌려준다(눈은 놓을 때 다시 굴린다).
    // 주사위·칩이 없어지면 각자 이벤트로 길을 다시 계산한다(GameScene). 사라진 자리에 고리를 퍼뜨려 무엇이 없어졌는지 보이게.
    private void CashOut(Vector2Int cell, SO_SkillCard_CashOut card)
    {
        CardHand hand = _getHand();
        if (_diceField.TryGetPlaced(cell, out PlacedDice placed) && _diceField.Remove(cell))
        {
            HandCard dice = HandCard.Of(new DiceCard(placed.Dice.Faces, placed.Dice.Colors));
            for (int i = 0; i < card.Copies; i++) hand?.Add(dice);
        }
        SO_ChipCard chip = _chips.Get(cell);
        if (chip != null && _chips.Remove(cell))
        {
            for (int i = 0; i < card.Copies; i++) hand?.Add(HandCard.Of(chip));
        }
        _effects.ShowBurst(Ground(cell), CashOutBurstRadius, card.IconColor);
    }

    // 캐시 아웃 고리는 칸 하나를 덮는 크기(칸 = 1) — 조정 값이 아니라 대상 칸의 크기다.
    private const float CashOutBurstRadius = 0.5f;

    private Vector3 Ground(Vector2 position) => new Vector3(position.x, _viewConfig.GetTileTop(TileType.Ground), position.y);

    private Vector3 Ground(Vector2Int cell) => Ground((Vector2)cell);

}
