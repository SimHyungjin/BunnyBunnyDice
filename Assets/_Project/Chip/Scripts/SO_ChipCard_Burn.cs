using UnityEngine;

/// <summary>
/// 화상(칩, 자홍): 맞힌 적이 잠시 초당 최대 체력의 일정 비율 피해를 입는다. 디버프 4순위.
/// 지속 시간이 길어(4초) 쏘는 간격이 긴 관통(자홍)도 끊기지 않고, 긴 줄에 한꺼번에 불을 붙인다.
/// 고정 피해는 후반 체력에 묻히므로 비율로 둔다 — 체력이 복리로 느는 후반일수록 강하다. 방어력을 받지 않고, 보스는 저항(SO_MonsterConfig)만큼 줄여 받는다.
/// 걸려 있는 동안 재생(몬스터 특성)도 줄인다(2026-11-01 사용자 — 화상 칩 체감이 별로 없었다). 한때 글을 줄이려고 뺐다가(2026-10-26) 되살렸다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Chip/Burn", fileName = "SO_ChipCard_Burn")]
public sealed class SO_ChipCard_Burn : SO_ChipCard
{
    [Tooltip("초당 최대 체력에 대한 피해 비율. 0.02면 초당 2%. 다시 걸면 쌓이지 않고 시간만 새로 센다.")]
    [Range(0f, 0.2f)] public float MaxHealthRatio = 0.02f;
    [Min(0f)] public float Duration = 4f;
    [Tooltip("걸려 있는 동안 재생을 줄이는 비율(0.5 = 절반). 빅 스택이면 세기를 곱하되 1(완전히 막음)을 넘지 않는다.")]
    [Range(0f, 1f)] public float RegenReduction = 0.5f;
}
