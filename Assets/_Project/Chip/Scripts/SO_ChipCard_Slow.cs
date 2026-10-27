using UnityEngine;

/// <summary>
/// 둔화(칩, 노랑): 맞힌 적의 이동 속도를 낮춘다. 디버프 2순위 — 거의 쉬지 않고 걸려 맞은 적 모두가 길에 오래 머문다.
/// 노랑(폭발)이 0.5초마다 뭉친 곳에 다시 걸어 끊기지 않는다. 빠름에 맞선다. 거대의 이동 방해 저항은 몬스터 쪽이 줄인다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Chip/Slow", fileName = "SO_ChipCard_Slow")]
public sealed class SO_ChipCard_Slow : SO_ChipCard
{
    [Tooltip("이동 속도를 줄이는 비율. 0.4면 60% 속도로 걷는다.")]
    [Range(0f, 1f)] public float Ratio = 0.4f;
    [Min(0f)] public float Duration = 1.5f;
}
