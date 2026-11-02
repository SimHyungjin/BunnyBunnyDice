using UnityEngine;

/// <summary>
/// 판 사건마다 낼 효과음 묶음(GameSounds가 쓴다). 큐를 필드로 모은 것이라 이름 오타가 컴파일 에러가 되고 순서와 무관하다(_Core Audio 규칙).
/// 소리 크기·피치·겹침 제한은 큐(SO_SoundCue)마다 있다 — 여기서는 어느 사건에 어느 큐를 낼지만 정한다. 비워 두면 그 사건은 소리가 없다.
/// 같은 동작(주사위가 떨어짐·칩이 놓임·카드를 받음)은 한 칸으로 묶는다 — 입구마다 칸을 두면 늘 같은 큐를 여러 번 끼워야 해서.
/// </summary>
[CreateAssetMenu(fileName = "SO_GameSounds", menuName = "Game/Audio/Game Sounds")]
public sealed class SO_GameSounds : SO_Config
{
    [Header("배경음")]
    [Tooltip("로비(첫 화면·판에서 돌아왔을 때).")]
    public SO_MusicTrack LobbyMusic;
    [Tooltip("판(뛰어들기가 끝나 판이 시작될 때부터). 다시 하기는 처음부터 다시 튼다.")]
    public SO_MusicTrack GameMusic;

    [Header("주사위")]
    [Tooltip("주사위가 판에 떨어졌다 — 빈 칸에 놓기·얹기·스킬의 다시 굴리기(리롤·잭팟·올인, 스킬 소리와 함께 난다)·판 시작 연출의 착지.")]
    public SO_SoundCue DiceLand;
    [Tooltip("굴림에 실패해 주사위가 부서졌다(더블 오어 낫싱).")]
    public SO_SoundCue DiceBust;
    [Tooltip("주사위의 각성이 화면에 드러났다. 풀 하우스는 한꺼번에 여럿이 켜지므로 큐의 최소 간격으로 겹침을 줄인다.")]
    public SO_SoundCue DiceAwaken;

    [Header("칩")]
    [Tooltip("칩이 놓였다 — 칩 놓기·바꿔 끼우기·판 시작 연출의 시작 칩 착지.")]
    public SO_SoundCue ChipLand;

    [Header("카드")]
    [Tooltip("카드를 받았다 — 손패에 들어옴(시작 손패·카드를 주는 스킬·레벨업) 또는 레벨업에서 영웅 강화 카드를 고름(손패로 가지 않는 카드). 한 장에 한 번만 난다.")]
    public SO_SoundCue CardGet;
    [Tooltip("레벨업 카드 고르기 창이 열렸다.")]
    public SO_SoundCue LevelUpOpen;

    [Header("스킬")]
    [Tooltip("컷인이 없는 스킬을 썼다(주사위·판·칸 대상 — 자주 쓰는 스킬).")]
    public SO_SoundCue SkillUse;
    [Tooltip("컷인이 나오는 스킬을 썼다(전용 카드·적 대상 스킬, SkillCutIn.HasCutIn).")]
    public SO_SoundCue SkillUseCutIn;

    [Header("판 시작(판 주사위 연출)")]
    [Tooltip("주사위가 판 가운데에서 솟아 흩어지기 시작했다.")]
    public SO_SoundCue BoardShake;
    [Tooltip("주사위 하나가 포켓에 쏙 빠졌다.")]
    public SO_SoundCue BoardPocket;

    [Header("영웅")]
    [Tooltip("영웅이 주사위 위에 착지했다(올라타 레이즈가 켜지는 순간 — 다른 주사위로 옮겨 타도). 지나가며 밟은 주사위·레이즈 카드에는 나지 않는다.")]
    public SO_SoundCue HeroLand;
    [Tooltip("영웅이 카드를 던졌다(치명타 포함).")]
    public SO_SoundCue HeroThrow;

    [Header("전투")]
    [Tooltip("주사위 하나가 쐈다. 초당 수십 번 나므로 아주 작게, 큐의 동시 재생 수·최소 간격으로 줄인다.")]
    public SO_SoundCue DiceShot;
    [Tooltip("적 하나가 죽었다(보스 제외). 많이 나므로 작게.")]
    public SO_SoundCue MonsterKill;
    [Tooltip("보스가 죽었다(잭팟 보스 포함).")]
    public SO_SoundCue BossKill;
    [Tooltip("적이 목표물에 닿아 피해를 줬다.")]
    public SO_SoundCue GoalHit;

    [Header("웨이브")]
    [Tooltip("웨이브가 시작됐다.")]
    public SO_SoundCue WaveStart;
    [Tooltip("판이 끝났다(목표물이 무너짐·포기).")]
    public SO_SoundCue GameOver;
}
