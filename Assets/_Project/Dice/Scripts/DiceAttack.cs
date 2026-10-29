using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주사위 한 번의 공격(기획서 9.4). 공격 방식(종류 SO의 하위 클래스)에 따라 누구를 어떻게 맞힐지 정하고, 피해·각성·표시까지 여기서 한다.
/// DiceCombat은 언제 쏠지만 정한다 — 공격 방식이 늘어도 공격 주기 코드는 그대로 두려고.
/// 노리는 대상은 선두 우선이다 — 강타(빨강)가 체력 높은 적을 노리면 오버딜이라는 약점이 사라진다.
/// 관통·폭발만 예외로 가장 많이 휘말리는 곳을 노린다(범위가 쓸모 있으려면 뭉친 곳을 맞혀야 한다, 기획서 9.4).
/// 치명타는 직접 맞힌 피해마다 굴리고, 폭발 파편은 효과라 치명타가 없다.
/// 붙은 강화(둔화·독·취약)는 이 주사위가 맞히는 모든 적에게 건다 — 폭발 파편 포함(기획서 9.7).
/// </summary>
public sealed class DiceAttack : IDisposable
{
    /// <summary>주사위 하나의 이번 공격.</summary>
    public struct Shot
    {
        public DiceState Dice;
        /// <summary>색 상태의 종류. 공격 방식과 그 값을 정한다.</summary>
        public SO_DiceType Type;
        public DiceStats Stats;
        /// <summary>주사위가 선 칸(맵 로컬 x, z). 관통선·파동의 중심.</summary>
        public Vector2 Origin;
        /// <summary>공격선이 나가는 점(맵 로컬).</summary>
        public Vector3 Muzzle;
        /// <summary>칩 보너스(사거리 칩)까지 반영한 사거리.</summary>
        public float Range;
        /// <summary>이 주사위의 몇 번째 공격인지(1부터). 저격 각성의 "N번째마다 확정 치명타"에 쓴다.</summary>
        public int ShotNumber;
        /// <summary>주사위가 올라앉은 칩. 없으면 null.</summary>
        public SO_ChipCard Attachment;
        /// <summary>오버드라이브 상태로 쏘는지(측정용 — 배율은 Stats에 이미 들어 있다).</summary>
        public DamageBoost Boost;
    }

    // 각성한 하양의 두 번째 파동. 첫 파동 뒤 잠시 있다가 그때 범위 안에 있는 몬스터를 맞힌다.
    private struct PendingPulse
    {
        public Vector2 Center;
        public float Range;
        public float Damage;
        public float MaxHealthRatio;
        public bool MaxHealthBossImmune;
        public float CritChance;
        public Color Color;
        public float Delay;
        public SO_ChipCard Attachment;
        public DamageSource Source;
    }

    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly SO_MonsterConfig _monsterConfig;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly MonsterField _monsters;
    private readonly AttackEffects _effects;
    private readonly List<PendingPulse> _pulses = new List<PendingPulse>();
    // 다중 타겟·체인이 고른 몬스터. 모자라면 늘린다.
    private int[] _targets = new int[8];
    private float[] _remaining = new float[8];
    // 지금 쏘는 주사위의 붙은 강화. 맞히는 곳마다 넘기지 않으려고 Fire 동안 들고 있는다.
    private SO_ChipCard _attachment;
    // 지금 쏘는 주사위의 색(측정 기록의 피해 출처). 붙은 강화와 같은 이유로 들고 있는다.
    private DamageSource _source;
    // 속사(초록)가 주사위 칸마다 이어 맞히는 적(MonsterState.Id)과 이어 맞힌 수. 대상이 바뀌면 0부터.
    private readonly Dictionary<Vector2Int, (int id, int hits)> _focus = new Dictionary<Vector2Int, (int, int)>();

    public DiceAttack(SO_DiceStatsConfig statsConfig, SO_MonsterConfig monsterConfig, SO_MapViewConfig viewConfig,
        MonsterField monsters, AttackEffects effects)
    {
        _statsConfig = statsConfig;
        _monsterConfig = monsterConfig;
        _viewConfig = viewConfig;
        _monsters = monsters;
        _effects = effects;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    /// <summary>쏜다. 사거리 안에 맞힐 몬스터가 없어 쏘지 않았으면 false — 쏘는 쪽은 재사용 대기를 걸지 않고 다음에 다시 본다.</summary>
    public bool Fire(in Shot shot)
    {
        if (shot.Type == null) return false;
        _attachment = shot.Attachment;
        _source = DamageSource.Dice(shot.Dice.Colors, shot.Boost);
        bool awaken = _statsConfig.IsAwakened(shot.Dice);
        Color color = shot.Type.Color;
        switch (shot.Type)
        {
            case SO_DiceType_Explosion explosion: return FireExplosion(shot, explosion, awaken, color);
            case SO_DiceType_Snipe snipe: return FireSnipe(shot, snipe, awaken, color);
            case SO_DiceType_Rapid rapid: return FireRapid(shot, rapid, color);
            case SO_DiceType_LongRange longRange: return FireLongRange(shot, longRange, color);
            case SO_DiceType_Pierce pierce: return FirePierce(shot, pierce, awaken, color);
            case SO_DiceType_Chain chain: return FireChain(shot, chain, awaken, color);
            case SO_DiceType_Pulse pulse: return FirePulse(shot, pulse, awaken, color);
            // 검정은 평범한 단일 공격이다. 속사의 빠르기와 각성(공격 속도), 저격의 각성(사거리)은 능력치(DiceStats)에서 나온다.
            default: return FireSingle(shot, color);
        }
    }

    /// <summary>판이 바뀌면 남은 두 번째 파동과 속사의 이어 맞히기를 버린다.</summary>
    public void Clear()
    {
        _pulses.Clear();
        _focus.Clear();
    }

    private bool FireSingle(in Shot shot, Color color)
    {
        int target = FindFirst(shot);
        if (target < 0) return false;
        Vector2 position = _monsters.States[target].Position;
        bool critical = HitDirect(target, DamageOn(shot, target, 1f), shot.Stats.CritChance);
        _effects.Show(shot.Muzzle, ToHitPoint(position), color, critical);
        return true;
    }

    // 강타(빨강): 각성하면 매 공격 확정 치명타(배율은 공통 배율 그대로) — 강한 한 방이 늘 두 배로 들어간다.
    private bool FireSnipe(in Shot shot, SO_DiceType_Snipe type, bool awaken, Color color)
    {
        int target = FindSingle(shot, type);
        if (target < 0) return false;
        Vector2 position = _monsters.States[target].Position;
        float damage = DamageOn(shot, target, 1f);
        bool critical = true;
        if (awaken) Hit(target, damage * _statsConfig.CritMultiplier, true);
        else critical = HitDirect(target, damage, shot.Stats.CritChance);
        _effects.Show(shot.Muzzle, ToHitPoint(position), color, critical);
        return true;
    }

    // 속사(초록): 같은 적을 이어 맞힐 때마다 피해가 쌓인다. 주사위 칸으로 누구를 몇 번 맞혔는지 기억한다.
    private bool FireRapid(in Shot shot, SO_DiceType_Rapid type, Color color)
    {
        int target = FindSingle(shot, type);
        if (target < 0) return false;
        MonsterState state = _monsters.States[target];
        Vector2Int cell = Vector2Int.RoundToInt(shot.Origin);
        int hits = _focus.TryGetValue(cell, out var focus) && focus.id == state.Id ? focus.hits + 1 : 0;
        _focus[cell] = (state.Id, hits);
        bool critical = HitDirect(target, DamageOn(shot, target, 1f + type.FocusBonusPerHit * hits), shot.Stats.CritChance);
        _effects.Show(shot.Muzzle, ToHitPoint(state.Position), color, critical);
        return true;
    }

    // 저격(파랑): 멀리 있는 적일수록 세다 — 사거리 끝에서 (1 + FarDamageBonus)배.
    private bool FireLongRange(in Shot shot, SO_DiceType_LongRange type, Color color)
    {
        int target = FindSingle(shot, type);
        if (target < 0) return false;
        Vector2 position = _monsters.States[target].Position;
        float distance = shot.Range > 0f ? Mathf.Clamp01((position - shot.Origin).magnitude / shot.Range) : 0f;
        bool critical = HitDirect(target, DamageOn(shot, target, 1f + type.FarDamageBonus * distance), shot.Stats.CritChance);
        _effects.Show(shot.Muzzle, ToHitPoint(position), color, critical);
        return true;
    }

    // 사거리 안 몬스터 중 그 자리에서 터지면 가장 많이 휘말리는 몬스터를 맞히고, 그 자리에서 터진다(관통과 같은 겨냥).
    // 선두만 노리면 폭발이 쓸모 있는 때가 거의 없었다 — 관통은 길을 바꿔 줄을 세우면 되지만, 선두 둘레를 뭉치게 할 방법은 없다.
    // 동점이면 선두를 남긴다. 각성하면 반경이 커진다.
    private bool FireExplosion(in Shot shot, SO_DiceType_Explosion type, bool awaken, Color color)
    {
        int lead = FindFirst(shot);
        if (lead < 0) return false;
        MonsterState[] states = _monsters.States;
        float growth = _statsConfig.GetGrowth(shot.Dice.Pips);
        float radius = Mathf.Min(type.ExplosionRadius * (1f + type.ExplosionRadiusGrowth * growth), type.MaxExplosionRadius)
            * (awaken ? 1f + type.AwakenExplosionRadiusBonus : 1f);
        float radiusSqr = radius * radius;

        int target = lead;
        int best = CountInCircle(states[lead].Position, radiusSqr);
        float rangeSqr = shot.Range * shot.Range;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (i == lead || !states[i].IsAlive || (states[i].Position - shot.Origin).sqrMagnitude > rangeSqr) continue;
            int hits = CountInCircle(states[i].Position, radiusSqr);
            if (hits <= best) continue;
            best = hits;
            target = i;
        }
        Vector2 center = states[target].Position;

        bool critical = HitDirect(target, DamageOn(shot, target, 1f), shot.Stats.CritChance);
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (i == target || !states[i].IsAlive || (states[i].Position - center).sqrMagnitude > radiusSqr) continue;
            Hit(i, DamageOn(shot, i, type.ExplosionDamageRatio), false);
        }

        float ground = _viewConfig.GetTileTop(TileType.Ground);
        _effects.ShowBurst(new Vector3(center.x, ground, center.y), radius, color);
        _effects.Show(shot.Muzzle, ToHitPoint(center), color, critical);
        return true;
    }

    // 사거리 끝까지 얇은 곧은 선을 긋고 선 위 몬스터를 모두 맞힌다. 겨냥은 선두가 아니라 사거리 안 몬스터 중
    // 선 위에 가장 많이 걸리는 쪽이다 — 얇은 선은 길과 나란할 때만 줄 전체를 꿰뚫으므로, 선두만 노리면 나란한 방향이 있어도 놓친다.
    // 동점이면 선두 쪽(먼저 본 후보)을 남긴다. 각성하면 선이 굵어진다.
    private bool FirePierce(in Shot shot, SO_DiceType_Pierce type, bool awaken, Color color)
    {
        int lead = FindFirst(shot);
        if (lead < 0) return false;
        MonsterState[] states = _monsters.States;
        float halfWidth = type.HalfWidth * (awaken ? type.AwakenWidthScale : 1f);
        float halfWidthSqr = halfWidth * halfWidth;
        float rangeSqr = shot.Range * shot.Range;

        Vector2 end = PierceEnd(shot, states[lead].Position);
        int best = CountOnLine(shot.Origin, end, halfWidthSqr);
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (i == lead || !states[i].IsAlive || (states[i].Position - shot.Origin).sqrMagnitude > rangeSqr) continue;
            Vector2 candidate = PierceEnd(shot, states[i].Position);
            int hits = CountOnLine(shot.Origin, candidate, halfWidthSqr);
            if (hits <= best) continue;
            best = hits;
            end = candidate;
        }

        bool anyCritical = false;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || DistanceToSegmentSqr(states[i].Position, shot.Origin, end) > halfWidthSqr) continue;
            anyCritical |= HitDirect(i, DamageOn(shot, i, 1f), shot.Stats.CritChance);
        }
        _effects.Show(shot.Muzzle, ToHitPoint(end), color, anyCritical, halfWidth / type.HalfWidth);
        return true;
    }

    // target 쪽으로 사거리 끝까지. 주사위 칸 안에는 몬스터가 들어오지 않지만, 겹쳐 서 있어도 방향이 0이 되지 않게 막는다.
    private static Vector2 PierceEnd(in Shot shot, Vector2 target)
    {
        Vector2 direction = target - shot.Origin;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        return shot.Origin + direction * shot.Range;
    }

    private int CountInCircle(Vector2 center, float radiusSqr)
    {
        MonsterState[] states = _monsters.States;
        int count = 0;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && (states[i].Position - center).sqrMagnitude <= radiusSqr) count++;
        return count;
    }

    private int CountOnLine(Vector2 from, Vector2 to, float halfWidthSqr)
    {
        MonsterState[] states = _monsters.States;
        int count = 0;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && DistanceToSegmentSqr(states[i].Position, from, to) <= halfWidthSqr) count++;
        return count;
    }

    // 선두를 맞히고 가까운 몬스터로 차례로 튄다. 튈 때마다 약해지고, 피해가 MinJumpDamage 아래로 떨어지면 멈춘다
    // — 횟수를 따로 두지 않아 공격력이 클수록(눈이 높을수록) 멀리 이어진다. 각성하면 덜 약해져 훨씬 길게 이어진다.
    // 선 굵기를 피해에 맞춰 줄여, 약해지는 것이 눈에 보이게 한다.
    private bool FireChain(in Shot shot, SO_DiceType_Chain type, bool awaken, Color color)
    {
        int current = FindFirst(shot);
        if (current < 0) return false;

        float falloff = awaken ? type.AwakenFalloff : type.Falloff;
        // 같은 몬스터는 한 번만 맞으므로 마릿수를 넘게 튀지 않는다.
        EnsureCapacity(Mathf.Max(_monsters.Count, 1));

        MonsterState[] states = _monsters.States;
        Vector3 from = shot.Muzzle;
        float scale = 1f;
        for (int hit = 0; hit < _monsters.Count; hit++)
        {
            Vector2 position = states[current].Position;
            bool critical = HitDirect(current, DamageOn(shot, current, scale), shot.Stats.CritChance);
            Vector3 to = ToHitPoint(position);
            _effects.Show(from, to, color, critical, scale);

            _targets[hit] = current;
            scale *= falloff;
            // 멈춤은 고정 피해로만 본다 — 최대 체력 피해를 넣으면 후반 큰 적에게선 거의 멈추지 않는다(총합은 감쇠로 묶여 있지만 튀는 수가 늘어난다).
            if (shot.Stats.Damage * scale < type.MinJumpDamage) break;
            current = MonsterTargeting.FindNearestExcept(states, _monsters.Count, position, type.JumpRange, _targets, hit + 1);
            if (current < 0) break;
            from = to;
        }
        return true;
    }

    // 자기 주변 사거리 안 모두를 맞힌다. 각성하면 잠시 뒤 두 번째 파동이 약하게 한 번 더 나간다.
    // (반경을 늘리면 짧은 사거리라는 약점을 메우게 되므로 같은 자리에서 두 겹으로 친다.)
    private bool FirePulse(in Shot shot, SO_DiceType_Pulse type, bool awaken, Color color)
    {
        if (!Pulse(shot.Origin, shot.Range, shot.Stats.Damage, shot.Stats.MaxHealthRatio, shot.Stats.MaxHealthBossImmune, shot.Stats.CritChance, color))
            return false;
        if (awaken)
        {
            _pulses.Add(new PendingPulse
            {
                Center = shot.Origin,
                Range = shot.Range,
                Damage = shot.Stats.Damage * type.AwakenPulseRatio,
                MaxHealthRatio = shot.Stats.MaxHealthRatio * type.AwakenPulseRatio,
                MaxHealthBossImmune = shot.Stats.MaxHealthBossImmune,
                CritChance = shot.Stats.CritChance,
                Color = color,
                Delay = type.AwakenPulseDelay,
                Attachment = _attachment,
                Source = _source,
            });
        }
        return true;
    }

    // 범위 안에 아무도 없으면 터지지 않는다(첫 파동은 쏘지 않은 것으로 본다).
    private bool Pulse(Vector2 center, float range, float damage, float maxHealthRatio, bool bossImmune, float critChance, Color color)
    {
        MonsterState[] states = _monsters.States;
        float rangeSqr = range * range;
        bool any = false;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || (states[i].Position - center).sqrMagnitude > rangeSqr) continue;
            HitDirect(i, damage + MaxHealthBonus(i, maxHealthRatio, bossImmune), critChance);
            any = true;
        }
        if (any) _effects.ShowBurst(new Vector3(center.x, _viewConfig.GetTileTop(TileType.Ground), center.y), range, color);
        return any;
    }

    // 게임 시간을 따른다. 몬스터가 멈추면(게임 오버) 두 번째 파동도 멈춘다.
    private void Tick(float deltaTime)
    {
        if (_pulses.Count == 0 || _monsters.Paused) return;
        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            PendingPulse pulse = _pulses[i];
            pulse.Delay -= deltaTime;
            if (pulse.Delay > 0f)
            {
                _pulses[i] = pulse;
                continue;
            }
            _attachment = pulse.Attachment;
            _source = pulse.Source;
            Pulse(pulse.Center, pulse.Range, pulse.Damage, pulse.MaxHealthRatio, pulse.MaxHealthBossImmune, pulse.CritChance, pulse.Color);
            _pulses[i] = _pulses[_pulses.Count - 1];
            _pulses.RemoveAt(_pulses.Count - 1);
        }
    }

    private int FindFirst(in Shot shot) =>
        MonsterTargeting.FindFirst(_monsters.States, _monsters.Count, shot.Origin, shot.Range, _monsters.Route);

    // 단일형: 종류가 켜 두면 최대 체력이 가장 큰 적부터(SO_DiceType.PreferToughest), 아니면 선두.
    private int FindSingle(in Shot shot, SO_DiceType type) => type.PreferToughest
        ? MonsterTargeting.FindToughest(_monsters.States, _monsters.Count, shot.Origin, shot.Range, _monsters.Route)
        : FindFirst(shot);

    // 한 방 피해 = (고정 + 맞은 적 최대 체력 × 비율) × scale. 공격 방식의 배율(이어 맞히기·거리·폭발 주변·체인 감쇠)은 최대 체력 피해에도 똑같이 곱한다.
    private float DamageOn(in Shot shot, int slot, float scale) =>
        (shot.Stats.Damage + MaxHealthBonus(slot, shot.Stats.MaxHealthRatio, shot.Stats.MaxHealthBossImmune)) * scale;

    // 범위형은 보스에게 최대 체력 피해를 주지 않는다(잡몹 담당 — SO_DiceType.MaxHealthBossImmune). 분열 조각은 보스가 아니다.
    private float MaxHealthBonus(int slot, float ratio, bool bossImmune)
    {
        if (ratio <= 0f || (bossImmune && _monsters.KindOf(slot).IsBoss)) return 0f;
        return _monsters.States[slot].MaxHealth * ratio;
    }

    // 치명타 배율은 방어력보다 먼저 곱한다(MonsterDamage) — 큰 주사위가 단단한 적에게 강한 이유.
    private bool HitDirect(int slot, float damage, float critChance)
    {
        bool critical = GameRandom.Crit.Value < critChance;
        Hit(slot, damage * (critical ? _statsConfig.CritMultiplier : 1f), critical);
        return critical;
    }

    // 강화를 피해보다 먼저 건다 — 취약이 그 공격부터 적용되게(취약을 붙이고 처음 맞힐 때 손해 보지 않게).
    private void Hit(int slot, float damage, bool critical)
    {
        if (_attachment != null) ChipEffects.OnHit(_monsters, slot, _attachment);
        _monsters.Hit(slot, damage, _source, 0f, critical);
    }

    // 몬스터는 바닥 칸만 걸으므로 맞는 점은 바닥 윗면 기준이다.
    private Vector3 ToHitPoint(Vector2 position) =>
        new Vector3(position.x, _viewConfig.GetTileTop(TileType.Ground) + _monsterConfig.HitHeight, position.y);

    private void EnsureCapacity(int count)
    {
        if (_targets.Length >= count) return;
        int size = Mathf.Max(count, _targets.Length * 2);
        _targets = new int[size];
        _remaining = new float[size];
    }

    private static float DistanceToSegmentSqr(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
        return (point - (a + ab * t)).sqrMagnitude;
    }
}
