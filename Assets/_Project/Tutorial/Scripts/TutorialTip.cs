using System;
using System.Collections.Generic;

/// <summary>
/// 팁 종류. 세이브에는 이름(문자열)으로 남기므로 순서를 바꿔도 되지만, 이름을 바꾸면 본 기록이 끊긴다.
/// 언제 뜨는지는 TutorialFlow가 종류마다 정한다 — 칩·스킬·전용·영웅 강화 팁은 레벨업 창에 그 종류가 처음 후보로 나왔을 때다.
/// </summary>
public enum TutorialTipId
{
    DiceCard,
    WaveStart,
    HeroMove,
    ChipCard,
    SkillCard,
    ExclusiveCard,
    HeroCard,
    HandLimit,
    // 영웅이 처음 주사위에 올라탔을 때(레이즈).
    Raise,
}

/// <summary>구멍을 뚫어 비출 대상. 화면 위 자리는 TutorialTargets가 매 프레임 다시 잰다(손패 부채꼴·영웅이 움직이므로).</summary>
public enum TutorialTarget
{
    None,
    // 손패에서 첫 주사위 카드.
    DiceCard,
    // 손패 전체.
    Hand,
    // 마지막으로 놓거나 얹은 주사위(없으면 판 위 아무 주사위).
    PlacedDice,
    Hero,
    PlayButton,
    // 판 전체(칸이 깔린 영역).
    Board,
    // 레벨업 창 후보 중 그 종류의 첫 카드.
    OfferChip,
    OfferSkill,
    OfferExclusive,
    OfferHero,
}

/// <summary>장을 넘기는 방법. Tap 말고는 구멍 안에서 그 행동을 해야 넘어가고, 그동안 구멍 밖 입력은 막는다.</summary>
public enum TutorialAction
{
    Tap,
    PlaceDice,
    StartWave,
    MoveHero,
}

/// <summary>팁 한 장: 글 + 비출 곳(둘까지) + 넘기는 방법.</summary>
[Serializable]
public sealed class TutorialPage
{
    // 숫자는 {0} 자리로 둔다(TutorialFlow가 손패 뭉치 상한을 넣는다).
    [Localized] public string Text;
    public TutorialTarget TargetA;
    public TutorialTarget TargetB;
    public TutorialAction Action;
    // 비추지 않고 누름만 통과시키는 곳(행동형 장 — 카드를 놓을 판, 영웅을 보낼 판).
    // 판은 원근으로 사다리꼴이라 사각 구멍으로 비추면 판 밖까지 밝혀지고 화면 대부분이 구멍이 되어 어색했다 — 판은 어둡게 둔 채 누를 수만 있게 한다.
    public TutorialTarget InputArea;
}

/// <summary>팁 하나 — 한 번에 이어 보이는 여러 장. 다 넘기면 본 것으로 저장한다.</summary>
[Serializable]
public sealed class TutorialTip
{
    public TutorialTipId Id;
    public List<TutorialPage> Pages = new List<TutorialPage>();
}
