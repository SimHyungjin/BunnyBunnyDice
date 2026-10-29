using UnityEngine;

/// <summary>
/// 주사위 한 종류(색 상태 하나)의 정의 — 종류마다 에셋 하나(기획서 6.5). 이름·색·기본 능력치는 여기, 공격 방식에만 있는 값과 각성은
/// 공격 방식마다의 하위 클래스에 둔다. 그래서 주사위 하나를 고칠 때 파일 하나만 열면 되고, 인스펙터에는 그 공격 방식의 값만 보인다.
/// 색 → 공격 방식 대응은 "어느 하위 클래스로 만든 에셋인가"가 정한다. 쏘는 규칙은 DiceAttack이 맡는다(데이터와 규칙을 떼어 둔다).
/// 등급(단색 > 2색 > 하양)은 배율을 따로 두지 않고 기본값으로 표현한다: 단색일수록 높게.
/// 능력치 = 기본 × (1 + 성장률 × (눈 − 1)^곡선) — 공격력·공격 속도·공격 방식 값만 자라고 사거리는 고정이다.
/// 곡선 등 모든 주사위가 같이 쓰는 값은 SO_DiceStatsConfig. 수치는 모두 잠정이다.
/// 주사위 설정(SO_DiceConfig)의 종류 목록이 참조하므로 의존성으로 함께 로드된다. 크기는 칸 단위, 시간은 초다.
/// </summary>
public abstract class SO_DiceType : SO_Config
{
    [Tooltip("이 종류의 색 상태. 종류 목록 안에서 겹치지 않게.")]
    public DiceColor Colors;
    [Tooltip("한 단어 색 이름(기획서 6.5 7색 섞기). 기획·조정용 — 카드에는 아이콘 색으로 보인다.")]
    [Localized] public string DisplayName;
    public Color Color = Color.gray;

    [Header("카드")]
    [Tooltip("공격 방식 이름(기획서 6.5). 카드 제목.")]
    [Localized] public string AttackName;
    [Tooltip("한두 줄 설명. 카드를 누르고 있으면 읽힌다.")]
    [Localized, TextArea] public string Description;
    [Tooltip("강한 상대(몬스터 특성, 기획서 9.1). 비우면 표시하지 않는다.")]
    [Localized] public string StrongAgainst;
    [Tooltip("약한 상대. 비우면 표시하지 않는다.")]
    [Localized] public string WeakAgainst;
    [Tooltip("각성 효과 한 줄. 조건(눈이 상한)과 세기(눈에 비례)는 모든 주사위가 같아 적지 않는다 — 얹으면 면체가 바뀌어 숫자는 금방 틀린다.")]
    [Localized, TextArea] public string AwakenDescription;

    [Header("기본 능력치")]
    public float Damage = 30f;
    [Tooltip("눈에 따른 공격력 성장률. 모든 색이 공통으로 공격력이 크고, 공격 방식마다 하나가 더 큰다.")]
    public float DamageGrowth = 0.4f;
    [Tooltip("초당 공격 수.")]
    public float AttackSpeed = 1f;
    [Tooltip("눈에 따른 공격 속도 성장률. 속사(초록)만 크게.")]
    public float AttackSpeedGrowth;
    [Tooltip("사거리(칸). 눈으로 자라지 않는다 — 놓기 전에 사거리를 보고 자리를 고르므로, 얹을 때마다 바뀌면 미로 설계가 흔들린다.")]
    public float Range = 2.5f;

    [Header("최대 체력 피해")]
    [Tooltip("한 방마다 더하는 피해 = 맞은 적 최대 체력 × 이 값(2026-10-29 — 고정 피해만으로는 웨이브 체력 복리를 못 따라간다). " +
             "눈으로 자라지 않고 각성 때만 고정 피해와 함께 커진다(SO_DiceStatsConfig.AwakenDamageScale). 범위형은 맞은 적마다 — 그래서 단일형보다 훨씬 작게 둔다.")]
    [Min(0f)] public float MaxHealthDamage;
}
