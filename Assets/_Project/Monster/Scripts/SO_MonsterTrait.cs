using UnityEngine;

/// <summary>
/// 몬스터 특성 하나(기획서 9.1 — 분노·재생·보호막·단단함·거대, 특성마다 에셋 하나). 몬스터 = 기본 종류(SO_MonsterType) + 특성 몇 개이고,
/// 조합은 웨이브 설정(SO_WaveConfig)이 정한다 — "보스 = 거대 + 특성 하나" 같은 조합마다 몬스터 에셋을 따로 만들지 않게.
/// 겹치면 배율(체력·속도·크기·이동 방해)은 곱하고 고정값(방어력·재생·목표물 피해·경험치)은 더한다(MonsterKind).
/// 웨이브 설정이 참조하므로 의존성으로 함께 로드된다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Monster/Monster Trait", fileName = "SO_MonsterTrait_")]
public sealed class SO_MonsterTrait : ScriptableObject
{
    public string DisplayName;
    [Tooltip("띠 공에 칠하는 색(기본 색에 섞은 값). 알파가 섞는 정도이고 0이면 색이 없는 특성(거대 — 크기로 보인다). "
        + "색 있는 특성이 둘이면 에셋 이름 순서로 앞이 띠, 뒤가 양끝(MonsterKind.GetBallColors).")]
    public Color Tint = new Color(1f, 1f, 1f, 0f);
    [Tooltip("겉모습 크기 배율. 걷는 길 계산의 반지름은 바꾸지 않는다(SO_MonsterConfig.Radius를 모두 같이 쓴다).")]
    [Min(0.1f)] public float VisualScale = 1f;

    [Header("배율 (겹치면 곱한다)")]
    [Min(0.01f)] public float HealthScale = 1f;
    [Min(0.01f)] public float SpeedScale = 1f;
    [Tooltip("둔화 비율·얼림·멈춤 시간·끌기·밀어내기 거리를 줄이는 비율. 0.5면 절반만 받는다 — 면역이 아니라 저항. " +
             "거대(보스)도 0이다(2026-11-01 사용자 — 90%면 칩 디버프가 보스에게 쓸모없고, 역할이 적은 스킬 카드가 보스에게서라도 세야 한다).")]
    [Range(0f, 1f)] public float ImpairResist;
    [Tooltip("받는 주사위 쪽 피해(주사위 고정·최대 체력 피해, 화상)를 줄이는 비율 — 영웅·스킬 피해는 줄이지 않는다(2026-11-01). 0.9면 10%만 받는다(거대 — 2026-10-29: 최대 체력 피해가 모든 색에 붙자 보스가 1초도 못 버텼다. " +
             "체력을 올리면 최대 체력 피해는 그대로라 피해를 줄인다). 겹치면 곱으로 줄어든다.")]
    [Range(0f, 0.99f)] public float DamageResist;

    [Header("고정값 (겹치면 더한다)")]
    [Tooltip("받는 피해에서 빼는 값의 고정 몫(단단함). 최소 피해 비율은 SO_MonsterConfig.MinDamageRatio.")]
    [Min(0f)] public float Armor;
    [Tooltip("방어력의 체력 비례 몫 — 자기 최대 체력 × 이 값을 Armor에 더한다(기획서 9.3). " +
             "고정값만 두면 체력·피해가 웨이브마다 크는데 방어력만 멈춰 중반부터 아무것도 막지 못했다(2026-10-30). " +
             "보스도 자기 체력 기준이다(2026-11-01) — 웨이브 기본 체력 기준이면 보스에게 방어력이 장식이었다.")]
    [Min(0f)] public float ArmorHealthRatio;
    [Tooltip("초당 회복량(최대 체력에 대한 비율, 재생). 화상이 걸려 있으면 줄어든다(화상 칩). 큰 한 방(빨강 강타)으로 회복 전에 끊는 것도 맞선다.")]
    [Min(0f)] public float RegenRatio;
    [Tooltip("보호막 — 나올 때 (ShieldHits + 웨이브 × ShieldPerWave)대만큼 맞는 동안은 피해를 ShieldDamageRatio만 받는다(지속 피해인 화상은 횟수를 깎지 않는다). " +
             "단단함(작은 한 방을 막는다)과 반대로 느리고 큰 한 방이 손해다 — 빠르게 여러 번·여러 마리를 때리는 색이 보호막을 금방 벗긴다. " +
             "고정 6대·50%는 색 시험에서 거의 아무것도 막지 못해 웨이브에 비례해 다 막게 바꿨다(2026-11-01).")]
    [Min(0)] public int ShieldHits;
    [Min(0f)] public float ShieldPerWave;
    [Tooltip("분노 — 잃은 체력 비율 × 이 값만큼 이동 속도가 늘어난다(1이면 체력이 바닥일 때 2배). 깎다 만 몹이 빨라져 도망친다 — 한 번에 끝내는 큰 한 방이나 둔화·얼음이 답이다(2026-11-01 — 그냥 빠르기만 하던 빠름을 바꿨다. 에셋 이름은 SO_MonsterTrait_Fast 그대로).")]
    [Min(0f)] public float EnrageSpeedBonus;
    [Tooltip("목표물 피해에 더하는 값(거대).")]
    [Min(0)] public int GoalDamageBonus;
    [Tooltip("경험치에 더하는 값.")]
    [Min(0)] public int ExpBonus;

    [Header("보호막 비율 (겹치면 곱한다)")]
    [Tooltip("보호막이 남은 동안 받는 피해 비율. 0.5면 절반.")]
    [Range(0f, 1f)] public float ShieldDamageRatio = 1f;
}
