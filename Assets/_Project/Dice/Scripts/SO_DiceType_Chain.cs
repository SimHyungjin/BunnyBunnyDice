using UnityEngine;

/// <summary>하늘 — 체인: 선두를 맞히고 가까운 몬스터로 차례로 튄다, 튈 때마다 약해진다(기획서 6.5).</summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Chain", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Chain : SO_DiceType
{
    [Header("체인")]
    [Tooltip("다음 대상으로 튈 수 있는 거리. 주사위 사거리와 무관하게 맞은 몬스터에서 잰다.")]
    [Min(0.1f)] public float JumpRange = 1.6f;
    [Tooltip("튈 때마다 피해에 곱하는 값. 0.5면 튈 때마다 절반.")]
    [Range(0f, 0.95f)] public float Falloff = 0.5f;
    [Tooltip("튈 피해가 이 값보다 작아지면 멈춘다(방어력 전 피해). 횟수 대신 이것으로 끊어, 공격력이 클수록 멀리 이어진다.")]
    [Min(0.01f)] public float MinJumpDamage = 10f;

    [Header("각성 (눈 20)")]
    [Tooltip("각성한 감쇠. 0.75면 튈 때마다 25%만 줄어 훨씬 길게 이어진다.")]
    [Range(0f, 0.95f)] public float AwakenFalloff = 0.75f;
}
