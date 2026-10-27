using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 플레이 봇(기획서 11.2·11.5). 사람과 같은 게임 행동만 코드로 부른다 — 레벨업 고르기(LevelUpChoice.AutoChoose),
/// 손패 카드 놓기(CardPlacement)·스킬 쓰기(SkillCaster), 영웅 이동(Hero.MoveTo). 판이 끝나면 다시 하기를 누르고 측정 기록(WaveLog CSV)이 남는다.
/// 규칙형이라 사람보다 서툴다 — "봇이 몇 웨이브까지 가는지"를 기준선으로 보고, 카드·캐릭터에 따른 차이를 비교하는 데 쓴다.
/// 미로는 판 전체를 탐색한 목표 배치(MazePlanner)대로 짓는다.
/// 미로 다음 판단은 사용자 플레이를 옮겼다(2026-10-27) — 모든 판단이 메인 딜 구간(BotBoard.KillZone)을 중심으로 돈다:
/// 범위 주사위·칩·사거리를 그 구간으로 모으고, 적이 그 구간을 뚫을 것 같을 때(Breached)만 영웅이 그 구간 주사위에 올라타고 스킬을 쓴다.
/// 평소 영웅은 주사위에서 내려 가장 앞선 적을 쫓아가 직접 때린다(올라탄 동안 눈이 영구로 깎인다).
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
    private readonly LevelUpChoice _levelUp;
    private readonly GameSpeed _speed;
    private readonly Func<bool> _canAct;
    private readonly Action _retry;
    private readonly MazePlanner _planner;
    private readonly BotBoard _board = new BotBoard();
    private readonly BotBatchReport _report = new BotBatchReport();
    private readonly List<int> _targets = new List<int>();

    // 비교 묶음으로 도는 동안 켤 때의 단계(되돌릴 값). 묶음으로 돌지 않으면 null.
    private int[] _savedStages;

    private float _actTimer;
    private float _heroTimer;
    private float _retryTimer = -1f;
    private bool _hasHeroCell;
    private Vector2Int _heroCell;

    /// <summary>켜져 있는지. 켜면 게임을 빠르게 돌리고 레벨업을 대신 고른다.</summary>
    public bool Enabled { get; private set; }


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
        SO_DiceConfig diceConfig, LevelUpChoice levelUp, GameSpeed speed, MazePlanner planner, Func<bool> canAct, Action retry)
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

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        _levelUp.AutoChoose = enabled ? ChooseLevelUp : (Func<IReadOnlyList<HandCard>, IReadOnlyList<int>, int>)null;
        _speed.FastForward = enabled ? _config.FastForward : 1f;
        _retryTimer = -1f;
        _hasHeroCell = false;
        if (enabled) _report.Clear();
        UpdateSearching();
        if (!enabled)
        {
            RestoreStages();
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
        for (int i = 0; i < _characters.Count; i++)
            if (_characters[i] != null && _characters[i] != _heroStats.Character) _rotation.Add(_characters[i]);
        // 켜는 판은 지금 캐릭터라, 첫 캐릭터로 바꾸고 바로 새 판을 시작한다.
        if (_rotation.Count == 0) return;
        _heroStats.SetCharacter(_rotation[0]);
        _retry();
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

    // 내다본 최선으로 돌 때만 계획을 찾게 한다(꺼져 있으면 사람이 놓을 때 렉만 생긴다).
    // 어시스트가 생기면 둘 중 하나라도 쓰면 켜지도록 바꾼다.
    private void UpdateSearching() => _planner.Searching = Enabled;

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
        else if (!done && _config.RotateCharacters) _heroStats.SetCharacter(RotationCharacter(_report.Count));
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

    private int ChooseLevelUp(IReadOnlyList<HandCard> candidates, IReadOnlyList<int> copies) =>
        BotCardChoice.Choose(_config, Mathf.Max(_getWave(), 1), candidates, copies, MazeBuilt(), CurrentSet(_report.Count));

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

    private bool TryPlaceDice(MapData map, HandCard card)
    {
        if (MazeBuilt() && TryFindStack(card.Dice, out Vector2Int stack))
            return _placement.TryPlace(map, stack, card);
        return _planner.TryGetNext(out Vector2Int cell) && _placement.TryPlace(map, cell, card);
    }

    // 얹을 주사위: 얹은 뒤의 모습으로 점수를 매긴다 — 몰림 × (1 + 기대 눈), 범위 주사위(섞인 색·하양)가 되면 더,
    // 메인 딜 구간에 닿으면 더 쳐 준다. 사용자는 메인 딜 구간의 범위 주사위를 키웠다.
    private bool TryFindStack(DiceCard card, out Vector2Int cell)
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
            float score = _board.Traffic(at, range) * (1f + after.Pips)
                * (IsArea(after.Colors) ? _config.AreaStackWeight : 1f)
                * (CoversKillZone(at, range) ? _config.KillZoneStackWeight : 1f);
            if (score <= best) continue;
            best = score;
            cell = at;
        }
        return best > 0f;
    }

    // 칩: 메인 딜 구간에 닿는 주사위 밑을 크게 쳐 준다. 디버프(취약·둔화·얼림·화상)는 몰림만, 강화(공격력·공속)는 몰림 × 눈.
    // 사거리 칩은 칩을 받으면 메인 딜 구간에 새로 닿는 주사위를 가장 크게 쳐 준다 — 사거리로 구간을 때리게 유도한다.
    private bool TryPlaceChip(MapData map, HandCard card)
    {
        SO_ChipCard chip = card.Chip;
        bool debuff = IsDebuff(chip);
        float bonus = ChipEffects.RangeBonus(chip);
        float best = -1f;
        Vector2Int target = default;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        for (int i = 0; i < placed.Count; i++)
        {
            Vector2Int cell = placed[i].Cell;
            if (_chips.Get(cell) != null) continue;
            float range = RangeAt(cell, placed[i].Dice.Colors, null);
            float boosted = range * (1f + bonus);
            float traffic = _board.Traffic(cell, boosted);
            float score = debuff ? traffic : traffic * (1f + placed[i].Dice.Pips);
            if (CoversKillZone(cell, boosted))
                score *= bonus > 0f && !CoversKillZone(cell, range) ? _config.KillZoneChipWeight * 2f : _config.KillZoneChipWeight;
            if (score <= best) continue;
            best = score;
            target = cell;
        }
        return best >= 0f && _placement.TryPlace(map, target, card);
    }

    private static bool IsDebuff(SO_ChipCard chip) =>
        chip is SO_ChipCard_Vulnerable || chip is SO_ChipCard_Slow || chip is SO_ChipCard_Freeze || chip is SO_ChipCard_Burn;

    // 스킬은 아껴 둔다 — 메인 딜 구간이 뚫릴 것 같을 때(또는 목표물 바로 앞까지 왔을 때)만 쓴다.
    // 막 쓰면 정작 뚫릴 때 손에 없다(사용자 지적: 미로에선 적이 늘 몰려 있어 "몰렸을 때"는 기준이 못 된다).
    private bool TryCast(SO_SkillCard skill)
    {
        if (skill is SO_SkillCard_AllIn) return TryCastAllIn(skill);
        if (skill.Target == SkillTarget.Dice && !(skill is SO_SkillCard_Overdrive)) return TryCastOnDice(skill);
        SO_BotConfig.RunSet set = CurrentSet(_report.Count);
        if (set != null && set.DamageSkillAnytime && skill is SO_AreaSkillCard anytime) return TryCastAnytime(anytime);
        if (!Breached(out bool bossBreaching)) return false;
        // 체크메이트: 보스가 뚫으려 하면 보스에게, 아니면 다른 범위 스킬처럼 떼에.
        if (skill is SO_SkillCard_Checkmate checkmate && bossBreaching && TryCastCheckmate(checkmate)) return true;
        if (skill is SO_AreaSkillCard area) return TryCastArea(area);
        if (skill is SO_SkillCard_Check) return TryCastCheck(skill);
        if (skill is SO_SkillCard_Overdrive) return TryCastOnDice(skill);
        // 폴드는 쓰지 않는다(미로를 허문다).
        return false;
    }

    // 메인 딜 구간을 넘어왔거나 넘어오려는 적 중 가장 많이 모인 곳에 쓴다.
    private bool TryCastArea(SO_AreaSkillCard card)
    {
        MonsterState[] states = _monsters.States;
        int bestCount = 0;
        Vector2 bestCenter = default;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || !IsPastKillZone(states[i].Position, 0)) continue;
            Vector2 center = states[i].Position;
            int count = _skills.CollectTargets(card, center, _targets);
            if (count <= bestCount) continue;
            bestCount = count;
            bestCenter = center;
        }
        if (bestCount < _config.BreachAreaMinTargets || !_skills.CanCast(card, bestCenter)) return false;
        _skills.Cast(card, bestCenter);
        return true;
    }

    // 데미지형 위주 묶음: 뚫릴 때를 기다리지 않는다. 체크메이트는 보스가 있으면 보스에게, 없으면(나머지도) 판 어디든 가장 많이 모인 곳에.
    private bool TryCastAnytime(SO_AreaSkillCard card)
    {
        MonsterState[] states = _monsters.States;
        if (card is SO_SkillCard_Checkmate)
        {
            for (int i = 0; i < _monsters.Count; i++)
            {
                if (!states[i].IsAlive || !_monsters.KindOf(i).IsBoss || !_skills.CanCast(card, states[i].Position)) continue;
                _skills.Cast(card, states[i].Position);
                return true;
            }
        }
        int bestCount = 0;
        Vector2 bestCenter = default;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            int count = _skills.CollectTargets(card, states[i].Position, _targets);
            if (count <= bestCount) continue;
            bestCount = count;
            bestCenter = states[i].Position;
        }
        if (bestCount < _config.AnytimeAreaMinTargets || !_skills.CanCast(card, bestCenter)) return false;
        _skills.Cast(card, bestCenter);
        return true;
    }

    // 체크메이트는 구간을 뚫으려는 보스에게.
    private bool TryCastCheckmate(SO_SkillCard_Checkmate card)
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive || !_monsters.KindOf(i).IsBoss || !IsPastKillZone(states[i].Position, _config.KillZoneMargin)) continue;
            if (!_skills.CanCast(card, states[i].Position)) continue;
            _skills.Cast(card, states[i].Position);
            return true;
        }
        return false;
    }

    // 체크는 구간을 넘어온 적이 많을 때 — 멈춘 사이 구간 주사위가 마저 깎는다.
    private bool TryCastCheck(SO_SkillCard card)
    {
        if (CountPastKillZone() < _config.CheckBreachCount || !_skills.CanCastBoard(card)) return false;
        _skills.CastBoard(card);
        return true;
    }

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
        // 오버드라이브(뚫릴 때만 부른다): 메인 딜 구간 주사위 중 지금 사거리 안 적이 많은 곳. 영웅이 올라탄 주사위는 이미 오버드라이브다.
        if (card is SO_SkillCard_Overdrive)
        {
            if (_hasHeroCell && cell == _heroCell) return 0f;
            return HeroScore(cell);
        }
        if (card is SO_SkillCard_Jackpot) return traffic * missing;
        // 다시 굴리기는 눈이 절반 밑일 때만 — 기대값이 지금보다 높다.
        if (card is SO_SkillCard_Reroll) return dice.Pips * 2 < dice.Faces ? traffic * missing : 0f;
        // 페인트는 섞인 색(등급이 낮은) 주사위를 단색으로 되돌릴 때만.
        if (card is SO_SkillCard_Paint) return ColorCount(dice.Colors) >= 2 ? traffic : 0f;
        return 0f;
    }

    // 영웅: 메인 딜 구간이 뚫릴 것 같을 때만 그 구간의 주사위에 올라탄다(공격·디버프 칩이 모인 곳이라 뒤쪽 주사위보다 세다).
    // 아니면 주사위에서 내려 가장 앞선 적을 쫓아가 직접 때린다. 적이 없으면 메인 딜 구간에서 기다린다.
    private void MoveHero(MapData map)
    {
        if (_hero.Held || !_board.Read(map, _config.KillZoneRadius)) return;
        if (Breached(out _))
        {
            float current = _hasHeroCell ? HeroScore(_heroCell) : 0f;
            float best = 0f;
            Vector2Int target = default;
            IReadOnlyList<PlacedDice> placed = _diceField.Placed;
            for (int i = 0; i < placed.Count; i++)
            {
                float score = HeroScore(placed[i].Cell);
                if (score <= best) continue;
                best = score;
                target = placed[i].Cell;
            }
            if (best <= 0f || (_hasHeroCell && (target == _heroCell || best <= current * _config.HeroSwitchGain))) return;
            if (_hero.MoveTo(target))
            {
                _hasHeroCell = true;
                _heroCell = target;
            }
            return;
        }

        // 디버프 카드가 있으면 디버프가 안 묻은 적이 몰린 곳으로 다니며 묻힌다. 그런 곳이 없으면 아래(선두 쫓기)로.
        if (_heroStats.DebuffCount > 0 && TryFindPaintCell(out Vector2Int paint))
        {
            if (_hero.MoveTo(paint)) _hasHeroCell = false;
            return;
        }

        // 쫓을 곳: 목표물에 가장 가까운(길 거리) 적이 선 칸 — 길 칸이라 주사위가 없어 올라타지 않는다.
        Vector2Int chase = _board.HasKillZone ? _board.KillZone : new Vector2Int(map.GoalRect.xMin, map.GoalRect.yMin);
        int lead = int.MaxValue;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            Vector2Int cell = Vector2Int.RoundToInt(states[i].Position);
            int distance = _board.DistanceAt(cell);
            if (distance < 0 || distance >= lead || _diceField.TryGetPlaced(cell, out _)) continue;
            lead = distance;
            chase = cell;
        }
        if (_hero.MoveTo(chase)) _hasHeroCell = false;
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

    // 메인 딜 구간 주사위일수록, 지금 사거리 안 적이 많을수록, 눈이 높을수록.
    private float HeroScore(Vector2Int cell)
    {
        if (!_diceField.TryGetPlaced(cell, out PlacedDice placed) || placed.Dice.IsSpent) return 0f;
        float range = RangeAt(cell, placed.Dice.Colors, _chips.Get(cell));
        float score = CountInRange(cell, range) * placed.Dice.Pips;
        return CoversKillZone(cell, range) ? score * _config.KillZoneStackWeight : score;
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

    // cell 주사위 사거리 안의 산 적 수.
    private int CountInRange(Vector2Int cell, float range)
    {
        float sqr = range * range;
        int count = 0;
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
            if (states[i].IsAlive && (states[i].Position - (Vector2)cell).sqrMagnitude <= sqr) count++;
        return count;
    }

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

    // 범위 주사위 = 색이 둘 이상(노랑·자홍·하늘·하양).
    private static bool IsArea(DiceColor colors) => ColorCount(colors) >= 2;

    private static int ColorCount(DiceColor colors)
    {
        int count = 0;
        if ((colors & DiceColor.Red) != 0) count++;
        if ((colors & DiceColor.Green) != 0) count++;
        if ((colors & DiceColor.Blue) != 0) count++;
        return count;
    }
}
