using UnityEngine;

/// <summary>하늘 — 관통(2026-11-04 사용자, 전엔 자홍 — 체인과 맞바꿨다): 길고 얇은 곧은 선, 선 위 모두를 맞힌다(기획서 6.5). 사거리 안에서 가장 많이 꿰뚫는 쪽을 겨냥하므로
/// 곧은 통로를 만들어 두면 줄 전체를 꿰뚫는다 — 미로 모양으로 세기가 갈리는 공격. 각성하면 맞은 적의 방어력을 일정 비율 무시한다.</summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Pierce", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Pierce : SO_DiceType
{
    [Header("관통")]
    [Tooltip("관통선의 반폭. 선에서 이 거리 안의 몬스터가 맞는다. 얇을수록 길과 나란할 때만 많이 맞는다 — 범위 공격과 갈리는 지점.")]
    [Min(0.02f)] public float HalfWidth = 0.15f;

    [Header("각성 (눈 20)")]
    [Tooltip("각성하면 맞은 적 방어력의 이 비율을 무시한다(0.5 = 절반). 한 방이 약한 범위형이라 방어력에 다 먹히지 않게 — 줄을 길과 나란히 놓아야 하는 배치 제약의 값(2026-11-04 사용자). " +
             "기본 효과는 색마다 공격 방식뿐이라 각성에만 둔다(전엔 선 굵기 2배 — 체감이 없었다).")]
    [Range(0f, 1f)] public float AwakenPenetration = 0.5f;
}
