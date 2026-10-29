using UnityEngine;

/// <summary>
/// 자동 플레이 — 봇(기획서 11.4)과 색 시험(ColorPowerTest), 둘이 같이 쓰는 빠른 시뮬과 정해 둔 맵 차례.
/// 판 흐름(GameScene)은 여기 함수 몇 개(AutoPlayRunStarting·AutoPlayBattleReady·AutoPlayGameOver)만 부르고 봇·시험을 모른다.
/// 봇은 출시 빌드에서도 만들어 두지만 켜는 곳이 테스트 키뿐이라 돌지 않는다. 색 시험은 테스트 키와 함께 개발 빌드에서만 만든다(GameScene.Debug).
/// </summary>
public sealed partial class GameScene
{
    // 미로 계획(어시스트·봇, 기획서 11.4)과 자동 플레이 봇(테스트 키 A).
    private MazePlanner _mazePlanner;
    private GameBot _bot;
    private SO_BotConfig _botConfig;
    // 색 시험(테스트 — 미로 전부를 한 색 d20 눈 20으로). 켜는 동안 무적·경험치 막기·영웅 공격 끔·적 이속 배율을 걸고, 끄면 켤 때 값으로 되돌린다.
    private ColorPowerTest _colorTest;
    private SO_ColorTestConfig _colorTestConfig;
    private bool _savedInvincible;
    private bool _savedExpBlocked;
    // 빠른 시뮬 동안 판 카메라가 그리던 층(되돌릴 값). 모든 층(Everything)이 -1이라 값으로 시뮬 여부를 가리지 않는다.
    private bool _simCulling;
    private int _simSavedCulling;

    // 봇은 사람과 같은 길(손패·배치·스킬·레벨업 고르기)만 부른다 — 그래서 판 시스템을 다 만든 뒤에 만든다.
    private void CreateBot(SO_BotConfig botConfig, SO_HeroConfig heroConfig, SO_DiceConfig diceConfig,
        SO_DiceStatsConfig diceStatsConfig)
    {
        _mazePlanner = new MazePlanner(_mazeConfig, () => _map, _diceField);
        _bot = new GameBot(botConfig, () => _map, () => _waves.Wave, _heroStats, heroConfig.Characters, _diceField, _chips, _hand, _placement, _skills, _monsters, _hero, diceConfig,
            diceStatsConfig, () => heroConfig.Overdrive, _levelUp, _speed, _mazePlanner, () => !_drag.Locked && !_lobbyStage.IsActive && !_monsters.Paused, BotRetry);
        _botConfig = botConfig;
        _bot.EnabledChanged += _ => ApplySimMode();
    }

    private void DisposeAutoPlay()
    {
        _bot?.Dispose();
        _colorTest?.Dispose();
        _mazePlanner?.Dispose();
    }

    // 봇·색 시험이 도는 동안은 첫 웨이브를 기다리지 않는다(WaveStartGate).
    private bool IsAutoPlaying => (_bot != null && _bot.Enabled) || (_colorTest != null && _colorTest.Running);

    private bool IsSim => (_bot != null && _bot.Enabled && _bot.Fast) || (_colorTest != null && _colorTest.Running);

    private void AutoPlayRunStarting() => _bot?.BeginRun();

    private void AutoPlayBattleReady() => _colorTest?.OnBattleReady();

    private void AutoPlayGameOver(RunSummary run) => _bot?.OnGameOver(_waveLog.Records, _seed, run.Wave);

    // 빠른 시뮬 중이면 걸음 크기는 그대로(결과가 같게) 한 프레임 걸음 수만 시뮬 값으로.
    private void ApplyFixedStep()
    {
        Core.Loop.SetGameFixedStep(_speedConfig.FixedStep > 0f ? _speedConfig.FixedStep : 0f,
            IsSim ? _botConfig.SimStepsPerFrame : _speedConfig.MaxStepsPerFrame);
    }

    // 빠른 시뮬: 판 카메라가 아무 층도 그리지 않게 하고(그리기 비용이 걸음 계산을 잡아먹지 않게) 한 프레임에 걸음을 많이 돈다.
    // 봇이 꺼지면(정한 판 수를 채워도) 되돌린다. UI(HUD 웨이브 수·결과 화면)는 그대로 그려져 진행은 보인다.
    // 카메라를 끄지 않는 이유: 끄면 화면을 지우는 카메라가 없어 UI가 지나간 자리가 겹겹이 남는다 — 층만 비우면 배경으로 지우기는 한다.
    private void ApplySimMode()
    {
        ApplyFixedStep();
        Camera camera = Core.Camera != null ? Core.Camera.MainCamera : null;
        if (camera == null) return;
        bool sim = IsSim;
        if (sim == _simCulling) return;
        _simCulling = sim;
        if (sim) _simSavedCulling = camera.cullingMask;
        camera.cullingMask = sim ? 0 : _simSavedCulling;
    }

    // 봇 켜기(A = 보면서, Shift+A = 빠른 시뮬). 로비에서 켜면 "게임 시작"과 같은 길로 들어간다 — 로비 화면이 닫히고 영웅이 뛰어든 뒤 봇이 손을 쓴다.
    private void ToggleBot(bool fast)
    {
        if (_bot.Enabled)
        {
            _bot.SetEnabled(false);
            return;
        }
        _bot.SetEnabled(true, fast);
        if (_lobbyStage.IsActive) _lobbyScreen.Start();
    }

    // 봇이 새 판을 시작한다(판이 끝난 뒤·비교 묶음의 첫 판). 판 중이면 다시 하기와 같은 길,
    // 로비면 테이블만 정해 둔 맵으로 바꾼다 — 뒤이어 "게임 시작"으로 뛰어들면 그 판으로 시작한다(로비에서 판 치우기를 하지 않게).
    private void BotRetry()
    {
        if (!_lobbyStage.IsActive)
        {
            Retry();
            return;
        }
        GenerateMap(TakeSeed(out int seed) ? seed : System.Environment.TickCount);
    }

    // 정해 둔 다음 맵(색 시험·봇 비교 묶음). 없으면 false(무작위 맵).
    private bool TakeSeed(out int seed)
    {
        if (_colorTest != null && _colorTest.TryTakeSeed(out seed)) return true;
        if (_bot != null && _bot.TryTakeSeed(out seed)) return true;
        seed = 0;
        return false;
    }

    // 색 시험 켜기·끄기. 봇과 함께 돌지 않는다(둘 다 판을 다시 시작한다).
    private void ToggleColorTest()
    {
        if (!_colorTest.Running && _bot.Enabled) _bot.SetEnabled(false);
        _colorTest.Toggle();
    }

    // 색 시험의 다음 판: 봇과 같은 길이고, 로비면 테이블을 그 맵으로 바꾼 뒤 뛰어든다.
    private void TestRetry()
    {
        BotRetry();
        if (_lobbyStage.IsActive && !_lobbyStage.IsEntering) _lobbyScreen.Start();
    }

    // 색 시험 동안의 테스트 상태 — 주사위 딜만 남긴다(ColorPowerTest). 끄면 켤 때 값으로 되돌린다.
    private void SetColorTestMode(bool on)
    {
        if (on)
        {
            _savedInvincible = _debug.GoalInvincible;
            _savedExpBlocked = _debug.ExpBlocked;
        }
        _debug.GoalInvincible = on || _savedInvincible;
        _debug.ExpBlocked = on || _savedExpBlocked;
        _heroAttack.Enabled = !on;
        _monsters.SpeedScale = on ? _colorTestConfig.SpeedScale : 1f;
        _speed.FastForward = on ? _botConfig.SimSpeed : 1f;
        ApplySimMode();
    }
}
