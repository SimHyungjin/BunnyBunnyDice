using UnityEngine;

/// <summary>
/// 사거리(칩, 파랑 = 사거리): 칩 위 주사위의 사거리를 늘린다.
/// 판 시작 칩으로도, 레벨업 카드로도 얻어 원하는 주사위에 붙인다. 사거리는 눈으로 자라지 않으므로(종류마다 고정) 각성한 파랑 말고는 이 칩만 늘린다.
/// 칸 수가 아니라 비율이다(2026-10-26) — +1칸은 사거리 1.8인 파동(하양)에게만 맞는 넓이가 두 배 넘게 커져 튀었다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Chip/Range", fileName = "SO_ChipCard_Range")]
public sealed class SO_ChipCard_Range : SO_ChipCard
{
    [Tooltip("늘리는 사거리 비율. 0.5면 1.5배.")]
    [Min(0f)] public float Ratio = 0.5f;
}
