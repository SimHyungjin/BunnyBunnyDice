using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 진행. 웨이브의 몬스터를 모두 내보내고 맵의 몬스터가 다 죽으면 쉬는 시간(WaveDelay) 뒤 다음 웨이브를 시작한다
/// — 시간으로만 넘기면 못 잡은 몬스터 위에 다음 웨이브가 겹쳐 쌓인다. 몬스터는 하나씩 내보내 달라고 알린다.
/// 어느 출현 구역에 어떻게 세울지는 받는 쪽(MonsterField)이 정한다 — 여기서는 몇 번째로 나오는지만 넘긴다.
/// 어떤 특성 조합이 나올지는 WaveComposition이 정하고, 여기서는 조합을 몬스터 모양(MonsterKind)으로 바꿔 줄 세운다.
/// 보스는 일반 몬스터 뒤에 보스 포켓(MapData.BossPocket)에서 나오고, 체력은 그 웨이브 일반 몬스터 체력 합의 몫이다(기획서 11.3).
/// 보스는 확률로 잭팟 보스가 된다 — 보스 웨이브가 시작될 때 정해 알리고(JackpotWarned), 보스가 나오면 끝을 알린다.
/// </summary>
public sealed class WaveSpawner : IDisposable
{
    private readonly SO_WaveConfig _config;
    private readonly Action<MonsterKind, int, float> _spawn;
    private readonly Action<MonsterKind, float> _spawnBoss;
    // 맵에 남은 몬스터 수. 0이 되어야 쉬는 시간을 센다(갇힌 몬스터는 MonsterField가 곧바로 죽여 기다리게 하지 않는다).
    private readonly Func<int> _getAliveCount;
    // 같은 조합은 같은 모양을 쓴다 — 표시가 모양별로 오브젝트를 재사용한다. 판 내내 가짓수가 수십을 넘지 않아 목록을 훑는다.
    private readonly List<MonsterKind> _kinds = new List<MonsterKind>();
    private readonly List<List<SO_MonsterTrait>> _regular = new List<List<SO_MonsterTrait>>();
    private readonly List<SO_MonsterTrait> _boss = new List<SO_MonsterTrait>();
    private readonly List<MonsterKind> _waveKinds = new List<MonsterKind>();
    private readonly List<SO_MonsterTrait> _bossTraits = new List<SO_MonsterTrait>();
    // 앞 웨이브가 다 나오기 전에 다음 웨이브가 시작되면(테스트 키) 뒤에 이어 붙인다.
    // 체력은 나오는 시점이 아니라 속한 웨이브로 정하므로 줄 세울 때 체력 배율을 같이 든다.
    private readonly Queue<(MonsterKind kind, float healthScale)> _pending = new Queue<(MonsterKind, float)>();
    private float _waveTimer;
    private float _spawnTimer;
    private int _spawnCount;
    private bool _running;
    // 잭팟 보스 예고 중인지(보스 포켓이 금색). 다음 보스를 잭팟으로 만들지(테스트 키).
    private bool _jackpotWarned;
    private bool _forceJackpot;
    // 이번 웨이브의 정리를 알렸는지. 쉬는 시간 동안 매 프레임 알리지 않게.
    private bool _clearReported = true;

    public int Wave { get; private set; }

    /// <summary>이번 웨이브의 일반 몬스터 모양(번갈아 나오는 순서). 측정 기록이 웨이브마다 어떤 특성이 나왔는지 남긴다.</summary>
    public IReadOnlyList<MonsterKind> WaveKinds => _waveKinds;

    /// <summary>이번 웨이브의 보스 모양. 보스가 없는 웨이브면 null.</summary>
    public MonsterKind WaveBoss { get; private set; }

    public event Action<int> WaveStarted;

    /// <summary>
    /// 웨이브를 다 잡았다(다 나왔고 맵에 남은 적이 없다 — 쉬는 시간이 시작된다): 웨이브 번호. 웨이브마다 한 번.
    /// 측정 기록(WaveLog)이 정리 시간을 잰다. 다 잡기 전에 다음 웨이브로 넘어가면(건너뛰기) 오지 않는다.
    /// </summary>
    public event Action<int> WaveCleared;

    /// <summary>이번 보스가 잭팟 보스다(보스 웨이브 시작). 보스 포켓을 금색으로 바꾼다.</summary>
    public event Action JackpotWarned;

    /// <summary>잭팟 예고가 끝났다(보스가 나왔거나 취소). 보스 포켓 색을 되돌린다.</summary>
    public event Action JackpotWarningEnded;

    /// <param name="spawn">몬스터 모양, 이번 판에서 몇 번째로 나오는지(0부터), 체력 배율.</param>
    /// <param name="spawnBoss">보스 모양, 체력 배율 — 보스 포켓에서 나온다.</param>
    /// <param name="getAliveCount">맵에 남은 몬스터 수.</param>
    public WaveSpawner(SO_WaveConfig config, Action<MonsterKind, int, float> spawn, Action<MonsterKind, float> spawnBoss, Func<int> getAliveCount)
    {
        _config = config;
        _spawn = spawn;
        _spawnBoss = spawnBoss;
        _getAliveCount = getAliveCount;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    /// <summary>
    /// 처음 상태로 되돌리고 멈춘다(새 판). 시작은 Begin으로 따로 한다
    /// — 판 주사위 연출 동안 멈춰 두는 사이에도 HUD가 이전 판 웨이브가 아니라 0을 보여야 한다.
    /// </summary>
    public void Reset()
    {
        Wave = 0;
        _pending.Clear();
        CancelJackpot();
        _clearReported = true;
        _spawnCount = 0;
        _waveTimer = _config.WaveDelay;
        _spawnTimer = 0f;
        _running = false;
    }

    /// <summary>Reset한 상태에서 웨이브 진행을 시작한다.</summary>
    public void Begin() => _running = true;

    /// <summary>더 내보내지 않는다(게임 오버).</summary>
    public void Stop() => _running = false;

    /// <summary>
    /// count 웨이브 뒤로 건너뛰어 바로 시작한다(테스트용). 아직 나오지 않은 적은 버린다
    /// — 대기열 뒤에 쌓기만 하면 웨이브 숫자만 넘어가고 실제로는 앞 웨이브의 약한 적이 한참 나와, 높은 웨이브의 강도를 볼 수 없다.
    /// 맵에 남은 적은 부르는 쪽이 치운다.
    /// </summary>
    public void SkipWaves(int count)
    {
        if (!_running || count < 1) return;
        _pending.Clear();
        CancelJackpot();
        Wave += count - 1;
        StartWave();
        _waveTimer = _config.WaveDelay;
    }

    // 게임 시간을 따른다 — 카드를 집는 동안 몬스터가 느려지면 나오는 간격도 같이 늘어야 몰리지 않는다.
    private void Tick(float deltaTime)
    {
        if (!_running) return;

        if (_pending.Count == 0)
        {
            // 남은 몬스터가 있는 동안은 쉬는 시간을 처음으로 되돌려 둔다 — 마지막 한 마리를 잡은 순간부터 센다.
            if (_getAliveCount() > 0)
            {
                _waveTimer = _config.WaveDelay;
                return;
            }
            if (!_clearReported)
            {
                _clearReported = true;
                WaveCleared?.Invoke(Wave);
            }
            _waveTimer -= deltaTime;
            if (_waveTimer > 0f) return;
            StartWave();
            _waveTimer = _config.WaveDelay;
            if (_pending.Count == 0) return;
        }

        _spawnTimer -= deltaTime;
        while (_pending.Count > 0 && _spawnTimer <= 0f)
        {
            var (kind, healthScale) = _pending.Dequeue();
            if (kind.IsBoss)
            {
                if (kind.IsJackpot) CancelJackpot();
                _spawnBoss(kind, healthScale);
            }
            else _spawn(kind, _spawnCount++, healthScale);
            _spawnTimer += _config.SpawnInterval;
        }
    }

    private void StartWave()
    {
        // 쉬던 중에 시작하면 첫 마리를 바로 내보낸다(앞 웨이브의 남은 간격을 기다리지 않게).
        if (_pending.Count == 0) _spawnTimer = 0f;

        Wave++;
        _clearReported = false;
        CancelJackpot();
        WaveBoss = null;
        SO_MonsterType type = _config.Monster;
        if (type != null)
        {
            bool hasBoss = WaveComposition.Compose(_config, Wave, GameRandom.Wave, _regular, _boss);
            _waveKinds.Clear();
            for (int i = 0; i < _regular.Count; i++) _waveKinds.Add(GetKind(type, _regular[i], false));
            // 여러 가지면 번갈아 내보낸다 — 한 가지가 몰려 나오면 그 특성에 맞는 주사위 하나로 다 막힌다.
            int count = _config.GetCount(Wave);
            float healthScale = _config.GetHealthScale(Wave);
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                MonsterKind kind = _waveKinds[i % _waveKinds.Count];
                _pending.Enqueue((kind, healthScale));
                total += type.Health * kind.HealthScale * healthScale;
            }
            if (hasBoss) EnqueueBoss(type, total);
        }
        WaveStarted?.Invoke(Wave);
    }

    /// <summary>다음 보스를 잭팟 보스로 만든다(테스트 키).</summary>
    public void ForceJackpot() => _forceJackpot = true;

    // 보스를 일반 몬스터 뒤에 줄 세운다. 체력은 이번 웨이브 일반 몬스터 체력 합(total)의 몫 — 특성 체력 배율은 나눠 지운다.
    private void EnqueueBoss(SO_MonsterType type, float total)
    {
        bool jackpot = _forceJackpot || GameRandom.Wave.NextDouble() < _config.JackpotChance;
        _forceJackpot = false;
        _bossTraits.Clear();
        _bossTraits.AddRange(_boss);
        if (jackpot)
        {
            for (int i = 0; i < _config.JackpotTraits.Count; i++)
                if (_config.JackpotTraits[i] != null && !_bossTraits.Contains(_config.JackpotTraits[i])) _bossTraits.Add(_config.JackpotTraits[i]);
        }
        MonsterKind boss = GetKind(type, _bossTraits, true, jackpot);
        WaveBoss = boss;
        float healthScale = total * _config.BossHealthShare / Mathf.Max(type.Health * boss.HealthScale, 1f);
        for (int i = 0; i < _config.BossCount; i++) _pending.Enqueue((boss, healthScale));
        if (!jackpot) return;
        _jackpotWarned = true;
        JackpotWarned?.Invoke();
    }

    // 예고 중이었다면 색을 되돌리게 알리고 지운다(보스가 나옴·새 판·웨이브 건너뛰기).
    private void CancelJackpot()
    {
        if (_jackpotWarned) JackpotWarningEnded?.Invoke();
        _jackpotWarned = false;
    }

    private MonsterKind GetKind(SO_MonsterType type, List<SO_MonsterTrait> traits, bool isBoss, bool isJackpot = false)
    {
        for (int i = 0; i < _kinds.Count; i++)
            if (_kinds[i].Matches(type, traits, isBoss, isJackpot)) return _kinds[i];
        // Compose가 돌려준 목록은 다음 웨이브에 다시 채워지므로 복사해 둔다.
        MonsterKind kind = new MonsterKind(type, new List<SO_MonsterTrait>(traits), isBoss, isJackpot);
        _kinds.Add(kind);
        return kind;
    }
}
