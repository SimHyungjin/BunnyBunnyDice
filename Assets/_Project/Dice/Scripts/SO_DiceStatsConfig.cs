using UnityEngine;

/// <summary>
/// 모든 주사위가 같이 쓰는 규칙(기획서 9.4): 눈에 따른 성장 곡선, 치명타, 각성 눈.
/// 주사위마다의 기본 능력치·공격 방식 값·각성 값은 종류마다의 SO_DiceType에 둔다. 수치는 모두 잠정이다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Dice Stats Config", fileName = nameof(SO_DiceStatsConfig))]
public sealed class SO_DiceStatsConfig : SO_Config
{
    [Header("성장")]
    [Tooltip("눈에 따른 성장 곡선의 지수. 1이면 눈에 비례, 작을수록 높은 눈에서 완만해진다(9.10 — 눈 1~20을 그대로 비례시키면 20배 차이). " +
             "능력치와 공격 방식 값(폭발 반경·타겟 수 등)이 같은 빠르기로 자라게 함께 쓴다.")]
    [Range(0.2f, 1f)] public float GrowthCurve = 0.6f;

    [Header("치명타")]
    [Tooltip("눈 하나당 치명타 확률. 0.01이면 눈 20에서 20%(9.4). 직접 맞힌 피해마다 굴린다.")]
    [Range(0f, 0.1f)] public float CritChancePerPip = 0.01f;
    [Tooltip("모든 주사위 공통 치명타 배율. 피해에만 적용한다.")]
    [Min(1f)] public float CritMultiplier = 2f;

    [Header("각성")]
    [Tooltip("색이 있는 주사위의 눈이 이 값에 닿으면 각성한다(기획서 9.4). 20이면 d20을 꽉 채웠을 때만 — 세기 단계 없이 늘 온전한 효과.")]
    [Min(1)] public int AwakenPips = 20;
    [Tooltip("각성하면 모든 색 공통으로 한 방 피해(고정 + 최대 체력 피해)에 곱한다 — 공격력만 키우면 후반엔 최대 체력 피해가 대부분이라 각성이 묻힌다. " +
             "색마다의 각성 효과는 그 위에 더해진다(2026-10-29).")]
    [Min(1f)] public float AwakenDamageScale = 2f;

    /// <summary>눈에 따른 성장 몫 (눈 − 1)^곡선.</summary>
    public float GetGrowth(int pips) => Mathf.Pow(Mathf.Max(pips - 1, 0), GrowthCurve);

    public bool IsAwakened(in DiceState dice) => DiceRules.IsAwakened(dice, AwakenPips);
}
