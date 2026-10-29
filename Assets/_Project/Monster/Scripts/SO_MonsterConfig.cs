using UnityEngine;

/// <summary>
/// 모든 몬스터가 나눠 쓰는 설정. 위치·크기는 칸 단위(한 칸 = 1)다.
/// 반지름이 종류별이 아니라 여기 하나인 이유: 걷는 길(GridRouteField)을 목표물에서 한 번만 퍼뜨려 모든 몬스터와 경로 표시가 같이 쓰기 때문이다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Monster/Monster Config", fileName = nameof(SO_MonsterConfig))]
public sealed class SO_MonsterConfig : SO_Config
{
    [Tooltip("걷는 길을 구할 때 쓰는 몬스터 반지름. 클수록 벽·주사위에서 멀찍이 돌고, 0이면 모서리 틈으로 새므로 0보다 커야 한다. "
        + "경로 표시도 이 값으로 구한 길을 그린다.")]
    [Range(0.05f, 0.45f)] public float Radius = 0.3f;
    [Tooltip("이동 방향으로 도는 빠르기(도/초).")]
    public float TurnSpeed = 540f;
    [Tooltip("공격선이 몬스터에 닿는 높이(바닥 윗면에서).")]
    public float HitHeight = 0.3f;
    [Tooltip("방어력이 아무리 높아도 원래 피해의 이 비율은 들어간다(기획서 9.11 — 면역이 아니라 효율 차이).")]
    [Range(0f, 1f)] public float MinDamageRatio = 0.2f;

    [Header("포켓에서 나오기 (표시만)")]
    [Tooltip("포켓 구멍에서 솟아 실제 자리까지 오는 시간(초). 판정·이동은 처음부터 실제 자리라 이 동안 표시만 따라온다. 0이면 연출 없음.")]
    [Min(0f)] public float EmergeDuration = 0.4f;
    [Tooltip("나올 때 처음 크기(배율). 구멍보다 큰 공도 구멍에서 나오는 것처럼 보이게 작게 시작한다.")]
    [Range(0.05f, 1f)] public float EmergeStartScale = 0.25f;
    [Tooltip("출발 높이(펠트 윗면 기준). 음수면 구멍 안에서 시작한다.")]
    public float EmergeDepth = -0.35f;
    [Tooltip("튀어 오르는 높이. 구멍에서 튕겨 나오듯 포물선을 그린다.")]
    [Min(0f)] public float EmergeHop = 0.3f;

    [Header("죽는 연출 (표시만)")]
    [Tooltip("일반 사망: 제자리에서 톡 튀며 작아져 사라지는 시간(초). 짧아야 많이 죽어도 판이 어지럽지 않다.")]
    [Min(0f)] public float DeathPopDuration = 0.18f;
    [Tooltip("일반 사망: 튀는 높이(칸).")]
    [Min(0f)] public float DeathPopHop = 0.25f;
    [Tooltip("스킬 처치: 판 주사위처럼 가장 가까운 포켓으로 날아 들어가는 시간(초). 0이면 일반 사망처럼 톡 사라진다. 주사위로 잡은 것과 눈으로 구분되게(기획서 10.2).")]
    [Min(0f)] public float SkillFlyDuration = 0.55f;
    [Tooltip("스킬 처치: 포물선 꼭대기 높이(칸). 솟았다가 구멍에 위에서 꽂힌다.")]
    [Min(0f)] public float SkillFlyHeight = 4f;
    [Tooltip("스킬 처치: 끝의 이 비율 동안 작아지며 구멍으로 쏙 들어간다.")]
    [Range(0.05f, 1f)] public float SkillShrinkPortion = 0.25f;
    [Tooltip("보스 처치: 가장 가까운 포켓으로 날아 들어가는 시간(초) — 스킬 처치와 같은 궤적을 더 크게. 스킬로 잡아도 보스는 이 연출이다. 드문 큰 사건이라 시선을 끌어도 된다.")]
    [Min(0f)] public float BossFlyDuration = 0.9f;
    [Tooltip("보스 처치: 포물선 꼭대기 높이(칸).")]
    [Min(0f)] public float BossFlyHeight = 6f;
    [Tooltip("보스 처치: 끝의 이 비율 동안 작아지며 구멍으로 들어간다.")]
    [Range(0.05f, 1f)] public float BossShrinkPortion = 0.25f;

    [Header("상태 이상")]
    [Tooltip("독 피해를 주는 간격(초). 이 간격마다 한 번씩 방어력을 적용해 피해를 준다.")]
    [Min(0.05f)] public float PoisonTickInterval = 0.5f;
    // 상태는 체력바 아래 네모로 보인다(MonsterHealthBars) — 몸 색은 특성(SO_MonsterTrait.Tint)이 쓰고, 여러 상태가 겹쳐도 하나씩 보이게.
    // 색은 그 상태를 거는 칩 색과 같게 둔다 — 칩과 상태를 한 번만 외우게(칩 색 = 효과, 기획서 9.7).
    [Tooltip("취약(받는 피해 증가) 표시 색. 취약 칩 색(하양).")]
    public Color VulnerableColor = Color.white;
    [Tooltip("둔화 표시 색. 둔화 칩 색(노랑).")]
    public Color SlowColor = new Color(0.96f, 0.85f, 0.2f);
    [Tooltip("얼림 표시 색. 얼림 칩 색(하늘).")]
    public Color FreezeColor = new Color(0.4f, 0.8f, 0.96f);
    [Tooltip("화상(독) 표시 색. 화상 칩 색(자홍).")]
    public Color PoisonColor = new Color(0.9f, 0.25f, 0.75f);
    [Tooltip("처음 잡아 두는 몬스터 칸 수. 넘으면 두 배로 늘린다 — 한 웨이브 최대 마릿수 정도로 두면 판 중에 늘리지 않는다.")]
    [Min(1)] public int InitialCapacity = 128;
    [Tooltip("분열한 조각들이 죽은 자리에서 걷던 방향으로 벌어지는 거리(칸). 완전히 겹치면 한 마리로 보인다.")]
    [Min(0f)] public float SplitOffset = 0.15f;

    [Header("공 무늬 (띠 공)")]
    [Tooltip("띠가 가운데에서 위아래로 덮는 각도(도). 30이면 띠와 양끝이 겉넓이를 반씩 나눈다 — 특성 둘 중 어느 쪽도 작아 보이지 않게. "
        + "특성이 하나면 무늬 없이 그 색 단색 공이다.")]
    [Range(5f, 85f)] public float BandHalfAngle = 30f;

    [Header("체력바")]
    [Tooltip("정점 색을 쓰는 반투명 셰이더(파티클 Unlit, 알파 블렌드). 비우면 체력바를 그리지 않는다.")]
    public Material HealthBarMaterial;
    [Tooltip("바닥 윗면에서 체력바 가운데까지 높이. 몬스터 겉모습 크기(거대)만큼 곱한다.")]
    [Min(0f)] public float HealthBarHeight = 0.75f;
    [Tooltip("체력바 폭·두께(칸). 폭은 겉모습 크기만큼 곱한다 — 거대·보스는 긴 바로 보인다.")]
    [Min(0.01f)] public float HealthBarWidth = 0.55f;
    [Min(0.005f)] public float HealthBarThickness = 0.07f;
    [Tooltip("바탕 안쪽 여백(칸). 채움이 바탕 테두리 안에 들어간다.")]
    [Min(0f)] public float HealthBarBorder = 0.012f;
    public Color HealthBarBackColor = new Color(0.05f, 0.03f, 0.08f, 0.85f);
    [Tooltip("체력이 가득할 때 → 바닥일 때 채움 색.")]
    public Color HealthBarFullColor = new Color(0.35f, 0.95f, 0.4f, 1f);
    public Color HealthBarLowColor = new Color(1f, 0.25f, 0.2f, 1f);
    [Tooltip("다치지 않은 몬스터는 체력바를 숨긴다 — 수십 마리가 다 가득 찬 바를 달면 길이 바로 덮인다. 상태 네모는 그래도 보인다.")]
    public bool HideFullHealthBar = true;

    [Header("상태 표시 (체력바 아래)")]
    [Tooltip("네모 한 변 = 체력바 폭 ÷ 상태 수(4) × 이 비율. 걸린 것만 바 왼쪽 끝부터 틈 없이 붙여 취약·둔화·얼림·화상 순서로 채운다.")]
    [Range(0.1f, 1f)] public float StatusSlotFill = 0.8f;
    [Tooltip("체력바 아래쪽 끝에서 상태 네모 줄까지의 틈(체력바 두께의 배수). 0이면 바에 딱 붙는다.")]
    [Min(0f)] public float StatusRowGap = 0f;
}
