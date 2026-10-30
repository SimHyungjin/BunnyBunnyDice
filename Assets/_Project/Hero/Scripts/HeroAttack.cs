using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅 자동 공격. 간격마다 사거리 안의 가장 가까운 몬스터를 노려 던지는 동작을 시작하고, 카드가 손을 떠나는 순간
/// (SO_HeroAnimationSet.AttackWindup 뒤) 맞힌다(투사체 판정 없음). 수치는 캐릭터·단계에 따른 HeroStats.
/// 준비 시간은 동작 재생이 아니라 데이터로 센다 — 화면 없이 도는 AI 시뮬도 같은 시각에 맞힌다.
/// 조작은 이동뿐이고 공격은 알아서 한다 — 영웅의 역할은 "어디에 서 있느냐"로 빈틈을 메우는 것(기획서 10.1).
/// 이동(Hero)과 떼어 둔 이유: 조작 방식이 바뀌어도 공격 규칙이 같게.
/// 주사위 위에 올라타 있으면 그 주사위 사거리로 친다(더 길 때만, 2026-10-26) — 레이즈로 주사위에 올라타면 길에서 멀어져
/// 자동 공격이 안 닿았다(올라탄 자리와 때리는 자리가 다퉜다).
/// </summary>
public sealed class HeroAttack : IDisposable
{
    private readonly SO_HeroConfig _config;
    private readonly HeroStats _stats;
    private readonly Hero _hero;
    private readonly MonsterField _monsters;
    private readonly HeroCardShots _cards;
    private readonly Func<Vector2Int, float> _standingRange;
    private float _cooldown;
    // 던지는 중인 공격들: 손을 떠나기까지 남은 시간과 처음 노린 몬스터(Id — 슬롯은 치우기로 당겨지므로).
    // 여럿을 둔다 — 공속 강화는 끝없이 쌓여 공격 간격이 준비 시간보다 짧아지는데, 앞 공격이 손을 떠나길 기다리면 공속이 거기서 막힌다.
    private readonly List<float> _pendingTimes = new List<float>();
    private readonly List<int> _pendingTargets = new List<int>();
    // 손을 떠날 때 고른 대상 슬롯들(가까운 순). 매번 비워 다시 쓴다.
    private readonly List<int> _targets = new List<int>();
    // _targets(고정 대상 다음부터)와 같은 순서의 거리 제곱 — 가까운 순으로 끼워 넣을 자리를 찾는다.
    private readonly List<float> _targetSqr = new List<float>();
    // 강화(브레이크 전용 카드 — 브레이크 샷): 남은 시간과 공격력·공격 속도 배율.
    private float _boostTime;
    private float _boostDamage = 1f;
    private float _boostSpeed = 1f;

    public HeroAttack(SO_HeroConfig config, HeroStats stats, Hero hero, MonsterField monsters, HeroCardShots cards, Func<Vector2Int, float> standingRange)
    {
        _standingRange = standingRange;
        _config = config;
        _stats = stats;
        _hero = hero;
        _monsters = monsters;
        _cards = cards;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    /// <summary>꺼 두면 공격하지 않는다(테스트 — 색 시험이 주사위 딜만 재려고 끈다, ColorPowerTest).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>강화 중인지(남은 시간이 있는지 — 브레이크 샷).</summary>
    public bool IsBoosted => _boostTime > 0f;

    /// <summary>영웅이 duration초 동안 공격력 damageScale배·공격 속도 speedScale배로 친다. 다시 걸면 시간이 새로 시작한다(겹쳐 세지 않음).</summary>
    public void Boost(float damageScale, float speedScale, float duration)
    {
        _boostTime = duration;
        _boostDamage = damageScale;
        _boostSpeed = Mathf.Max(speedScale, 0.01f);
        // 지금 기다리던 대기 시간도 빨라진 간격까지 줄인다 — 쓰자마자 빨라진 것이 보이게.
        _cooldown = Mathf.Min(_cooldown, _stats.AttackInterval / _boostSpeed);
    }

    /// <summary>판이 바뀌면 강화를 끝낸다.</summary>
    public void Clear() => _boostTime = 0f;

    // 게임 시간을 따른다 — 카드를 집는 동안의 감속(게임 속도 50%)을 공격도 똑같이 받는다.
    private void Tick(float deltaTime)
    {
        // 게임 오버로 몬스터가 멈추면 공격도 멈춘다(주사위와 같게).
        if (_monsters.Paused || !Enabled) return;
        _boostTime = Mathf.Max(_boostTime - deltaTime, 0f);

        Vector2 position = _hero.Position;
        float range = Range(position);

        for (int i = 0; i < _pendingTimes.Count; i++)
        {
            _pendingTimes[i] -= deltaTime;
            if (_pendingTimes[i] > 0f) continue;
            Release(position, range, _pendingTargets[i]);
            _pendingTimes.RemoveAt(i);
            _pendingTargets.RemoveAt(i);
            i--;
        }

        // 쿨다운은 준비를 시작할 때부터 센다 — 계속 싸우는 동안 공격 간격이 준비 시간만큼 늘지 않게.
        // 남은 시간은 이월해 한 프레임에 여러 번도 시작한다(간격이 프레임보다 짧아도 공속대로 친다).
        // 적이 없으면 준비된 채로 기다릴 뿐 쌓아 두지 않는다(DiceCombat과 같게).
        _cooldown -= deltaTime;
        float interval = _stats.AttackInterval / (IsBoosted ? _boostSpeed : 1f);
        SO_HeroAnimationSet animations = _hero.Animations;
        float windup = animations != null ? animations.AttackWindup(interval) : 0f;
        while (_cooldown <= 0f)
        {
            int target = MonsterTargeting.FindNearest(_monsters.States, _monsters.Count, position, range);
            if (target < 0)
            {
                _cooldown = 0f;
                break;
            }
            _hero.PlayAttack(interval, _monsters.States[target].Position);
            // 이번 공격은 -_cooldown초 전에 시작했어야 했다 — 그만큼 손을 떠나는 시각도 당긴다.
            float remaining = windup + _cooldown;
            int targetId = _monsters.States[target].Id;
            if (remaining > 0f)
            {
                _pendingTimes.Add(remaining);
                _pendingTargets.Add(targetId);
            }
            else Release(position, range, targetId);

            if (interval <= 0f)
            {
                _cooldown = 0f;
                break;
            }
            _cooldown += interval;
        }
    }

    // 카드가 손을 떠나는 순간. 한 번에 던지는 카드 수(투사체)만큼 대상을 고르고 고르게 나눠 맞힌다.
    // 첫 대상은 처음 노린 적이 살아 있고 조금 넓힌 사거리 안이면 그 적, 아니면(주사위가 먼저 잡았거나 멀어졌으면) 지금 가장 가까운 적.
    // 나머지는 사거리 안 가까운 적부터 한 마리에 한 장씩, 적이 카드보다 적으면 앞에서부터 다시 돈다(한 마리면 다 그 적에게).
    // 카드마다 따로 맞히므로 치명타도 방어력도 카드마다다 — 공격력 강화(한 방이 셈)와 갈리는 지점이다.
    // 아무도 없으면 던지지 않고 쿨다운도 돌려준다 — 드문 일이라 헛손질로 벌하지 않는다.
    private void Release(Vector2 position, float range, int targetId)
    {
        int count = _stats.Projectiles;
        CollectTargets(position, range, targetId, count);
        if (_targets.Count == 0)
        {
            _cooldown = Mathf.Min(_cooldown, 0f);
            return;
        }

        // 카드는 던지는 손에서 나가 몸 바깥쪽(몸 가운데 → 손)으로 먼저 튀어 나간다. 손을 못 찾으면(임시 모델)
        // 발 높이 + AttackHeight에서 곧게(주사위 위에 서 있으면 그 위에서).
        Vector3 from = _hero.ThrowPoint ?? new Vector3(position.x, _hero.Floor + _config.AttackHeight, position.y);
        Vector3 launch = new Vector3(from.x - position.x, 0f, from.z - position.y);
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < count; i++)
        {
            int slot = _targets[i % _targets.Count];
            // 앞 카드에 죽었으면 살아 있는 다른 대상에게. 다 죽었으면 남는 카드는 버린다(남는 피해).
            if (!states[slot].IsAlive) slot = FirstAliveTarget();
            if (slot < 0) break;

            // 치명타는 영웅 공격과 스킬에만 있는 영웅 능력치다(기획서 10.2) — 주사위 치명타(SO_DiceStatsConfig)와 따로 굴린다.
            bool critical = GameRandom.Crit.Value < _stats.CritChance;
            float damage = _stats.Attack * (IsBoosted ? _boostDamage : 1f);
            _monsters.Hit(slot, damage * (critical ? _stats.CritMultiplier : 1f), DamageSource.Hero, 0f, critical);
            _cards.Throw(from, launch, states[slot].Id, critical, i, count);
        }
    }

    // 이번에 던질 대상 슬롯들을 _targets에 가까운 순으로 담는다(최대 count마리, 같은 적은 한 번).
    // 몬스터를 한 번만 훑으며 가까운 count마리를 끼워 넣는다 — 장마다 전체를 다시 훑으면 투사체·공속이 오를수록
    // (공격 수 × 장 수 × 마릿수) 무거워졌다(몬스터를 모은 시험에서 프레임당 4ms — 2026-10-28 프로파일).
    private void CollectTargets(Vector2 position, float range, int lockedId, int count)
    {
        _targets.Clear();
        _targetSqr.Clear();
        int locked = FindLocked(lockedId, position, range * _config.AttackKeepRangeScale);
        if (locked >= 0) _targets.Add(locked);
        int start = _targets.Count;
        int wanted = count - start;
        if (wanted <= 0) return;

        MonsterState[] states = _monsters.States;
        float rangeSqr = range * range;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (i == locked || !states[i].IsAlive) continue;
            float sqr = (states[i].Position - position).sqrMagnitude;
            if (sqr > rangeSqr) continue;
            int found = _targetSqr.Count;
            if (found == wanted && sqr >= _targetSqr[found - 1]) continue;

            // 가까운 순 자리에 끼우고, 넘치면 가장 먼 것을 뺀다. 같은 거리면 먼저 본 것을 앞에 둔다.
            int at = found;
            while (at > 0 && _targetSqr[at - 1] > sqr) at--;
            _targetSqr.Insert(at, sqr);
            _targets.Insert(start + at, i);
            if (_targetSqr.Count > wanted)
            {
                _targetSqr.RemoveAt(wanted);
                _targets.RemoveAt(start + wanted);
            }
        }
    }

    private int FirstAliveTarget()
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _targets.Count; i++)
            if (states[_targets[i]].IsAlive) return _targets[i];
        return -1;
    }

    // 처음 노린 몬스터의 지금 슬롯. 죽었거나 range 밖이면 -1.
    private int FindLocked(int targetId, Vector2 position, float range)
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (states[i].Id != targetId) continue;
            bool inRange = (states[i].Position - position).sqrMagnitude <= range * range;
            return states[i].IsAlive && inRange ? i : -1;
        }
        return -1;
    }

    // 깡총 뛰는 중에도 밟은 칸의 주사위 사거리로 친다 — 주사위 사이를 건널 때마다 사거리가 깜빡이지 않게.
    private float Range(Vector2 position) =>
        Mathf.Max(_stats.Range, _standingRange != null ? _standingRange(GridCircle.CellOf(position)) : 0f);
}
