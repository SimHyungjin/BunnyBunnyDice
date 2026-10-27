using UnityEngine;

/// <summary>공격 속도(칩, 초록 = 속도): 칩 위 주사위의 공격 속도를 늘린다(기획서 9.7, 2026-10-26 개편 — 칩 색 = 효과).</summary>
[CreateAssetMenu(menuName = "Def/Chip/Attack Speed", fileName = "SO_ChipCard_AttackSpeed")]
public sealed class SO_ChipCard_AttackSpeed : SO_ChipCard
{
    [Tooltip("늘리는 공격 속도 비율. 0.5면 1.5배.")]
    [Min(0f)] public float Ratio = 0.5f;
}
