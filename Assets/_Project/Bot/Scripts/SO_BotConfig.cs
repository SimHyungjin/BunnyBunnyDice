using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 플레이 봇의 판단 값(기획서 11.2·11.5). 봇은 개발 도구지만 판단 값을 고쳐 가며 사람에 가깝게 맞추므로 코드 상수가 아니라 여기 둔다.
/// 레벨업 점수는 웨이브 구간마다 따로 둔다 — 사용자 판 기록(37·35웨이브)에서 고르는 카드가 구간마다 뚜렷이 달랐다:
/// 1~10웨이브 거의 주사위만, 11~20 주사위 3/4 + 잭팟, 21~ 주사위 절반 + 영웅·칩·스킬.
/// 후보 셋 중 점수가 가장 높은 카드를 고른다(같은 상황이면 늘 같은 선택 — 비교가 흔들리지 않게).
/// </summary>
[CreateAssetMenu(menuName = "Def/Bot/Bot Config", fileName = nameof(SO_BotConfig))]
public sealed class SO_BotConfig : SO_Config
{
    /// <summary>한 웨이브 구간의 카드 점수. 높을수록 먼저 고른다.</summary>
    [Serializable]
    public sealed class PhaseScores
    {
        [Tooltip("이 웨이브까지 이 점수를 쓴다. 마지막 구간은 그 뒤 전부.")]
        [Min(1)] public int UntilWave = 10;
        [Tooltip("주사위 카드 = 이 값 + 면체 수 × FacesWeight.")]
        public float Dice = 100f;
        public float FacesWeight = 1f;
        public float HeroDamage = 30f;
        public float HeroCrit = 30f;
        public float Chip = 40f;
        public float AreaSkill = 30f;
        public float Checkmate = 30f;
        public float Check = 30f;
        public float Overdrive = 30f;
        public float Jackpot = 40f;
        public float Reroll = 20f;
        public float Paint = 20f;
        public float AllIn = 10f;
        [Tooltip("그 밖의 카드(폴드 등 봇이 쓰지 않는 카드).")]
        public float Other = 0f;
    }

    /// <summary>
    /// 비교 묶음 하나: 능력치 단계와 카드 종류별 점수 배율. 같은 맵(Seeds)에서 이 둘만 바꿔 돌려 무엇이 판을 바꾸는지 본다.
    /// 배율은 구간 점수(Phases)에 곱한다 — 미로 전 주사위 가산(MazeDiceBonus)에는 곱하지 않는다(어느 성향이든 미로는 먼저 짓는다).
    /// </summary>
    [Serializable]
    public sealed class RunSet
    {
        [Tooltip("요약 CSV의 set 칸에 남는 이름.")]
        public string Label = "";
        [Tooltip("HeroStat 순서: 공격력·공속·이속·사거리·치명 확률·치명 피해. 1~6. 비우면 켤 때의 단계 그대로.")]
        public int[] Stages = new int[0];
        [Min(0f)] public float DiceScale = 1f;
        [Min(0f)] public float ChipScale = 1f;
        [Tooltip("데미지형 스킬 = 영웅 공격력 비례 범위 스킬(포켓·레이크·백스핀·체크메이트, SO_AreaSkillCard).")]
        [Min(0f)] public float DamageSkillScale = 1f;
        [Tooltip("그 밖의 스킬(잭팟·다시 굴리기·페인트·오버드라이브·체크·올인·폴드).")]
        [Min(0f)] public float OtherSkillScale = 1f;
        [Min(0f)] public float HeroScale = 1f;
        [Tooltip("켜면 데미지형 스킬을 뚫릴 때까지 아끼지 않고 적이 AnytimeAreaMinTargets 이상 모이면 바로 쓴다(체크메이트는 보스가 보이면) " +
                 "— 데미지형 위주로 고르면서 아껴 두기만 하면 그 전략을 잴 수 없다.")]
        public bool DamageSkillAnytime;
    }

    [Header("레벨업 점수 (웨이브 구간, 앞에서부터)")]
    public List<PhaseScores> Phases = new List<PhaseScores>();
    [Tooltip("미로를 다 짓기 전이면 주사위 카드에 더한다 — 구간과 상관없이 미로가 먼저다.")]
    public float MazeDiceBonus = 40f;
    [Tooltip("받는 장수가 둘 이상(도박사)이면 한 장마다 점수에 곱해 더하는 비율.")]
    [Min(0f)] public float ExtraCopy = 0.3f;

    [Header("메인 딜 구간 (사용자 플레이 — 범위 주사위·칩·사거리를 한 구간에 모은다)")]
    [Tooltip("메인 딜 구간 반경(칸). 길 위에서 이 반경 안 몰림 합이 가장 큰 칸이 구간 중심이다.")]
    [Min(0.5f)] public float KillZoneRadius = 2.5f;
    [Tooltip("얹기·영웅·오버드라이브: 메인 딜 구간에 닿는 주사위면 점수에 곱한다.")]
    [Min(1f)] public float KillZoneStackWeight = 2f;
    [Tooltip("칩: 메인 딜 구간에 닿는 주사위 밑이면 점수에 곱한다(사거리 칩으로 새로 닿게 되면 그 두 배).")]
    [Min(1f)] public float KillZoneChipWeight = 3f;

    [Header("미로·얹기")]
    [Tooltip("얹은 뒤 범위 주사위(섞인 색·하양)가 되는 자리면 점수에 곱한다 — 몰리는 곳의 범위 주사위를 키운다(사용자 플레이).")]
    [Min(0f)] public float AreaStackWeight = 1.5f;

    [Header("손")]
    [Tooltip("카드 한 장을 쓰는 간격(초, 게임 시간) — 손이 느린 사람 정도.")]
    [Min(0.05f)] public float ActInterval = 0.4f;
    [Tooltip("판이 바뀐 뒤(주사위·칩을 놓은 뒤) 미로 계획을 다시 받기까지(초, 게임 시간) — 생각하는 시간. 그동안 새 주사위는 놓지 않고 칩·스킬은 쓴다. "
        + "탐색은 워커 스레드에서 돌고, 이 시간이 지났는데 안 끝났으면 게임이 멈춰 기다린다(결과는 같다 — 시뮬레이션 재현). "
        + "배속에서 이 시간(÷ 배속)이 탐색 시간보다 짧으면 멈춤이 잦아진다.")]
    [Min(0f)] public float MazeThinkTime = 0.5f;
    [Tooltip("영웅이 움직일 곳을 다시 고르는 간격(초). 평소엔 가장 앞선 적을 쫓고, 뚫릴 때만 주사위에 올라탄다.")]
    [Min(0.05f)] public float HeroInterval = 1f;
    [Tooltip("지금 주사위보다 이만큼 나아야 옮긴다 — 비슷한 주사위 사이를 오가며 걷기만 하지 않게.")]
    [Min(1f)] public float HeroSwitchGain = 1.3f;
    [Tooltip("영웅 디버프 카드가 있으면 선두를 쫓는 대신, 디버프가 안 묻은 적이 영웅 사거리 안에 이만큼 이상 모이는 곳으로 다닌다 — "
        + "디버프 카드의 값어치는 여러 적에게 묻히는 데서 나온다(사용자). 대상 고르기(가장 가까운 적)는 그대로라 선두를 막는 역할은 남는다.")]
    [Min(1)] public int PaintMinTargets = 3;

    [Header("뚫림 — 이때만 영웅이 올라타고 스킬을 쓴다(사용자: 뚫릴 것 같을 때만 썼다)")]
    [Tooltip("구간을 넘어온 적 중 체력이 이 비율 이상 남은 적이 BreachCount 이상이면 뚫린다고 본다.")]
    [Range(0f, 1f)] public float BreachHealthRatio = 0.4f;
    [Min(1)] public int BreachCount = 3;
    [Tooltip("보스는 구간 중심 + 이 칸 수 안에 들어오면 뚫린다고 본다(일찍 대비).")]
    [Min(0)] public int KillZoneMargin = 2;
    [Tooltip("목표물 중심에서 이 거리(칸) 안에 적이 오면 무조건 뚫린 것.")]
    [Min(0f)] public float DangerRadius = 2f;
    [Tooltip("뚫릴 때 범위 스킬: 구간을 넘어온 적이 원 안에 이만큼이면 쓴다.")]
    [Min(1)] public int BreachAreaMinTargets = 2;
    [Tooltip("뚫릴 때 체크: 구간을 넘어온 적이 이만큼이면 쓴다.")]
    [Min(1)] public int CheckBreachCount = 5;

    [Tooltip("데미지형 스킬을 바로 쓰는 묶음(RunSet.DamageSkillAnytime): 원 안 적이 이만큼이면 쓴다.")]
    [Min(1)] public int AnytimeAreaMinTargets = 8;

    [Header("그 밖의 스킬")]
    [Tooltip("올인: 판 주사위의 평균 눈 비율이 이보다 낮을 때 쓴다.")]
    [Range(0f, 1f)] public float AllInBelow = 0.35f;

    [Header("여러 판 돌리기")]
    [Tooltip("켠 뒤 이만큼 판을 돌리면 요약 파일(RunLogs/bot_summary_*.csv)을 남기고 멈춘다. 0이면 끌 때까지 계속.")]
    [Min(0)] public int BatchRuns = 5;
    [Tooltip("켜면 켤 때의 캐릭터를 뺀 나머지를 돌아가며 RunsPerCharacter판씩 돌리고(SO_HeroConfig.Characters 순서), 다 돌면 요약을 남기고 멈춘다. BatchRuns 대신.")]
    public bool RotateCharacters;
    [Min(1)] public int RunsPerCharacter = 1;
    [Tooltip("비어 있지 않으면 지금 캐릭터로 묶음마다 RunsPerSet판씩 돌린다(BatchRuns·RotateCharacters 대신). " +
             "캐릭터를 넓히기 전에 한 캐릭터로 단계·카드 성향의 영향을 먼저 본다. 다 돌거나 봇을 끄면 켤 때의 단계로 되돌린다.")]
    public List<RunSet> RunSets = new List<RunSet>();
    [Min(1)] public int RunsPerSet = 5;
    [Tooltip("비어 있지 않으면 묶음마다 k번째 판을 Seeds[k]의 맵으로 돈다 — 묶음끼리 같은 맵으로 짝지어 비교해 맵 운(시작 칩 배치)을 덜어 낸다. " +
             "맵·칩만 같고 레벨업 후보·굴림은 판마다 다르다.")]
    public List<int> Seeds = new List<int>();
    [Tooltip("판이 끝난 뒤 다시 하기까지(초, 실제 시간).")]
    [Min(0f)] public float RetryDelay = 1.5f;
    [Tooltip("봇이 켜져 있는 동안의 게임 속도 배율(보면서 돌리기 — A).")]
    [Min(0.1f)] public float FastForward = 3f;

    [Header("빠른 시뮬 (Shift+A — 화면을 그리지 않는다)")]
    [Tooltip("한 프레임에 도는 게임 걸음 수(SO_GameSpeedConfig.FixedStep 하나가 한 걸음). 판 그리기를 끄고 이만큼씩 돌린다 — 클수록 빠르지만 프레임이 길어져 창이 굼떠진다. "
        + "걸음 크기는 보면서 돌리기와 같아 결과가 같다.")]
    [Min(1)] public int SimStepsPerFrame = 200;
    [Tooltip("빠른 시뮬의 게임 속도 배율. 실제 빠르기는 위 걸음 수가 정한다(넘는 시간은 버린다) — 그 상한을 늘 채울 만큼 크게 둔다.")]
    [Min(1f)] public float SimSpeed = 1000f;

    /// <summary>wave에 쓸 점수 구간. 구간이 없으면 null.</summary>
    public PhaseScores GetPhase(int wave)
    {
        for (int i = 0; i < Phases.Count; i++)
            if (wave <= Phases[i].UntilWave) return Phases[i];
        return Phases.Count > 0 ? Phases[Phases.Count - 1] : null;
    }
}
