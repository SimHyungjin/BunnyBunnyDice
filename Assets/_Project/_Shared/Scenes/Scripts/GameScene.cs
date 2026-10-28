using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 씬 진입. 씬 파일에는 환경(카메라·조명·볼륨)만 두고, 게임 오브젝트는 전부 여기서 SO를 불러 만든다.
/// 씬 이름 "GameScene"과 클래스 이름이 같아야 SceneFlowManager가 찾아 실행한다.
///
/// 로비도 이 씬이다(기획서 12.6): 로비 뒤의 테이블이 곧 판이라 씬을 나누지 않는다. 흐름은 로비 → 게임 시작(영웅이 판으로 뛰어듦, LobbyStage)
/// → 판(판 주사위 연출 → 웨이브) → 결과 → 다시 하기(새 판) 또는 로비로. 판을 접을 때 씬을 다시 불러오지 않고 판 상태를 코드로 되돌린다
/// (ResetRun — 나중에 봇이 판을 연달아 돌릴 때도 같은 길을 쓴다).
///
/// 설정은 원본 SO가 아니라 실행 중 복사본(ConfigTuning)을 쓴다. 복사본이 바뀌면(GameTuning 인스펙터 또는 원본 SO 수정)
/// 그 설정을 쓰는 부분만 다시 적용한다.
/// </summary>
public sealed class GameScene : SceneBase
{
    private readonly ConfigTuning _tuning = new ConfigTuning();
    private readonly GameSpeed _speed = new GameSpeed();

    private SO_MapGenConfig _genConfig;
    private SO_MapViewConfig _viewConfig;
    private SO_MapCameraConfig _cameraConfig;
    private SO_MonsterConfig _monsterConfig;

    private Transform _mapRoot;
    private MapView _mapView;
    private MapGridView _grid;
    private EnvironmentView _environment;
    private BoardRollPresenter _boardRoll;
    // 판 치우기(다시 하기·로비로): 판 위 주사위·칩이 하늘로 날아간다.
    private BoardSweep _sweep;
    private SceneLighting _lighting;
    private ScreenWatcher _screen;
    private MapPathView _pathView;
    private PlacementPreview _preview;
    private DiceField _diceField;
    private ChipField _chips;
    private SO_LevelUpConfig _levelUpConfig;
    private CardHand _hand;
    private HandFraming _handFraming;
    private CardDragController _drag;
    private HeroStats _heroStats;
    private TraitHooks _traits;
    private Hero _hero;
    private CardPlacement _placement;
    // 미로 계획(어시스트·봇, 기획서 11.4)과 자동 플레이 봇(테스트 키 A).
    private MazePlanner _mazePlanner;
    // 맵 생성 검사(기획서 3.3)의 설정 — 미로를 지은 뒤 한 구멍만 아주 짧아지는 맵을 다시 굴리고 보스 구멍을 정한다.
    private SO_PlacementPreviewConfig _probeConfig;
    // 워커 스레드에서 도는 맵 생성의 차례 번호. 더 새 요청이 오거나 씬을 나가면 늘려, 늦게 끝난 옛 결과를 버린다.
    private int _mapJob;
    // 미로 검사가 도는 중 — 시작 칩 자리가 아직 바뀔 수 있다(굴림은 확정).
    private bool _mapPending;
    private GameBot _bot;
    private HeroTapInput _tapInput;
    // 영웅 오버드라이브(영웅이 올라탄 주사위, 기획서 10.9)와 그 표시.
    private HeroOverdrive _heroOverdrive;
    private HeroOverdriveView _heroOverdriveView;
    private MonsterField _monsters;
    private AttackEffects _attackEffects;
    private HeroCardShots _heroCards;
    private DamageNumbers _damageNumbers;
    private MonsterHealthBars _healthBars;
    private DiceAttack _diceAttack;
    private DiceCombat _diceCombat;
    private SkillCaster _skills;
    private SkillPreview _skillPreview;
    private HeroAttack _heroAttack;
    private WaveSpawner _waves;
    private WaveLog _waveLog;
    private GoalHealth _goal;
    private GoalHitReaction _goalHit;
    private PlayerLevel _level;
    private LevelUpChoice _levelUp;
    private GameResult _result;
    private SO_CardHandConfig _handConfig;
    private UI_GameHud _hud;
    private LobbyStage _lobbyStage;
    private HeroSpotlight _heroSpotlight;
    private LobbyScreen _lobbyScreen;
    private LobbyDecor _lobbyDecor;
    private MetaProfile _meta;
    // 판 밖 주사위 연출(로비·선택 창·결과 화면이 함께 쓴다).
    private SO_MetaDiceConfig _metaDiceConfig;
    private CharacterSelectScreen _characterSelect;
    private SettingsScreen _settings;
    // HUD 배속 버튼(1 → 2 → 3배).
    private GameSpeedToggle _speedToggle;
    private WaveStartGate _waveStart;
    // 카드를 끌어 놓는 중인지(그리드를 켠다). 첫 웨이브를 기다리는 동안에도 켠다.
    private bool _placing;
    private bool _paused;
    // 판 주사위 연출 중(첫 웨이브 시작 전) — 시작·일시정지 버튼은 ▶로 보이되 누름을 받지 않는다.
    private bool _rolling;
    // HUD 보스 체력 바(기획서 11.3).
    private BossHealthBar _bossBar;
    // 이번 판의 측정값(결과 화면·메타 해금 진행도).
    private readonly RunStats _run = new RunStats();
    // 테스트용 경험치 막기(레벨업 팝업 없이 웨이브·스킬을 시험할 때). 켜는 곳은 테스트 키·창뿐이라 출시 빌드에서는 늘 꺼져 있다.
    // PlayerLevel이 아니라 여기 두는 이유: 테스트 상태가 게임 규칙 클래스에 섞이지 않게(경험치를 넘기는 연결만 막는다).
    private bool _expBlocked = false;
    // 테스트용 무적(목표물이 피해를 받지 않는다). 경험치 막기와 같은 이유로 여기 둔다 — 목표물 규칙(GoalHealth)에 테스트 상태를 섞지 않게.
    // 이전에는 목표물 체력을 아주 크게 두어 대신했지만, 그러면 사람 테스트에서 질 수가 없었다.
    private bool _goalInvincible = false;
    private MapData _map;
    private GridRouteField _route;
    private int _seed;

    public override async UniTask EnterScene(CancellationToken token)
    {
        // 서로 의존이 없어 한꺼번에 불러온다.
        var (genAsset, viewAsset, cameraAsset, handAsset) = await UniTask.WhenAll(
            Core.Resource.LoadAsync<SO_MapGenConfig>(ct: token),
            Core.Resource.LoadAsync<SO_MapViewConfig>(ct: token),
            Core.Resource.LoadAsync<SO_MapCameraConfig>(ct: token),
            Core.Resource.LoadAsync<SO_CardHandConfig>(ct: token));
        var (pathAsset, previewAsset, dragAsset, environmentAsset, lightingAsset, heroAsset, boardRollAsset) = await UniTask.WhenAll(
            Core.Resource.LoadAsync<SO_MapPathViewConfig>(ct: token),
            Core.Resource.LoadAsync<SO_PlacementPreviewConfig>(ct: token),
            Core.Resource.LoadAsync<SO_CardDragConfig>(ct: token),
            Core.Resource.LoadAsync<SO_EnvironmentConfig>(ct: token),
            Core.Resource.LoadAsync<SO_LightingConfig>(ct: token),
            Core.Resource.LoadAsync<SO_HeroConfig>(ct: token),
            Core.Resource.LoadAsync<SO_BoardRollConfig>(ct: token));
        var (metaAsset, characterSelectAsset, metaDiceAsset, speedAsset, botAsset) = await UniTask.WhenAll(
            Core.Resource.LoadAsync<SO_MetaConfig>(ct: token),
            Core.Resource.LoadAsync<SO_CharacterSelectConfig>(ct: token),
            Core.Resource.LoadAsync<SO_MetaDiceConfig>(ct: token),
            Core.Resource.LoadAsync<SO_GameSpeedConfig>(ct: token),
            Core.Resource.LoadAsync<SO_BotConfig>(ct: token));
        var (monsterAsset, waveAsset, goalAsset, attackEffectAsset, levelUpAsset, diceAsset, diceStatsAsset, damageNumberAsset, lobbyAsset) = await UniTask.WhenAll(
            Core.Resource.LoadAsync<SO_MonsterConfig>(ct: token),
            Core.Resource.LoadAsync<SO_WaveConfig>(ct: token),
            Core.Resource.LoadAsync<SO_GoalConfig>(ct: token),
            Core.Resource.LoadAsync<SO_AttackEffectConfig>(ct: token),
            Core.Resource.LoadAsync<SO_LevelUpConfig>(ct: token),
            Core.Resource.LoadAsync<SO_DiceConfig>(ct: token),
            Core.Resource.LoadAsync<SO_DiceStatsConfig>(ct: token),
            Core.Resource.LoadAsync<SO_DamageNumberConfig>(ct: token),
            Core.Resource.LoadAsync<SO_LobbyConfig>(ct: token));

        _genConfig = _tuning.Copy(genAsset);
        _viewConfig = _tuning.Copy(viewAsset);
        _cameraConfig = _tuning.Copy(cameraAsset);
        SO_CardHandConfig handConfig = _tuning.Copy(handAsset);
        SO_MapPathViewConfig pathConfig = _tuning.Copy(pathAsset);
        SO_PlacementPreviewConfig previewConfig = _tuning.Copy(previewAsset);
        _probeConfig = previewConfig;
        SO_CardDragConfig dragConfig = _tuning.Copy(dragAsset);
        SO_EnvironmentConfig environmentConfig = _tuning.Copy(environmentAsset);
        SO_LightingConfig lightingConfig = _tuning.Copy(lightingAsset);
        SO_HeroConfig heroConfig = _tuning.Copy(heroAsset);
        SO_BoardRollConfig boardRollConfig = _tuning.Copy(boardRollAsset);
        _monsterConfig = _tuning.Copy(monsterAsset);
        SO_WaveConfig waveConfig = _tuning.Copy(waveAsset);
        SO_GoalConfig goalConfig = _tuning.Copy(goalAsset);
        SO_AttackEffectConfig attackEffectConfig = _tuning.Copy(attackEffectAsset);
        SO_LevelUpConfig levelUpConfig = _tuning.Copy(levelUpAsset);
        SO_DiceConfig diceConfig = _tuning.Copy(diceAsset);
        SO_DiceStatsConfig diceStatsConfig = _tuning.Copy(diceStatsAsset);
        SO_DamageNumberConfig damageNumberConfig = _tuning.Copy(damageNumberAsset);
        SO_LobbyConfig lobbyConfig = _tuning.Copy(lobbyAsset);
        SO_MetaConfig metaConfig = _tuning.Copy(metaAsset);
        SO_CharacterSelectConfig characterSelectConfig = _tuning.Copy(characterSelectAsset);
        _metaDiceConfig = _tuning.Copy(metaDiceAsset);
        SO_GameSpeedConfig speedConfig = _tuning.Copy(speedAsset);
        SO_BotConfig botConfig = _tuning.Copy(botAsset);

        _lighting = new SceneLighting(lightingConfig);
        _lighting.Apply(Core.Camera.MainCamera);

        // 씬에 만든 일반 오브젝트라 씬을 벗어나면 함께 사라진다.
        _mapRoot = new GameObject("Map").transform;
        _mapView = new MapView(_mapRoot, _viewConfig, _lighting.OutlineMaterial);
        _grid = new MapGridView(_mapRoot, _viewConfig);
        _environment = new EnvironmentView(_mapRoot, environmentConfig);
        _pathView = new MapPathView(_mapRoot, pathConfig);
        // 영웅 능력치·특성은 주사위 굴림(특성)·스킬 피해·카드 글이 함께 쓰므로 먼저 만든다.
        _heroStats = new HeroStats(heroConfig);
        // 캐릭터·단계를 세이브에서 받는다(영웅 겉모습을 만들기 전에 — 다시 만들지 않게).
        _meta = new MetaProfile(heroConfig, metaConfig);
        _meta.Bind(_heroStats);
        _run.Ended += run => _meta.Record(_heroStats.Character, run);
        _traits = new TraitHooks(_heroStats);
        _diceField = new DiceField(_mapRoot, diceConfig, diceStatsConfig, _viewConfig, () => Core.Camera.MainCamera, _lighting.OutlineMaterial,
            _traits);
        _chips = new ChipField(_mapRoot, _viewConfig, _lighting.OutlineMaterial);
        // 시작 칩 종류는 레벨업에 나오는 칩 카드에서 같은 가중치로 뽑는다.
        _levelUpConfig = levelUpConfig;
        _preview = new PlacementPreview(_mapRoot, previewConfig, _viewConfig, diceConfig, diceStatsConfig, _chips, _lighting.OutlineMaterial);
        // 맵 검사(워커 스레드)가 끝나기 전에는 시작 칩을 떨어뜨리지 않는다 — 주사위가 더 구르며 기다린다.
        _boardRoll = new BoardRollPresenter(_mapRoot, boardRollConfig, diceConfig, _chips, _environment, () => Core.Camera.MainCamera,
            () => !_mapPending);
        _sweep = new BoardSweep(boardRollConfig);
        _hero = new Hero(_mapRoot, heroConfig, _heroStats, () => _map, GetHeroFloor, _lighting.CharacterOutlineMaterial);
        _placement = new CardPlacement(_diceField, _chips);
        _lobbyStage = new LobbyStage(lobbyConfig, _viewConfig, _hero, _mapRoot, () => _map, () => Core.Camera.MainCamera, FitCamera);
        // 로비에서는 무대 조명, 게임 중에는 판 위에서 영웅을 놓치지 않게 하는 표시. 뛰어드는 동안 로비 카메라 비율로 섞인다.
        if (heroConfig.Spotlight != null)
            _heroSpotlight = new HeroSpotlight(_mapRoot, () => heroConfig.Spotlight, _lighting, _hero, () => _map,
                () => _lobbyStage.LobbyWeight, () => Core.Camera.MainCamera);
        // 뛰어들기가 끝나면 HUD를 보이고 판을 시작한다(판 주사위 연출부터).
        _lobbyStage.Entered += () =>
        {
            _hud?.SetShown(true);
            StartBattle();
        };
        _lobbyDecor = new LobbyDecor(_mapRoot, lobbyConfig, _viewConfig, diceConfig, levelUpConfig, _lighting.OutlineMaterial);
        // 게임 시작: 영웅이 뛰어들고 테이블 장식은 사라진다(뒤이어 판 주사위 연출이 판을 채운다).
        // 얼굴 사진은 캐릭터 SO(모델·색)를 고치면 다시 찍는다.
        HeroPortraits portraits = new HeroPortraits(characterSelectConfig, _lighting.CharacterOutlineMaterial);
        heroConfig.Changed += portraits.Clear;
        // 판 중에 열면 판을 멈춘다(같은 속도 요청 묶음을 쓴다).
        _settings = new SettingsScreen(_speed);
        _characterSelect = new CharacterSelectScreen(heroConfig.Characters, _heroStats, _meta, portraits, characterSelectConfig,
            _metaDiceConfig);
        _lobbyScreen = new LobbyScreen(_heroStats, _meta, _metaDiceConfig, () =>
        {
            _characterSelect.Close();
            _settings?.Close();
            _lobbyStage.Enter();
            _lobbyDecor.Hide();
        }, _characterSelect.Show, _settings.Show);
        // 판에서 돌아올 때는 영웅이 난간에 앉은 뒤에 로비 화면을 띄운다.
        _lobbyStage.Exited += _lobbyScreen.Show;
        _monsters = new MonsterField(_mapRoot, _monsterConfig, _viewConfig, _lighting.OutlineMaterial);
        // 적은 열린 포켓 구멍에서 솟아 나온다(표시만).
        _monsters.SpawnOrigin = cell => _environment.GetPocketCenter(_map, cell);
        _healthBars = new MonsterHealthBars(_mapRoot, _monsterConfig, _viewConfig, _monsters, () => Core.Camera.MainCamera);
        _attackEffects = new AttackEffects(_mapRoot, attackEffectConfig);
        _damageNumbers = new DamageNumbers(_mapRoot, damageNumberConfig, () => Core.Camera.MainCamera);
        _diceAttack = new DiceAttack(diceStatsConfig, _monsterConfig, _viewConfig, _monsters, _attackEffects);
        _diceCombat = new DiceCombat(_viewConfig, diceStatsConfig, diceConfig, _diceField, _chips, _monsters, _diceAttack);
        _heroOverdrive = new HeroOverdrive(heroConfig, _hero, _diceField, _diceCombat, _monsters);
        _heroOverdriveView = new HeroOverdriveView(heroConfig, _viewConfig, _heroOverdrive, _attackEffects);
        _skills = new SkillCaster(_monsters, _diceField, _chips, _diceCombat, _attackEffects, _heroStats, _viewConfig, () => _map);
        _skillPreview = new SkillPreview(_mapRoot, previewConfig, _viewConfig, _skills, _monsters, _diceField);
        _heroCards = new HeroCardShots(_mapRoot, heroConfig.CardShot, _monsters, _monsterConfig, _viewConfig);
        _heroAttack = new HeroAttack(heroConfig, _heroStats, _hero, _monsters, _heroCards,
            _diceCombat.GetRange);
        _waves = new WaveSpawner(waveConfig, _monsters.Spawn, _monsters.SpawnBoss, () => _monsters.Count);
        // 잭팟 보스 예고: 보스 웨이브 동안 보스 포켓의 빛이 보스 색에서 금색으로 바뀐다.
        _waves.JackpotWarned += () => _environment.SetPocketJackpot(true);
        _waves.JackpotWarningEnded += () => _environment.SetPocketJackpot(false);
        _goal = new GoalHealth(goalConfig);
        _goalHit = new GoalHitReaction(goalConfig, _mapView, _attackEffects);
        _level = new PlayerLevel(levelUpConfig);
        // 측정 기록(기획서 11.2): 웨이브마다 정리 시간·새어 나간 적·피해 출처·고른 카드를 모아 CSV로 남긴다.
        _waveLog = new WaveLog(() => _goal.Current, () => _level.Level, () => _heroOverdrive.Active, () => _diceField.Placed);
        _waves.WaveStarted += _waveLog.OnWaveStarted;
        _waves.WaveCleared += _waveLog.OnWaveCleared;
        _monsters.DamageDealt += _waveLog.CountDamage;
        _heroOverdrive.PipDrained += _waveLog.CountPipDrained;
        _skills.Used += _waveLog.CountSkillUse;
        _skills.AreaCast += _waveLog.CountAreaCast;
        // 영웅은 주사위·칩을 밟고 넘어가므로(기획서 10.8) 길이 바뀌어도 몬스터 길만 다시 계산한다.
        _diceField.DicePlaced += _ =>
        {
            _run.CountDicePlaced();
            RebuildRoute();
        };
        // 빈 칸의 칩은 새 벽이고, 주사위 칸의 칩은 주사위를 올려 앉힌다(길은 그대로지만 같이 다시 계산해도 싸다).
        // 폴드로 주사위·칩이 없어지면 칸이 열려 길이 바뀐다.
        _diceField.DiceRemoved += (_, _) => RebuildRoute();
        _diceField.DiceAwakened += _ => _run.CountAwakening();
        _chips.ChipRemoved += _ => RebuildRoute();
        _chips.ChipPlaced += cell =>
        {
            _diceField.RefreshAt(cell);
            RebuildRoute();
        };
        _monsters.ReachedGoal += type =>
        {
            // 무적이어도 받았을 피해를 센다 — 무적으로 버티며 높은 웨이브를 잴 때도 얼마나 뚫렸는지 보이게.
            _waveLog.CountLeak(type.GoalDamage);
            // 잭팟처럼 피해가 없는 몬스터는 맞는 연출도 없이 빠져나간다.
            if (type.GoalDamage <= 0) return;
            if (!_goalInvincible) _goal.Damage(type.GoalDamage);
            _goalHit.Play();
        };
        _monsters.Killed += (type, position) =>
        {
            _run.CountKill(type.IsJackpot);
            _waveLog.CountKill();
            // 보스: 카드 고르기 한 번, 잭팟 보스는 두 번(레벨은 오르지 않는다, 기획서 11.3). 분열 조각은 보스가 아니다.
            if (type.IsBoss && !type.IsPiece)
            {
                int choices = type.IsJackpot ? waveConfig.JackpotRewardChoices : waveConfig.BossRewardChoices;
                for (int i = 0; i < choices; i++) _levelUp?.Enqueue();
            }
            // 특성이 처치 경험치를 바꾸면(럭키보이 — 확률로 2배) 더 들어간 만큼 금색 "+N"을 띄운다.
            IKillExpModifier modifier = _traits.Find<IKillExpModifier>();
            int exp = modifier != null ? modifier.ModifyExp(type.Exp) : type.Exp;
            GainExp(exp);
            if (exp > type.Exp) _damageNumbers.Show(position, _viewConfig.GetTileTop(TileType.Ground), exp - type.Exp, DamageKind.BonusExp);
        };
        _result = new GameResult(Retry, EnterLobby, _metaDiceConfig);
        _handConfig = handConfig;
        // 몬스터는 바닥 칸만 걸으므로 숫자는 바닥 윗면 기준으로 띄운다.
        _monsters.Damaged += (position, amount, kind) =>
            _damageNumbers.Show(position, _viewConfig.GetTileTop(TileType.Ground), amount, kind);
        _level.Changed += RefreshHud;
        _goal.Changed += RefreshHud;
        _goal.Died += GameOver;
        _waves.WaveStarted += _ => RefreshHud();

        // 첫 화면은 로비다. 로비를 먼저 세워 두어야 맵 생성이 판을 시작하지 않고 굴리기 전 모습으로 둔다.
        // 첫 맵은 바로 만든다 — 씬을 불러오는 중이라 멈춤이 보이지 않고, 로비 카메라·테이블이 첫 프레임부터 맵을 쓴다.
        _lobbyStage.Show();
        GenerateMap(System.Environment.TickCount, true);

        _hud = await Core.UI.ShowHud<UI_GameHud>(ct: token);
        // 첫 화면은 로비라 숨겨 둔다(뛰어들기가 끝나면 보인다).
        _hud.SetShown(!_lobbyStage.IsActive);
        RefreshHud();
        _hand = new CardHand(_hud.HandRoot, handConfig, diceConfig, diceStatsConfig, _heroStats);
        MapPicker picker = new MapPicker(_mapRoot, _viewConfig);
        _drag = new CardDragController(_hand, dragConfig, picker, _preview, _diceField, _chips, _skills, _skillPreview, _placement,
            _speed, () => _map, () => Core.Camera.MainCamera);
        _drag.PlacingChanged += placing =>
        {
            _placing = placing;
            RefreshGrid();
        };
        // 첫 판의 판 주사위 연출은 손패보다 먼저 시작하므로 잠금을 여기서 이어받는다.
        _drag.Locked = _boardRoll.IsPlaying;
        _levelUp = new LevelUpChoice(levelUpConfig, handConfig, diceConfig, diceStatsConfig, _heroStats, _traits, _hand, _speed);
        _level.LeveledUp += _levelUp.Enqueue;
        _levelUp.HeroCardChosen += _heroStats.Apply;
        _levelUp.CardChosen += _waveLog.CountCard;
        _levelUp.Offered += _waveLog.CountOffers;
        _levelUp.Opened += _drag.Cancel;
        _mazePlanner = new MazePlanner(previewConfig, () => _map, _diceField);
        _bot = new GameBot(botConfig, () => _map, () => _waves.Wave, _heroStats, heroConfig.Characters, _diceField, _chips, _hand, _placement, _skills, _monsters, _hero, diceConfig,
            _levelUp, _speed, _mazePlanner, () => !_drag.Locked && !_lobbyStage.IsActive && !_monsters.Paused, Retry);
        _tapInput = new HeroTapInput(_hud.TouchArea, _hero, picker, () => _map, () => Core.Camera.MainCamera, () => _drag.Busy);
        DealStartHand();
        // 손패를 다 쓰면 판을 아래로 넓히고, 카드가 들어오면 되돌린다(아래 여백만 섞는다).
        _handFraming = new HandFraming(() => _cameraConfig, () => handConfig, () => _hand.Count, () => _drag.Busy, FitCamera);

        // 설정이 바뀌면 그 설정을 쓰는 부분만 다시 적용한다. (드래그·웨이브·주사위·주사위 능력치·공격 방식·공격선·레벨업 선택 설정은 쓸 때마다 읽으므로 따로 할 일이 없다)
        _genConfig.Changed += () => GenerateMap(_seed);
        _viewConfig.Changed += RebuildMapViews;
        _cameraConfig.Changed += FitCamera;
        pathConfig.Changed += () => _pathView.Rebuild(_map, _route);
        _monsterConfig.Changed += RebuildRoute;
        goalConfig.Changed += RefreshHud;
        levelUpConfig.Changed += RefreshHud;
        previewConfig.Changed += () =>
        {
            _preview.Reset();
            _skillPreview.Reset();
        };
        handConfig.Changed += _hand.Relayout;
        _hud.Resized += _hand.Relayout;
        // 판 중 설정: 멈추고, 포기하면 게임 오버와 같다(결과 화면 — 지금 웨이브까지의 보상을 받는다).
        _hud.SettingsClicked += () => _settings.ShowInGame(GameOver);
        _bossBar = new BossHealthBar(_monsters, _hud);
        _speedToggle = new GameSpeedToggle(speedConfig, _speed);
        _speedToggle.Changed += _hud.ShowSpeed;
        _hud.SpeedClicked += _speedToggle.Next;
        // 배속 버튼 왼쪽의 시작·일시정지 버튼: 첫 웨이브 전엔 시작, 그 뒤로는 일시정지·재개.
        _waveStart = new WaveStartGate(() => _waves.Begin(), () => _bot != null && _bot.Enabled);
        _waveStart.WaitingChanged += _ =>
        {
            RefreshPlayPause();
            RefreshGrid();
        };
        _hud.PauseClicked += () =>
        {
            if (_rolling) return;
            if (_waveStart.Waiting) _waveStart.Start();
            else SetPaused(!_paused);
        };
        RefreshPlayPause();
        speedConfig.Changed += _speedToggle.Reset;
        _speedToggle.Reset();
        diceConfig.Changed += () =>
        {
            _diceField.RebuildViews(_map);
            _preview.Reset();
        };
        environmentConfig.Changed += () => _environment.Build(_map, _seed);
        lightingConfig.Changed += () => _lighting.Apply(Core.Camera.MainCamera);
        // 캐릭터 SO(하위 설정)를 고쳐도 heroConfig.Changed가 불린다(ConfigTuning). 능력치는 HeroStats가 읽을 때마다 계산하므로 겉모습만 다시 만든다.
        _heroStats.CharacterChanged += _hero.RebuildView;
        heroConfig.Changed += _hero.RebuildView;

        // 기기 회전·폴더블 접기/펴기로 화면 비율이 바뀌면 방향과 거리를 다시 맞춘다.
        _screen = new ScreenWatcher();
        _screen.Resized += FitCamera;

        new GameObject("GameTuning").AddComponent<GameTuning>().Show(_tuning);

        _lobbyScreen.Show();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 테스트 키. 같은 목록이 테스트 창(F1)의 "기능" 탭에 버튼·키 안내로 나온다.
        GameDebugKeys keys = _mapRoot.gameObject.AddComponent<GameDebugKeys>();
        keys.Add(Key.R, false, "새 맵(새 판, 로비에서는 테이블만 바꾼다)", () => GenerateMap(System.Environment.TickCount));
        keys.Add(Key.C, false, "바로 레벨업(다음 레벨까지 남은 경험치를 채운다)", LevelUpNow);
        keys.Add(Key.U, false, "칩 카드 한 장씩 추가", () => AddAttachmentCards(levelUpConfig));
        keys.Add(Key.K, false, "스킬 카드 한 장씩 추가", () => AddSkillCards(levelUpConfig));
        keys.Add(Key.N, false, "다음 웨이브 바로 시작", () => SkipWaves(1));
        keys.Add(Key.N, true, $"{LongSkip}웨이브 건너뛰기", () => SkipWaves(LongSkip));
        keys.Add(Key.E, false, "경험치 막기", () => _expBlocked = !_expBlocked, () => _expBlocked);
        keys.Add(Key.J, false, "다음 보스를 잭팟 보스로(보스 웨이브가 시작될 때 정해진다)", () => _waves.ForceJackpot());
        keys.Add(Key.I, false, "무적(목표물이 피해를 받지 않는다)", () => _goalInvincible = !_goalInvincible, () => _goalInvincible);
        keys.Add(Key.L, false, "언어 바꾸기(한국어↔영어)", CycleLanguage);
        // 로비·뛰어들기 연출을 되풀이해 보려고. 결과 화면의 "로비로"와 같은 길이다(판을 접고 새 테이블).
        keys.Add(Key.B, false, "로비로(판을 접는다)", EnterLobby);
        // 판 중에 켠다. 지면 다시 하기를 눌러 계속 돌고, 판마다 측정 기록 CSV가 남는다(기획서 11.2).
        keys.Add(Key.A, false, "자동 플레이(봇 — 판 중에 켠다, SO_BotConfig.BatchRuns판 뒤 요약을 남기고 멈춘다)", () => _bot.SetEnabled(!_bot.Enabled), () => _bot.Enabled);
        // 원하는 카드를 골라 넣는 테스트 창(F1).
        GameDebugPanel panel = _mapRoot.gameObject.AddComponent<GameDebugPanel>();
        panel.Init(levelUpConfig, diceConfig, keys.Bindings, _hand.Add, () =>
        {
            _drag.Cancel();
            _hand.Clear();
            _hand.Relayout();
        });
        // 캐릭터·능력치 단계를 바꿔 보는 "영웅" 탭(캐릭터 선택 창이 생기기 전까지 캐릭터를 고르는 곳 — 로비 패널도 따라 바뀐다).
        panel.InitHero(heroConfig, _heroStats, levelUpConfig.HeroCards);
        panel.InitMeta(_meta);
        panel.InitLog(_waveLog);
#endif
    }

    public override void ExitScene()
    {
        // 도는 중인 맵 생성이 끝나도 판을 세우지 않게.
        _mapJob++;
        _lobbyScreen?.Close();
        _characterSelect?.Dispose();
        _settings?.Close();
        _heroSpotlight?.Dispose();
        _lobbyStage?.Dispose();
        _lobbyDecor?.Dispose();
        _meta?.Dispose();
        _screen?.Dispose();
        _handFraming?.Dispose();
        _bot?.Dispose();
        _bossBar?.Dispose();
        _mazePlanner?.Dispose();
        _drag?.Dispose();
        _pathView?.Dispose();
        _grid?.Dispose();
        _tapInput?.Dispose();
        _hero?.Dispose();
        _waves?.Dispose();
        _waveLog?.Dispose();
        _heroAttack?.Dispose();
        _skillPreview?.Dispose();
        _heroOverdriveView?.Dispose();
        _heroOverdrive?.Dispose();
        _diceCombat?.Dispose();
        _diceAttack?.Dispose();
        _diceField?.Dispose();
        _goalHit?.Dispose();
        _attackEffects?.Dispose();
        _heroCards?.Dispose();
        _waveStart?.Dispose();
        _damageNumbers?.Dispose();
        _healthBars?.Dispose();
        _monsters?.Dispose();
        _lighting?.Dispose();
        _boardRoll?.Dispose();
        _sweep?.Dispose();
        _environment?.Dispose();
        _levelUp?.Cancel();
        _result?.Close();
        _speed.Dispose();
        _tuning.Dispose();
    }

    // 맵 생성은 두 단계다(MapGenerator): 굴림 + 첫 칩 배치(수 ms)는 그 자리에서 하고 판을 바로 세운다(판 주사위 연출도 바로 시작).
    // 미로 검사·칩 다시 놓기(0.2~2초)는 워커 스레드에서 돌린다 — 메인 스레드에서 돌리면 연출 직전에 화면이 멈췄다.
    // 굴림은 검사가 바꾸지 않으므로 연출은 칩이 떨어질 때까지만 기다리면 되고, 그 전에 안 끝나면 주사위가 더 구른다(BoardRollPresenter).
    // 결과가 오면 칩만 바꿔 다시 그린다(SwapMap). 같은 시드면 같은 맵이다. wait = 검사까지 그 자리에서(첫 맵).
    private void GenerateMap(int seed, bool wait = false)
    {
        _seed = seed;
        int job = ++_mapJob;
        // 옛 판을 먼저 멈추고 비운다.
        _waves.Stop();
        _diceField.Clear();
        _heroOverdrive?.Clear();
        _monsters.Clear();
        _attackEffects.Clear();
        _heroCards.Clear();
        _damageNumbers.Clear();
        _diceAttack.Clear();

        // 검사는 판마다 새로 만든다 — 요청이 겹치면(R 키 연타·설정 변경) 두 생성이 버퍼를 함께 쓰지 않게.
        MazeRouteProbe probe = new MazeRouteProbe(_probeConfig);
        MapData rolled = MapGenerator.Roll(_genConfig, seed);
        if (wait)
        {
            _mapPending = false;
            ApplyMap(MapGenerator.Refine(_genConfig, seed, rolled, probe));
            return;
        }
        // 검사가 끝날 때까지 판 주사위 연출이 칩을 떨어뜨리지 않고 판도 시작하지 않는다.
        _mapPending = true;
        ApplyMap(rolled);
        RefineMapAsync(seed, job, rolled, probe).Forget();
    }

    private async UniTaskVoid RefineMapAsync(int seed, int job, MapData rolled, MazeRouteProbe probe)
    {
        MapData map = await UniTask.RunOnThreadPool(() => MapGenerator.Refine(_genConfig, seed, rolled, probe));
        if (job != _mapJob) return;
        // 칩을 다시 놓았으면 바꿔 그린다. 굴림·구멍은 같아 연출(날아갈 구멍)은 그대로 이어진다.
        if (map != rolled) SwapMap(map);
        _mapPending = false;
    }

    // 검사가 칩만 다시 놓은 맵으로 바꾼다. 판 주사위 연출 중(칩이 떨어지기 전)이라 칩·경로는 아직 숨겨져 있다.
    private void SwapMap(MapData map)
    {
        _map = map;
        Debug.Log($"[GameScene] map seed={_seed} chips re-placed by maze check");
        _chips.Build(_map, _levelUpConfig.ChipCards, _seed);
        RebuildMapViews();
        _boardRoll.RefreshChips();
        RebuildRoute();
    }

    private void ApplyMap(MapData map)
    {
        _map = map;
        Debug.Log($"[GameScene] map seed={_seed} roll={string.Join(",", _map.Roll)} spawns={_map.Spawns.Count}");

        _chips.Build(_map, _levelUpConfig.ChipCards, _seed);
        RebuildMapViews();
        RebuildRoute();
        _hero.Respawn();
        // 로비(뛰어드는 중 포함)에서는 판을 시작하지 않는다 — 뛰어들기가 끝나면 LobbyStage.Entered가 시작한다.
        if (_lobbyStage.IsActive) PrepareLobbyBoard();
        else StartBattle();
    }

    private void RebuildMapViews()
    {
        _preview.Reset();
        _mapView.Build(_map);
        _chips.RebuildViews();
        _grid.Build(_map);
        // 테이블 펠트가 판 바닥이고 인쇄 선이 둘레 벽·출현 구역 자리를 따르므로 지형과 함께 다시 만든다.
        _environment.Build(_map, _seed);
        _diceField.RebuildViews(_map);
        // 칩을 다시 만들면 보이게 되므로, 로비에서는 다시 숨긴다(뛰어든 뒤 판 주사위 연출이 떨어뜨린다).
        if (_lobbyStage.IsActive) _boardRoll.Hold();
        FitCamera();
    }

    // 영웅이 밟고 서는 높이: 목표물 칩 위면 그 윗면, 주사위가 있으면 몸체 윗면, 아니면 칸 윗면(칩 위면 칩 윗면).
    // 목표물 칩은 두 칸에 걸친 원판이라 칸이 아니라 원 안인지로 본다(표식 프리팹을 쓰면 높이를 모르므로 바닥).
    private float GetHeroFloor(Vector2 position)
    {
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        if (_map == null) return ground;
        Vector2 goal = _map.GoalRect.center - new Vector2(0.5f, 0.5f);
        float goalRadius = _viewConfig.GoalChipDiameter * 0.5f;
        if (_viewConfig.GoalMarkerPrefab == null && (position - goal).sqrMagnitude <= goalRadius * goalRadius) return ground + _viewConfig.GoalChipHeight;
        Vector2Int cell = GridCircle.CellOf(position);
        if (!_map.InBounds(cell.x, cell.y)) return ground;
        if (_diceField.TryGetSurface(cell, out float surface)) return surface;
        return _viewConfig.GetCellTop(_map, cell.x, cell.y);
    }

    // 몬스터와 경로 표시가 같은 길을 쓴다 — 표시가 실제 움직임과 어긋나지 않게.
    private void RebuildRoute()
    {
        _route = MapPathfinder.BuildRouteField(_map, _monsterConfig.Radius);
        _pathView.Rebuild(_map, _route);
        _monsters.SetRoute(_map, _route);
    }

    // 다시 하기: 판 위 주사위·칩이 하늘로 날아가고(판 치우기), 다 날아가면 새 판의 판 주사위 연출이 시작된다(StartBattle).
    private void Retry()
    {
        SweepBoard();
        // 봇이 비교 묶음으로 돌 때는 정해 둔 맵으로(같은 맵끼리 짝지어 비교한다).
        GenerateMap(_bot != null && _bot.TryTakeSeed(out int seed) ? seed : System.Environment.TickCount);
    }

    // 로비로(결과 화면의 로비로): 판 위 물건이 날아가는 동안 영웅이 난간으로 뛰어오르고(뛰어들기를 거꾸로) 카메라가 로비 자리로 빠진다.
    // 새 테이블의 장식은 톡톡 생기고(PrepareLobbyBoard), 영웅이 앉으면 로비 화면이 뜬다(LobbyStage.Exited).
    private void EnterLobby()
    {
        SweepBoard();
        _lobbyStage.Exit();
        GenerateMap(System.Environment.TickCount);
        _hud?.SetShown(false);
    }

    // 판 위 표시를 판 치우기에 넘긴다. 판 상태는 곧 GenerateMap이 새로 만든다. 몬스터는 남기지 않는다(날리기엔 수가 많고 판이 끝났다).
    private void SweepBoard()
    {
        _diceField.DetachViews(_sweep);
        _chips.DetachViews(_sweep);
    }

    // 판 상태를 새 판 직전으로 되돌린다(새 판·로비 공통). 맵·주사위·몬스터는 GenerateMap이 새로 만든다.
    private void ResetRun()
    {
        // 판 도중에 접으면(로비로·새 맵) 거기까지 남긴다.
        _waveLog?.End();
        _monsters.Paused = false;
        _goal.Reset();
        _level.Reset();
        // 영웅 강화는 이번 판 동안만이다. 캐릭터·단계는 그대로 둔다.
        _heroStats.ResetBonuses();
        _levelUp?.Cancel();
        _result?.Close();
        // 새 판은 1배속부터 — 판 주사위 연출·초반 배치를 지난 판 배속으로 시작하지 않게.
        _speedToggle?.Reset();
        _waveStart?.Cancel();
        _rolling = false;
        SetPaused(false);
        _run.Reset();
        _waves.Reset();
    }

    // 로비의 테이블: 굴리기 전 모습(칩 숨김·포켓 빛 끔·경로 숨김)이고 손패는 비운다.
    private void PrepareLobbyBoard()
    {
        ResetRun();
        // 다시 하기 직후 로비로 가면 기다리던 판 주사위 연출을 버린다(로비 테이블은 굴리기 전 모습이다).
        _sweep.Then(null);
        SetBoardRollLock(true);
        _boardRoll.Hold();
        // 뛰어드는 중에 테이블이 바뀌면(R 키) 장식은 다시 깔지 않는다 — 이미 치우는 중이다.
        // 판에서 돌아오는 중이면 톡톡 생기게 한다(판 위 물건이 날아가는 동안).
        if (!_lobbyStage.IsEntering) _lobbyDecor.Show(_map, _lobbyStage.IsExiting);
        _drag?.Cancel();
        _hand?.Clear();
        RefreshHud();
    }

    // 새 판은 새 맵과 함께 시작한다(뛰어들기 끝·R 키·맵 설정 변경·결과 화면의 다시 하기). 손패도 시작 손패로 되돌린다.
    private void StartBattle()
    {
        ResetRun();
        _waveLog.Begin(_heroStats.Character != null ? _heroStats.Character.name : "None", _seed);
        // 판 주사위 연출(굴림 결과를 보여 줌)이 끝나면 웨이브를 시작한다. 그동안 경로 표시를 숨기고(결과가 미리 드러나지 않게)
        // 카드 배치를 잠근다(아직 떨어지지 않은 칩 칸에 놓지 않게).
        // 판 치우기 중이면(다시 하기) 새 칩을 숨겨 둔 채 다 날아가기를 기다린다 — 날아가는 칩과 떨어지는 칩이 섞이지 않게.
        SetBoardRollLock(true);
        _rolling = true;
        RefreshPlayPause();
        _boardRoll.Hold();
        _sweep.Then(() => _boardRoll.Play(_map, () =>
        {
            SetBoardRollLock(false);
            _rolling = false;
            _waveStart.Arm();
        }));
        DealStartHand();
        RefreshHud();
    }

    // 일시정지는 게임 속도 요청 0이다 — 레벨업 감속과 같은 길이라 둘이 겹쳐도 가장 느린 쪽(멈춤)이 이긴다.
    private void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused) _speed.Request(this, 0f);
        else _speed.Release(this);
        RefreshPlayPause();
        RefreshGrid();
    }

    private void RefreshPlayPause() => _hud?.ShowPlayPause(_paused || _rolling || (_waveStart != null && _waveStart.Waiting));

    // 멈춘 동안에도 켠다 — 멈춤은 칸을 세며 다음 배치를 계획하는 시간이다(첫 웨이브 기다림과 같다).
    private void RefreshGrid() => _grid?.SetShown(_placing || _paused || (_waveStart != null && _waveStart.Waiting));

    private void SetBoardRollLock(bool locked)
    {
        _pathView.SetVisible(!locked);
        // 연출이 끝나 드러날 때 선이 통째로 뚝 생기지 않고 첫 빛을 따라 그려지게.
        if (!locked) _pathView.Reveal();
        if (_drag != null) _drag.Locked = locked;
        // 판 주사위 연출은 실제 시간이라 멈출 수 없으므로 그동안 판 중 설정(멈춤·포기)도 막는다.
        _hud?.SetSettingsEnabled(!locked);
    }

    // 게임 오버: 출현과 이동을 멈추고 결과 화면을 띄운다. 맵은 그대로 두어 결과 화면 뒤로 어디가 뚫렸는지 보인다.
    private void GameOver()
    {
        Debug.Log($"[GameScene] game over at wave {_waves.Wave}");
        _waves.Stop();
        _monsters.Paused = true;
        _levelUp?.Cancel();
        // 판 기록의 웨이브는 클리어한 웨이브다(진 웨이브는 빼고) — 결과 화면·보상·최고 기록·해금이 모두 이 값을 쓴다.
        RunSummary run = _run.Finish(Mathf.Max(_waves.Wave - 1, 0), _level.Level);
        _waveLog.End();
        // 보상은 판이 끝날 때(Finish → Ended → MetaProfile.Record) 정해졌다.
        _result.Show(run, _meta.LastReward);
        _bot?.OnGameOver(_waveLog.Records, _seed, run.Wave);
    }

    // 새 판은 시작 손패로 시작한다. 손패는 HUD가 뜬 뒤에 생기므로 그 전(첫 맵 생성)에는 건너뛰고, 손패를 만든 뒤 따로 부른다.
    private void DealStartHand()
    {
        if (_hand == null) return;
        _drag?.Cancel();
        _hand.Clear();
        for (int i = 0; i < _handConfig.StartHand.Count; i++) _hand.Add(HandCard.Of(_handConfig.StartHand[i]));
    }

    private void RefreshHud()
    {
        if (_hud != null) _hud.ShowStatus(_goal.Current, _goal.Max, _waves.Wave, _level.Level, _level.Exp, _level.ExpToNext);
    }

    // 게임 자리로 맞춘 뒤, 로비(뛰어드는 중 포함)면 그 위에 로비 자리를 섞는다(LobbyStage).
    private void FitCamera()
    {
        if (_map == null) return;
        MapCameraFitter.Fit(Core.Camera.MainCamera, _mapRoot, _map, _cameraConfig, _viewConfig, _handFraming != null ? _handFraming.Weight : 0f);
        _lobbyStage.ApplyCamera();
    }

    private void GainExp(int exp)
    {
        if (!_expBlocked) _level.AddExp(exp);
    }

    // Shift+N으로 건너뛰는 웨이브 수(테스트 편의 값이라 SO로 빼지 않는다).
    private const int LongSkip = 10;

    // 테스트용 웨이브 건너뛰기: 남은 적을 치우고(경험치 없이) 그 웨이브를 바로 시작한다. 로비·판 주사위 연출 중에는 웨이브가 멈춰 있어 무시된다.
    private void SkipWaves(int count)
    {
        if (_lobbyStage.IsActive || _boardRoll.IsPlaying || _mapPending) return;
        _monsters.Clear();
        _waves.SkipWaves(count);
        RefreshHud();
    }

    // 테스트용 레벨업: 실제 레벨업과 같은 흐름(레벨·HUD·후보 팝업·특성)을 지나게 경험치를 채운다.
    // 경험치 막기(E)와 상관없이 오른다 — 막아 둔 채로 원할 때만 레벨업을 시험할 수 있게.
    // 로비에서는 무시한다(판이 시작되지 않았는데 후보 팝업이 뜨지 않게).
    private void LevelUpNow()
    {
        if (!_lobbyStage.IsActive) _level.AddExp(_level.ExpToNext - _level.Exp);
    }

    // 테스트용 언어 전환: 표의 다음 언어로 바꾼다. 화면 글은 언어 변경 이벤트를 받아 그 자리에서 바뀐다
    // (설정 화면이 생기기 전까지의 확인용). 고른 언어는 설정에 저장된다.
    private static void CycleLanguage()
    {
        LocalizationManager localization = Core.Localization;
        if (localization == null || localization.Languages.Count < 2) return;
        int index = 0;
        for (int i = 0; i < localization.Languages.Count; i++)
            if (localization.Languages[i] == localization.Language) index = i;
        Core.Settings.Language = localization.Languages[(index + 1) % localization.Languages.Count];
    }

    // 스킬을 한 번에 시험하려고 스킬 카드를 한 장씩 넣는다(페인트 7색 포함).
    private void AddSkillCards(SO_LevelUpConfig levelUpConfig)
    {
        for (int i = 0; i < levelUpConfig.SkillCards.Count; i++)
        {
            if (levelUpConfig.SkillCards[i] != null) _hand.Add(HandCard.Of(levelUpConfig.SkillCards[i]));
        }
    }

    // 칩을 한 번에 시험하려고 칩 카드를 한 장씩 넣는다.
    private void AddAttachmentCards(SO_LevelUpConfig levelUpConfig)
    {
        for (int i = 0; i < levelUpConfig.ChipCards.Count; i++)
        {
            if (levelUpConfig.ChipCards[i] != null) _hand.Add(HandCard.Of(levelUpConfig.ChipCards[i]));
        }
    }
}
