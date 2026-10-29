using UnityEngine;

/// <summary>
/// 백스핀(적 대상): 모든 적을 지금 걷는 방향의 반대쪽으로 튕겨낸다(기획서 5.3) — 끌어치기처럼 맞은 공이 뒤로 밀려난다.
/// 길을 거슬러 가는 것이 아니라 곧게 밀려나므로, 벽·주사위·칩에 닿으면 거기서 멈춘다. 시간을 버는 기술.
/// 거대·보스는 이동 방해 저항만큼 짧게 밀려난다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Backspin", fileName = "SO_SkillCard_Backspin")]
public sealed class SO_SkillCard_Backspin : SO_AreaSkillCard
{
    [Tooltip("밀어내는 거리(칸). 저항이 있으면 곱한다.")]
    [Min(0f)] public float Distance = 4f;
    [Tooltip("몸체가 튕겨 미끄러지는 시간(초, 표시만 — 판정은 바로 밀린 자리에서 한다). 점점 느려진다.")]
    [Min(0f)] public float SlideDuration = 0.35f;
}
