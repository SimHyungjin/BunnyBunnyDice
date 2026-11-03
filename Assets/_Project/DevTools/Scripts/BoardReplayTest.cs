using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 배치 다시 돌리기(테스트 키 Shift+T, 2026-11-03 사용자): 사람이 한 판의 기록(PlayLog, RunLogs/play_*.csv)에서 웨이브마다의 판을 다시 세우고,
/// 판의 주사위 색만 바꿔 가며(원래 → 빨강 → … → 하양) 같은 웨이브를 돌려 "다 하양이었다면 얼마나 깎고 얼마나 다가왔나"를 잰다.
/// 색 시험(ColorPowerTest)은 미로 계획 칸을 한 색으로 채우지만, 이것은 사람이 실제로 쌓은 칸·면체·눈·칩을 그대로 쓴다.
///
/// 판 다시 세우기: 맵은 기록의 시드로 다시 만든다(같은 시드 = 같은 맵·시작 칩). 칩만 있는 칸(벽)은 기록에 판째로 남지 않아
/// 시작 칩에 칩 놓기·캐시 아웃을 차례대로 다시 해서 맞추고, 주사위 칸은 웨이브 시작 때 찍힌 판(wave_start)으로 놓는다.
/// 웨이브 구성(특성·잭팟)은 WaveSpawner.JumpTo가 건너뛰는 웨이브의 난수까지 써서 실제 판과 같게 한다.
/// 목표물은 무적·경험치 막기(새도 끝까지 잰다). 영웅은 기록된 자리에서 그때까지 고른 영웅 카드로 공격만 한다(올라타지 않는다).
/// 결과는 RunLogs/board_replay_*.csv — 웨이브마다 깎은 체력, 샌 수·체력, 가장 깊이 들어온 적의 길 진행률, 평균 진행률, 보스 진행률·남은 체력, 출처별 피해.
/// </summary>
public sealed class BoardReplayTest : IDisposable
{
    // 기록에서 판을 바꾸는 일 하나(차례대로 다시 한다).
    private struct BoardEvent
    {
        public float Time;
        public bool CashOut;
        public Vector2Int Cell;
        public string Chip;
        public float Strength;
    }

    private struct WaveBoard
    {
        public float Time;
        public string Board;
        public Vector2Int? Hero;
    }

    private struct HeroPick
    {
        public float Time;
        public string Card;
        public int Count;
    }

    private struct StartChip
    {
        public Vector2Int Cell;
        public SO_ChipCard Card;
        public float Strength;
    }

    // 웨이브 동안 몬스터 하나가 길을 얼마나 왔는지(목표물까지 걷는 거리 — 처음 본 때와 가장 가까웠던 때).
    private struct Track
    {
        public float Start;
        public float Min;
        public bool Boss;
        public double MaxHealth;
    }

    private const string CashOutCard = "SO_SkillCard_CashOut";
    private const string HeroCardPrefix = "SO_HeroCard_";
    private const string ChipPrefix = "SO_ChipCard_";

    private readonly SO_ColorTestConfig _config;
    private readonly SO_LevelUpConfig _levelUp;
    private readonly SO_HeroConfig _heroConfig;
    private readonly Func<MapData> _getMap;
    private readonly DiceField _dice;
    private readonly ChipField _chips;
    private readonly MonsterField _monsters;
    private readonly WaveSpawner _waves;
    private readonly Hero _hero;
    private readonly HeroStats _heroStats;
    private readonly HeroAttack _heroAttack;
    private readonly Action _retry;

    // 읽은 기록.
    private readonly Dictionary<int, WaveBoard> _boards = new Dictionary<int, WaveBoard>();
    private readonly List<BoardEvent> _events = new List<BoardEvent>();
    private readonly List<HeroPick> _picks = new List<HeroPick>();
    private int _seed;
    private string _character;
    private string _logName;

    private readonly List<StartChip> _startChips = new List<StartChip>();
    private readonly List<string> _fields = new List<string>();
    private readonly StringBuilder _rows = new StringBuilder();
    private string _path;
    private int _run;
    private int _segment;
    private int _jumpTo;
    private bool _measuring;
    private bool _hasNextSeed;
    private bool _pendingRetry;
    private SO_HeroCharacter _savedCharacter;

    // 웨이브 측정값.
    private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
    private readonly double[] _bySource = new double[DamageSource.Count];
    private double _dealt;
    private double _leakedHealth;
    private int _leaks;
    private double _bossLeftRatio;
    private float _seconds;

    public bool Running { get; private set; }

    public event Action<bool> RunningChanged;

    public BoardReplayTest(SO_ColorTestConfig config, SO_LevelUpConfig levelUp, SO_HeroConfig heroConfig, Func<MapData> getMap, DiceField dice,
        ChipField chips, MonsterField monsters, WaveSpawner waves, Hero hero, HeroStats heroStats, HeroAttack heroAttack, Action retry)
    {
        _config = config;
        _levelUp = levelUp;
        _heroConfig = heroConfig;
        _getMap = getMap;
        _dice = dice;
        _chips = chips;
        _monsters = monsters;
        _waves = waves;
        _hero = hero;
        _heroStats = heroStats;
        _heroAttack = heroAttack;
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
        if (_config.ReplaySegments.Count == 0)
        {
            Debug.LogWarning("[BoardReplay] SO_ColorTestConfig.ReplaySegments가 비어 있다.");
            return;
        }
        if (!ReadLog()) return;
        _run = 0;
        _rows.Clear();
        _rows.Append("log,variant,wave,traits,boss,seconds,dealt,leaks,leaked_health,ratio,max_progress,mean_progress,boss_progress,boss_hp_left");
        for (int i = 0; i < DamageSource.Count; i++) _rows.Append(',').Append(DamageSource.NameOf(i));
        _rows.Append('\n');
        _path = Path.Combine(Application.persistentDataPath, "RunLogs", $"board_replay_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        // 기록한 판의 캐릭터로 돈다(캐릭터마다 영웅 능력치·특성이 다르다). 끝나면 되돌린다.
        _savedCharacter = _heroStats.Character;
        SO_HeroCharacter character = FindCharacter(_character);
        if (character != null) _heroStats.SetCharacter(character);
        Running = true;
        RunningChanged?.Invoke(true);
        StartRun();
    }

    /// <summary>다음 판의 맵(다시 하기가 한 번 가져간다) — 기록한 판의 시드.</summary>
    public bool TryTakeSeed(out int seed)
    {
        seed = _seed;
        bool has = Running && _hasNextSeed;
        _hasNextSeed = false;
        return has;
    }

    /// <summary>판 주사위 연출이 끝나 웨이브를 시작하기 직전(GameScene). 시작 칩을 기억해 두고 첫 구간으로 갈 준비를 한다.</summary>
    public void OnBattleReady()
    {
        if (!Running) return;
        MapData map = _getMap();
        if (map == null) return;
        _startChips.Clear();
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                SO_ChipCard card = _chips.Get(cell);
                if (card != null) _startChips.Add(new StartChip { Cell = cell, Card = card, Strength = _chips.GetSet(cell).Strength });
            }
        }
        _segment = 0;
        _measuring = false;
        _heroAttack.Enabled = _config.ReplayHero;
    }

    private void StartRun()
    {
        _hasNextSeed = true;
        _jumpTo = 0;
        _measuring = false;
        _pendingRetry = true;
    }

    private void Stop()
    {
        if (_savedCharacter != null) _heroStats.SetCharacter(_savedCharacter);
        _heroStats.ResetBonuses();
        Running = false;
        _hasNextSeed = false;
        _pendingRetry = false;
        _jumpTo = 0;
        _measuring = false;
        RunningChanged?.Invoke(false);
    }

    // 다음 판은 다음 프레임에 시작한다 — 웨이브 정리 이벤트 안에서 판을 갈아엎지 않게(ColorPowerTest와 같다).
    private void RealTick(float deltaTime)
    {
        if (!_pendingRetry) return;
        _pendingRetry = false;
        _retry();
    }

    private void Tick(float deltaTime)
    {
        if (!Running) return;
        // 구간으로 건너뛰기도 이벤트 밖(다음 걸음)에서 — 첫 웨이브가 이미 내보낸 적은 치운다.
        if (_jumpTo > 0 && _waves.Wave > 0)
        {
            int target = _jumpTo;
            _jumpTo = 0;
            _monsters.Clear();
            _waves.JumpTo(target);
            return;
        }
        if (!_measuring) return;
        _seconds += deltaTime;
        GridRouteField route = _monsters.Route;
        if (route == null) return;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            float distance = route.GetDistance(GridCircle.CellOf(states[i].Position));
            if (distance < 0f) continue;
            if (_tracks.TryGetValue(states[i].Id, out Track track))
            {
                if (distance < track.Min)
                {
                    track.Min = distance;
                    _tracks[states[i].Id] = track;
                }
                continue;
            }
            _tracks[states[i].Id] = new Track
            {
                Start = distance,
                Min = distance,
                Boss = _monsters.KindOf(i).IsBoss,
                MaxHealth = states[i].MaxHealth,
            };
        }
    }

    private void OnWaveStarted(int wave)
    {
        if (!Running || _segment >= _config.ReplaySegments.Count) return;
        Vector2Int segment = _config.ReplaySegments[_segment];
        if (wave < segment.x)
        {
            _jumpTo = segment.x;
            return;
        }
        if (!_boards.TryGetValue(wave, out WaveBoard board))
        {
            // 기록이 그 웨이브까지 가지 않았다(판이 먼저 끝났다) — 이번 색은 여기까지.
            Debug.LogWarning($"[BoardReplay] 기록에 웨이브 {wave}의 판이 없다 — 이번 색은 여기서 끝낸다.");
            NextRun();
            return;
        }
        ApplyBoard(board);
        ResetMeasure();
        _measuring = true;
    }

    private void OnWaveCleared(int wave)
    {
        if (!Running || !_measuring) return;
        _measuring = false;
        WriteRow(wave);
        Vector2Int segment = _config.ReplaySegments[_segment];
        if (wave < segment.y) return;
        _segment++;
        if (_segment < _config.ReplaySegments.Count)
        {
            _jumpTo = _config.ReplaySegments[_segment].x;
            return;
        }
        NextRun();
    }

    private void NextRun()
    {
        _measuring = false;
        Write();
        Debug.Log($"[BoardReplay] {_run + 1}/{VariantCount} {VariantName(_run)} 끝");
        _run++;
        if (_run >= VariantCount)
        {
            Debug.Log($"[BoardReplay] 끝: {_path}");
            Stop();
            return;
        }
        StartRun();
    }

    // 원래 색 한 판 + 바꿔 볼 색마다 한 판.
    private int VariantCount => 1 + _config.ReplayColors.Count;

    private bool TryGetVariantColor(int run, out DiceColor color)
    {
        color = DiceColor.Clear;
        if (run <= 0) return false;
        color = DiceColors.Normalize(_config.ReplayColors[run - 1]);
        return true;
    }

    private string VariantName(int run) =>
        TryGetVariantColor(run, out DiceColor color) ? DamageSource.NameOf((int)color) : "Original";

    // ---- 판 다시 세우기 ----

    private void ApplyBoard(WaveBoard board)
    {
        MapData map = _getMap();
        if (map == null) return;
        // 판을 비운다(주사위 → 칩). 없애기는 맵 점유도 함께 푼다.
        while (_dice.Placed.Count > 0) _dice.Remove(_dice.Placed[_dice.Placed.Count - 1].Cell);
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                _chips.Remove(new Vector2Int(x, y));

        // 칩: 시작 칩 → 그 웨이브 전까지의 칩 놓기·캐시 아웃을 차례대로.
        for (int i = 0; i < _startChips.Count; i++) _chips.Place(_startChips[i].Cell, _startChips[i].Card, _startChips[i].Strength);
        for (int i = 0; i < _events.Count; i++)
        {
            BoardEvent e = _events[i];
            if (e.Time >= board.Time) break;
            if (e.CashOut)
            {
                _chips.Remove(e.Cell);
                continue;
            }
            SetChip(e.Cell, e.Chip, e.Strength);
        }

        // 주사위: 웨이브 시작 때 찍힌 판 그대로(색만 바꿔 볼 수 있다).
        bool recolor = TryGetVariantColor(_run, out DiceColor variant);
        string[] entries = board.Board.Split(new[] { " ; " }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            if (!TryParseDice(entries[i], out Vector2Int cell, out DiceState state, out bool awakened, out string chip, out float strength))
            {
                Debug.LogWarning($"[BoardReplay] 판 항목을 읽지 못했다: {entries[i]}");
                continue;
            }
            if (chip != null) SetChip(cell, chip, strength);
            else _chips.Remove(cell);
            if (recolor) state.Colors = variant;
            PlaceDice(map, cell, state, awakened);
        }

        PlaceHero(map, board.Hero);
    }

    // 칩 종류는 클래스 이름으로 찾는다(실행 중 복사본이라 에셋 이름보다 확실하다).
    private void SetChip(Vector2Int cell, string name, float strength)
    {
        SO_ChipCard card = FindChip(name);
        if (card == null)
        {
            Debug.LogWarning($"[BoardReplay] 칩을 찾지 못했다: {name}");
            return;
        }
        _chips.Remove(cell);
        _chips.Place(cell, card, strength);
    }

    private SO_ChipCard FindChip(string name)
    {
        string typeName = ChipPrefix + name;
        for (int i = 0; i < _levelUp.ChipCards.Count; i++)
        {
            SO_ChipCard card = _levelUp.ChipCards[i];
            if (card != null && card.GetType().Name == typeName) return card;
        }
        return null;
    }

    // 놓기는 눈을 굴리므로(더블 오어 낫싱이면 부서질 수도 있다) 놓은 뒤 기록된 상태로 덮는다.
    private void PlaceDice(MapData map, Vector2Int cell, DiceState state, bool awakened)
    {
        DiceCard card = new DiceCard(state.Faces, state.Colors);
        for (int tries = 0; tries < 10 && !_dice.TryGetPlaced(cell, out _); tries++) _dice.Place(map, cell, card);
        if (!_dice.TryGetPlaced(cell, out _))
        {
            Debug.LogWarning($"[BoardReplay] {cell.x}:{cell.y}에 주사위를 놓지 못했다.");
            return;
        }
        _dice.Change(cell, (ref DiceState dice) => dice = state, false);
        // 눈 20이 아닌데 각성(강제 각성 스킬)이었으면 그대로.
        if (awakened && !_dice.IsAwakened(cell)) _dice.SetForcedAwake(cell, true);
    }

    // 영웅: 그때까지 고른 영웅 카드를 다시 적용하고, 기록된 칸(주사위 칸이면 가장 가까운 빈 바닥)에 세운다 — 주사위 위면 올라타 버린다.
    private void PlaceHero(MapData map, Vector2Int? recorded)
    {
        _heroStats.ResetBonuses();
        float time = _boards.TryGetValue(_waves.Wave, out WaveBoard board) ? board.Time : float.MaxValue;
        for (int i = 0; i < _picks.Count; i++)
        {
            if (_picks[i].Time >= time) break;
            SO_HeroCard card = FindHeroCard(_picks[i].Card);
            if (card == null) continue;
            for (int k = 0; k < _picks[i].Count; k++) _heroStats.Apply(card);
        }
        Vector2Int target = recorded ?? GridCircle.CellOf(_hero.Position);
        _hero.Teleport(NearestFreeGround(map, target));
    }

    private SO_HeroCard FindHeroCard(string name)
    {
        for (int i = 0; i < _levelUp.HeroCards.Count; i++)
            if (_levelUp.HeroCards[i] != null && _levelUp.HeroCards[i].name == name) return _levelUp.HeroCards[i];
        Debug.LogWarning($"[BoardReplay] 영웅 카드를 찾지 못했다: {name}");
        return null;
    }

    private SO_HeroCharacter FindCharacter(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < _heroConfig.Characters.Count; i++)
            if (_heroConfig.Characters[i] != null && _heroConfig.Characters[i].name == name) return _heroConfig.Characters[i];
        Debug.LogWarning($"[BoardReplay] 캐릭터를 찾지 못했다: {name} — 지금 캐릭터로 돈다.");
        return null;
    }

    private static Vector2 NearestFreeGround(MapData map, Vector2Int from)
    {
        Vector2Int best = from;
        int bestDistance = int.MaxValue;
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                if (map.Get(x, y) != TileType.Ground || map.HasDice(x, y) || map.HasChip(x, y)) continue;
                int dx = x - from.x;
                int dy = y - from.y;
                int distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = new Vector2Int(x, y);
            }
        }
        return best;
    }

    // ---- 측정 ----

    private void ResetMeasure()
    {
        _tracks.Clear();
        Array.Clear(_bySource, 0, _bySource.Length);
        _dealt = 0d;
        _leakedHealth = 0d;
        _leaks = 0;
        _bossLeftRatio = 0d;
        _seconds = 0f;
    }

    private void CountDamage(DamageSource source, double amount)
    {
        if (!Running || !_measuring) return;
        _dealt += amount;
        _bySource[source.Index] += amount;
    }

    private void CountLeak(MonsterKind kind, double health)
    {
        if (!Running || !_measuring) return;
        _leakedHealth += health;
        _leaks++;
        if (!kind.IsBoss) return;
        // 보스는 한 마리라 처음 본 최대 체력으로 남은 비율을 낸다.
        foreach (KeyValuePair<int, Track> pair in _tracks)
        {
            if (!pair.Value.Boss || pair.Value.MaxHealth <= 0d) continue;
            _bossLeftRatio = health / pair.Value.MaxHealth;
            break;
        }
    }

    private void WriteRow(int wave)
    {
        // 길 진행률 = 1 − 가장 가까웠던 거리 ÷ 처음 거리. 목표물에 닿은 적은 거리 0에 가깝다.
        float maxProgress = 0f;
        float sumProgress = 0f;
        int count = 0;
        float bossProgress = -1f;
        foreach (KeyValuePair<int, Track> pair in _tracks)
        {
            Track track = pair.Value;
            float progress = track.Start > 0f ? Mathf.Clamp01(1f - track.Min / track.Start) : 0f;
            if (progress > maxProgress) maxProgress = progress;
            sumProgress += progress;
            count++;
            if (track.Boss) bossProgress = progress;
        }
        double total = _dealt + _leakedHealth;
        CultureInfo invariant = CultureInfo.InvariantCulture;
        _rows.Append(_logName).Append(',')
            .Append(VariantName(_run)).Append(',')
            .Append(wave).Append(',')
            .Append(Traits()).Append(',')
            .Append(_waves.WaveBoss != null ? 1 : 0).Append(',')
            .Append(_seconds.ToString("0.0", invariant)).Append(',')
            .Append(_dealt.ToString("0", invariant)).Append(',')
            .Append(_leaks).Append(',')
            .Append(_leakedHealth.ToString("0", invariant)).Append(',')
            .Append((total > 0d ? _dealt / total : 1d).ToString("0.0000", invariant)).Append(',')
            .Append(maxProgress.ToString("0.000", invariant)).Append(',')
            .Append((count > 0 ? sumProgress / count : 0f).ToString("0.000", invariant)).Append(',')
            .Append(bossProgress >= 0f ? bossProgress.ToString("0.000", invariant) : "").Append(',')
            .Append(_waves.WaveBoss != null ? _bossLeftRatio.ToString("0.000", invariant) : "");
        for (int i = 0; i < _bySource.Length; i++) _rows.Append(',').Append(_bySource[i].ToString("0", invariant));
        _rows.Append('\n');
    }

    private string Traits()
    {
        StringBuilder text = new StringBuilder();
        IReadOnlyList<MonsterKind> kinds = _waves.WaveKinds;
        for (int i = 0; i < kinds.Count; i++)
        {
            if (i > 0) text.Append('|');
            if (kinds[i].Traits.Count == 0) text.Append("Normal");
            for (int k = 0; k < kinds[i].Traits.Count; k++)
            {
                if (k > 0) text.Append('+');
                text.Append(kinds[i].Traits[k].name.Replace("SO_MonsterTrait_", ""));
            }
        }
        return text.ToString();
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, _rows.ToString(), new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BoardReplay] 결과를 쓰지 못했다: {_path}\n{e.Message}");
        }
    }

    // ---- 기록 읽기 ----

    private bool ReadLog()
    {
        string folder = Path.Combine(Application.persistentDataPath, "RunLogs");
        string path = string.IsNullOrEmpty(_config.ReplayLog) ? LatestPlayLog(folder) : Path.Combine(folder, _config.ReplayLog);
        if (path == null || !File.Exists(path))
        {
            Debug.LogWarning($"[BoardReplay] 플레이 기록이 없다: {(path ?? folder)}");
            return false;
        }
        _logName = Path.GetFileNameWithoutExtension(path);
        _boards.Clear();
        _events.Clear();
        _picks.Clear();
        _character = null;
        _seed = 0;
        // 파일 이름 끝의 _seed<숫자>가 맵 시드다.
        int seedAt = _logName.LastIndexOf("_seed", StringComparison.Ordinal);
        if (seedAt < 0 || !int.TryParse(_logName.Substring(seedAt + 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out _seed))
        {
            Debug.LogWarning($"[BoardReplay] 파일 이름에서 시드를 읽지 못했다: {_logName}");
            return false;
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        // 0 t, 1 wave, 2 level, 3 goal, 4 event, 5 card, 6 cell, 7 dice, 8 detail, 9 hero, 10 riding, 11 boss
        for (int i = 1; i < lines.Length; i++)
        {
            SplitCsv(lines[i], _fields);
            if (_fields.Count < 10) continue;
            float time = float.Parse(_fields[0], CultureInfo.InvariantCulture);
            string kind = _fields[4];
            string card = _fields[5];
            switch (kind)
            {
                case "run_start":
                    _character = card;
                    break;
                case "wave_start":
                    int wave = int.Parse(_fields[1], CultureInfo.InvariantCulture);
                    _boards[wave] = new WaveBoard
                    {
                        Time = time,
                        Board = _fields[8],
                        Hero = TryParseCell(_fields[9], out Vector2Int hero) ? hero : (Vector2Int?)null,
                    };
                    break;
                case "chip":
                    if (!TryParseCell(_fields[6], out Vector2Int chipCell)) break;
                    if (!TryParseChip(_fields[7], out string chip, out float strength))
                    {
                        chip = card.StartsWith(ChipPrefix, StringComparison.Ordinal) ? card.Substring(ChipPrefix.Length) : card;
                        strength = 1f;
                    }
                    _events.Add(new BoardEvent { Time = time, Cell = chipCell, Chip = chip, Strength = strength });
                    break;
                case "skill":
                    if (card == CashOutCard && TryParseCell(_fields[6], out Vector2Int cashCell))
                        _events.Add(new BoardEvent { Time = time, Cell = cashCell, CashOut = true });
                    break;
                case "pick":
                    if (!card.StartsWith(HeroCardPrefix, StringComparison.Ordinal)) break;
                    int count = 1;
                    if (_fields[8].StartsWith("x", StringComparison.Ordinal)) int.TryParse(_fields[8].Substring(1), out count);
                    _picks.Add(new HeroPick { Time = time, Card = card, Count = Mathf.Max(count, 1) });
                    break;
            }
        }
        Debug.Log($"[BoardReplay] {_logName}: 웨이브 판 {_boards.Count}개, 판 바꾸기 {_events.Count}번, 영웅 카드 {_picks.Count}장, 캐릭터 {_character}");
        return _boards.Count > 0;
    }

    private static string LatestPlayLog(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        string[] files = Directory.GetFiles(folder, "play_*.csv");
        string latest = null;
        DateTime latestTime = DateTime.MinValue;
        for (int i = 0; i < files.Length; i++)
        {
            DateTime time = File.GetLastWriteTime(files[i]);
            if (time <= latestTime) continue;
            latestTime = time;
            latest = files[i];
        }
        return latest;
    }

    // 따옴표 칸("..", 안의 ""는 ")을 다루는 CSV 한 줄 나누기.
    private static void SplitCsv(string line, List<string> fields)
    {
        fields.Clear();
        StringBuilder field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else quoted = false;
                continue;
            }
            if (c == '"') quoted = true;
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else field.Append(c);
        }
        fields.Add(field.ToString());
    }

    private static bool TryParseCell(string text, out Vector2Int cell)
    {
        cell = default;
        int colon = text.IndexOf(':');
        if (colon <= 0) return false;
        if (!int.TryParse(text.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)) return false;
        if (!int.TryParse(text.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) return false;
        cell = new Vector2Int(x, y);
        return true;
    }

    // "[Range]" 또는 "[Range x2]"(빅 스택 칩)를 찾는다.
    private static bool TryParseChip(string text, out string name, out float strength)
    {
        name = null;
        strength = 1f;
        int open = text.IndexOf('[');
        int close = text.IndexOf(']');
        if (open < 0 || close <= open) return false;
        string inner = text.Substring(open + 1, close - open - 1);
        int space = inner.IndexOf(' ');
        if (space < 0)
        {
            name = inner;
            return true;
        }
        name = inner.Substring(0, space);
        string rest = inner.Substring(space + 1);
        if (rest.StartsWith("x", StringComparison.Ordinal))
            float.TryParse(rest.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out strength);
        return true;
    }

    // "x:y=d20 Yellow 20 A [Damage]" (PlayLog.Board).
    private static bool TryParseDice(string entry, out Vector2Int cell, out DiceState state, out bool awakened, out string chip, out float strength)
    {
        state = default;
        awakened = false;
        TryParseChip(entry, out chip, out strength);
        int equals = entry.IndexOf('=');
        cell = default;
        if (equals <= 0 || !TryParseCell(entry.Substring(0, equals).Trim(), out cell)) return false;
        string body = entry.Substring(equals + 1);
        int bracket = body.IndexOf('[');
        if (bracket >= 0) body = body.Substring(0, bracket);
        string[] parts = body.Trim().Split(' ');
        if (parts.Length < 3 || !parts[0].StartsWith("d", StringComparison.Ordinal)) return false;
        if (!int.TryParse(parts[0].Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int faces)) return false;
        if (!TryParseColor(parts[1], out DiceColor colors)) return false;
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pips)) return false;
        awakened = parts.Length > 3 && parts[3] == "A";
        state = new DiceState { Faces = faces, Colors = colors, Pips = pips };
        return true;
    }

    // 색 이름은 DamageSource의 0~7번(DiceColor 비트 그대로).
    private static bool TryParseColor(string name, out DiceColor colors)
    {
        for (int i = 0; i < 8; i++)
        {
            if (DamageSource.NameOf(i) != name) continue;
            colors = (DiceColor)i;
            return true;
        }
        colors = DiceColor.Clear;
        return false;
    }
}
