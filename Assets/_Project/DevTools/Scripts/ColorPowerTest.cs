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
/// </summary>
public sealed class ColorPowerTest : IDisposable
{
    private readonly SO_ColorTestConfig _config;
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
    private float _seconds;
    private bool _hasNextSeed;
    // 다음 판은 다음 프레임에 시작한다 — 웨이브 정리 이벤트 안에서 판을 갈아엎으면 웨이브 갱신이 남은 줄을 새 판 위에서 이어 돈다.
    private bool _pendingRetry;

    /// <summary>도는 중인지. 켜고 끄는 동안 GameScene이 테스트 상태(무적·경험치 막기·영웅 공격·적 이속)와 빠른 시뮬을 건다.</summary>
    public bool Running { get; private set; }

    public event Action<bool> RunningChanged;

    /// <param name="retry">다음 판을 시작한다(판 중이면 다시 하기, 로비면 뛰어들기까지). 맵은 TryTakeSeed로 가져간다.</param>
    public ColorPowerTest(SO_ColorTestConfig config, Func<MapData> getMap, DiceField diceField, MonsterField monsters, WaveSpawner waves, Action retry)
    {
        _config = config;
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
        _rows.AppendLine("seed,color,wave,seconds,dealt,leaked_health,leaks,ratio");
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
        List<Vector2Int> plan = map.MazePlan;
        for (int i = 0; i < plan.Count; i++)
        {
            Vector2Int cell = plan[i];
            if (!map.IsBuildable(cell.x, cell.y) || map.HasDice(cell.x, cell.y) || map.HasChip(cell.x, cell.y)) continue;
            _diceField.Place(map, cell, card);
            // 얹기는 눈을 더하고 상한을 넘는 몫은 버린다 — 상한까지 얹으면 굴림과 상관없이 눈 20(각성)이다.
            for (int k = 0; k < _config.Faces && _diceField.TryGetPlaced(cell, out PlacedDice placed) && !placed.Dice.IsMaxed; k++)
                _diceField.Stack(cell, card);
        }
        ResetWave();
    }

    private int CurrentSeed => _config.Seeds[(_run / _config.Colors.Count) % _config.Seeds.Count];

    // 같은 맵에서 색을 다 돈 뒤 다음 맵 — 맵마다 색끼리 짝지어 본다.
    private DiceColor CurrentColor => _config.Colors[_run % _config.Colors.Count];

    private int TotalRuns => _config.Colors.Count * _config.Seeds.Count;

    private void StartRun()
    {
        _hasNextSeed = true;
        ResetWave();
        _pendingRetry = true;
    }

    private void Stop()
    {
        Running = false;
        _hasNextSeed = false;
        _pendingRetry = false;
        RunningChanged?.Invoke(false);
    }

    private void ResetWave()
    {
        _dealt = 0f;
        _leakedHealth = 0f;
        _leaks = 0;
        _seconds = 0f;
    }

    private void RealTick(float deltaTime)
    {
        if (!_pendingRetry) return;
        _pendingRetry = false;
        _retry();
    }

    private void Tick(float deltaTime)
    {
        if (Running) _seconds += deltaTime;
    }

    private void CountDamage(DamageSource source, float amount)
    {
        if (Running) _dealt += amount;
    }

    private void CountLeak(MonsterKind kind, float health)
    {
        if (!Running) return;
        _leakedHealth += health;
        _leaks++;
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
            .Append(wave).Append(',')
            .Append(_seconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
            .Append(_dealt.ToString("0", CultureInfo.InvariantCulture)).Append(',')
            .Append(_leakedHealth.ToString("0", CultureInfo.InvariantCulture)).Append(',')
            .Append(_leaks).Append(',')
            .AppendLine(ratio.ToString("0.0000", CultureInfo.InvariantCulture));
        ResetWave();
        if (ratio >= _config.StopRatio && wave < _config.MaxWave) return;

        Write();
        Debug.Log($"[ColorPowerTest] {_run + 1}/{TotalRuns} seed={CurrentSeed} {ColorName(CurrentColor)} 끝 — wave {wave}, 처리 {ratio:P0}");
        _run++;
        if (_run >= TotalRuns)
        {
            Debug.Log($"[ColorPowerTest] 끝: {_path}");
            Stop();
            return;
        }
        StartRun();
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
