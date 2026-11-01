using System;
using UnityEngine;

/// <summary>
/// 풀 하우스(레인보우 전용, 주사위 대상): 한동안 그 자리에 7색 주사위가 같이 있는 것처럼 쏜다(2026-10-29 사용자 — 판을 뒤집는 카드라 강하게).
/// 색마다 자기 사거리·공격 간격으로 따로 쏘고(2026-10-31 사용자), 같은 시간 동안 레이즈도 걸린다. 눈·레이즈·칩 배율은 7색 모두에 걸린다.
/// 그동안은 눈과 상관없이 각성한 것으로 쏜다(2026-11-01 사용자 — 지운 세븐의 강제 각성처럼. 눈이 낮은 판은 5웨이브 보스도 못 잡았다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Full House", fileName = "SO_SkillCard_FullHouse")]
public sealed class SO_SkillCard_FullHouse : SO_SkillCard
{
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 10f;
    [Tooltip("쓰는 순간 주사위에서 퍼지는 고리 반경(칸).")]
    [Min(0f)] public float BurstRadius = 1.2f;
    [Tooltip("지속 시간 동안 함께 거는 레이즈(배율을 이 카드에서 읽는다 — 값을 한 곳에만 둔다). 색마다 따로 쏘게 해도 다른 전용 카드보다 약해 더했다(2026-10-31 사용자). 비우면 레이즈 없음.")]
    public SO_SkillCard_Raise Raise;

    public override SkillTarget Target => SkillTarget.Dice;

    // 실행 중 조정(GameTuning)이 원본 레이즈 에셋을 고치지 않게 복사본으로 바꿔 끼운다.
    public override void SwapNested(Func<SO_Config, SO_Config> swap) => Raise = (SO_SkillCard_Raise)swap(Raise);
}
