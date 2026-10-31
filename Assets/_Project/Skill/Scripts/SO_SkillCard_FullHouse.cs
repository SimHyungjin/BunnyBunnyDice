using UnityEngine;

/// <summary>
/// 풀 하우스(레인보우 전용, 주사위 대상): 한동안 그 자리에 7색 주사위가 같이 있는 것처럼 쏜다(2026-10-29 사용자 — 판을 뒤집는 카드라 강하게).
/// 색마다 자기 사거리·공격 간격으로 따로 쏘고(2026-10-31 사용자), 눈·레이즈·칩 배율은 7색 모두에 걸린다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Full House", fileName = "SO_SkillCard_FullHouse")]
public sealed class SO_SkillCard_FullHouse : SO_SkillCard
{
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 10f;
    [Tooltip("쓰는 순간 주사위에서 퍼지는 고리 반경(칸).")]
    [Min(0f)] public float BurstRadius = 1.2f;

    public override SkillTarget Target => SkillTarget.Dice;
}
