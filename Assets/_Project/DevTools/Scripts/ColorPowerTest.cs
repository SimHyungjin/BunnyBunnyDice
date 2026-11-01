using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 색 시험(기획서 6.5 개편 준비, 2026-10-29): 미로 전부를 한 색 d20 눈 20으로 채우고 웨이브가 무너질 때까지 돌려 색마다 순수 딜을 잰다.
/// 다른 변수는 뺀다 — 영웅 공격·레벨업·칩 없음, 목표물 무적. 디버프는 칩으로 걸면 단일 색이 퍼뜨리지 못해 "딜이 약한지, 못 퍼뜨리는지"가 섞이므로
/// 모든 적에게 늘 걸린 셈으로 이동 속도 배율 하나(SO_ColorTestConfig.SpeedScale — 둔화 25% × 얼림 0.7/2초)를 건다(사용자 결정).
/// 미로는 맵 생성 검사의 미로 계획(MapData.MazePlan)이라 색이 바뀌어도 같은 칸이다.
/// 웨이브마다 처리 비율 = 깎은 체력 ÷ (깎은 체력 + 새어 나간 적의 남은 체력). 비율이 StopRatio 아래로 떨어지거나 MaxWave에 닿으면 다음 판.
/// 판 반복·빠른 시뮬은 봇과 같은 길을 쓴다(GameScene). 결과는 RunLogs/color_test_*.csv.
/// Ranges를 채우면 시험 색의 사거리를 그 값으로 바꿔 가며 돈다 — 실행 중 복사본(SO_DiceConfig.Types)만 바꾸고 판이 끝나면 되돌린다.
/// Traits를 채우면 웨이브 설정 복사본의 주기를 그 특성 하나로 바꿔 가며 돈다(판이 끝나면 되돌린다).
/// </summary>
public sealed class ColorPowerTest : IDisposable
{
    private readonly SO_ColorTestConfig _config;
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_WaveConfig _waveConfig;
    private readonly Func<MapData> _getMap;
    private readonly DiceField _diceField;
    private readonly MonsterField _monsters;
    private readonly WaveSpawner _waves;
    private readonly Action _retry;
    private readonly StringBuilder _rows = new StringBuilder();

    private int _run;
    private string _path;
    private float _dealt;
    private float _leakedHealth;
    private int _leaks;
    // 5웨이브마다 나오는 보스만 따로 — 웨이브 처리 비율에는 함께 나온 일반 몹이 섞여 보스를 잡았는지 보이지 않는다.
    // 보스 체력은 처음 본 때의 최대 체력으로 더한다(같은 보스를 두 번 세지 않게 Id로 거른다).
    private readonly HashSet<int> _bossIds = new HashSet<int>();
    private double _bossHealth;
    private double _bossLeaked;
    private float _seconds;
    private bool _hasNextSeed;
    // 다음 판은 다음 프레임에 시작한다 — 웨이브 정리 이벤트 안에서 판을 갈아엎으면 웨이브 갱신이 남은 줄을 새 판 위에서 이어 돈다.
    private bool _pendingRetry;
    private readonly BotBoard _board = new BotBoard();
    private readonly List<Vector2Int> _cells = new List<Vector2Int>();
    // 사거리를 바꿔 둔 종류와 원래 값(판이 끝나면 되돌린다).
    private SO_DiceType _rangeType;
    private float _savedRange;
    // 특성을 바꿔 둔 동안의 원래 웨이브 설정 값.
    private bool _traitApplied;
    private readonly List<SO_MonsterTrait> _savedCycle = new List<SO_MonsterTrait>();
    private int _savedExtra;
    private int _savedTraitMin;
    private int _savedTraitMax;

    /// <summary>도는 중인지. 켜고 끄는 동안 GameScene이 테스트 상태(무적·경험치 막기·영웅 공격·적 이속)와 빠른 시뮬을 건다.</summary>
    public bool Running { get; private set; }

    public event Action<bool> RunningChanged;

    /// <param name="retry">다음 판을 시작한다(판 중이면 다시 하기, 로비면 뛰어들기까지). 맵은 TryTakeSeed로 가져간다.</param>
    public ColorPowerTest(SO_ColorTestConfig config, SO_DiceConfig diceConfig, SO_WaveConfig waveConfig, Func<MapData> getMap, DiceField diceField, MonsterField monsters, WaveSpawner waves, Action retry)
    {
        _config = config;
        _diceConfig = diceConfig;
        _waveConfig = waveConfig;
        _getMap = getMap;
        _diceField = diceField;
        _monsters = monsters;
        _waves = waves;
        _retry = retry;
        _monsters.DamageDealt += CountDamage;
        _monsters.ReachedGoal += CountLeak;
        _waves.WaveStarted += OnWaveStarted;
        _waves.WaveCleared += OnWaveCleared;
        Core.Loop.OnGameUpdate += Tick;
        Core.Loop.OnUpdate += RealTick;
    }

    public void Dispose()
    {
        RestoreRange();
        RestoreTrait();
        _monsters.DamageDealt -= CountDamage;
        _monsters.ReachedGoal -= CountLeak;
        _waves.WaveStarted -= OnWaveStarted;
        _waves.WaveCleared -= OnWaveCleared;
        Core.Loop.OnGameUpdate -= Tick;
        Core.Loop.OnUpdate -= RealTick;
    }

    public void Toggle()
    {
        if (Running)
        {
            Stop();
            return;
        }
        if (_config.Colors.Count == 0 || _config.Seeds.Count == 0)
        {
            Debug.LogWarning("[ColorPowerTest] SO_ColorTestConfig.Colors·Seeds가 비어 있다.");
            return;
        }
        _run = 0;
        _rows.Clear();
        _rows.AppendLine("seed,color,trait,range,wave,seconds,dealt,leaked_health,leaks,ratio,boss_ratio");
        _path = Path.Combine(Application.persistentDataPath, "RunLogs", $"color_test_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        Running = true;
        RunningChanged?.Invoke(true);
        StartRun();
    }

    /// <summary>다음 판의 맵(다시 하기가 한 번 가져간다).</summary>
    public bool TryTakeSeed(out int seed)
    {
        seed = Running ? CurrentSeed : 0;
        bool has = _hasNextSeed;
        _hasNextSeed = false;
        return has;
    }

    /// <summary>판 주사위 연출이 끝나 웨이브를 시작하기 직전(GameScene). 미로 계획 칸마다 시험 색 d20을 놓고 눈을 상한까지 얹는다.</summary>
    public void OnBattleReady()
    {
        if (!Running) return;
        MapData map = _getMap();
        if (map == null) return;
        DiceCard card = new DiceCard(_config.Faces, CurrentColor);
        PickCells(map);
        for (int i = 0; i < _cells.Count; i++)
        {
            Vector2Int cell = _cells[i];
            _diceField.Place(map, cell, card);
            // 얹기는 눈을 더하고 상한을 넘는 몫은 버린다 — 상한까지 얹으면 굴림과 상관없이 눈 20(각성)이다.
            for (int k = 0; k < _config.Faces && _diceField.TryGetPlaced(cell, out PlacedDice placed) && !placed.Dice.IsMaxed; k++)
                _diceField.Stack(cell, card);
        }
        // 길 칸만 남기고 채웠을 때 막혔으면 새는 적이 없어 결과가 거짓으로 좋게 나온다 — 알린다.
        if (!MapPathfinder.AllSpawnsReachable(map, MapPathfinder.BuildDistanceField(map)))
            Debug.LogWarning($"[ColorPowerTest] seed={CurrentSeed} 길이 막혔다(주사위 {_cells.Count}개).");
        ResetWave();
    }

    // 사거리를 돌지 않으면 한 칸(원래 사거리)으로 친다.
    private int RangeCount => Math.Max(_config.Ranges.Count, 1);

    // 주사위를 놓을 칸. 수 제한이 있으면 미로 칸을 모두 벽으로 막아 길을 확정한 뒤, 이번 색의 사거리로 길을 가장 많이 덮는 칸부터 고르고 그 칸만 다시 연다.
    // 벽은 이번 판의 맵에만 남는다(다음 판은 맵을 새로 만든다). 덮는 양이 같으면 미로 계획 차례 — 맵마다 결과가 같게.
    private void PickCells(MapData map)
    {
        _cells.Clear();
        List<Vector2Int> plan = map.MazePlan;
        for (int i = 0; i < plan.Count; i++)
        {
            Vector2Int cell = plan[i];
            if (map.IsBuildable(cell.x, cell.y) && !map.HasDice(cell.x, cell.y) && !map.HasChip(cell.x, cell.y)) _cells.Add(cell);
        }
        if (_config.FillOffPath)
        {
            FillOffPath(map);
            return;
        }
        if (_config.MaxDice <= 0 || _cells.Count <= _config.MaxDice) return;

        for (int i = 0; i < _cells.Count; i++) map.Set(_cells[i].x, _cells[i].y, TileType.Wall);
        if (!_board.Read(map, 0f))
        {
            for (int i = 0; i < _cells.Count; i++) map.Set(_cells[i].x, _cells[i].y, TileType.Ground);
            return;
        }
        SO_DiceType type = _diceConfig.GetDiceType(CurrentColor);
        float range = type != null ? type.Range : 0f;
        List<Vector2Int> picked = new List<Vector2Int>();
        for (int n = 0; n < _config.MaxDice; n++)
        {
            int best = -1;
            int bestTraffic = -1;
            for (int i = 0; i < _cells.Count; i++)
            {
                int traffic = _board.Traffic(_cells[i], range);
                if (traffic <= bestTraffic) continue;
                bestTraffic = traffic;
                best = i;
            }
            picked.Add(_cells[best]);
            _cells.RemoveAt(best);
        }
        _cells.Clear();
        _cells.AddRange(picked);
        for (int i = 0; i < _cells.Count; i++) map.Set(_cells[i].x, _cells[i].y, TileType.Ground);
    }

    // 미로 칸을 벽으로 잠깐 막아 길을 확정하고, 길 칸이 아닌 빈 땅을 모두 더한다. 길 칸은 그대로라 미로 모양이 같다.
    private void FillOffPath(MapData map)
    {
        for (int i = 0; i < _cells.Count; i++) map.Set(_cells[i].x, _cells[i].y, TileType.Wall);
        bool read = _board.Read(map, 0f);
        for (int i = 0; i < _cells.Count; i++) map.Set(_cells[i].x, _cells[i].y, TileType.Ground);
        if (!read) return;
        HashSet<Vector2Int> route = new HashSet<Vector2Int>();
        for (int i = 0; i < _board.Route.Count; i++) route.Add(_board.Route[i]);
        HashSet<Vector2Int> maze = new HashSet<Vector2Int>(_cells);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (route.Contains(cell) || maze.Contains(cell)) continue;
                if (map.IsBuildable(x, y) && !map.HasDice(x, y) && !map.HasChip(x, y)) _cells.Add(cell);
            }
        }
    }

    private int TraitCount => Math.Max(_config.Traits.Count, 1);

    private int CurrentSeed => _config.Seeds[(_run / (_config.Colors.Count * TraitCount * RangeCount)) % _config.Seeds.Count];

    // 같은 맵에서 색(과 특성·사거리)을 다 돈 뒤 다음 맵 — 맵마다 색끼리 짝지어 본다.
    private DiceColor CurrentColor => _config.Colors[(_run / (TraitCount * RangeCount)) % _config.Colors.Count];

    private SO_MonsterTrait CurrentTrait => _config.Traits.Count > 0 ? _config.Traits[(_run / RangeCount) % _config.Traits.Count] : null;

    private int TotalRuns => _config.Colors.Count * _config.Seeds.Count * TraitCount * RangeCount;

    private void StartRun()
    {
        ApplyRange();
        ApplyTrait();
        _hasNextSeed = true;
        ResetWave();
        _pendingRetry = true;
    }

    private void Stop()
    {
        RestoreRange();
        RestoreTrait();
        Running = false;
        _hasNextSeed = false;
        _pendingRetry = false;
        RunningChanged?.Invoke(false);
    }

    // 이번 판 색의 사거리를 시험 값으로 바꾼다. 앞 판에서 바꾼 것은 먼저 되돌린다(색이 바뀌었을 수 있다).
    private void ApplyRange()
    {
        RestoreRange();
        if (_config.Ranges.Count == 0) return;
        _rangeType = _diceConfig.GetDiceType(CurrentColor);
        if (_rangeType == null) return;
        _savedRange = _rangeType.Range;
        _rangeType.Range = _config.Ranges[_run % _config.Ranges.Count];
    }

    private void RestoreRange()
    {
        if (_rangeType == null) return;
        _rangeType.Range = _savedRange;
        _rangeType = null;
    }

    // 이번 판의 특성 하나로 주기를 바꾼다. 빈 칸이면 특성 없는 몬스터만(무작위 규칙에 특성 0개).
    private void ApplyTrait()
    {
        RestoreTrait();
        if (_config.Traits.Count == 0) return;
        _savedCycle.Clear();
        _savedCycle.AddRange(_waveConfig.CycleTraits);
        _savedExtra = _waveConfig.CycleExtraTraits;
        _savedTraitMin = _waveConfig.RandomTraitMin;
        _savedTraitMax = _waveConfig.RandomTraitMax;
        _traitApplied = true;
        SO_MonsterTrait trait = CurrentTrait;
        _waveConfig.CycleTraits.Clear();
        if (trait != null) _waveConfig.CycleTraits.Add(trait);
        _waveConfig.CycleExtraTraits = 0;
        _waveConfig.RandomTraitMin = 0;
        _waveConfig.RandomTraitMax = 0;
    }

    private void RestoreTrait()
    {
        if (!_traitApplied) return;
        _waveConfig.CycleTraits.Clear();
        _waveConfig.CycleTraits.AddRange(_savedCycle);
        _waveConfig.CycleExtraTraits = _savedExtra;
        _waveConfig.RandomTraitMin = _savedTraitMin;
        _waveConfig.RandomTraitMax = _savedTraitMax;
        _traitApplied = false;
    }

    private void ResetWave()
    {
        _dealt = 0f;
        _leakedHealth = 0f;
        _leaks = 0;
        _seconds = 0f;
        _bossIds.Clear();
        _bossHealth = 0d;
        _bossLeaked = 0d;
    }

    private void RealTick(float deltaTime)
    {
        if (!_pendingRetry) return;
        _pendingRetry = false;
        _retry();
    }

    private void Tick(float deltaTime)
    {
        if (!Running) return;
        _seconds += deltaTime;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || _bossIds.Contains(states[i].Id)) continue;
            MonsterKind kind = _monsters.KindOf(i);
            if (!kind.IsBoss) continue;
            _bossIds.Add(states[i].Id);
            _bossHealth += states[i].MaxHealth;
        }
    }

    private void CountDamage(DamageSource source, double amount)
    {
        if (Running) _dealt += (float)amount;
    }

    private void CountLeak(MonsterKind kind, double health)
    {
        if (!Running) return;
        _leakedHealth += (float)health;
        _leaks++;
        if (kind.IsBoss) _bossLeaked += health;
    }

    private void OnWaveStarted(int wave)
    {
        if (Running) _seconds = 0f;
    }

    private void OnWaveCleared(int wave)
    {
        if (!Running) return;
        float total = _dealt + _leakedHealth;
        float ratio = total > 0f ? _dealt / total : 1f;
        _rows.Append(CurrentSeed).Append(',')
            .Append(ColorName(CurrentColor)).Append(',')
            .Append(CurrentTrait != null ? CurrentTrait.name.Replace("SO_MonsterTrait_", "") : (_config.Traits.Count > 0 ? "None" : "")).Append(',')
            .Append(CurrentRange().ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
            .Append(wave).Append(',')
            .Append(_seconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
            .Append(_dealt.ToString("0", CultureInfo.InvariantCulture)).Append(',')
            .Append(_leakedHealth.ToString("0", CultureInfo.InvariantCulture)).Append(',')
            .Append(_leaks).Append(',')
            .Append(ratio.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
            // 보스가 없는 웨이브는 비운다.
            .AppendLine(_bossHealth > 0d ? (1d - _bossLeaked / _bossHealth).ToString("0.0000", CultureInfo.InvariantCulture) : "");
        ResetWave();
        if (ratio >= _config.StopRatio && wave < _config.MaxWave) return;

        Write();
        Debug.Log($"[ColorPowerTest] {_run + 1}/{TotalRuns} seed={CurrentSeed} {ColorName(CurrentColor)} 사거리 {CurrentRange():0.00} 끝 — wave {wave}, 처리 {ratio:P0}");
        _run++;
        if (_run >= TotalRuns)
        {
            Debug.Log($"[ColorPowerTest] 끝: {_path}");
            Stop();
            return;
        }
        StartRun();
    }

    private float CurrentRange()
    {
        SO_DiceType type = _diceConfig.GetDiceType(CurrentColor);
        return type != null ? type.Range : 0f;
    }

    private void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path));
        File.WriteAllText(_path, _rows.ToString(), new UTF8Encoding(true));
    }

    private static string ColorName(DiceColor colors)
    {
        switch (DiceColors.Normalize(colors))
        {
            case DiceColor.Red: return "Red";
            case DiceColor.Green: return "Green";
            case DiceColor.Blue: return "Blue";
            case DiceColor.Red | DiceColor.Green: return "Yellow";
            case DiceColor.Red | DiceColor.Blue: return "Magenta";
            case DiceColor.Green | DiceColor.Blue: return "Cyan";
            case DiceColors.All: return "White";
            default: return "Black";
        }
    }
}
