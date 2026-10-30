using UnityEngine;

/// <summary>
/// 백스핀(적 대상): 모든 적을 지금 걷는 방향의 반대쪽으로 튕겨낸다(기획서 8) — 끌어치기처럼 맞은 공이 뒤로 밀려난다.
/// 길을 거슬러 가는 것이 아니라 곧게 밀려나 벽·주사위·칩에 닿을 때까지 간다(2026-10-31 사용자 — 처음엔 4칸까지). 미로가 길수록 멀리 밀려
/// 미로를 짓는 보상이 된다. 시간을 버는 기술. 거대·보스는 이동 방해 저항만큼 짧게 밀려난다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Backspin", fileName = "SO_SkillCard_Backspin")]
public sealed class SO_SkillCard_Backspin : SO_EnemySkillCard
{
    [Tooltip("몸체가 튕겨 미끄러지는 빠르기(칸/초, 표시만 — 판정은 바로 밀린 자리에서 한다). 점점 느려진다. 밀리는 거리가 제각각이라 시간이 아니라 빠르기로 둔다.")]
    [Min(0.1f)] public float SlideSpeed = 12f;
}
