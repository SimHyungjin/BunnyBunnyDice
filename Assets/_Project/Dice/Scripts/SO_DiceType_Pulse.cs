using UnityEngine;

/// <summary>하양 — 파동: 일정 간격으로 자기 주변 사거리 안 모두를 맞힌다(기획서 6.5). 기본값이 가장 낮고 사거리도 짧다.</summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Pulse", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Pulse : SO_DiceType
{
    [Header("각성 (눈 20)")]
    [Tooltip("두 번째 파동이 첫 파동 뒤 나가는 시간.")]
    [Min(0.05f)] public float AwakenPulseDelay = 0.2f;
    [Tooltip("두 번째 파동의 피해(첫 파동 대비). 하양은 각성이 흔해 작게 둔다(6.5).")]
    [Range(0f, 1f)] public float AwakenPulseRatio = 0.5f;
}
