using UnityEngine;

/// <summary>
/// 얼림(칩, 하늘): 맞힌 적을 잠깐 멈추고, 풀린 뒤 한동안은 다시 얼지 않는다(면역 — 계속 얼려 묶어 두지 못하게). 디버프 3순위.
/// 평균으로는 둔화보다 약하지만 순간 완전히 멈춰 세운다 — 목표물 앞에서 새는 적을 막는 위기 대응.
/// 하늘(체인)이 선두 무리를 자주 때려 면역이 풀리는 순간 바로 다시 얼린다. 거대는 이동 방해 저항만큼 짧게 언다(몬스터 쪽에서 줄인다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Chip/Freeze", fileName = "SO_ChipCard_Freeze")]
public sealed class SO_ChipCard_Freeze : SO_ChipCard
{
    [Tooltip("멈추는 시간(초).")]
    [Min(0f)] public float Duration = 0.8f;
    [Tooltip("풀린 뒤 다시 얼지 않는 시간(초). 짧으면 칠 때마다 묶여 웨이브가 멈춘다.")]
    [Min(0f)] public float Immunity = 1.5f;
}
