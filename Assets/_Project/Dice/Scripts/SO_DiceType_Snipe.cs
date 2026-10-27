using UnityEngine;

/// <summary>
/// 빨강 — 강타: 강하고 느린 한 방, 짧은 사거리(기획서 9.4, 2026-10-26 개편 — 이전에는 파랑의 저격이었다).
/// 체력 낮은 적에게는 남는 피해가 버려진다(오버딜). 클래스 이름은 에셋 참조(GUID)를 지키려고 그대로 둔다.
/// 한 방마다 맞은 적 최대 체력의 일부를 더 준다(2026-10-26) — 주사위 피해는 웨이브를 따라 커지지 않아 후반 체력(복리)에 묻혔다.
/// 체력 비례라 큰 적(보스)일수록 세고, 무리의 작은 적에게는 거의 그대로다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Snipe", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Snipe : SO_DiceType
{
    [Tooltip("한 방마다 더하는 피해 = 맞은 적 최대 체력 × 이 값. 치명타면 같이 두 배가 된다.")]
    [Min(0f)] public float MaxHealthDamage = 0.005f;

    // 각성: 매 공격 확정 치명타(배율은 공통 배율, SO_DiceStatsConfig.CritMultiplier).
}
