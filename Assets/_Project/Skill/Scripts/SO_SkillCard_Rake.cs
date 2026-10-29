using UnityEngine;

/// <summary>
/// 레이크(적 대상): 모든 적을 놓은 자리로 단숨에 끌어와 뭉친다(기획서 5.3). 폭발(노랑)·파동(하양)이 한 번에 많이 맞히게 하는 준비 기술.
/// 판 전체로 넓어져 적 대상 스킬 중 피해 배율이 가장 낮다. 보스도 끌려온다(2026-10-29 사용자 결정 — 처음엔 원 안의 적만, 보스는 움직이지 않았다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Rake", fileName = "SO_SkillCard_Rake")]
public sealed class SO_SkillCard_Rake : SO_AreaSkillCard
{
    [Tooltip("모음점 둘레에 흩어 세우는 거리(칸). 0이면 한 점에 완전히 겹쳐 한 마리로 보인다.")]
    [Min(0f)] public float Scatter = 0.15f;
    [Tooltip("몸체가 빨려 들어오는 시간(초, 표시만 — 판정은 바로 모음점에서 한다). 점점 빨라진다.")]
    [Min(0f)] public float SlideDuration = 0.25f;
    [Tooltip("쓰는 순간 모음점에서 퍼지는 고리 반경(칸) — 어디로 모였는지 보이게.")]
    [Min(0f)] public float BurstRadius = 1.2f;
}
