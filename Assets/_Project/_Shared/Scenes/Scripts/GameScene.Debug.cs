using UnityEngine.InputSystem;

/// <summary>
/// 테스트 키·테스트 창(F1)과 그 명령들. 에디터·개발 빌드에서만 만든다 — 출시 빌드에서는 SetUpDebugTools가 비어 호출째 빠진다.
/// 판 흐름이 읽는 테스트 상태(경험치 막기·무적)는 GameDebugFlags에 모아 판 흐름 코드에 #if를 넣지 않는다.
/// </summary>
public sealed partial class GameScene
{
    partial void SetUpDebugTools(SO_LevelUpConfig levelUpConfig, SO_DiceConfig diceConfig, SO_HeroConfig heroConfig);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    partial void SetUpDebugTools(SO_LevelUpConfig levelUpConfig, SO_DiceConfig diceConfig, SO_HeroConfig heroConfig)
    {
        // 테스트 키. 같은 목록이 테스트 창(F1)의 "기능" 탭에 버튼·키 안내로 나온다.
        GameDebugKeys keys = _mapRoot.gameObject.AddComponent<GameDebugKeys>();
        keys.Add(Key.R, false, "새 맵(새 판, 로비에서는 테이블만 바꾼다)", () => GenerateMap(System.Environment.TickCount));
        keys.Add(Key.C, false, "바로 레벨업(다음 레벨까지 남은 경험치를 채운다)", LevelUpNow);
        keys.Add(Key.U, false, "칩 카드 한 장씩 추가", () => AddAttachmentCards(levelUpConfig));
        keys.Add(Key.K, false, "스킬 카드 한 장씩 추가", () => AddSkillCards(levelUpConfig));
        keys.Add(Key.N, false, "다음 웨이브 바로 시작", () => SkipWaves(1));
        keys.Add(Key.N, true, $"{LongSkip}웨이브 건너뛰기", () => SkipWaves(LongSkip));
        keys.Add(Key.E, false, "경험치 막기", () => _debug.ExpBlocked = !_debug.ExpBlocked, () => _debug.ExpBlocked);
        keys.Add(Key.J, false, "다음 보스를 잭팟 보스로(보스 웨이브가 시작될 때 정해진다)", () => _waves.ForceJackpot());
        keys.Add(Key.I, false, "무적(목표물이 피해를 받지 않는다)", () => _debug.GoalInvincible = !_debug.GoalInvincible, () => _debug.GoalInvincible);
        keys.Add(Key.L, false, "언어 바꾸기(한국어↔영어)", CycleLanguage);
        // 로비·뛰어들기 연출을 되풀이해 보려고. 결과 화면의 "로비로"와 같은 길이다(판을 접고 새 테이블).
        keys.Add(Key.B, false, "로비로(판을 접는다)", EnterLobby);
        // 로비·판 어디서나 켠다. 지면 다시 하기를 눌러 계속 돌고, 판마다 측정 기록 CSV가 남는다(기획서 11.2).
        // 보면서 돌리기는 봇 행동을 눈으로 확인하려고(행동 원칙을 고칠 때), 빠른 시뮬은 판 수를 많이 돌리려고 나눴다 — 결과는 같다.
        keys.Add(Key.A, false, "자동 플레이(봇, 보면서 — SO_BotConfig.BatchRuns판 뒤 요약을 남기고 멈춘다)", () => ToggleBot(false), () => _bot.Enabled && !_bot.Fast);
        keys.Add(Key.A, true, "빠른 시뮬(봇, 화면을 그리지 않는다 — 결과는 A와 같다)", () => ToggleBot(true), () => _bot.Enabled && _bot.Fast);
        // 색마다 순수 딜을 재는 시험(기획서 11.5). 빠른 시뮬로 SO_ColorTestConfig.Seeds × Colors판을 돌고 RunLogs/color_test_*.csv를 남긴다.
        _colorTest = new ColorPowerTest(_colorTestConfig, () => _map, _diceField, _monsters, _waves, TestRetry);
        _colorTest.RunningChanged += SetColorTestMode;
        keys.Add(Key.T, false, "색 시험(미로 전부를 한 색 d20 눈 20으로 — 영웅·칩 없이, 적은 늘 디버프)", ToggleColorTest, () => _colorTest.Running);
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
#endif
}
