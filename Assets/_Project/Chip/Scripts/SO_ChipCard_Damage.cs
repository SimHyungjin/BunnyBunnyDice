using UnityEngine;

/// <summary>
/// 공격력(칩, 빨강 = 힘): 칩 위 주사위의 공격력을 늘린다(기획서 7, 2026-10-26 개편 — 칩 색 = 효과).
/// 받는 피해를 늘리는 방깍(하양)과 세기는 비슷하지만, 이 칩은 이 주사위 하나에만, 방깍은 맞은 적을 때리는 모두에게 붙는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Chip/Damage", fileName = "SO_ChipCard_Damage")]
public sealed class SO_ChipCard_Damage : SO_ChipCard
{
    [Tooltip("늘리는 공격력 비율. 0.5면 1.5배.")]
    [Min(0f)] public float Ratio = 0.5f;
}
