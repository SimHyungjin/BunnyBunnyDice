using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 플레이 봇의 판단 값(기획서 11.2·11.5). 봇은 개발 도구지만 판단 값을 고쳐 가며 사람에 가깝게 맞추므로 코드 상수가 아니라 여기 둔다.
/// 레벨업은 사용자 판 교본 2차(2026-10-28)의 우선순위를 따른다 — 영웅 능력치 카드가 어떤 상황이든 먼저, 없으면 미로 → 메인 딜 구간 순.
/// 후보 셋 중 점수가 가장 높은 카드를 고른다(같은 상황이면 늘 같은 선택 — 비교가 흔들리지 않게).
/// </summary>
[CreateAssetMenu(menuName = "Def/Bot/Bot Config", fileName = nameof(SO_BotConfig))]
public sealed class SO_BotConfig : SO_Config
{
    /// <summary>
    /// 레벨업 카드 점수(높을수록 먼저). 사용자 판 교본 2차(2026-10-28):
    /// 능력치 카드는 어떤 상황이든 먼저(1웨이브부터 — 그래서 미로가 늦어 죽기도 한다, 사용자 감수).
    /// 없으면 미로가 덜 지어졌을 때는 미로 주사위(빨강 > 파랑·자홍 > 나머지, d6·d12)와 화상 칩(미로의 일부 — 길 전체에 화상을 오래 묻힌다),
    /// 어느 정도 지어졌으면 d20(메인 딜 구간에 모은다)과 메인 딜 구간 칩(빠진 디버프 → 빨강·초록).
    /// </summary>
    [Serializable]
    public sealed class ChoiceScores
    {
        [Tooltip("영웅 능력치 카드(공격력·투사체·치명 확률·치명 피해). 받는 장수(x2)만큼 곱한다. 미로 주사위(220~300)보다 조금 아래 — " +
                 "1000(언제나 먼저)이면 주사위가 최대 체력 피해로 후반까지 일하게 된 뒤(2026-10-29) 초반에 주사위를 못 모아 평균 15웨이브에 무너졌다. 같은 맵 6개: ×0.1 36 · ×0.2 43.5 · ×0.35 38웨이브.")]
        public float HeroStat = 200f;
        [Header("미로를 짓는 동안 (MazeEnoughDice 전)")]
        public float MazeDiceRed = 300f;
        [Tooltip("파랑(저격)·자홍(관통) — 미로가 약할 때 넓게 덮어 준다.")]
        public float MazeDiceBlueMagenta = 260f;
        public float MazeDiceOther = 220f;
        [Tooltip("화상 칩 — 미로의 일부라 미로 주사위만큼 친다.")]
        public float MazeBurnChip = 280f;
        [Tooltip("미로를 짓는 동안의 d20(미로가 먼저라 낮다).")]
        public float EarlyD20 = 150f;
        [Tooltip("미로를 짓는 동안의 화상 말고 다른 칩(디버프·빨강·초록·사거리) — 메인 딜 구간 칩이라 미로가 먼저다.")]
        public float EarlyChip = 120f;
        [Header("미로를 어느 정도 지은 뒤")]
        [Tooltip("범위 색(노랑·자홍·하늘·하양) d20 — 메인 딜 구간에 모은다.")]
        public float D20Area = 400f;
        public float D20Single = 350f;
        [Tooltip("메인 딜 구간에 아직 없는 디버프 칩(둔화·얼림·취약 — 종류마다 하나면 된다).")]
        public float MissingDebuffChip = 330f;
        [Tooltip("빨강(공격력)·초록(공속) 칩 — 메인 딜 구간 딜링 주사위 밑.")]
        public float AttackChip = 300f;
        public float BurnChip = 250f;
        [Tooltip("d6·d12 — 미로를 지은 뒤엔 메인 딜 구간 주사위에 얹는다.")]
        public float LateDice = 150f;
        [Header("언제든")]
        [Tooltip("메인 딜 구간에 이미 있는 디버프 칩.")]
        public float DuplicateDebuffChip = 120f;
        public float RangeChip = 120f;
        [Tooltip("영웅 디버프 카드(사용자: 좋지만 미묘).")]
        public float HeroDebuff = 40f;
        public float Jackpot = 200f;
        public float EnemySkill = 120f;
        public float Check = 100f;
        public float Raise = 150f;
        public float Reroll = 80f;
        public float Paint = 100f;
        public float AllIn = 50f;
        [Tooltip("캐시 아웃(하이 롤러) — 미로를 어느 정도 지은 뒤, 메인 딜 구간을 막는 칸이 있고 손에 캐시 아웃이 없을 때만. 아니면 0.")]
        [UnityEngine.Serialization.FormerlySerializedAs("Fold")]
        public float CashOut = 250f;
        [Tooltip("그 밖의 캐릭터 전용 카드(풀 하우스·브레이크 샷·스네이크 아이즈·더블 업) — 판을 뒤집는 카드라 높게.")]
        public float Exclusive = 300f;
    }

    /// <summary>
    /// 비교 묶음 하나: 능력치 단계와 카드 종류별 점수 배율. 같은 맵(Seeds)에서 이 둘만 바꿔 돌려 무엇이 판을 바꾸는지 본다.
    /// 배율은 레벨업 점수(Choice)에 종류별로 곱한다.
    /// </summary>
    [Serializable]
    public sealed class RunSet
    {
        [Tooltip("요약 CSV의 set 칸에 남는 이름.")]
        public string Label = "";
        [Tooltip("HeroStat 순서: 공격력·공속·이속·사거리·치명 확률·치명 피해·투사체. 1~6. 비우면 켤 때의 단계 그대로.")]
        public int[] Stages = new int[0];
        [Min(0f)] public float DiceScale = 1f;
        [Min(0f)] public float ChipScale = 1f;
        [Tooltip("데미지형 스킬 = 영웅 공격력 비례 적 대상 스킬(포켓·랙·백스핀·체크메이트, SO_EnemySkillCard).")]
        [Min(0f)] public float DamageSkillScale = 1f;
        [Tooltip("그 밖의 스킬(잭팟·리롤·페인트·레이즈·체크·올인·전용 카드).")]
        [Min(0f)] public float OtherSkillScale = 1f;
        [Min(0f)] public float HeroScale = 1f;
        [Tooltip("켜면 데미지형 스킬을 뚫릴 때까지 아끼지 않고 판 위 적이 AnytimeEnemyMinTargets 이상이면 바로 쓴다(체크메이트는 보스가 보이면) " +
                 "— 데미지형 위주로 고르면서 아껴 두기만 하면 그 전략을 잴 수 없다.")]
        public bool DamageSkillAnytime;
    }

    [Header("레벨업 (사용자 판 교본 2차)")]
    public ChoiceScores Choice = new ChoiceScores();
    [Tooltip("놓인 주사위가 이만큼이면 미로를 \"어느 정도\" 지은 것으로 본다 — 그때부터 d20을 메인 딜 구간에 모은다. "
        + "낮을수록 일찍 모아 고점이 오르지만 초반에 죽기 쉽다(사용자). 미로 계획은 20개(SO_MazePlanConfig.TargetDice).")]
    [Min(0)] public int MazeEnoughDice = 14;
    [Tooltip("미로 칸을 메인 딜 구간 밖부터 놓는다(구간 칸은 d20 자리로 남긴다). 가운데 포켓(목표물까지 3칸)이 열린 맵에서는 길을 늘릴 칸이 " +
             "목표물 앞, 곧 구간 안이라 가장 급한 칸을 미루는지 시험한다(2026-10-29 — 보스가 가운데 포켓인 맵 평균 21웨이브, 안 열린 맵 38).")]
    public bool MazeOutsideZoneFirst = true;

    [Header("메인 딜 구간 (사용자 플레이 — 범위 주사위·칩·사거리를 한 구간에 모은다)")]
    [Tooltip("메인 딜 구간 반경(칸). 길 위에서 이 반경 안 몰림 합이 가장 큰 칸이 구간 중심이다.")]
    [Min(0.5f)] public float KillZoneRadius = 2.5f;
    [Tooltip("얹기·영웅·레이즈: 메인 딜 구간에 닿는 주사위면 점수에 곱한다.")]
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
    [Tooltip("영웅이 움직일 곳을 다시 고르는 간격(초). 미로를 짓는 동안엔 남은 적을 쫓아 정리하고, 지은 뒤엔 메인 딜 구간에서 오가며 싸운다.")]
    [Min(0.05f)] public float HeroInterval = 1f;
    [Tooltip("미로를 지은 뒤 영웅이 오가는 구역: 메인 딜 구간 중심에서 출현 쪽으로 이 칸 수(앞)부터 목표물 쪽으로 이 칸 수(뒤)까지. "
        + "사용자: 메인 딜 구간 앞부터 디버프가 묻은 구역까지 카이팅한다.")]
    [Min(0)] public int KiteFront = 3;
    [Min(0)] public int KiteBack = 2;
    [Tooltip("영웅 디버프 카드가 있으면 선두를 쫓는 대신, 디버프가 안 묻은 적이 영웅 사거리 안에 이만큼 이상 모이는 곳으로 다닌다 — "
        + "디버프 카드의 값어치는 여러 적에게 묻히는 데서 나온다(사용자). 대상 고르기(가장 가까운 적)는 그대로라 선두를 막는 역할은 남는다.")]
    [Min(1)] public int PaintMinTargets = 3;

    [Header("레이즈 (영웅 올라타기·스킬 카드 — BotEscape)")]
    [Tooltip("남은 길에서 받을 피해 어림에 곱한다. 1이면 어림 그대로, 낮을수록 \"못 잡을 것\"으로 봐 일찍·자주 쓴다.")]
    [Min(0.1f)] public float RaiseMargin = 1f;
    [Tooltip("메인 딜 구간 중심 + 이 칸 수 안에 들어온 적부터 빠져나갈지 본다.")]
    [Min(0)] public int EscapeLookAhead = 2;

    [Header("뚫림 — 이때만 적 대상 스킬·체크를 쓴다(사용자: 뚫릴 것 같을 때만 썼다)")]
    [Tooltip("구간을 넘어온 적 중 체력이 이 비율 이상 남은 적이 BreachCount 이상이면 뚫린다고 본다.")]
    [Range(0f, 1f)] public float BreachHealthRatio = 0.4f;
    [Min(1)] public int BreachCount = 3;
    [Tooltip("보스는 구간 중심 + 이 칸 수 안에 들어오면 뚫린다고 본다(일찍 대비).")]
    [Min(0)] public int KillZoneMargin = 2;
    [Tooltip("목표물 중심에서 이 거리(칸) 안에 적이 오면 무조건 뚫린 것.")]
    [Min(0f)] public float DangerRadius = 2f;
    [Tooltip("뚫릴 때 적 대상 스킬(판 전체): 구간을 넘어온 적이 이만큼이면 쓴다.")]
    [Min(1)] public int BreachEnemyMinTargets = 2;
    [Tooltip("뚫릴 때 체크: 구간을 넘어온 적이 이만큼이면 쓴다.")]
    [Min(1)] public int CheckBreachCount = 5;

    [Tooltip("데미지형 스킬을 바로 쓰는 묶음(RunSet.DamageSkillAnytime): 판 위 적이 이만큼이면 쓴다.")]
    [Min(1)] public int AnytimeEnemyMinTargets = 8;

    [Header("그 밖의 스킬")]
    [Tooltip("올인: 판 주사위의 평균 눈 비율이 이보다 낮을 때 쓴다.")]
    [Range(0f, 1f)] public float AllInBelow = 0.35f;

    [Header("여러 판 돌리기")]
    [Tooltip("켠 뒤 이만큼 판을 돌리면 요약 파일(RunLogs/bot_summary_*.csv)을 남기고 멈춘다. 0이면 끌 때까지 계속.")]
    [Min(0)] public int BatchRuns = 5;
    [Tooltip("켜면 켤 때의 캐릭터를 뺀 나머지를 돌아가며 RunsPerCharacter판씩 돌리고(SO_HeroConfig.Characters 순서), 다 돌면 요약을 남기고 멈춘다. BatchRuns 대신.")]
    public bool RotateCharacters;
    [Min(1)] public int RunsPerCharacter = 1;
    [Tooltip("캐릭터를 돌아가며 돌 때 모든 캐릭터에 거는 능력치 단계(HeroStat 순서, 비우면 세이브 그대로). 단계는 세이브에 남으므로 다 돌거나 끄면 캐릭터마다 되돌린다. " +
             "Seeds가 있으면 캐릭터마다 k번째 판을 Seeds[k]의 맵으로 — 같은 맵·같은 단계로 특성 차이만 본다.")]
    public int[] RotationStages = new int[0];
    [Tooltip("비어 있지 않으면 이 캐릭터만 돈다 — 한 캐릭터만 고친 뒤 같은 맵으로 그 캐릭터만 다시 잴 때(나머지는 앞선 요약과 짝짓는다).")]
    public List<SO_HeroCharacter> RotationOnly = new List<SO_HeroCharacter>();
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

}
