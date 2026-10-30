using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 위의 몬스터들. 출현 구역에 세우고, 공용 걷는 길을 따라 걷게 하고, 목표물에 닿거나 죽으면 알린 뒤 치운다.
/// 몬스터는 객체가 아니라 상태 배열(MonsterState)의 한 칸이다. 지울 때는 마지막 칸을 그 자리로 옮겨 배열을 빈틈없이 유지한다
/// — 수가 많아져도 할당 없이 한 번에 훑을 수 있고, 나중에 ECS·GPU로 넘길 때 배열을 그대로 넘길 수 있게.
/// 걷는 길은 밖(GameScene)에서 만들어 넣는다 — 경로 표시(MapPathView)와 같은 길을 써서 표시가 실제 움직임과 어긋나지 않게.
/// 특성(기획서 9.1)도 여기서 적용한다: 방어력·이동 방해 저항은 맞을 때, 재생은 매 갱신, 분열은 치울 때.
/// </summary>
public sealed class MonsterField : IDisposable
{
    private readonly SO_MonsterConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly MonsterRenderer _renderer;
    // 상태에는 모양을 직접 넣지 않고 이 목록의 번호를 넣는다(MonsterState 참고).
    private readonly List<MonsterKind> _kinds = new List<MonsterKind>();
    private MonsterState[] _states;
    private int _count;
    private int _nextId;
    private MapData _map;
    private GridRouteField _route;

    /// <summary>몬스터가 목표물에 닿았다(이미 치운 뒤): 모양과 남은 체력. 피해는 받는 쪽이 모양(GoalDamage)을 보고 정한다.</summary>
    public event Action<MonsterKind, double> ReachedGoal;

    /// <summary>피해가 들어갔다: 맞은 자리(맵 로컬 x, z), 실제로 들어간 피해(정수 값), 종류. 피해 숫자 표시가 구독한다.</summary>
    public event Action<Vector2, double, DamageKind> Damaged;

    /// <summary>
    /// 피해가 들어갔다: 출처와 실제로 깎인 체력(남은 체력을 넘는 몫은 빼고). 측정 기록(WaveLog)이 구독한다
    /// — 넘친 몫까지 세면 큰 한 방이 실제보다 일한 것처럼 보인다.
    /// </summary>
    public event Action<DamageSource, double> DamageDealt;

    /// <summary>몬스터가 죽었다(이미 치운 뒤). 경험치 등은 받는 쪽이 모양을 보고 정한다.</summary>
    public event Action<MonsterKind, Vector2> Killed;

    /// <summary>멈추면(게임 오버) 걷지 않고 그 자리에 남는다.</summary>
    public bool Paused { get; set; }

    /// <summary>모든 몬스터의 이동 속도 배율(테스트 — 색 시험이 "모든 적에게 늘 디버프"를 이 값 하나로 건다, ColorPowerTest). 평소 1.</summary>
    public float SpeedScale { get; set; } = 1f;

    public int Count => _count;

    /// <summary>앞에서부터 Count개만 유효하다. 공격하는 쪽(DiceCombat·HeroAttack)이 그대로 훑어 대상을 고른다.</summary>
    public MonsterState[] States => _states;

    public GridRouteField Route => _route;

    /// <param name="outline">툰 아웃라인 머티리얼. 없으면 null.</param>
    public MonsterField(Transform parent, SO_MonsterConfig config, SO_MapViewConfig viewConfig, Material outline)
    {
        _config = config;
        _viewConfig = viewConfig;
        _states = new MonsterState[config.InitialCapacity];
        _renderer = new MonsterRenderer(parent, config, viewConfig, config.InitialCapacity, outline);
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnGameUpdate -= Tick;
        Clear();
    }

    /// <summary>새 길(새 맵·주사위 배치·반지름 변경). 걷던 몬스터는 지금 자리에서 경유점을 다시 정한다(갇힘도 이때 다시 본다).</summary>
    public void SetRoute(MapData map, GridRouteField route)
    {
        _map = map;
        _route = route;
        for (int i = 0; i < _count; i++) MonsterMovement.Retarget(ref _states[i], route);
    }

    /// <summary>
    /// gate번째 출현 차례(MapData.SpawnGates)의 포켓에 세운다. 차례 수보다 크면 처음부터 다시 센다(웨이브가 돌아가며 쓴다).
    /// 차례는 주사위 하나당 한 칸이라, 같은 눈이 겹친 포켓에서 그만큼 더 나온다.
    /// healthScale은 웨이브에 따라 기본 체력에 곱하는 값이다(특성의 체력 배율은 모양이 따로 곱한다).
    /// </summary>
    public void Spawn(MonsterKind kind, int gate, float healthScale)
    {
        if (_map == null || _route == null || _map.SpawnGates.Count == 0) return;
        Vector2Int cell = _map.SpawnGates[gate % _map.SpawnGates.Count];
        Vector3? origin = SpawnOrigin != null ? SpawnOrigin(cell) : (Vector3?)null;
        Add(kind, cell, SpawnHealth(kind, healthScale), kind.Base.Health * healthScale, origin);
    }

    /// <summary>
    /// 보스를 보스 포켓(MapData.BossPocket)에 세운다. 보스 포켓이 없으면(옛 맵) 첫 출현 차례.
    /// waveHealthScale은 그 웨이브 일반 몬스터의 체력 배율 — 방어력은 보스 자기 체력이 아니라 이것을 따른다(MonsterState.WaveHealth).
    /// </summary>
    public void SpawnBoss(MonsterKind kind, float healthScale, float waveHealthScale)
    {
        if (_map == null || _route == null || _map.SpawnGates.Count == 0) return;
        Vector2Int cell = _map.InBounds(_map.BossPocket.x, _map.BossPocket.y) ? _map.BossPocket : _map.SpawnGates[0];
        Vector3? origin = SpawnOrigin != null ? SpawnOrigin(cell) : (Vector3?)null;
        Add(kind, cell, SpawnHealth(kind, healthScale), kind.Base.Health * waveHealthScale, origin);
    }

    // 체력은 double로 곱한다 — 후반 보스는 수십억이라 float(유효 7자리)로 곱하면 끝자리가 뭉개진 채 나온다.
    private static double SpawnHealth(MonsterKind kind, float healthScale) =>
        Math.Max(1d, Math.Round((double)kind.Base.Health * kind.HealthScale * healthScale));

    /// <summary>
    /// 출현 구역 칸 → 몬스터가 솟아 나오는 표시상의 자리(맵 로컬, 포켓 구멍 가운데). 테이블 모양을 아는 쪽(EnvironmentView)이 정한다.
    /// 비우면 나오는 연출 없이 칸에 바로 나타난다.
    /// </summary>
    public Func<Vector2Int, Vector3> SpawnOrigin { get; set; }

    private void Add(MonsterKind kind, Vector2 position, double health, float waveHealth, Vector3? origin = null)
    {
        int kindIndex = _kinds.IndexOf(kind);
        if (kindIndex < 0)
        {
            kindIndex = _kinds.Count;
            _kinds.Add(kind);
        }
        if (_count == _states.Length) Array.Resize(ref _states, _states.Length * 2);

        MonsterState state = new MonsterState
        {
            TypeIndex = kindIndex,
            Id = _nextId++,
            Position = position,
            Health = health,
            MaxHealth = health,
            WaveHealth = waveHealth,
        };
        MonsterMovement.Retarget(ref state, _route);
        _states[_count] = state;
        _renderer.Add(_count, kind, state, origin);
        _count++;
    }

    /// <summary>
    /// slot의 몬스터에 피해를 준다. damage는 치명타 등 공격하는 쪽 배율을 곱한 값이고, 방어력은 여기서 뺀다
    /// — 주사위·영웅·효과 피해가 모두 같은 공식(MonsterDamage)을 지나게. 죽어도 바로 지우지 않는다(MonsterState.Health 참고).
    /// </summary>
    public void Hit(int slot, float damage, DamageSource source, float penetration = 0f, bool critical = false)
    {
        ApplyDamage(slot, damage, penetration, critical ? DamageKind.Critical : DamageKind.Normal, source);
    }

    /// <summary>화상을 건다(초당 최대 체력의 ratio). 보스는 화상 피해도 다른 피해처럼 피해 저항(SO_MonsterTrait.DamageResist)으로 줄여 받는다.</summary>
    public void Burn(int slot, float ratio, float duration) =>
        MonsterStatus.Burn(ref _states[slot], ratio, duration);

    public void Vulnerable(int slot, float ratio, float duration) =>
        MonsterStatus.Vulnerable(ref _states[slot], ratio, duration);

    // 이동 방해 저항(거대)은 거는 쪽이 아니라 여기서 줄인다 — 주사위·강화 카드가 몬스터 특성을 몰라도 되게.
    public void Freeze(int slot, float duration, float immunity) =>
        MonsterStatus.Freeze(ref _states[slot], duration * KindOf(slot).ImpairScale, immunity);

    /// <summary>멈춤(스킬 체크) — 얼림 면역을 무시한다. 저항은 얼림과 같이 여기서 줄인다.</summary>
    public void Hold(int slot, float duration) =>
        MonsterStatus.Hold(ref _states[slot], duration * KindOf(slot).ImpairScale);

    public void Slow(int slot, float ratio, float duration) =>
        MonsterStatus.Slow(ref _states[slot], ratio * KindOf(slot).ImpairScale, duration);

    /// <summary>
    /// 처치한다(포켓 스킬, 기획서 8). 남은 체력만큼의 피해 숫자를 띄우고, 치우기·경험치는 다른 죽음과 같이 다음 갱신의 RemoveDead가 한다.
    /// 몸체는 스킬 처치 연출로 포켓에 들어간다(SinkBySkill).
    /// </summary>
    public void Execute(int slot)
    {
        ref MonsterState state = ref _states[slot];
        if (!state.IsAlive) return;
        double dealt = Math.Ceiling(state.Health);
        DamageDealt?.Invoke(DamageSource.Skill, state.Health);
        state.Health = 0d;
        SinkBySkill(slot);
        Damaged?.Invoke(state.Position, dealt, DamageKind.Critical);
    }

    /// <summary>
    /// 스킬로 죽은 몬스터의 몸체를 가장 가까운 포켓으로 날려 보낸다(표시만 — 치우기는 RemoveDead). 살아 있으면 아무 일도 없다.
    /// 보스는 자기 연출(BossFly)이 따로 있어 건드리지 않는다.
    /// </summary>
    public void SinkBySkill(int slot)
    {
        ref MonsterState state = ref _states[slot];
        if (state.IsAlive || KindOf(slot).IsBoss || _config.SkillFlyDuration <= 0f) return;
        _renderer.Sink(slot, NearestPocketHole(state.Position), _config.SkillFlyDuration, _config.SkillFlyHeight, _config.SkillShrinkPortion);
    }

    /// <summary>
    /// 몬스터를 position으로 옮긴다(랙·백스핀 스킬). 걷던 경유점은 옛 자리 기준이므로 새 자리에서 다시 정한다.
    /// 옮길 자리가 설 수 있는 칸인지는 부르는 쪽(SkillRules)이 본다.
    /// 판정은 바로 새 자리에서 하고, 몸체는 slideDuration 동안 옛 자리에서 미끄러져 온다(표시만 — MonsterRenderer.Slide).
    /// </summary>
    public void Displace(int slot, Vector2 position, float slideDuration, bool accelerate)
    {
        if (_route == null) return;
        _renderer.Slide(slot, position, slideDuration, accelerate);
        _states[slot].Position = position;
        MonsterMovement.Retarget(ref _states[slot], _route);
    }

    /// <summary>position에서 가장 가까운 포켓 구멍 가운데(맵 로컬). 닫힌 포켓도 구멍은 있다. 구멍 자리를 모르면(SpawnOrigin 없음) 포켓 칸.</summary>
    public Vector3 NearestPocketHole(Vector2 position)
    {
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        Vector3 best = new Vector3(position.x, ground, position.y);
        if (_map == null) return best;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _map.Pockets.Count; i++)
        {
            Vector2Int cell = _map.Pockets[i];
            Vector3 hole = SpawnOrigin != null ? SpawnOrigin(cell) : new Vector3(cell.x, ground, cell.y);
            float sqr = (new Vector2(hole.x, hole.z) - position).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = hole;
        }
        return best;
    }

    /// <summary>slot 몬스터의 모양(특성). 체력바 크기 등 표시가 쓴다.</summary>
    public MonsterKind KindOf(int slot) => _kinds[_states[slot].TypeIndex];

    public void Clear()
    {
        _renderer.Clear(_count);
        _count = 0;
    }

    // 게임 시간을 따른다 — 카드를 집는 동안의 감속(게임 속도 50%)을 몬스터도 똑같이 받는다.
    private void Tick(float deltaTime)
    {
        if (_map == null) return;
        RemoveDead();
        if (!Paused) Step(deltaTime);
        _renderer.Sync(_states, _count, deltaTime);
    }

    private void Step(float deltaTime)
    {
        RectInt goal = _map.GoalRect;
        // 뒤에서부터 돌아야 지운 자리에 옮겨 온 마지막 칸(이미 움직인 칸)을 다시 움직이지 않는다.
        for (int i = _count - 1; i >= 0; i--)
        {
            ref MonsterState state = ref _states[i];
            float burn = MonsterStatus.Tick(ref state, deltaTime, _config.BurnTickInterval);
            // 화상은 화상 칩(ChipEffects)과 핫 핸드 카드(SO_HeroDebuffCard)가 건다.
            if (burn > 0f) ApplyDamage(i, burn, 0f, DamageKind.Burn, DamageSource.Burn);
            // 화상으로 죽었으면 걷지 않는다 — 죽은 채 목표물에 들어가 피해를 주지 않게(치우기는 다음 갱신의 RemoveDead).
            if (!state.IsAlive) continue;
            // 갇히면 죽는다(기획서 3.5) — 넓은 구역째 막히면 어느 주사위 사거리에도 들지 않아 웨이브가 끝나지 않을 수 있다.
            // 잡은 것으로 쳐서 경험치는 준다(치우기·알림은 다음 갱신의 RemoveDead).
            if (state.Trapped)
            {
                state.Health = 0d;
                continue;
            }

            MonsterKind kind = _kinds[state.TypeIndex];
            float regen = kind.RegenRatio;
            if (regen > 0f)
                state.Health = Math.Min(state.Health + state.MaxHealth * regen * deltaTime, state.MaxHealth);

            float speed = kind.MoveSpeed * MonsterStatus.GetSpeedScale(state) * SpeedScale;
            MonsterMovement.Advance(ref state, speed * deltaTime, _route);
            // 목표물 영역에 발을 들이면 닿은 것으로 본다(길 끝인 목표물 칸 중심까지 걸어 들어가지 않는다).
            if (!goal.Contains(GridCircle.CellOf(state.Position))) continue;

            double health = state.Health;
            RemoveAt(i);
            ReachedGoal?.Invoke(kind, health);
            // 이번 피해로 게임 오버가 되면 남은 몬스터는 이번 프레임부터 멈춘다.
            if (Paused) return;
        }
    }

    // 피해 종류는 숫자 색에 쓴다. 치명타·화상은 그대로 두고, 일반 피해만 방어력에 깎였으면 깎임으로 바꾼다.
    // 화상은 최대 체력 비율이라 방어력을 받지 않는다(기획서 7). 방어력은 취약으로 깎인 만큼 빼고 본다.
    /// <summary>
    /// slot에 damage를 맞히면 깎일 체력(Hit과 같은 공식, 상태는 바꾸지 않는다). 스킬 미리보기가 놓기 전에 결과를 보여 줄 때 쓴다(포켓 처치 예상).
    /// </summary>
    public double EstimateHit(int slot, float damage, float penetration = 0f) =>
        Math.Max(1d, Math.Round((double)RawDamage(slot, damage, penetration, DamageKind.Normal, out _)));

    // 방어력·취약·피해 저항을 지난 피해(반올림 전). damage는 취약을 곱한 값으로 돌려준다(작아진 피해 표시 판단용).
    private float RawDamage(int slot, float damage, float penetration, DamageKind kind, out float boosted)
    {
        ref MonsterState state = ref _states[slot];
        // 방깍(취약)은 받는 피해 배율이라 치명타처럼 방어력보다 먼저 곱한다(MonsterDamage). 화상에도 붙는다 — "모든 공격".
        if (state.IsVulnerable) damage *= 1f + state.VulnerableRatio;
        boosted = damage;
        MonsterKind target = _kinds[state.TypeIndex];
        float raw = kind == DamageKind.Burn ? damage : MonsterDamage.Apply(damage, target.GetArmor(state.WaveHealth), penetration, _config.MinDamageRatio);
        // 피해 저항(거대)은 출처를 가리지 않는다 — 주사위·영웅·스킬·화상이 몬스터 특성을 몰라도 되게 받는 쪽에서 줄인다.
        // 방어력을 뺀 뒤에 줄인다: 먼저 줄이면 작아진 피해에서 방어력을 빼 단단한 보스(5웨이브 거대+단단함)가 의도(체력 1/10과 맞바꿈)보다 몇 배 단단해졌다.
        return raw * target.DamageScale;
    }

    private void ApplyDamage(int slot, float damage, float penetration, DamageKind kind, DamageSource source)
    {
        ref MonsterState state = ref _states[slot];
        float raw = RawDamage(slot, damage, penetration, kind, out damage);

        // 피해는 정수로 들어간다 — 화면 숫자와 실제로 깎인 체력이 같게. 맞힌 피해는 반올림하고 최소 1(맞혔는데 0이면 안 맞은 것처럼 보인다).
        // 화상은 한 번에 1이 안 될 만큼 작게 자주 들어오므로 나머지를 모아 1이 넘을 때 준다.
        // 정수 값을 double로 든다 — int로 바꾸면 21억을 넘는 한 방(후반 최대 체력 비율 피해)이 음수가 되어 오히려 체력을 채웠다.
        double dealt;
        if (kind == DamageKind.Burn)
        {
            float total = raw + state.BurnCarry;
            dealt = Math.Floor((double)total);
            state.BurnCarry = (float)(total - dealt);
            if (dealt <= 0d) return;
        }
        else dealt = Math.Max(1d, Math.Round((double)raw));
        // 이미 죽은 몬스터(치우기 전)를 더 때린 몫은 0이다.
        DamageDealt?.Invoke(source, Math.Min(dealt, Math.Max(state.Health, 0d)));
        state.Health -= dealt;

        if (Damaged == null) return;
        if (kind == DamageKind.Normal && raw < damage) kind = DamageKind.Reduced;
        Damaged(state.Position, dealt, kind);
    }

    // 걷기 전에 치워야 지난 프레임에 죽은 몬스터가 목표물에 들어가 피해를 주지 않는다.
    // 분열 조각은 배열 끝에 붙으므로, 뒤에서부터 도는 이 루프가 이번에 다시 보지 않는다.
    private void RemoveDead()
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            if (_states[i].IsAlive) continue;
            MonsterState dead = _states[i];
            MonsterKind kind = _kinds[dead.TypeIndex];
            // 죽는 연출: 스킬 처치(SinkBySkill)가 아니면 보스는 포켓으로 날아가고, 나머지는 제자리에서 톡 사라진다.
            if (!_renderer.IsLeaving(i))
            {
                if (kind.IsBoss) _renderer.Sink(i, NearestPocketHole(dead.Position), _config.BossFlyDuration, _config.BossFlyHeight, _config.BossShrinkPortion);
                else _renderer.Pop(i, _config.DeathPopDuration, _config.DeathPopHop);
            }
            RemoveAt(i);
            Split(kind, dead);
            Killed?.Invoke(kind, dead.Position);
        }
    }

    // 죽은 자리에서 걷던 방향 앞뒤로 벌려 세운다. 조각은 나뉘는 특성이 빠진 모양이라 다시 나뉘지 않는다.
    private void Split(MonsterKind kind, in MonsterState dead)
    {
        SO_MonsterTrait split = kind.SplitTrait;
        // 갇혀 죽었으면 나뉘지 않는다 — 조각도 같은 자리에 갇혀 곧바로 죽는다.
        if (split == null || dead.Trapped) return;

        MonsterKind piece = kind.GetPiece();
        double health = Math.Max(1d, Math.Round(dead.MaxHealth * split.SplitHealthRatio));
        Vector2 direction = dead.Heading.sqrMagnitude > 0f ? dead.Heading.normalized : Vector2.zero;
        for (int i = 0; i < split.SplitCount; i++)
        {
            // 가운데를 0으로 두고 고르게 벌린다(2마리면 −0.5, +0.5).
            float offset = split.SplitCount > 1 ? i / (split.SplitCount - 1f) - 0.5f : 0f;
            Add(piece, dead.Position + direction * (offset * 2f * _config.SplitOffset), health, dead.WaveHealth);
        }
    }

    private void RemoveAt(int index)
    {
        int last = _count - 1;
        _states[index] = _states[last];
        _renderer.RemoveAt(index, last);
        _count--;
    }
}
