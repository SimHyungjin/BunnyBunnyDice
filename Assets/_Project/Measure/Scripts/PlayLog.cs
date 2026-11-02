using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 사람이 한 판을 어떻게 했는지 행동마다 한 줄씩 남긴다(RunLogs/play_*.csv) — 사용자 판을 읽고 봇에 옮기려고(2026-11-02 사용자).
/// WaveLog(웨이브마다 결과 숫자)와 달리 "언제·어디에·무엇을" 남긴다: 카드 놓기(놓은 뒤 주사위 상태), 스킬(대상 자리), 레벨업 후보·고른 카드,
/// 영웅 이동 명령·올라타기(ride = 레이즈 켜짐)·내리기(ride_end = 레이즈 꺼짐), 각성, 샌 적, 보스 처치. 웨이브가 시작될 때마다 판 전체(주사위 칸·면·색·눈·각성·칩)를 한 줄로 찍어 미로 모양을 다시 볼 수 있다.
/// 줄마다 그때의 게임 시간·웨이브·레벨·목표물 체력·영웅 칸·올라탄 주사위·보스(칸·체력 %)를 붙인다.
/// 봇·색 시험이 도는 동안은 남기지 않는다(isHuman). 웨이브가 시작될 때마다 파일을 다시 써서 중간에 꺼도 거기까지 남는다.
/// 테스트·분석용이라 출시 빌드에서는 파일을 쓰지 않는다.
/// </summary>
public sealed class PlayLog : IDisposable
{
    private const string Header = "t,wave,level,goal,event,card,cell,dice,detail,hero,riding,boss";

    private readonly Func<bool> _isHuman;
    private readonly Func<int> _getWave;
    private readonly Func<int> _getLevel;
    private readonly Func<int> _getGoal;
    private readonly DiceField _dice;
    private readonly ChipField _chips;
    private readonly CardPlacement _placement;
    private readonly SkillCaster _skills;
    private readonly LevelUpChoice _levelUp;
    private readonly Hero _hero;
    private readonly HeroRaise _heroRaise;
    private readonly MonsterField _monsters;
    private readonly WaveSpawner _waves;
    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly StringBuilder _rows = new StringBuilder();
    private readonly StringBuilder _line = new StringBuilder();
    private string _path;
    private float _time;
    private bool _running;
    // 올라탄 주사위(내릴 때 어느 주사위에서 내렸는지 남긴다). 안 타고 있으면 null.
    private Vector2Int? _riding;

    public PlayLog(Func<bool> isHuman, Func<int> getWave, Func<int> getLevel, Func<int> getGoal, DiceField dice, ChipField chips,
        CardPlacement placement, SkillCaster skills, LevelUpChoice levelUp, Hero hero, HeroRaise heroRaise, MonsterField monsters,
        WaveSpawner waves, SO_DiceStatsConfig statsConfig)
    {
        _isHuman = isHuman;
        _getWave = getWave;
        _getLevel = getLevel;
        _getGoal = getGoal;
        _dice = dice;
        _chips = chips;
        _placement = placement;
        _skills = skills;
        _levelUp = levelUp;
        _hero = hero;
        _heroRaise = heroRaise;
        _monsters = monsters;
        _waves = waves;
        _statsConfig = statsConfig;

        _placement.Placed += OnPlaced;
        _skills.UsedAt += OnSkill;
        _levelUp.Offered += OnOffered;
        _levelUp.CardChosen += OnChosen;
        _hero.MoveOrdered += OnHeroMove;
        _heroRaise.Mounted += OnMounted;
        _dice.DiceAwakened += OnAwakened;
        _monsters.ReachedGoal += OnLeak;
        _monsters.Killed += OnKilled;
        _waves.WaveStarted += OnWaveStarted;
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose()
    {
        _placement.Placed -= OnPlaced;
        _skills.UsedAt -= OnSkill;
        _levelUp.Offered -= OnOffered;
        _levelUp.CardChosen -= OnChosen;
        _hero.MoveOrdered -= OnHeroMove;
        _heroRaise.Mounted -= OnMounted;
        _dice.DiceAwakened -= OnAwakened;
        _monsters.ReachedGoal -= OnLeak;
        _monsters.Killed -= OnKilled;
        _waves.WaveStarted -= OnWaveStarted;
        Core.Loop.OnGameUpdate -= Tick;
    }

    /// <summary>판 시작(GameScene.StartBattle). 사람이 할 때만 남긴다. map은 목표물·출현 구멍을 첫 줄에 적는다.</summary>
    public void Begin(string character, int seed, MapData map)
    {
        _running = _isHuman();
        _rows.Clear();
        _time = 0f;
        _riding = null;
        if (!_running) return;
        _path = Path.Combine(Application.persistentDataPath, "RunLogs", $"play_{DateTime.Now:yyyyMMdd_HHmmss}_{character}_seed{seed}.csv");
        _rows.Append(Header).Append('\n');
        string detail = map != null ? $"size {map.Width}x{map.Height} goal {Cell(map.GoalRect.position)} {map.GoalRect.width}x{map.GoalRect.height} spawns {Cells(map.SpawnGates)}" : "";
        Row("run_start", character, null, "", detail);
    }

    /// <summary>판 끝(게임 오버·판을 접음). 파일을 쓰고 멈춘다.</summary>
    public void End()
    {
        if (!_running) return;
        Row("run_end", "", null, "", Board());
        Write();
        _running = false;
    }

    // 내리기는 이벤트가 없어 걸음마다 본다 — 올라탄 상태가 꺼지면(내려옴·주사위가 바닥남) 한 줄.
    private void Tick(float deltaTime)
    {
        if (!_running) return;
        _time += deltaTime;
        if (_riding.HasValue && !_heroRaise.Active)
        {
            Vector2Int cell = _riding.Value;
            _riding = null;
            Row("ride_end", "", cell, DiceAt(cell), "");
        }
    }

    private void OnWaveStarted(int wave)
    {
        if (!_running) return;
        Row("wave_start", "", null, "", Board());
        // 중간에 꺼도 지난 웨이브까지는 남게.
        Write();
    }

    private void OnPlaced(HandCard card, Vector2Int cell, PlacementResult result)
    {
        if (!_running) return;
        string kind = card.IsChip ? "chip" : result == PlacementResult.Stack ? "stack" : "place";
        Row(kind, CardName(card), cell, DiceAt(cell), "");
    }

    private void OnSkill(SO_SkillCard card, Vector2? at)
    {
        if (!_running || card == null) return;
        Vector2Int? cell = at.HasValue ? Vector2Int.RoundToInt(at.Value) : (Vector2Int?)null;
        Row("skill", card.name, cell, cell.HasValue ? DiceAt(cell.Value) : "", "");
    }

    private void OnOffered(IReadOnlyList<HandCard> offers)
    {
        if (!_running) return;
        _line.Clear();
        for (int i = 0; i < offers.Count; i++)
        {
            if (i > 0) _line.Append(" | ");
            _line.Append(CardName(offers[i]));
        }
        Row("offer", "", null, "", _line.ToString());
    }

    private void OnChosen(HandCard card, int count) => RowIf("pick", CardName(card), null, "", count > 1 ? $"x{count}" : "");

    private void OnHeroMove(Vector2Int cell) => RowIf("hero_move", "", cell, DiceAt(cell), "");

    private void OnMounted(Vector2Int cell)
    {
        if (!_running) return;
        // 다른 주사위로 옮겨 타면 앞 주사위에서 내린 것이다.
        if (_riding.HasValue && _riding.Value != cell) Row("ride_end", "", _riding.Value, DiceAt(_riding.Value), "");
        _riding = cell;
        Row("ride", "", cell, DiceAt(cell), "");
    }

    private void OnAwakened(Vector2Int cell) => RowIf("awaken", "", cell, DiceAt(cell), "");

    private void OnLeak(MonsterKind kind, double damage) =>
        RowIf("leak", "", null, "", $"{(kind.IsBoss ? "BOSS " : "")}{TraitsOf(kind)} dmg {damage.ToString("0", CultureInfo.InvariantCulture)}");

    private void OnKilled(MonsterKind kind, Vector2 position)
    {
        if (kind.IsBoss) RowIf("boss_kill", "", Vector2Int.RoundToInt(position), "", TraitsOf(kind));
    }

    private void RowIf(string kind, string card, Vector2Int? cell, string dice, string detail)
    {
        if (_running) Row(kind, card, cell, dice, detail);
    }

    private void Row(string kind, string card, Vector2Int? cell, string dice, string detail)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        _rows.Append(_time.ToString("0.0", invariant)).Append(',')
            .Append(_getWave()).Append(',')
            .Append(_getLevel()).Append(',')
            .Append(_getGoal()).Append(',')
            .Append(kind).Append(',')
            .Append(Quote(card)).Append(',')
            .Append(cell.HasValue ? Cell(cell.Value) : "").Append(',')
            .Append(Quote(dice)).Append(',')
            .Append(Quote(detail)).Append(',')
            .Append(Cell(GridCircle.CellOf(_hero.Position))).Append(',')
            .Append(_heroRaise.Active ? Cell(_heroRaise.Cell) : "").Append(',')
            .Append(Quote(BossText()))
            .Append('\n');
    }

    // 판 위 모든 주사위: "x:y=d20 Red 17 A [Damage x2]" 를 " ; "로 잇는다.
    private string Board()
    {
        StringBuilder board = new StringBuilder();
        IReadOnlyList<PlacedDice> placed = _dice.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            if (i > 0) board.Append(" ; ");
            board.Append(Cell(placed[i].Cell)).Append('=').Append(Describe(placed[i].Cell, placed[i].Dice));
        }
        return board.ToString();
    }

    private string DiceAt(Vector2Int cell) =>
        _dice.TryGetPlaced(cell, out PlacedDice placed) ? Describe(cell, placed.Dice) : ChipText(cell);

    private string Describe(Vector2Int cell, in DiceState dice)
    {
        string text = $"d{dice.Faces} {DamageSource.NameOf((int)DiceColors.Normalize(dice.Colors))} {dice.Pips}";
        if (_statsConfig.IsAwakened(dice)) text += " A";
        string chip = ChipText(cell);
        return chip.Length > 0 ? $"{text} {chip}" : text;
    }

    private string ChipText(Vector2Int cell)
    {
        SO_ChipCard chip = _chips.Get(cell);
        if (chip == null) return "";
        float strength = _chips.GetSet(cell).Strength;
        string name = chip.name.StartsWith("SO_ChipCard_", StringComparison.Ordinal) ? chip.name.Substring("SO_ChipCard_".Length) : chip.name;
        return strength > 1f ? $"[{name} x{strength.ToString("0.#", CultureInfo.InvariantCulture)}]" : $"[{name}]";
    }

    // 살아 있는 보스: "x:y hp 63%". 없으면 빈칸.
    private string BossText()
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || !_monsters.KindOf(i).IsBoss) continue;
            double ratio = states[i].MaxHealth > 0 ? states[i].Health / states[i].MaxHealth : 0;
            return $"{Cell(Vector2Int.RoundToInt(states[i].Position))} hp {(ratio * 100).ToString("0", CultureInfo.InvariantCulture)}%";
        }
        return "";
    }

    private static string TraitsOf(MonsterKind kind)
    {
        const string prefix = "SO_MonsterTrait_";
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < kind.Traits.Count; i++)
        {
            if (i > 0) text.Append('+');
            string name = kind.Traits[i].name;
            text.Append(name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name);
        }
        return text.Length > 0 ? text.ToString() : "Normal";
    }

    private static string CardName(HandCard card) =>
        card.Asset != null ? card.Asset.name : $"d{card.Dice.Faces}_{DamageSource.NameOf((int)DiceColors.Normalize(card.Dice.Colors))}";

    private static string Cell(Vector2Int cell) => $"{cell.x}:{cell.y}";

    private static string Cells(IReadOnlyList<Vector2Int> cells)
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0) text.Append(' ');
            text.Append(Cell(cells[i]));
        }
        return text.ToString();
    }

    private static string Quote(string text) =>
        string.IsNullOrEmpty(text) ? "" : text.IndexOf(',') >= 0 || text.IndexOf('"') >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

    private void Write()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, _rows.ToString(), new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayLog] 기록을 쓰지 못했다: {_path}\n{e.Message}");
        }
#endif
    }
}
