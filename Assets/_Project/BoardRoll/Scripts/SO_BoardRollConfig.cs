using UnityEngine;

/// <summary>
/// 판 주사위 연출 설정(기획서 3.3). 결과는 맵 생성이 이미 정했고(MapData.Roll), 이 연출은 그 결과를 보여 줄 뿐이다.
/// 순서: 판 가운데에서 분수처럼 터져 흩어짐 → 다 떨어지면 눈이 함께 톡 커지며 확정 → 하나씩 하늘로 날아 자기 번호 포켓에 쏙(그 포켓이 빛남)
/// → 시작 칩(칩 카드)이 왼쪽부터 하나씩 착착 떨어짐 → 경로 표시·웨이브 시작. 건너뛸 수 없고, 그동안 카드 배치를 잠근다.
/// 시간은 실제 시간(초)이다.
/// </summary>
[CreateAssetMenu(menuName = "Def/BoardRoll/Board Roll Config", fileName = nameof(SO_BoardRollConfig))]
public sealed class SO_BoardRollConfig : SO_Config
{
    [Tooltip("끄면 연출 없이 바로 시작한다(테스트용).")]
    public bool Enabled = true;

    [Header("주사위 모양")]
    [Tooltip("타워 주사위(d6) 크기에 곱한다. 작게 둬 판을 덜 가린다.")]
    [Min(0.1f)] public float DieScale = 0.8f;
    [Tooltip("몸체 색. 타워 주사위 색(공격 방식)과 헷갈리지 않게 무채색.")]
    public Color DieColor = new Color(0.96f, 0.93f, 0.98f);
    [Tooltip("눈 글자 색.")]
    public Color PipColor = new Color(0.141f, 0.082f, 0.184f);

    [Header("1. 분수처럼 터지기")]
    [Tooltip("주사위마다 터져 나오는 시차.")]
    [Min(0f)] public float FountainStagger = 0.06f;
    [Tooltip("판 가운데에서 떨어질 자리까지 나는 시간.")]
    [Min(0.05f)] public float FountainDuration = 0.7f;
    [Tooltip("솟는 높이(주사위마다 이 사이에서 고른다).")]
    public Vector2 FountainHeight = new Vector2(2.2f, 3.2f);
    [Tooltip("나는 동안 도는 바퀴 수. 끝으로 갈수록 줄어 반듯하게 떨어진다.")]
    [Min(0f)] public float FountainTurns = 2.5f;
    [Tooltip("판 가운데에서 떨어질 자리가 흩어지는 범위(칸, 반폭 x·z).")]
    public Vector2 ScatterExtent = new Vector2(3.5f, 1.8f);
    [Tooltip("떨어질 자리끼리의 최소 거리(칸).")]
    [Min(0f)] public float MinSpacing = 1f;

    [Header("1-1. 데구르르 구르기")]
    [Tooltip("떨어진 뒤 흩어지던 방향으로 모서리를 넘기며 구르는 기본 횟수(면). 맵 계산(미로 검사)이 안 끝났으면 끝날 때까지 더 구른다.")]
    [Min(0)] public int RollTurns = 3;
    [Tooltip("첫 번 구르는 시간. 한 번 구를 때마다 RollSlowdown배씩 느려진다(RollMaxTurnDuration까지).")]
    [Min(0.02f)] public float RollTurnDuration = 0.1f;
    [Min(1f)] public float RollSlowdown = 1.35f;
    [Tooltip("더 구를 때 한 번 구르는 시간의 상한 — 계산을 오래 기다려도 멈춘 듯 느려지지 않게.")]
    [Min(0.02f)] public float RollMaxTurnDuration = 0.4f;

    [Header("2. 확정")]
    [Tooltip("다 떨어지면 주사위와 눈이 함께 톡 커졌다 돌아온다.")]
    [Min(0f)] public float ConfirmDuration = 0.3f;
    [Min(1f)] public float ConfirmScale = 1.4f;

    [Header("3. 포켓으로 날아가기")]
    [Tooltip("주사위마다 날아가는 시차.")]
    [Min(0f)] public float FlyStagger = 0.12f;
    [Min(0.05f)] public float FlyDuration = 0.55f;
    [Tooltip("포물선 꼭대기 높이. 하늘로 솟았다가 구멍에 위에서 꽂힌다.")]
    [Min(0f)] public float FlyHeight = 4f;
    [Tooltip("구멍에 빠질 때 내려가는 깊이(펠트 윗면 기준, 음수).")]
    public float SinkDepth = -0.4f;
    [Tooltip("구멍에 빠지며 줄어드는 크기(배율)와, 줄기 시작하는 시점(비행의 끝 몇 %).")]
    [Range(0.05f, 1f)] public float SinkScale = 0.3f;
    [Range(0.05f, 1f)] public float SinkPortion = 0.25f;

    [Header("4. 시작 칩 착착")]
    [Tooltip("마지막 주사위가 빠진 뒤 첫 칩까지.")]
    [Min(0f)] public float ChipDelay = 0.1f;
    [Tooltip("칩마다 떨어지는 간격. 칩이 많으면 전체가 ChipMaxTotal을 넘지 않게 줄인다.")]
    [Min(0f)] public float ChipInterval = 0.07f;
    [Min(0f)] public float ChipMaxTotal = 1f;
    [Min(0.01f)] public float ChipFallDuration = 0.18f;
    [Min(0f)] public float ChipDropHeight = 2f;
    [Tooltip("닿을 때 눌렸다 돌아오는 세기(세로 비율)와 시간.")]
    [Range(0f, 0.9f)] public float ChipSquash = 0.25f;
    [Min(0f)] public float ChipSquashDuration = 0.12f;

    [Tooltip("다 끝나고 웨이브를 시작하기 전 잠깐의 여유.")]
    [Min(0f)] public float EndDelay = 0.15f;

    [Header("판 치우기 (다시 하기·로비로 — BoardSweep)")]
    [Tooltip("주사위·칩이 날아오르기 시작하는 시각을 이 안에서 무작위로 흩는다.")]
    [Min(0f)] public float SweepSpread = 0.35f;
    [Tooltip("하나가 날아올라 사라지기까지.")]
    [Min(0.05f)] public float SweepDuration = 0.55f;
    [Tooltip("날아오르는 높이(맵 단위). 화면 밖으로 빠질 만큼.")]
    [Min(0f)] public float SweepHeight = 12f;
    [Tooltip("날아오르며 도는 각도.")]
    [Min(0f)] public float SweepSpin = 540f;
}
