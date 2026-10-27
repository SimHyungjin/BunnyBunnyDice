using UnityEngine;

/// <summary>
/// 레이크(범위·즉발): 원 안의 적을 모음점으로 단숨에 끌어와 뭉친다(기획서 5.3). 폭발(노랑)·파동(하양)이 한 번에 많이 맞히게 하는 준비 기술.
/// 처음엔 몇 초 동안 끄는 지속형이었으나 범위 스킬은 모두 즉발로 정했다(2026-10-24).
/// 보스는 끌려오지 않고, 원 안에 보스가 있으면 보스 자리가 모음점이 된다 — 무거운 보스에게 다른 적이 붙는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Rake", fileName = "SO_SkillCard_Rake")]
public sealed class SO_SkillCard_Rake : SO_AreaSkillCard
{
    [Tooltip("모음점 둘레에 흩어 세우는 거리(칸). 0이면 한 점에 완전히 겹쳐 한 마리로 보인다.")]
    [Min(0f)] public float Scatter = 0.15f;
    [Tooltip("몸체가 빨려 들어오는 시간(초, 표시만 — 판정은 바로 모음점에서 한다). 점점 빨라진다.")]
    [Min(0f)] public float SlideDuration = 0.25f;
}
