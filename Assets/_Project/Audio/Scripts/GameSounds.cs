using System;
using UnityEngine;

/// <summary>
/// 판 사건마다 효과음을 낸다(표시만). 판 시스템은 소리를 모르고, 여기가 그 시스템들이 이미 내보내는 이벤트를 구독한다
/// — 소리를 더하거나 바꿀 때 판 규칙 코드를 건드리지 않게.
///
/// 모두 2D로 낸다: 판 전체가 한 화면에 보이는 위에서 본 시점이라 거리 감쇠가 오히려 어디서 났는지를 흐린다.
/// 끌어 놓는 동안(고스트 미리보기)은 소리를 내지 않는다 — 놓을 곳을 고르며 오가는 동안 울리면 시끄럽기만 하고, 놓은 결과(주사위·칩·스킬)가 소리를 낸다.
/// 빠른 시뮬(봇·색 시험) 동안은 내지 않는다 — 한 프레임에 수십 걸음을 돌아 소리가 뭉개지기만 한다.
/// </summary>
public sealed class GameSounds : IDisposable
{
    private readonly SO_GameSounds _sounds;
    private readonly Func<bool> _muted;
    private readonly DiceField _dice;
    private readonly ChipField _chips;
    private readonly CardHand _hand;
    private readonly LevelUpChoice _levelUp;
    private readonly SkillCaster _skills;
    private readonly MonsterField _monsters;
    private readonly GoalHealth _goal;
    private readonly BoardRollPresenter _boardRoll;
    private readonly DiceAttack _diceAttack;
    private readonly HeroCardShots _heroCards;
    private readonly WaveSpawner _waves;
    private readonly HeroRaise _heroRaise;

    public GameSounds(SO_GameSounds sounds, Func<bool> muted, DiceField dice, ChipField chips, CardHand hand,
        LevelUpChoice levelUp, SkillCaster skills, MonsterField monsters, GoalHealth goal, BoardRollPresenter boardRoll,
        DiceAttack diceAttack, HeroCardShots heroCards, WaveSpawner waves, HeroRaise heroRaise)
    {
        _sounds = sounds;
        _muted = muted;
        _dice = dice;
        _chips = chips;
        _hand = hand;
        _levelUp = levelUp;
        _skills = skills;
        _monsters = monsters;
        _goal = goal;
        _boardRoll = boardRoll;
        _diceAttack = diceAttack;
        _heroCards = heroCards;
        _waves = waves;
        _heroRaise = heroRaise;

        _dice.DicePlaced += OnDicePlaced;
        _dice.DiceStacked += OnDiceStacked;
        _dice.DiceRerolled += OnDiceRerolled;
        _dice.DiceBusted += OnDiceBusted;
        _dice.AwakenRevealed += OnAwakenRevealed;
        _chips.ChipPlaced += OnChipPlaced;
        _hand.CardAdded += OnCardAdded;
        _levelUp.Opened += OnLevelUpOpened;
        _levelUp.CardChosen += OnLevelUpChosen;
        _skills.Used += OnSkillUsed;
        _monsters.Killed += OnKilled;
        _monsters.ReachedGoal += OnReachedGoal;
        _goal.Died += OnGoalDied;
        _boardRoll.Started += OnBoardStarted;
        _boardRoll.DieLanded += OnBoardDieLanded;
        _boardRoll.DiePocketed += OnBoardDiePocketed;
        _boardRoll.ChipLanded += OnBoardChipLanded;
        _diceAttack.Fired += OnDiceFired;
        _heroCards.Thrown += OnHeroThrown;
        _waves.WaveStarted += OnWaveStarted;
        _heroRaise.Mounted += OnHeroMounted;
    }

    public void Dispose()
    {
        _dice.DicePlaced -= OnDicePlaced;
        _dice.DiceStacked -= OnDiceStacked;
        _dice.DiceRerolled -= OnDiceRerolled;
        _dice.DiceBusted -= OnDiceBusted;
        _dice.AwakenRevealed -= OnAwakenRevealed;
        _chips.ChipPlaced -= OnChipPlaced;
        _hand.CardAdded -= OnCardAdded;
        _levelUp.Opened -= OnLevelUpOpened;
        _levelUp.CardChosen -= OnLevelUpChosen;
        _skills.Used -= OnSkillUsed;
        _monsters.Killed -= OnKilled;
        _monsters.ReachedGoal -= OnReachedGoal;
        _goal.Died -= OnGoalDied;
        _boardRoll.Started -= OnBoardStarted;
        _boardRoll.DieLanded -= OnBoardDieLanded;
        _boardRoll.DiePocketed -= OnBoardDiePocketed;
        _boardRoll.ChipLanded -= OnBoardChipLanded;
        _diceAttack.Fired -= OnDiceFired;
        _heroCards.Thrown -= OnHeroThrown;
        _waves.WaveStarted -= OnWaveStarted;
        _heroRaise.Mounted -= OnHeroMounted;
    }

    /// <summary>로비 곡으로 바꾼다(겹쳐 바꾸기). 이미 그 곡이면 이어서 흐른다.</summary>
    public void PlayLobbyMusic() => App.PlayMusic(_sounds.LobbyMusic);

    /// <summary>판 곡으로 바꾼다.</summary>
    public void PlayGameMusic() => App.PlayMusic(_sounds.GameMusic);

    private void Play(SO_SoundCue cue)
    {
        if (cue == null || _muted()) return;
        App.PlaySfx(cue);
    }

    private void OnDicePlaced(Vector2Int _) => Play(_sounds.DiceLand);
    private void OnDiceStacked(Vector2Int _) => Play(_sounds.DiceLand);
    private void OnDiceRerolled(Vector2Int _) => Play(_sounds.DiceLand);
    private void OnDiceBusted(Vector2Int _) => Play(_sounds.DiceBust);
    private void OnAwakenRevealed(Vector3 _) => Play(_sounds.DiceAwaken);
    private void OnChipPlaced(Vector2Int _) => Play(_sounds.ChipLand);
    private void OnCardAdded() => Play(_sounds.CardGet);

    private void OnLevelUpOpened() => Play(_sounds.LevelUpOpen);
    // 손패로 가는 카드는 들어올 때(OnCardAdded) 나므로 겹치지 않게, 손패로 가지 않는 영웅 강화 카드만 낸다.
    private void OnLevelUpChosen(HandCard card, int _)
    {
        if (card.IsHero) Play(_sounds.CardGet);
    }

    // 카드마다 소리를 달면 무엇이 무슨 소리인지 헷갈려(사용자), 컷인 있는 스킬과 없는 스킬 두 갈래로만 나눈다 — 컷인과 같은 기준.
    private void OnSkillUsed(SO_SkillCard card)
    {
        if (card != null) Play(SkillCutIn.HasCutIn(card) ? _sounds.SkillUseCutIn : _sounds.SkillUse);
    }

    private void OnKilled(MonsterKind kind, Vector2 _) => Play(kind.IsBoss ? _sounds.BossKill : _sounds.MonsterKill);

    // 피해가 없는 몬스터는 맞는 연출도 없이 빠져나가므로(GameScene) 소리도 내지 않는다.
    private void OnReachedGoal(MonsterKind kind, double _)
    {
        if (kind.GoalDamage > 0) Play(_sounds.GoalHit);
    }

    private void OnGoalDied() => Play(_sounds.GameOver);
    private void OnDiceFired() => Play(_sounds.DiceShot);

    private void OnHeroThrown(bool _) => Play(_sounds.HeroThrow);

    private void OnWaveStarted(int _) => Play(_sounds.WaveStart);
    // 통통 뛰어다니는 착지감 — 레이즈 카드(멀리서 거는 레이즈)는 스킬 소리가 따로 있어 여기에 넣지 않는다.
    private void OnHeroMounted(Vector2Int _) => Play(_sounds.HeroLand);

    private void OnBoardStarted() => Play(_sounds.BoardShake);
    private void OnBoardDieLanded() => Play(_sounds.DiceLand);
    private void OnBoardDiePocketed() => Play(_sounds.BoardPocket);
    private void OnBoardChipLanded() => Play(_sounds.ChipLand);
}
