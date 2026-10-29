using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 플레이 봇(기획서 11.2·11.5). 사람과 같은 게임 행동만 코드로 부른다 — 레벨업 고르기(LevelUpChoice.AutoChoose),
/// 손패 카드 놓기(CardPlacement)·스킬 쓰기(SkillCaster), 영웅 이동(Hero.MoveTo). 판이 끝나면 다시 하기를 누르고 측정 기록(WaveLog CSV)이 남는다.
/// 규칙형이라 사람보다 서툴다 — "봇이 몇 웨이브까지 가는지"를 기준선으로 보고, 카드·캐릭터에 따른 차이를 비교하는 데 쓴다.
/// 미로는 판 전체를 탐색한 목표 배치(MazePlanner)대로 짓는다.
/// 판단은 사용자 판 교본 2차(2026-10-28, 기획서 11.5)를 옮겼다 — 판을 미로와 메인 딜 구간(BotBoard.KillZone)으로 나눈다:
/// 미로는 d6·d12와 화상 칩(체력 비례 피해라 싼 주사위로도 후반까지 들어간다), 메인 딜 구간은 d20·범위 주사위와 디버프(종류마다 하나)·빨강·초록 칩.
/// 영웅은 미로를 짓는 동안 남은 적을 쫓아 정리하고, 지은 뒤엔 메인 딜 구간에서 오가며 싸운다.
/// 오버드라이브는 빠져나갈 적을 걸면 잡을 수 있을 때만(BotEscape). 범위 스킬·체크는 구간이 뚫릴 때만(Breached).
/// 판단 값은 SO_BotConfig. 정한 판 수를 돌면 요약(BotBatchReport)을 남기고 멈춘다. 캐릭터를 돌아가며 돌릴 수도 있다(RotateCharacters).
/// 비교 묶음(RunSets — 능력치 단계·카드 성향)마다 같은 맵(Seeds)으로 돌릴 수도 있다. 단계는 세이브와 이어져 있어(MetaProfile) 도는 동안은 세이브에도 그 단계가 남으므로,
/// 끝나거나 끌 때 켤 때의 단계로 되돌린다.
/// </summary>
public sealed class GameBot : IDisposable
{
    private readonly SO_BotConfig _config;
    private readonly Func<MapData> _getMap;
    private readonly Func<int> _getWave;
    private readonly HeroStats _heroStats;
    private readonly IReadOnlyList<SO_HeroCharacter> _characters;
    // 이번에 돌아가며 돌릴 캐릭터들 — 켤 때의 캐릭터는 빼고(이미 돌려 본 기준선) 나머지를 목록 순서로.
    private readonly List<SO_HeroCharacter> _rotation = new List<SO_HeroCharacter>();
    private readonly DiceField _diceField;
    private readonly ChipField _chips;
    private readonly CardHand _hand;
    private readonly CardPlacement _placement;
    private readonly SkillCaster _skills;
    private readonly MonsterField _monsters;
    private readonly Hero _hero;
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_DiceStatsConfig _statsConfig;
    // 영웅이 올라탄 주사위의 오버드라이브 배율(SO_HeroConfig.Overdrive — 스킬 카드와 같은 값).
    private readonly Func<SO_SkillCard_Overdrive> _heroOverdrive;
    private readonly LevelUpChoice _levelUp;
    private readonly GameSpeed _speed;
    private readonly Func<bool> _canAct;
    private readonly Action _retry;
    private readonly MazePlanner _planner;
    private readonly BotBoard _board = new BotBoard();
    private readonly BotEscape _escape = new BotEscape();
    private readonly BotBatchReport _report = new BotBatchReport();
    private readonly List<int> _targets = new List<int>();

    // 비교 묶음으로 도는 동안 켤 때의 단계(되돌릴 값). 묶음으로 돌지 않으면 null.
    private int[] _savedStages;
    // 캐릭터를 돌아가며 돌 때 켤 때의 캐릭터와, 단계를 덮어쓴 캐릭터마다 원래 단계(세이브에 남으므로 끝나면 되돌린다).
    private SO_HeroCharacter _rotationOrigin;
    private readonly Dictionary<SO_HeroCharacter, int[]> _rotationSavedStages = new Dictionary<SO_HeroCharacter, int[]>();

    private float _actTimer;
    private float _heroTimer;
    private float _retryTimer = -1f;
    private bool _hasHeroCell;
    private Vector2Int _heroCell;

    /// <summary>켜져 있는지. 켜면 게임을 빠르게 돌리고 레벨업을 대신 고른다.</summary>
    public bool Enabled { get; private set; }

    /// <summary>빠른 시뮬(화면을 그리지 않고 한 프레임에 걸음을 많이 돈다)로 켜졌는지. 켜고 끄는 것은 GameScene이 EnabledChanged로 한다.</summary>
    public bool Fast { get; private set; }

    /// <summary>켜지거나 꺼졌다(정한 판 수를 채워 스스로 꺼질 때도). 빠른 시뮬이면 GameScene이 그리기·걸음 수를 바꾼다.</summary>
    public event Action<bool> EnabledChanged;


    /// <summary>봇이 끝낸 판 수(켠 뒤로).</summary>
    public int Runs => _report.Count;

    /// <summary>마지막으로 남긴 요약 파일 경로(없으면 null).</summary>
    public string LastReportPath { get; private set; }

    /// <param name="getWave">지금 웨이브(레벨업 점수 구간).</param>
    /// <param name="canAct">판이 돌고 있어 카드를 쓸 수 있는지(로비·판 주사위 연출·게임 오버가 아님).</param>
    /// <param name="retry">결과 화면의 다시 하기와 같은 길.</param>
    /// <param name="characters">캐릭터를 돌아가며 돌릴 때의 순서.</param>
    public GameBot(SO_BotConfig config, Func<MapData> getMap, Func<int> getWave, HeroStats heroStats, IReadOnlyList<SO_HeroCharacter> characters,
        DiceField diceField, ChipField chips, CardHand hand, CardPlacement placement, SkillCaster skills, MonsterField monsters, Hero hero,
        SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig, Func<SO_SkillCard_Overdrive> heroOverdrive, LevelUpChoice levelUp,
        GameSpeed speed, MazePlanner planner, Func<bool> canAct, Action retry)
    {
        _config = config;
        _heroStats = heroStats;
        _characters = characters;
        _getMap = getMap;
        _getWave = getWave;
        _planner = planner;
        _diceField = diceField;
        _chips = chips;
        _hand = hand;
        _placement = placement;
        _skills = skills;
        _monsters = monsters;
        _hero = hero;
        _diceConfig = diceConfig;
        _statsConfig = statsConfig;
        _heroOverdrive = heroOverdrive;
        _levelUp = levelUp;
        _speed = speed;
        _canAct = canAct;
        _retry = retry;
        Core.Loop.OnUpdate += Tick;
        Core.Loop.OnGameUpdate += GameTick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        Core.Loop.OnGameUpdate -= GameTick;
        SetEnabled(false);
    }

    /// <summary>켜고 끈다. fast면 빠른 시뮬(그리기·걸음 수는 GameScene이 EnabledChanged를 듣고 바꾼다).</summary>
    public void SetEnabled(bool enabled, bool fast = false)
    {
        Fast = enabled && fast;
        Switch(enabled);
        EnabledChanged?.Invoke(enabled);
    }

    private void Switch(bool enabled)
    {
        Enabled = enabled;
        _levelUp.AutoChoose = enabled ? ChooseLevelUp : (Func<IReadOnlyList<HandCard>, IReadOnlyList<int>, int>)null;
        _speed.FastForward = !enabled ? 1f : Fast ? _config.SimSpeed : _config.FastForward;
        _retryTimer = -1f;
        _hasHeroCell = false;
        if (enabled) _report.Clear();
        if (!enabled)
        {
            RestoreStages();
            RestoreRotation();
            return;
        }
        if (_config.RunSets.Count > 0)
        {
            // 끄지 않고 다시 켜면 이미 묶음 단계라 덮어쓰지 않는다.
            if (_savedStages == null)
            {
                _savedStages = new int[HeroStats.StatCount];
                for (int i = 0; i < _savedStages.Length; i++) _savedStages[i] = _heroStats.GetStage((HeroStat)i);
            }
            // 켜는 판은 켤 때의 단계·맵으로 시작했으므로 첫 묶음으로 바꾸고 바로 새 판을 시작한다.
            ApplySet(0);
            _retry();
            return;
        }
        if (!_config.RotateCharacters) return;
        _rotation.Clear();
        // 맵(Seeds)이나 단계(RotationStages)를 맞춰 짝지어 비교할 때는 켤 때의 캐릭터도 같은 조건으로 다시 돈다 — 켤 때의 판은 조건이 다르다.
        bool paired = _config.Seeds.Count > 0 || _config.RotationStages.Length > 0;
        for (int i = 0; i < _characters.Count; i++)
        {
            SO_HeroCharacter character = _characters[i];
            if (character == null || (!paired && character == _heroStats.Character)) continue;
            if (_config.RotationOnly.Count > 0 && !_config.RotationOnly.Contains(character)) continue;
            _rotation.Add(character);
        }
        // 켜는 판은 지금 캐릭터라, 첫 캐릭터로 바꾸고 바로 새 판을 시작한다.
        if (_rotation.Count == 0) return;
        _rotationOrigin = _heroStats.Character;
        ApplyRotation(0);
        _retry();
    }

    // runs번째 판의 캐릭터·단계·맵을 건다(캐릭터를 돌아가며 돌 때).
    private void ApplyRotation(int runs)
    {
        SO_HeroCharacter character = RotationCharacter(runs);
        if (character == null) return;
        _heroStats.SetCharacter(character);
        if (_config.RotationStages.Length > 0)
        {
            if (!_rotationSavedStages.ContainsKey(character))
            {
                int[] saved = new int[HeroStats.StatCount];
                for (int i = 0; i < saved.Length; i++) saved[i] = _heroStats.GetStage((HeroStat)i);
                _rotationSavedStages[character] = saved;
            }
            _heroStats.LoadStages(_config.RotationStages);
        }
        int k = runs % Mathf.Max(_config.RunsPerCharacter, 1);
        _hasNextSeed = _config.Seeds.Count > 0;
        if (_hasNextSeed) _nextSeed = _config.Seeds[k % _config.Seeds.Count];
    }

    // 돌아가며 덮어쓴 단계를 캐릭터마다 되돌리고 켤 때의 캐릭터로 돌아간다.
    private void RestoreRotation()
    {
        foreach (KeyValuePair<SO_HeroCharacter, int[]> pair in _rotationSavedStages)
        {
            _heroStats.SetCharacter(pair.Key);
            _heroStats.LoadStages(pair.Value);
        }
        _rotationSavedStages.Clear();
        if (_rotationOrigin != null) _heroStats.SetCharacter(_rotationOrigin);
        _rotationOrigin = null;
    }

    private void RestoreStages()
    {
        _hasNextSeed = false;
        if (_savedStages == null) return;
        _heroStats.LoadStages(_savedStages);
        _savedStages = null;
    }

    /// <summary>다음 판을 이 맵으로(비교 묶음의 Seeds). 다시 하기가 한 번 가져간다 — 없으면 false(무작위 맵).</summary>
    public bool TryTakeSeed(out int seed)
    {
        seed = _nextSeed;
        bool has = _hasNextSeed;
        _hasNextSeed = false;
        return has;
    }

    private int _nextSeed;
    private bool _hasNextSeed;

    // 비교 묶음으로 돌 때 runs번째 판의 묶음. 다 돌았으면 null.
    private SO_BotConfig.RunSet CurrentSet(int runs)
    {
        if (_savedStages == null) return null;
        int index = runs / Mathf.Max(_config.RunsPerSet, 1);
        return index < _config.RunSets.Count ? _config.RunSets[index] : null;
    }

    // runs번째 판을 시작하기 전에 그 묶음의 단계·맵을 건다.
    private void ApplySet(int runs)
    {
        SO_BotConfig.RunSet set = CurrentSet(runs);
        if (set == null) return;
        _heroStats.LoadStages(set.Stages.Length > 0 ? set.Stages : _savedStages);
        int k = runs % Mathf.Max(_config.RunsPerSet, 1);
        _hasNextSeed = _config.Seeds.Count > 0;
        if (_hasNextSeed) _nextSeed = _config.Seeds[k % _config.Seeds.Count];
    }

    // 캐릭터를 돌아가며 돌릴 때 지금 판 수로 정한 캐릭터. 다 돌았으면 null.
    private SO_HeroCharacter RotationCharacter(int runs)
    {
        int index = runs / Mathf.Max(_config.RunsPerCharacter, 1);
        return index < _rotation.Count ? _rotation[index] : null;
    }

    /// <summary>
    /// 새 판을 시작한다(GameScene.StartBattle). 손 타이머를 비운다 — 지난 판에서 남은 값으로 시작하면
    /// 같은 시드라도 첫 손 시각이 판마다 달라 처음부터 갈라진다.
    /// </summary>
    public void BeginRun()
    {
        _actTimer = 0f;
        _heroTimer = 0f;
        _hasHeroCell = false;
    }

    /// <summary>판이 끝났다. 켜져 있으면 요약에 더하고, 정한 판 수를 채웠으면 요약을 남기고 멈추고, 아니면 잠시 뒤 다시 한다.</summary>
    /// <param name="records">그 판의 웨이브 기록(WaveLog.Records).</param>
    /// <param name="waveCleared">깬 웨이브(진 웨이브 제외).</param>
    public void OnGameOver(IReadOnlyList<WaveRecord> records, int seed, int waveCleared)
    {
        if (!Enabled) return;
        SO_BotConfig.RunSet played = CurrentSet(_report.Count);
        _report.Add(records, _heroStats.Character != null ? _heroStats.Character.name : "", played != null ? played.Label : "", StagesText(), seed, waveCleared,
            // 요약 열(maze)은 예전 기록과 나란히 보려고 남긴다 — 지금은 늘 내다본 최선이다.
            "Planned");
        bool runSets = _savedStages != null;
        bool done = runSets ? CurrentSet(_report.Count) == null
            : _config.RotateCharacters ? RotationCharacter(_report.Count) == null
            : _config.BatchRuns > 0 && _report.Count >= _config.BatchRuns;
        // 다음 판의 캐릭터·단계는 다시 하기 전에 바꾼다 — 새 판이 시작될 때 기록 파일 이름·영웅 모습이 새 캐릭터로 잡히게.
        if (!done && runSets) ApplySet(_report.Count);
        else if (!done && _config.RotateCharacters) ApplyRotation(_report.Count);
        if (done)
        {
            LastReportPath = _report.Write();
            Debug.Log($"[GameBot] {_report.Count}판 요약: {LastReportPath}");
            SetEnabled(false);
            return;
        }
        _retryTimer = _config.RetryDelay;
    }

    // 요약에 남길 단계("5-6-3-5-1-2", HeroStat 순서).
    private string StagesText()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < HeroStats.StatCount; i++)
        {
            if (i > 0) builder.Append('-');
            builder.Append(_heroStats.GetStage((HeroStat)i));
        }
        return builder.ToString();
    }

    private int ChooseLevelUp(IReadOnlyList<HandCard> candidates, IReadOnlyList<int> copies)
    {
        BotChoiceContext context = new BotChoiceContext { MazeEnough = MazeEnough() };
        MapData map = _getMap();
        if (map != null && _board.Read(map, _config.KillZoneRadius))
        {
            context.HasSlow = HasZoneChip<SO_ChipCard_Slow>();
            context.HasFreeze = HasZoneChip<SO_ChipCard_Freeze>();
            context.HasVulnerable = HasZoneChip<SO_ChipCard_Vulnerable>();
            // 폴드는 미로를 어느 정도 지은 뒤(d20이 들어오기 시작할 때)에만, 한 장씩만 — 그 전에 고르면 채울 주사위가 오지 않아
            // 손에서 썩고 주사위 자리만 뺏었다(건설가: 스킬 6.0장 골라 2.5장만 씀, 주사위 22장 대 다른 캐릭터 30장+, 2026-10-29).
            context.HasFoldTarget = context.MazeEnough && !HoldsFold() && TryFindFoldTarget(map, false, out _);
            context.HasBurnSpot = HasBurnSpot();
        }
        return BotCardChoice.Choose(_config, candidates, copies, context, CurrentSet(_report.Count));
    }

    // 미로를 "어느 정도" 지었는지 — 사용자: 그 뒤로 d20을 메인 딜 구간에 모은다.
    private bool MazeEnough() => _diceField.Placed.Count >= _config.MazeEnoughDice;

    // 메인 딜 구간에 닿는 주사위 밑에 T 칩이 있는지(디버프는 종류마다 하나면 된다 — 사용자).
    private bool HasZoneChip<T>() where T : SO_ChipCard
    {
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            if (!(_chips.Get(placed[i].Cell) is T)) continue;
            if (CoversKillZone(placed[i].Cell, RangeAt(placed[i].Cell, placed[i].Dice.Colors, _chips.Get(placed[i].Cell)))) return true;
        }
        return false;
    }

    // 미로를 다 지었는지 — 목표 배치에 놓을 칸이 남지 않았으면 다 지은 것이다.
    private bool MazeBuilt() => !_planner.HasPlannedCell();

    // 다시 하기는 실제 시간으로 기다린다 — 판이 끝난 뒤라 게임 시간이 멈춰 있어도 넘어가게.
    private void Tick(float deltaTime)
    {
        if (!Enabled || _retryTimer < 0f) return;
        _retryTimer -= deltaTime;
        if (_retryTimer < 0f)
        {
            _hasHeroCell = false;
            _retry();
        }
    }

    // 게임 시간을 따른다 — 빠르게 돌려도 같은 빠르기로 손을 쓴다.
    private void GameTick(float deltaTime)
    {
        if (!Enabled || _retryTimer >= 0f) return;
        MapData map = _getMap();
        if (map == null || !_canAct() || _hand.Held != null) return;

        // 미로 계획은 손을 쓸 수 있는 걸음에서만 진행한다 — 판 주사위 연출(실제 시간) 동안 생각 시간이 흐르면
        // 첫 계획을 받는 시각이 기기마다 달라진다.
        _planner.Advance(deltaTime, _config.MazeThinkTime);
        _actTimer -= deltaTime;
        if (_actTimer <= 0f)
        {
            _actTimer = _config.ActInterval;
            PlayOneCard(map);
        }
        _heroTimer -= deltaTime;
        if (_heroTimer <= 0f)
        {
            _heroTimer = _config.HeroInterval;
            MoveHero(map);
        }
    }

    // 손패에서 쓸 수 있는 카드 한 장을 쓴다. 주사위가 먼저(미로), 그다음 칩·스킬.
    private void PlayOneCard(MapData map)
    {
        if (!_board.Read(map, _config.KillZoneRadius)) return;
        // 폴드는 채울 주사위보다 먼저 쓴다 — 주사위를 먼저 놓으면 손에 채울 것이 남지 않아 폴드를 쓸 때가 오지 않았다.
        for (int i = 0; i < _hand.Count; i++)
        {
            HandCard card = _hand.GetCard(i);
            if (!card.IsSkill || !(card.Skill is SO_SkillCard_Fold) || !TryCastFold(card.Skill)) continue;
            _hand.TryRemove(card);
            return;
        }
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < _hand.Count; i++)
            {
                HandCard card = _hand.GetCard(i);
                if (card.IsDice != (pass == 0)) continue;
                if (TryUse(map, card))
                {
                    _hand.TryRemove(card);
                    return;
                }
            }
        }
    }

    private bool TryUse(MapData map, HandCard card)
    {
        if (card.IsDice) return TryPlaceDice(map, card);
        if (card.IsChip) return TryPlaceChip(map, card);
        if (card.IsSkill) return TryCast(card.Skill);
        return false;
    }

    // d20은 메인 딜 구간 쪽, d6·d12는 미로(사용자). 생각 중(판이 바뀐 뒤 새 계획을 아직 받지 않음)이면 주사위는 쥐고 있는다 — 칩·스킬은 그대로 쓴다.
    private bool TryPlaceDice(MapData map, HandCard card)
    {
        if (_planner.Busy) return false;
        DiceCard dice = card.Dice;
        if (dice.Faces >= 20)
        {
            // 메인 딜 구간의 빈 칸 → 구간 딜링 주사위에 얹기 → 그것도 없으면 미로 계획 칸.
            if (TryFindZoneCell(map, dice, out Vector2Int zone)) return _placement.TryPlace(map, zone, card);
            if (TryFindStack(dice, true, out Vector2Int zoneStack)) return _placement.TryPlace(map, zoneStack, card);
            if (_planner.TryGetNext(null, out Vector2Int any)) return _placement.TryPlace(map, any, card);
            return TryFindOpenCell(map, dice, out Vector2Int open20) && _placement.TryPlace(map, open20, card);
        }
        // 미로를 다 지은 뒤엔 메인 딜 구간 딜링 주사위를 키운다(사용자). 구간에 얹을 곳이 없으면 어디든 가장 이득인 곳.
        if (MazeBuilt())
        {
            if (TryFindStack(dice, true, out Vector2Int stack) || TryFindStack(dice, false, out stack)) return _placement.TryPlace(map, stack, card);
            return TryFindOpenCell(map, dice, out Vector2Int openBuilt) && _placement.TryPlace(map, openBuilt, card);
        }
        // 미로 칸은 메인 딜 구간 밖부터 — 구간 칸은 d20 자리로 남긴다. 밖에 남은 계획 칸이 없으면 아무 계획 칸.
        Func<Vector2Int, bool> allow = _config.MazeOutsideZoneFirst ? IsOutsideZone : null;
        if (_planner.TryGetNext(allow, out Vector2Int cell) || _planner.TryGetNext(null, out cell))
            return _placement.TryPlace(map, cell, card);
        return TryFindOpenCell(map, dice, out Vector2Int open) && _placement.TryPlace(map, open, card);
    }

    // 계획 칸도 얹을 곳도 없을 때: 길을 막지 않는 빈 칸 중 그 색 사거리로 덮는 몰림이 가장 큰 칸.
    // 계획이 비면(벽이 바뀐 뒤 다시 찾은 계획에 남은 칸이 없음) 주사위를 영영 쥐고만 있어 판이 무너졌다(2026-10-29, 시드 70030003 — 고른 주사위 11개 중 7개만 놓음).
    private bool TryFindOpenCell(MapData map, DiceCard dice, out Vector2Int cell)
    {
        cell = default;
        SO_DiceType type = _diceConfig.GetDiceType(dice.Colors);
        float range = type != null ? type.Range : 0f;
        int best = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (map.HasChip(x, y) || DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
                int traffic = _board.Traffic(candidate, range);
                if (traffic <= best) continue;
                best = traffic;
                cell = candidate;
            }
        }
        return best > 0;
    }

    private bool IsInZone(Vector2Int cell) =>
        _board.HasKillZone && ((Vector2)(cell - _board.KillZone)).sqrMagnitude <= _config.KillZoneRadius * _config.KillZoneRadius;

    private bool IsOutsideZone(Vector2Int cell) => !IsInZone(cell);

    // d20을 놓을 메인 딜 구간의 빈 칸: 그 색 사거리로 덮는 몰림이 가장 큰 칸. 미로 계획 밖이어도 놓는다
    // (사용자는 d20을 구간에 최대한 모은다 — 길을 막지 않는지만 본다, 계획은 MazePlanner가 다시 찾는다).
    private bool TryFindZoneCell(MapData map, DiceCard dice, out Vector2Int cell)
    {
        cell = default;
        if (!_board.HasKillZone) return false;
        SO_DiceType type = _diceConfig.GetDiceType(dice.Colors);
        float range = type != null ? type.Range : 0f;
        int radius = Mathf.CeilToInt(_config.KillZoneRadius);
        float best = 0f;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int candidate = _board.KillZone + new Vector2Int(x, y);
                if (!IsInZone(candidate) || !map.InBounds(candidate.x, candidate.y)) continue;
                if (DicePlacement.Check(map, candidate) != PlacementResult.Ok) continue;
                float score = _board.Traffic(candidate, range);
                if (score <= best) continue;
                best = score;
                cell = candidate;
            }
        }
        return best > 0f;
    }

    // 얹을 주사위: 얹은 뒤의 모습으로 점수를 매긴다 — 몰림 × (1 + 기대 눈), 범위 주사위(섞인 색·하양)가 되면 더,
    // 메인 딜 구간에 닿으면 더 쳐 준다. 사용자는 메인 딜 구간의 범위 주사위를 키웠다. zoneOnly면 구간에 닿는 주사위만.
    private bool TryFindStack(DiceCard card, bool zoneOnly, out Vector2Int cell)
    {
        cell = default;
        float best = 0f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            DiceState dice = placed[i].Dice;
            if (!DiceRules.CanStack(dice, card)) continue;
            DiceState after = dice;
            DiceRules.Stack(ref after, card, (card.Faces + 1) / 2);
            if (after.Pips <= dice.Pips) continue;
            Vector2Int at = placed[i].Cell;
            float range = RangeAt(at, after.Colors, _chips.Get(at));
            if (zoneOnly && !CoversKillZone(at, range)) continue;
            float score = _board.Traffic(at, range) * (1f + after.Pips)
                * (BotDice.IsArea(after.Colors) ? _config.AreaStackWeight : 1f)
                * (CoversKillZone(at, range) ? _config.KillZoneStackWeight : 1f);
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best > 0f;
    }

    // 칩은 종류마다 자리가 다르다(사용자 판 교본 2차). 모두 칩이 없는 주사위 밑에만 놓는다(빈 칸에는 놓지 않는다).
    // - 디버프(둔화·얼림·취약): 메인 딜 구간을 가장 넓게 덮는 범위 주사위 밑 — 눈은 상관없고 맞히기만 하면 된다.
    // - 빨강·초록: 메인 딜 구간 딜링 주사위 밑(몰림 × 눈). 빨강 단색 주사위엔 초록만(빨강 칩은 겹쳐 봐야 덜 는다).
    // - 화상: 미로 쪽 범위 주사위 밑 — 아직 화상이 안 닿는 길을 가장 많이 새로 덮는 곳 × 공격 속도(4초 안에 다시 맞혀야 끊기지 않는다).
    // - 사거리: 받으면 메인 딜 구간에 새로 닿는 주사위.
    private bool TryPlaceChip(MapData map, HandCard card)
    {
        SO_ChipCard chip = card.Chip;
        Vector2Int target;
        bool found;
        if (chip is SO_ChipCard_Burn) found = TryFindBurnCell(out target);
        else if (chip is SO_ChipCard_Slow || chip is SO_ChipCard_Freeze || chip is SO_ChipCard_Vulnerable) found = TryFindDebuffCell(out target);
        else if (chip is SO_ChipCard_Damage || chip is SO_ChipCard_AttackSpeed) found = TryFindBoostCell(chip, out target);
        else found = TryFindRangeCell(chip, out target);
        return found && _placement.TryPlace(map, target, card);
    }

    // 디버프: 메인 딜 구간에 닿는 범위 주사위 > 구간에 닿는 주사위 > 그 밖. 같은 등급이면 덮는 몰림이 큰 곳.
    private bool TryFindDebuffCell(out Vector2Int cell)
    {
        cell = default;
        int bestTier = -1;
        float best = -1f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            if (_chips.Get(at) != null) continue;
            float range = RangeAt(at, placed[i].Dice.Colors, null);
            int tier = CoversKillZone(at, range) ? (BotDice.IsArea(placed[i].Dice.Colors) ? 2 : 1) : 0;
            float score = _board.Traffic(at, range);
            if (tier < bestTier || (tier == bestTier && score <= best)) continue;
            bestTier = tier;
            best = score;
            cell = at;
        }
        return bestTier >= 0;
    }

    // 빨강·초록: 메인 딜 구간에 닿는 주사위 > 그 밖. 같은 등급이면 몰림 × (1 + 눈), 범위 주사위면 더.
    private bool TryFindBoostCell(SO_ChipCard chip, out Vector2Int cell)
    {
        cell = default;
        int bestTier = -1;
        float best = -1f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            DiceState dice = placed[i].Dice;
            if (_chips.Get(at) != null) continue;
            if (chip is SO_ChipCard_Damage && dice.Colors == DiceColor.Red) continue;
            float range = RangeAt(at, dice.Colors, null);
            int tier = CoversKillZone(at, range) ? 1 : 0;
            float score = _board.Traffic(at, range) * (1f + dice.Pips) * (BotDice.IsArea(dice.Colors) ? _config.AreaStackWeight : 1f);
            if (tier < bestTier || (tier == bestTier && score <= best)) continue;
            bestTier = tier;
            best = score;
            cell = at;
        }
        return bestTier >= 0;
    }

    // 화상: 범위 주사위 > 단일, 메인 딜 구간 밖 > 안(사용자: 화상은 미로 쪽 — 구간에선 중요하지 않다).
    // 같은 등급이면 아직 화상 주사위가 안 덮는 길 칸 수 × 공격 속도 — 화상 칩이 여럿이면 길 전체에 고르게 퍼진다.
    private bool TryFindBurnCell(out Vector2Int cell)
    {
        cell = default;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        _burned.Clear();
        for (int i = 0; i < placed.Count; i++)
        {
            if (!(_chips.Get(placed[i].Cell) is SO_ChipCard_Burn)) continue;
            float range = RangeAt(placed[i].Cell, placed[i].Dice.Colors, _chips.Get(placed[i].Cell));
            MarkRoute(placed[i].Cell, range, _burned);
        }

        int bestTier = -1;
        float best = -1f;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            DiceState dice = placed[i].Dice;
            if (_chips.Get(at) != null) continue;
            float range = RangeAt(at, dice.Colors, null);
            int tier = (BotDice.IsArea(dice.Colors) ? 2 : 0) + (CoversKillZone(at, range) ? 0 : 1);
            DiceStats stats = DiceStats.Compute(dice, _diceConfig.GetDiceType(dice.Colors), _statsConfig);
            float speed = stats.AttackInterval > 0f ? 1f / stats.AttackInterval : 0f;
            float score = NewRouteCells(at, range, _burned) * speed;
            if (tier < bestTier || (tier == bestTier && score <= best)) continue;
            bestTier = tier;
            best = score;
            cell = at;
        }
        return bestTier >= 0;
    }

    private readonly HashSet<Vector2Int> _burned = new HashSet<Vector2Int>();

    // 화상 칩을 깔 자리: 칩 없는 범위 주사위 중 아직 화상 주사위가 안 덮는 길을 새로 덮는 것이 있다.
    private bool HasBurnSpot()
    {
        return TryFindBurnCell(out Vector2Int cell) && _diceField.TryGetPlaced(cell, out PlacedDice placed)
            && BotDice.IsArea(placed.Dice.Colors) && NewRouteCells(cell, RangeAt(cell, placed.Dice.Colors, null), _burned) > 0;
    }

    private void MarkRoute(Vector2Int center, float range, HashSet<Vector2Int> marked)
    {
        float sqr = range * range;
        IReadOnlyList<Vector2Int> route = _board.Route;
        for (int i = 0; i < route.Count; i++)
            if (((Vector2)(route[i] - center)).sqrMagnitude <= sqr) marked.Add(route[i]);
    }

    private int NewRouteCells(Vector2Int center, float range, HashSet<Vector2Int> marked)
    {
        float sqr = range * range;
        int count = 0;
        IReadOnlyList<Vector2Int> route = _board.Route;
        for (int i = 0; i < route.Count; i++)
            if (!marked.Contains(route[i]) && ((Vector2)(route[i] - center)).sqrMagnitude <= sqr) count++;
        return count;
    }

    // 사거리: 칩을 받으면 메인 딜 구간에 새로 닿는 주사위를 가장 크게 쳐 준다 — 사거리로 구간을 때리게 유도한다.
    private bool TryFindRangeCell(SO_ChipCard chip, out Vector2Int cell)
    {
        cell = default;
        float bonus = ChipEffects.RangeBonus(chip);
        float best = -1f;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int at = placed[i].Cell;
            if (_chips.Get(at) != null) continue;
            float range = RangeAt(at, placed[i].Dice.Colors, null);
            float boosted = range * (1f + bonus);
            float score = _board.Traffic(at, boosted) * (1f + placed[i].Dice.Pips);
            if (CoversKillZone(at, boosted))
                score *= !CoversKillZone(at, range) ? _config.KillZoneChipWeight * 2f : _config.KillZoneChipWeight;
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best >= 0f;
    }

    // 스킬은 아껴 둔다 — 메인 딜 구간이 뚫릴 것 같을 때(또는 목표물 바로 앞까지 왔을 때)만 쓴다.
    // 막 쓰면 정작 뚫릴 때 손에 없다(사용자 지적: 미로에선 적이 늘 몰려 있어 "몰렸을 때"는 기준이 못 된다).
    private bool TryCast(SO_SkillCard skill)
    {
        if (skill is SO_SkillCard_AllIn) return TryCastAllIn(skill);
        if (skill is SO_SkillCard_Fold) return TryCastFold(skill);
        if (skill is SO_SkillCard_Overdrive overdrive) return TryCastOverdrive(overdrive);
        if (skill.Target == SkillTarget.Dice) return TryCastOnDice(skill);
        SO_BotConfig.RunSet set = CurrentSet(_report.Count);
        if (set != null && set.DamageSkillAnytime && skill is SO_AreaSkillCard anytime) return TryCastAnytime(anytime);
        if (!Breached(out bool bossBreaching)) return false;
        // 체크메이트: 보스가 뚫으려 하면 바로, 아니면 다른 적 대상 스킬처럼 떼가 넘어올 때.
        if (skill is SO_SkillCard_Checkmate && bossBreaching) return TryCastAt((SO_AreaSkillCard)skill);
        if (skill is SO_SkillCard_Check check) return TryCastCheck(check);
        if (skill is SO_AreaSkillCard area) return TryCastArea(area);
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

    // 폴드(건설가): 메인 딜 구간을 막는 칸을 치워 딜링 주사위 자리를 넓힌다(사용자: 칩·실수로 못 쓰게 된 딜 구간을 넓힌다).
    // 손에 그 자리를 채울 d20·범위 주사위가 있을 때만 — 치우기만 하고 못 채우면 미로만 허문다.
    private bool TryCastFold(SO_SkillCard card)
    {
        MapData map = _getMap();
        if (map == null || !TryFindFoldTarget(map, true, out Vector2Int cell)) return false;
        _skills.CastOnCell(cell, card);
        return true;
    }

    /// <summary>
    /// 폴드로 치울 칸: 메인 딜 구간 안의 칩만 있는 칸(시작 칩 등), 그다음 미로용 단일 주사위(d6·d12 단색·검정). 덮는 몰림이 큰 곳부터.
    /// needFiller면 손에 그 자리를 채울 d20·범위 주사위가 있어야 한다.
    /// </summary>
    private bool TryFindFoldTarget(MapData map, bool needFiller, out Vector2Int cell)
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

    private bool HoldsFold()
    {
        for (int i = 0; i < _hand.Count; i++)
        {
            HandCard card = _hand.GetCard(i);
            if (card.IsSkill && card.Skill is SO_SkillCard_Fold) return true;
        }
        return false;
    }

    // 적 대상 스킬은 판 전체라 놓는 자리는 레이크의 모음점만 정한다 — 메인 딜 구간 한가운데로 모은다(넘어온 적을 구간으로 되돌린다).
    // 구간이 없으면 판 한가운데.
    private bool TryCastAt(SO_AreaSkillCard card)
    {
        MapData map = _getMap();
        if (map == null) return false;
        Vector2 point = _board.HasKillZone ? (Vector2)_board.KillZone : new Vector2(map.Width / 2, map.Height / 2);
        if (!_skills.CanCast(card, point)) return false;
        _skills.Cast(card, point);
        return true;
    }

    // 메인 딜 구간을 넘어온 적이 몇 마리 이상일 때.
    private bool TryCastArea(SO_AreaSkillCard card) =>
        CountPastKillZone() >= _config.BreachAreaMinTargets && TryCastAt(card);

    // 데미지형 위주 묶음: 뚫릴 때를 기다리지 않는다. 체크메이트는 보스가 있으면, 나머지는 판 위 적이 몇 마리 이상이면.
    private bool TryCastAnytime(SO_AreaSkillCard card)
    {
        if (card is SO_SkillCard_Checkmate) return HasBoss() && TryCastAt(card);
        return _skills.CollectTargets(card, _targets) >= _config.AnytimeAreaMinTargets && TryCastAt(card);
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

    // 영웅(사용자 판 교본 2차):
    // 1) 빠져나갈 적을 오버드라이브로 잡을 수 있으면 그 주사위에 올라탄다(BotEscape) — 올라탄 동안 눈이 영구로 깎이니 그때만.
    // 2) 미로를 짓는 동안엔 주사위가 못 잡은 적을 쫓아 정리한다(영웅 디버프 카드가 있으면 안 묻은 적이 몰린 곳으로).
    // 3) 미로를 지은 뒤엔 메인 딜 구간 앞부터 디버프가 묻는 구역까지 오가며 싸운다 — 구역을 지난(뚫린) 적이 있으면 그쪽부터,
    //    없으면 구역 안에서 가장 앞선 적, 적이 없으면 구역 앞에서 기다린다. 영웅이 주 딜이라 취약(+30%)이 묻는 곳에서 때려야 한다.
    private void MoveHero(MapData map)
    {
        if (_hero.Held || !_board.Read(map, _config.KillZoneRadius)) return;
        SO_SkillCard_Overdrive overdrive = _heroOverdrive();
        if (overdrive != null && FindOverdriveTarget(overdrive.DamageScale, overdrive.SpeedScale, float.MaxValue, false, out Vector2Int ride))
        {
            if (_hasHeroCell && ride == _heroCell) return;
            if (_hero.MoveTo(ride))
            {
                _hasHeroCell = true;
                _heroCell = ride;
            }
            return;
        }

        if (!MazeBuilt())
        {
            if (_heroStats.DebuffCount > 0 && TryFindPaintCell(out Vector2Int paint)) MoveHeroTo(paint);
            else MoveHeroTo(TryFindLead(0, int.MaxValue, out Vector2Int lead) ? lead : ZoneFrontCell(map));
            return;
        }

        int back = _board.KillZoneDistance - _config.KiteBack;
        int front = _board.KillZoneDistance + _config.KiteFront;
        if (TryFindLead(0, back - 1, out Vector2Int breach)) MoveHeroTo(breach);
        else if (TryFindLead(back, front, out Vector2Int kite)) MoveHeroTo(kite);
        else MoveHeroTo(ZoneFrontCell(map));
    }

    // 주사위에서 내려 cell로 간다(길 칸이라 주사위가 없다).
    private void MoveHeroTo(Vector2Int cell)
    {
        if (_hero.MoveTo(cell)) _hasHeroCell = false;
    }

    // 목표물까지 길 거리가 [min, max]인 산 적 중 가장 앞선(목표물에 가까운) 적이 선 칸. 주사위 칸에 선 적은 뺀다(올라타지 않게).
    private bool TryFindLead(int min, int max, out Vector2Int cell)
    {
        cell = default;
        int lead = int.MaxValue;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            Vector2Int at = Vector2Int.RoundToInt(states[i].Position);
            int distance = _board.DistanceAt(at);
            if (distance < min || distance > max || distance >= lead || _diceField.TryGetPlaced(at, out _)) continue;
            lead = distance;
            cell = at;
        }
        return lead != int.MaxValue;
    }

    // 메인 딜 구간 앞(출현 쪽으로 KiteFront칸)의 길 칸 — 적이 없을 때 기다리는 곳. 구간이 없으면 목표물 앞.
    private Vector2Int ZoneFrontCell(MapData map)
    {
        if (!_board.HasKillZone) return new Vector2Int(map.GoalRect.xMin, map.GoalRect.yMin);
        int target = _board.KillZoneDistance + _config.KiteFront;
        Vector2Int best = _board.KillZone;
        int bestGap = int.MaxValue;
        IReadOnlyList<Vector2Int> route = _board.Route;
        for (int i = 0; i < route.Count; i++)
        {
            int gap = Mathf.Abs(_board.DistanceAt(route[i]) - target);
            if (gap >= bestGap) continue;
            bestGap = gap;
            best = route[i];
        }
        return best;
    }

    // 영웅이 서면 사거리 안에 디버프가 안 묻은 적이 가장 많이 드는 적의 칸(주사위 없는 칸). PaintMinTargets보다 적으면 false.
    // 후보를 적이 선 칸으로만 잡는다 — 적이 있는 곳 둘레가 곧 묻힐 곳이라 판 전체 칸을 훑지 않아도 된다.
    private bool TryFindPaintCell(out Vector2Int cell)
    {
        cell = default;
        float rangeSqr = _heroStats.Range * _heroStats.Range;
        MonsterState[] states = _monsters.States;
        int best = _config.PaintMinTargets - 1;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || !NeedsHeroDebuff(states[i])) continue;
            Vector2Int candidate = Vector2Int.RoundToInt(states[i].Position);
            if (_board.DistanceAt(candidate) < 0 || _diceField.TryGetPlaced(candidate, out _)) continue;
            int count = 0;
            for (int j = 0; j < _monsters.Count; j++)
            {
                if (!states[j].IsAlive || !NeedsHeroDebuff(states[j])) continue;
                if ((states[j].Position - states[i].Position).sqrMagnitude <= rangeSqr) count++;
            }
            if (count <= best) continue;
            best = count;
            cell = candidate;
        }
        return best >= _config.PaintMinTargets;
    }

    // 영웅 디버프 중 하나라도 안 묻어 있는지(칩이 건 같은 디버프도 묻은 것으로 본다 — 센 쪽이 남으니 덧칠은 낭비에 가깝다).
    private bool NeedsHeroDebuff(in MonsterState state)
    {
        for (int d = 0; d < _heroStats.DebuffCount; d++)
        {
            switch (_heroStats.GetDebuff(d, out _).Debuff)
            {
                case HeroDebuff.Vulnerable: if (!state.IsVulnerable) return true; break;
                case HeroDebuff.Slow: if (!state.IsSlowed) return true; break;
                case HeroDebuff.Freeze: if (!state.IsFrozen) return true; break;
                case HeroDebuff.Burn: if (!state.IsPoisoned) return true; break;
            }
        }
        return false;
    }

    /// <summary>
    /// 메인 딜 구간이 뚫릴 것 같은지: 구간을 넘어온 적 중 체력이 넉넉히 남은 적이 BreachCount 이상이거나, 보스가 구간에 들어왔거나,
    /// 목표물 바로 앞(DangerRadius)까지 적이 왔다.
    /// </summary>
    private bool Breached(out bool bossBreaching)
    {
        bossBreaching = false;
        if (!_board.HasKillZone) return false;
        int strong = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            MonsterKind kind = _monsters.KindOf(i);
            if (kind.IsBoss && !kind.IsPiece && IsPastKillZone(states[i].Position, _config.KillZoneMargin)) bossBreaching = true;
            if (IsPastKillZone(states[i].Position, 0) && states[i].Health >= states[i].MaxHealth * _config.BreachHealthRatio) strong++;
        }
        return bossBreaching || strong >= _config.BreachCount || CountNearGoal(_config.DangerRadius) > 0;
    }

    // 목표물까지 길 거리가 메인 딜 구간 중심 + margin보다 가깝다 — 구간에 들어왔거나 넘어왔다(margin이 클수록 일찍 본다).
    private bool IsPastKillZone(Vector2 position, int margin)
    {
        int distance = _board.DistanceAt(Vector2Int.RoundToInt(position));
        return distance >= 0 && distance <= _board.KillZoneDistance + margin;
    }

    private int CountPastKillZone()
    {
        int count = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && IsPastKillZone(states[i].Position, 0)) count++;
        return count;
    }

    private bool CoversKillZone(Vector2Int cell, float range) =>
        _board.HasKillZone && ((Vector2)(_board.KillZone - cell)).sqrMagnitude <= range * range;

    // 목표물 중심에서 radius 안의 산 적 수.
    private int CountNearGoal(float radius)
    {
        MapData map = _getMap();
        if (map == null) return 0;
        Vector2 goal = map.GoalRect.center - new Vector2(0.5f, 0.5f);
        float sqr = radius * radius;
        int count = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && (states[i].Position - goal).sqrMagnitude <= sqr) count++;
        return count;
    }

    // 칸의 주사위가 colors일 때의 사거리(칩 보너스 포함 — 실제 공격과 같은 계산, ChipEffects.RangeBonus).
    private float RangeAt(Vector2Int cell, DiceColor colors, SO_ChipCard chip)
    {
        SO_DiceType type = _diceConfig.GetDiceType(colors);
        return type != null ? type.Range * (1f + ChipEffects.RangeBonus(chip)) : 0f;
    }
}
