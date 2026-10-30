using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 플레이 봇(기획서 11.2·11.5). 사람과 같은 게임 행동만 코드로 부른다 — 레벨업 고르기(LevelUpChoice.AutoChoose),
/// 손패 카드 놓기(CardPlacement)·스킬 쓰기(SkillCaster), 영웅 이동(Hero.MoveTo). 판이 끝나면 다시 하기를 누르고 측정 기록(WaveLog CSV)이 남는다.
/// 규칙형이라 사람보다 서툴다 — "봇이 몇 웨이브까지 가는지"를 기준선으로 보고, 카드·캐릭터에 따른 차이를 비교하는 데 쓴다.
/// 미로는 판 전체를 탐색한 목표 배치(MazePlanner)대로 짓는다.
/// 판단은 사용자 판 교본 2차(2026-10-28, 기획서 11.4)를 옮겼다 — 판을 미로와 메인 딜 구간(BotBoard.KillZone)으로 나눈다:
/// 미로는 d6·d12와 화상 칩(체력 비례 피해라 싼 주사위로도 후반까지 들어간다), 메인 딜 구간은 d20·범위 주사위와 디버프(종류마다 하나)·빨강·초록 칩.
/// 영웅은 미로를 짓는 동안 남은 적을 쫓아 정리하고, 지은 뒤엔 메인 딜 구간에서 오가며 싸운다.
/// 레이즈는 빠져나갈 적을 걸면 잡을 수 있을 때만(BotEscape). 적 대상 스킬·체크는 구간이 뚫릴 때만(Breached).
/// 판단 값은 SO_BotConfig. 정한 판 수를 돌면 요약(BotBatchReport)을 남기고 멈춘다. 캐릭터를 돌아가며 돌릴 수도 있다(RotateCharacters).
/// 비교 묶음(RunSets — 능력치 단계·카드 성향)마다 같은 맵(Seeds)으로 돌릴 수도 있다. 단계는 세이브와 이어져 있어(MetaProfile) 도는 동안은 세이브에도 그 단계가 남으므로,
/// 끝나거나 끌 때 켤 때의 단계로 되돌린다.
///
/// 여기는 켜기·판 반복·손 순서이고, 판단은 카드 종류별 partial 파일(Dice·Chips·Skills·Hero)과 판 읽기(Board)에 있다.
/// </summary>
public sealed partial class GameBot : IDisposable
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
    // 영웅이 올라탄 주사위의 레이즈 배율(SO_HeroConfig.Raise — 스킬 카드와 같은 값).
    private readonly Func<SO_SkillCard_Raise> _heroRaise;
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


    /// <summary>마지막으로 남긴 요약 파일 경로(없으면 null).</summary>
    public string LastReportPath { get; private set; }

    /// <param name="getWave">지금 웨이브(레벨업 점수 구간).</param>
    /// <param name="canAct">판이 돌고 있어 카드를 쓸 수 있는지(로비·판 주사위 연출·게임 오버가 아님).</param>
    /// <param name="retry">결과 화면의 다시 하기와 같은 길.</param>
    /// <param name="characters">캐릭터를 돌아가며 돌릴 때의 순서.</param>
    public GameBot(SO_BotConfig config, Func<MapData> getMap, Func<int> getWave, HeroStats heroStats, IReadOnlyList<SO_HeroCharacter> characters,
        DiceField diceField, ChipField chips, CardHand hand, CardPlacement placement, SkillCaster skills, MonsterField monsters, Hero hero,
        SO_DiceConfig diceConfig, SO_DiceStatsConfig statsConfig, Func<SO_SkillCard_Raise> heroRaise, LevelUpChoice levelUp,
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
        _heroRaise = heroRaise;
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
            // 캐시 아웃는 미로를 어느 정도 지은 뒤(d20이 들어오기 시작할 때)에만, 한 장씩만 — 그 전에 고르면 채울 주사위가 오지 않아
            // 손에서 썩고 주사위 자리만 뺏었다(하이 롤러: 스킬 6.0장 골라 2.5장만 씀, 주사위 22장 대 다른 캐릭터 30장+, 2026-10-29).
            context.HasCashOutTarget = context.MazeEnough && !HoldsCashOut() && TryFindCashOutTarget(map, false, out _);
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
        // 캐시 아웃는 채울 주사위보다 먼저 쓴다 — 주사위를 먼저 놓으면 손에 채울 것이 남지 않아 캐시 아웃를 쓸 때가 오지 않았다.
        for (int i = 0; i < _hand.Count; i++)
        {
            HandCard card = _hand.GetCard(i);
            if (!card.IsSkill || !(card.Skill is SO_SkillCard_CashOut) || !TryCastCashOut(card.Skill)) continue;
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
        // 레벨업 밖(코인 플립)에서 들어온 영웅 강화 카드는 바로 쓴다 — 아껴 둘 이유가 없다.
        if (card.IsHero)
        {
            _skills.UseHeroCard(card.Hero);
            return true;
        }
        return false;
    }
}
