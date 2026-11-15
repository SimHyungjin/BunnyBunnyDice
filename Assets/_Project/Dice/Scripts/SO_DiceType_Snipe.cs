using UnityEngine;

/// <summary>
/// 빨강 — 강타: 강하고 느린 한 방, 짧은 사거리(기획서 6.5).
/// 체력 낮은 적에게는 남는 피해가 버려진다(오버딜). 클래스 이름(Snipe)은 옛 이름인데 에셋 참조(GUID)를 지키려고 그대로 둔다.
/// 기본으로 매 공격 확정 치명타다(배율은 공통 배율, SO_DiceStatsConfig.CritMultiplier). 각성하면 그 치명타 배율이 더 오른다.
/// 2026-11-14 사용자: 단색은 기본 피해가 모자라 옛 각성(확정 치명)을 기본으로 내리고, 각성은 자기 피해를 더 키우는 효과로 —
/// 적에게 거는 효과(보호막 깨기 등)는 한 마리에 하나만 걸려 같은 색을 둘째부터 놓을 이유가 없다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Snipe", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Snipe : SO_DiceType
{
    [Header("각성 (눈 20)")]
    [Tooltip("각성하면 치명타 배율에 더하는 값(1 = 공통 배율 ×2가 ×3). 카드에는 치명타 피해 +%로 보인다.")]
    [Min(0f)] public float AwakenCritBonus = 1f;
}
