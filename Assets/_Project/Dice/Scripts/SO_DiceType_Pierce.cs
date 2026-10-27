using UnityEngine;

/// <summary>자홍 — 관통: 길고 얇은 곧은 선, 선 위 모두를 맞힌다(기획서 9.4). 사거리 안에서 가장 많이 꿰뚫는 쪽을 겨냥하므로
/// 곧은 통로를 만들어 두면 줄 전체를 꿰뚫는다 — 미로 모양으로 세기가 갈리는 공격.</summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Pierce", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Pierce : SO_DiceType
{
    [Header("관통")]
    [Tooltip("관통선의 반폭. 선에서 이 거리 안의 몬스터가 맞는다. 얇을수록 길과 나란할 때만 많이 맞는다 — 범위 공격과 갈리는 지점.")]
    [Min(0.02f)] public float HalfWidth = 0.15f;

    [Header("각성 (눈 20)")]
    [Tooltip("각성하면 반폭에 곱하는 값.")]
    [Min(1f)] public float AwakenWidthScale = 2f;
}
