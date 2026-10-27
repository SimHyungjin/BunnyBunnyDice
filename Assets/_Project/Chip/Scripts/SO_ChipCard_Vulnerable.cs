using UnityEngine;

/// <summary>
/// 방깍(칩, 하양): 맞힌 적이 잠시 모든 공격에 더 아프게 맞는다. 디버프 1순위 — 하양(파동)이 주변을 통째로 끊김 없이 덮어 가장 잘 퍼뜨린다.
/// 몬스터에 거는 상태라 다른 주사위·영웅·스킬의 피해에도 적용된다 — 같은 길목을 둘 이상이 덮으면 공격력 칩보다 낫게.
/// 겹치지 않고 센 쪽이 남는다(하나면 충분 — 길목마다 깔아 두는 칩). 방어력을 깎던 이전 안(−20)은 0 아래로 내려가면
/// 매 타격 고정 피해가 되어 1발 피해가 작은 범위형에서 튀고, 후반엔 묻혀 사라져서 비율로 바꿨다(2026-10-26).
/// </summary>
[CreateAssetMenu(menuName = "Def/Chip/Vulnerable", fileName = "SO_ChipCard_Vulnerable")]
public sealed class SO_ChipCard_Vulnerable : SO_ChipCard
{
    [Tooltip("받는 피해에 더하는 비율. 0.3이면 1.3배. 방어력보다 먼저 곱한다(치명타와 같다).")]
    [Min(0f)] public float Ratio = 0.3f;
    [Min(0f)] public float Duration = 2f;
}
