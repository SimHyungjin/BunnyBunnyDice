using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>GameBot의 스킬 카드 쓰기 — 아껴 두다 메인 딜 구간이 뚫릴 것 같을 때 쓴다. 카드마다 쓰는 때·자리.</summary>
public sealed partial class GameBot
{
    // 스킬은 아껴 둔다 — 메인 딜 구간이 뚫릴 것 같을 때(또는 목표물 바로 앞까지 왔을 때)만 쓴다.
    // 막 쓰면 정작 뚫릴 때 손에 없다(사용자 지적: 미로에선 적이 늘 몰려 있어 "몰렸을 때"는 기준이 못 된다).
    private bool TryCast(SO_SkillCard skill)
    {
        if (skill is SO_SkillCard_AllIn) return TryCastAllIn(skill);
        if (skill is SO_SkillCard_CashOut) return TryCastCashOut(skill);
        // 하우스 엣지는 일찍 쓸수록 이득이다 — 적이 있으면 바로. 리스핀은 1이 둘 이상일 때.
        if (skill is SO_SkillCard_HouseEdge) return _monsters.Count > 0 && CastBoard(skill);
        if (skill is SO_SkillCard_Respin) return CountLowest() >= _config.RespinMinDice && CastBoard(skill);
        // 화력 카드(풀 하우스·브레이크 샷·세븐스 헤븐)는 다른 스킬처럼 뚫릴 때.
        if (skill is SO_SkillCard_BreakShot || skill is SO_SkillCard_SevensHeaven) return Breached(out _) && CastBoard(skill);
        if (skill is SO_SkillCard_FullHouse) return Breached(out _) && TryCastFullHouse(skill);
        if (skill is SO_SkillCard_Overdrive overdrive) return TryCastOverdrive(overdrive);
        if (skill.Target == SkillTarget.Dice) return TryCastOnDice(skill);
        SO_BotConfig.RunSet set = CurrentSet(_report.Count);
        if (set != null && set.DamageSkillAnytime && skill is SO_EnemySkillCard anytime) return TryCastAnytime(anytime);
        if (!Breached(out bool bossBreaching)) return false;
        // 체크메이트: 보스가 뚫으려 하면 바로, 아니면 다른 적 대상 스킬처럼 떼가 넘어올 때.
        if (skill is SO_SkillCard_Checkmate && bossBreaching) return TryCastAt((SO_EnemySkillCard)skill);
        if (skill is SO_SkillCard_Check check) return TryCastCheck(check);
        if (skill is SO_EnemySkillCard enemies) return TryCastEnemies(enemies);
        return false;
    }

    // 오버드라이브 카드: 빠져나갈 적을 걸면 잡을 수 있는 주사위에만(BotEscape). 영웅이 올라탄 주사위는 이미 오버드라이브다.
    private bool TryCastOverdrive(SO_SkillCard_Overdrive card)
    {
        if (!FindOverdriveTarget(card.DamageScale, card.SpeedScale, card.Duration, true, out Vector2Int cell)) return false;
        _skills.CastOnCell(cell, card);
        return true;
    }

    // 빠져나갈 적을 잡을 오버드라이브 자리. excludeHero면 영웅이 올라탄 주사위는 뺀다(카드는 겹쳐 세지지 않는다).
    private bool FindOverdriveTarget(float damageScale, float speedScale, float duration, bool excludeHero, out Vector2Int cell)
    {
        cell = default;
        if (!_board.HasKillZone) return false;
        Vector2Int? exclude = excludeHero && _hasHeroCell ? _heroCell : (Vector2Int?)null;
        return _escape.FindTarget(_board, _monsters, _diceField, _chips, _diceConfig, _statsConfig,
            _board.KillZoneDistance + _config.EscapeLookAhead, damageScale, speedScale, duration, _config.OverdriveMargin, exclude, out cell);
    }

    // 캐시 아웃(빌더): 메인 딜 구간을 막는 칸을 치워 딜링 주사위 자리를 넓힌다(사용자: 칩·실수로 못 쓰게 된 딜 구간을 넓힌다).
    // 손에 그 자리를 채울 d20·범위 주사위가 있을 때만 — 치우기만 하고 못 채우면 미로만 허문다.
    private bool TryCastCashOut(SO_SkillCard card)
    {
        MapData map = _getMap();
        if (map == null || !TryFindCashOutTarget(map, true, out Vector2Int cell)) return false;
        _skills.CastOnCell(cell, card);
        return true;
    }

    /// <summary>
    /// 캐시 아웃로 치울 칸: 메인 딜 구간 안의 칩만 있는 칸(시작 칩 등), 그다음 미로용 단일 주사위(d6·d12 단색·검정). 덮는 몰림이 큰 곳부터.
    /// needFiller면 손에 그 자리를 채울 d20·범위 주사위가 있어야 한다.
    /// </summary>
    private bool TryFindCashOutTarget(MapData map, bool needFiller, out Vector2Int cell)
    {
        cell = default;
        if (!_board.HasKillZone || (needFiller && !HasZoneFiller(map))) return false;
        int radius = Mathf.CeilToInt(_config.KillZoneRadius);
        int bestTier = -1;
        float best = -1f;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int at = _board.KillZone + new Vector2Int(x, y);
                if (!IsInZone(at) || !map.InBounds(at.x, at.y)) continue;
                int tier;
                if (map.HasChip(at.x, at.y) && !map.HasDice(at.x, at.y)) tier = 1;
                else if (_diceField.TryGetPlaced(at, out PlacedDice placed) && placed.Dice.Faces < 20 && !BotDice.IsArea(placed.Dice.Colors)) tier = 0;
                else continue;
                float score = _board.Traffic(at, _config.KillZoneRadius);
                if (tier < bestTier || (tier == bestTier && score <= best)) continue;
                bestTier = tier;
                best = score;
                cell = at;
            }
        }
        return bestTier >= 0;
    }

    // 구간에 빈 칸이 남아 있으면 치울 필요가 없다 — 주사위는 그 빈 칸으로 간다.
    private bool HasZoneFiller(MapData map)
    {
        for (int i = 0; i < _hand.Count; i++)
        {
            HandCard card = _hand.GetCard(i);
            if (!card.IsDice || (card.Dice.Faces < 20 && !BotDice.IsArea(card.Dice.Colors))) continue;
            if (!TryFindZoneCell(map, card.Dice, out _)) return true;
        }
        return false;
    }

    private bool CastBoard(SO_SkillCard card)
    {
        if (!_skills.CanCastBoard(card)) return false;
        _skills.CastBoard(card);
        return true;
    }

    private int CountLowest()
    {
        int count = 0;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            if (placed[i].Dice.Pips == 1) count++;
        }
        return count;
    }

    // 풀 하우스: 메인 딜 구간에 닿는 주사위 중 그 사거리로 덮는 몰림이 가장 큰 주사위.
    private bool TryCastFullHouse(SO_SkillCard card)
    {
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        float best = 0f;
        Vector2Int target = default;
        for (int i = 0; i < placed.Count; i++)
        {
            if (placed[i].Dice.IsSpent || !IsInZone(placed[i].Cell)) continue;
            SO_DiceType type = _diceConfig.GetDiceType(placed[i].Dice.Colors);
            float score = _board.Traffic(placed[i].Cell, type != null ? type.Range : 0f);
            if (score <= best) continue;
            best = score;
            target = placed[i].Cell;
        }
        if (best <= 0f) return false;
        _skills.CastOnCell(target, card);
        return true;
    }

    private bool HoldsCashOut()
    {
        for (int i = 0; i < _hand.Count; i++)
        {
            HandCard card = _hand.GetCard(i);
            if (card.IsSkill && card.Skill is SO_SkillCard_CashOut) return true;
        }
        return false;
    }

    // 적 대상 스킬은 판 전체라 놓는 자리는 레이크의 모음점만 정한다 — 메인 딜 구간 한가운데로 모은다(넘어온 적을 구간으로 되돌린다).
    // 구간이 없으면 판 한가운데.
    private bool TryCastAt(SO_EnemySkillCard card)
    {
        MapData map = _getMap();
        if (map == null) return false;
        Vector2 point = _board.HasKillZone ? (Vector2)_board.KillZone : new Vector2(map.Width / 2, map.Height / 2);
        if (!_skills.CanCast(card, point)) return false;
        _skills.Cast(card, point);
        return true;
    }

    // 메인 딜 구간을 넘어온 적이 몇 마리 이상일 때.
    private bool TryCastEnemies(SO_EnemySkillCard card) =>
        CountPastKillZone() >= _config.BreachEnemyMinTargets && TryCastAt(card);

    // 데미지형 위주 묶음: 뚫릴 때를 기다리지 않는다. 체크메이트는 보스가 있으면, 나머지는 판 위 적이 몇 마리 이상이면.
    private bool TryCastAnytime(SO_EnemySkillCard card)
    {
        if (card is SO_SkillCard_Checkmate) return HasBoss() && TryCastAt(card);
        return _skills.CollectTargets(card, _targets) >= _config.AnytimeEnemyMinTargets && TryCastAt(card);
    }

    private bool HasBoss()
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (states[i].IsAlive && _monsters.KindOf(i).IsBoss) return true;
        }
        return false;
    }

    // 체크는 구간을 넘어온 적이 많을 때 — 멈춘 사이 구간 주사위가 마저 깎는다.
    private bool TryCastCheck(SO_SkillCard_Check card) =>
        CountPastKillZone() >= _config.CheckBreachCount && TryCastAt(card);

    private bool TryCastAllIn(SO_SkillCard card)
    {
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        if (placed.Count == 0) return false;
        float ratio = 0f;
        for (int i = 0; i < placed.Count; i++) ratio += placed[i].Dice.Pips / (float)placed[i].Dice.Faces;
        if (ratio / placed.Count >= _config.AllInBelow) return false;
        _skills.CastBoard(card);
        return true;
    }

    // 주사위 대상 스킬: 쓸 수 있는 주사위 중 쓰면 가장 이득인 곳.
    private bool TryCastOnDice(SO_SkillCard card)
    {
        float best = 0f;
        Vector2Int target = default;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            if (!DicePlacement.IsAccepted(SkillRules.CheckDice(dice, card))) continue;
            float score = DiceSkillScore(card, dice, placed[i].Cell);
            if (score <= best) continue;
            best = score;
            target = placed[i].Cell;
        }
        if (best <= 0f) return false;
        _skills.CastOnCell(target, card);
        return true;
    }

    private float DiceSkillScore(SO_SkillCard card, in DiceState dice, Vector2Int cell)
    {
        float range = RangeAt(cell, dice.Colors, _chips.Get(cell));
        float traffic = _board.Traffic(cell, range) * (CoversKillZone(cell, range) ? _config.KillZoneStackWeight : 1f);
        int missing = dice.Faces - dice.Pips;
        if (card is SO_SkillCard_Jackpot) return traffic * missing;
        // 다시 굴리기는 눈이 절반 밑일 때만 — 기대값이 지금보다 높다.
        if (card is SO_SkillCard_Reroll) return dice.Pips * 2 < dice.Faces ? traffic * missing : 0f;
        // 페인트는 섞인 색(등급이 낮은) 주사위를 단색으로 되돌릴 때만.
        if (card is SO_SkillCard_Paint) return BotDice.ColorCount(dice.Colors) >= 2 ? traffic : 0f;
        return 0f;
    }
}
