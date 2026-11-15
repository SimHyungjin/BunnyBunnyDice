using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주사위 한 번의 공격(기획서 6.5). 공격 방식(종류 SO의 하위 클래스)에 따라 누구를 어떻게 맞힐지 정하고, 피해·각성·표시까지 여기서 한다.
/// DiceCombat은 언제 쏠지만 정한다 — 공격 방식이 늘어도 공격 주기 코드는 그대로 두려고.
/// 노리는 대상은 선두 우선이다 — 강타(빨강)가 체력 높은 적을 노리면 오버딜이라는 약점이 사라진다.
/// 관통만 예외로 가장 많이 꿰뚫는 쪽을 노린다(얇은 선은 길과 나란할 때만 줄 전체를 꿰뚫는다, 기획서 6.5).
/// 치명타는 직접 맞힌 피해마다 굴리고, 폭발 파편은 효과라 치명타가 없다.
/// 붙은 강화(둔화·화상·취약)는 이 주사위가 맞히는 모든 적에게 건다 — 폭발 파편 포함(기획서 7).
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
        /// <summary>주사위가 선 칸(맵 로컬 x, z). 관통선·난사의 중심.</summary>
        public Vector2 Origin;
        /// <summary>공격선이 나가는 점(맵 로컬).</summary>
        public Vector3 Muzzle;
        /// <summary>칩 보너스(사거리 칩)까지 반영한 사거리.</summary>
        public float Range;
        /// <summary>이 주사위의 몇 번째 공격인지(1부터).</summary>
        public int ShotNumber;
        /// <summary>주사위가 올라앉은 칩(두 장까지 + 세기). 없으면 비어 있다.</summary>
        public ChipSet Attachment;
        /// <summary>레이즈 상태로 쏘는지(측정용 — 배율은 Stats에 이미 들어 있다).</summary>
        public DamageBoost Boost;
        /// <summary>각성으로 쏘는지(눈이 각성 눈에 닿았다).</summary>
        public bool Awakened;
    }

    // 각성한 하양의 두 번째 난사. 첫 난사 뒤 잠시 있다가 그때 범위 안에 있는 몬스터를 맞힌다.
    private struct PendingVolley
    {
        public Vector2 Center;
        public float Range;
        public float Damage;
        public float MaxHealthRatio;
        public float CritChance;
        public Color Color;
        public SO_DiceType_Barrage Type;
        public float Delay;
        public ChipSet Attachment;
        public DamageSource Source;
    }

    /// <summary>주사위 하나가 쐈다(맞힐 대상이 있어 실제로 쏜 때만). 연출(소리)용.</summary>
    public event Action Fired;

    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly SO_MonsterConfig _monsterConfig;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly MonsterField _monsters;
    private readonly AttackEffects _effects;
    private readonly List<PendingVolley> _volleys = new List<PendingVolley>();
    // 다중 타겟·체인이 고른 몬스터. 모자라면 늘린다.
    private int[] _targets = new int[8];
    private float[] _remaining = new float[8];
    // 난사 범위 안 몬스터와 거리(제곱) — 맞히는 수 상한이 있어 가까운 순으로 고른다.
    private readonly List<int> _barrageHits = new List<int>();
    private readonly List<float> _barrageDistances = new List<float>();
    // 지금 쏘는 주사위의 붙은 강화. 맞히는 곳마다 넘기지 않으려고 Fire 동안 들고 있는다.
    private ChipSet _attachment;
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
        bool fired = FireShot(shot);
        if (fired) Fired?.Invoke();
        return fired;
    }

    private bool FireShot(in Shot shot)
    {
        if (shot.Type == null) return false;
        _attachment = shot.Attachment;
        _source = DamageSource.Dice(shot.Dice.Colors, shot.Boost);
        bool awaken = shot.Awakened;
        Color color = shot.Type.Color;
        switch (shot.Type)
        {
            case SO_DiceType_Explosion explosion: return FireExplosion(shot, explosion, awaken, color);
            case SO_DiceType_Snipe snipe: return FireSnipe(shot, snipe, awaken, color);
            case SO_DiceType_Rapid rapid: return FireRapid(shot, rapid, awaken, color);
            case SO_DiceType_LongRange longRange: return FireLongRange(shot, longRange, awaken, color);
            case SO_DiceType_Pierce pierce: return FirePierce(shot, pierce, awaken, color);
            case SO_DiceType_Chain chain: return FireChain(shot, chain, awaken, color);
            case SO_DiceType_Barrage barrage: return FireBarrage(shot, barrage, awaken, color);
            // 검정은 평범한 단일 공격이다. 속사의 빠르기는 능력치(DiceStats)에서 나온다.
            default: return FireSingle(shot, color);
        }
    }

    /// <summary>판이 바뀌면 남은 두 번째 난사와 속사의 이어 맞히기를 버린다.</summary>
    public void Clear()
    {
        _volleys.Clear();
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

    // 강타(빨강): 매 공격 확정 치명타. 각성하면 치명타 배율이 더 오른다(AwakenCritBonus) — 둘째·셋째 빨강도 각자 자기 피해가 커진다.
    private bool FireSnipe(in Shot shot, SO_DiceType_Snipe type, bool awaken, Color color)
    {
        int target = FindFirst(shot);
        if (target < 0) return false;
        Vector2 position = _monsters.States[target].Position;
        float multiplier = _statsConfig.CritMultiplier + (awaken ? type.AwakenCritBonus : 0f);
        Hit(target, DamageOn(shot, target, multiplier), true);
        _effects.Show(shot.Muzzle, ToHitPoint(position), color, true);
        return true;
    }

    // 속사(초록): 같은 적을 이어 맞힐 때마다 피해가 쌓인다. 주사위 칸으로 누구를 몇 번 맞혔는지 기억한다. 각성의 공격 속도는 능력치(DiceStats)에서.
    private bool FireRapid(in Shot shot, SO_DiceType_Rapid type, bool awaken, Color color)
    {
        int target = FindFirst(shot);
        if (target < 0) return false;
        MonsterState state = _monsters.States[target];
        Vector2Int cell = Vector2Int.RoundToInt(shot.Origin);
        int hits = _focus.TryGetValue(cell, out var focus) && focus.id == state.Id ? focus.hits + 1 : 0;
        _focus[cell] = (state.Id, hits);
        float scale = 1f + type.FocusBonusPerHit * hits;
        bool critical = HitDirect(target, DamageOn(shot, target, scale), shot.Stats.CritChance);
        _effects.Show(shot.Muzzle, ToHitPoint(state.Position), color, critical);
        return true;
    }

    // 저격(파랑): 멀리 있는 적일수록 세다 — 한 칸마다 FarDamagePerCell씩(각성하면 AwakenFarDamagePerCell을 더), 상한 없이(사거리가 늘면 끝의 피해도 는다).
    private bool FireLongRange(in Shot shot, SO_DiceType_LongRange type, bool awaken, Color color)
    {
        int target = FindFirst(shot);
        if (target < 0) return false;
        Vector2 position = _monsters.States[target].Position;
        float distance = (position - shot.Origin).magnitude;
        float perCell = type.FarDamagePerCell + (awaken ? type.AwakenFarDamagePerCell : 0f);
        bool critical = HitDirect(target, DamageOn(shot, target, 1f + perCell * distance), shot.Stats.CritChance);
        _effects.Show(shot.Muzzle, ToHitPoint(position), color, critical);
        return true;
    }

    // 선두를 맞히고 그 자리에서 터진다. 각성하면 반경이 커진다.
    // 한때(2026-10-26) 가장 많이 휘말리는 몬스터를 골라 맞혔는데, 뭉쳐 다니는 판(길 하나로 모으기)에서 매번 가장 빽빽한 곳에 떨어져
    // 2색 성장 절반을 맞히는 수로 넘어섰다 — 퍼뎀을 뺀 뒤 노랑이 판을 끌어 선두로 되돌렸다(2026-11-03 사용자).
    private bool FireExplosion(in Shot shot, SO_DiceType_Explosion type, bool awaken, Color color)
    {
        int target = FindFirst(shot);
        if (target < 0) return false;
        MonsterState[] states = _monsters.States;
        float growth = _statsConfig.GetGrowth(shot.Dice.Pips);
        float radius = Mathf.Min(type.ExplosionRadius * (1f + type.ExplosionRadiusGrowth * growth), type.MaxExplosionRadius)
            * (awaken ? 1f + type.AwakenExplosionRadiusBonus : 1f);
        float radiusSqr = radius * radius;
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
    // 동점이면 선두 쪽(먼저 본 후보)을 남긴다. 각성하면 맞은 적마다 방어력의 일정 비율을 무시한다.
    // 관통 값은 고정량이라(MonsterDamage) 맞은 적의 방어력에 비율을 곱해 넘긴다 — 적마다 방어력이 달라도 같은 비율로 뚫게.
    private bool FirePierce(in Shot shot, SO_DiceType_Pierce type, bool awaken, Color color)
    {
        int lead = FindFirst(shot);
        if (lead < 0) return false;
        MonsterState[] states = _monsters.States;
        float halfWidth = type.HalfWidth;
        float penetration = awaken ? type.AwakenPenetration : 0f;
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
            float ignored = _monsters.KindOf(i).GetArmor(states[i].MaxHealth, states[i].WaveArmor) * penetration;
            anyCritical |= HitDirect(i, DamageOn(shot, i, 1f), shot.Stats.CritChance, ignored);
        }
        _effects.Show(shot.Muzzle, ToHitPoint(end), color, anyCritical, 1f);
        return true;
    }

    // target 쪽으로 사거리 끝까지. 주사위 칸 안에는 몬스터가 들어오지 않지만, 겹쳐 서 있어도 방향이 0이 되지 않게 막는다.
    private static Vector2 PierceEnd(in Shot shot, Vector2 target)
    {
        Vector2 direction = target - shot.Origin;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        return shot.Origin + direction * shot.Range;
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

    // 자기 주변 사거리 안 모두를 맞힌다. 각성하면 잠시 뒤 두 번째 난사가 약하게 한 번 더 나간다.
    // (반경을 늘리면 짧은 사거리라는 약점을 메우게 되므로 같은 자리에서 두 겹으로 친다.)
    private bool FireBarrage(in Shot shot, SO_DiceType_Barrage type, bool awaken, Color color)
    {
        if (!Barrage(shot.Origin, shot.Range, shot.Stats.Damage, shot.Stats.MaxHealthRatio, shot.Stats.CritChance, color, type))
            return false;
        if (awaken)
        {
            _volleys.Add(new PendingVolley
            {
                Center = shot.Origin,
                Range = shot.Range,
                Damage = shot.Stats.Damage * type.AwakenRatio,
                MaxHealthRatio = shot.Stats.MaxHealthRatio * type.AwakenRatio,
                CritChance = shot.Stats.CritChance,
                Color = color,
                Type = type,
                Delay = type.AwakenDelay,
                Attachment = _attachment,
                Source = _source,
            });
        }
        return true;
    }

    // 범위 안에 아무도 없으면 터지지 않는다(첫 난사는 쏘지 않은 것으로 본다).
    // 범위는 그리지 않는다 — 사거리까지 퍼지는 고리는 공속이 빨라(1.5) 여러 개가 겹쳐 화면을 덮었고, 늘 떠 있는 고리·원판도 어색했다(2026-11-01 사용자).
    // 대신 맞은 적 발밑마다 작은 고리가 터진다 — 범위는 맞은 적들로 드러난다. 주사위 맥박은 두지 않는다(2026-11-01 사용자).
    // 맞히는 수는 SO_DiceType_Barrage.MaxTargets까지, 가까운 순이다.
    private bool Barrage(Vector2 center, float range, float damage, float maxHealthRatio, float critChance, Color color, SO_DiceType_Barrage type)
    {
        MonsterState[] states = _monsters.States;
        float rangeSqr = range * range;
        _barrageHits.Clear();
        _barrageDistances.Clear();
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            float distanceSqr = (states[i].Position - center).sqrMagnitude;
            if (distanceSqr > rangeSqr) continue;
            _barrageHits.Add(i);
            _barrageDistances.Add(distanceSqr);
        }
        if (_barrageHits.Count == 0) return false;

        // 상한을 넘으면 가까운 것부터 앞으로 모은다(상한만큼만 고르는 선택 정렬 — 상한이 작아 전체 정렬보다 싸다).
        int count = Mathf.Min(_barrageHits.Count, Mathf.Max(type.MaxTargets, 1));
        if (count < _barrageHits.Count)
        {
            for (int k = 0; k < count; k++)
            {
                int nearest = k;
                for (int j = k + 1; j < _barrageHits.Count; j++)
                    if (_barrageDistances[j] < _barrageDistances[nearest]) nearest = j;
                if (nearest == k) continue;
                (_barrageHits[k], _barrageHits[nearest]) = (_barrageHits[nearest], _barrageHits[k]);
                (_barrageDistances[k], _barrageDistances[nearest]) = (_barrageDistances[nearest], _barrageDistances[k]);
            }
        }

        float ground = _viewConfig.GetTileTop(TileType.Ground);
        for (int k = 0; k < count; k++)
        {
            int slot = _barrageHits[k];
            Vector2 at = states[slot].Position;
            HitDirect(slot, damage + MaxHealthBonus(slot, maxHealthRatio), critChance);
            _effects.ShowSpark(new Vector3(at.x, ground, at.y), type.HitSparkRadius, color);
        }
        return true;
    }

    // 게임 시간을 따른다. 몬스터가 멈추면(게임 오버) 두 번째 난사도 멈춘다.
    private void Tick(float deltaTime)
    {
        if (_volleys.Count == 0 || _monsters.Paused) return;
        for (int i = _volleys.Count - 1; i >= 0; i--)
        {
            PendingVolley volley = _volleys[i];
            volley.Delay -= deltaTime;
            if (volley.Delay > 0f)
            {
                _volleys[i] = volley;
                continue;
            }
            _attachment = volley.Attachment;
            _source = volley.Source;
            Barrage(volley.Center, volley.Range, volley.Damage, volley.MaxHealthRatio, volley.CritChance, volley.Color, volley.Type);
            _volleys[i] = _volleys[_volleys.Count - 1];
            _volleys.RemoveAt(_volleys.Count - 1);
        }
    }

    private int FindFirst(in Shot shot) =>
        MonsterTargeting.FindFirst(_monsters.States, _monsters.Count, shot.Origin, shot.Range, _monsters.Route);

    // 한 방 피해 = (고정 + 맞은 적 최대 체력 × 비율) × scale. 공격 방식의 배율(이어 맞히기·거리·폭발 주변·체인 감쇠)은 최대 체력 피해에도 똑같이 곱한다.
    private float DamageOn(in Shot shot, int slot, float scale) =>
        (shot.Stats.Damage + MaxHealthBonus(slot, shot.Stats.MaxHealthRatio)) * scale;

    private float MaxHealthBonus(int slot, float ratio)
    {
        if (ratio <= 0f) return 0f;
        return (float)(_monsters.States[slot].MaxHealth * ratio);
    }

    // 치명타 배율은 방어력보다 먼저 곱한다(MonsterDamage) — 큰 주사위가 단단한 적에게 강한 이유.
    // penetration = 방어력에서 빼는 양(관통 — 하늘).
    private bool HitDirect(int slot, float damage, float critChance, float penetration = 0f)
    {
        bool critical = GameRandom.Crit.Value < critChance;
        Hit(slot, damage * (critical ? _statsConfig.CritMultiplier : 1f), critical, penetration);
        return critical;
    }

    // 강화를 피해보다 먼저 건다 — 취약이 그 공격부터 적용되게(취약을 붙이고 처음 맞힐 때 손해 보지 않게).
    private void Hit(int slot, float damage, bool critical, float penetration = 0f)
    {
        _attachment.OnHit(_monsters, slot);
        _monsters.Hit(slot, damage, _source, penetration, critical);
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
