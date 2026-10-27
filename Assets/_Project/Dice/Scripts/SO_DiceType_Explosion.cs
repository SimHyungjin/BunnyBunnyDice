using UnityEngine;

/// <summary>
/// 노랑 — 범위 폭발: 사거리 안에서 가장 많이 휘말리는 몬스터를 맞히고 그 자리에서 터져 반경 안 다른 몬스터에게도 피해(기획서 9.4).
/// 2026-10-26 개편 전에는 빨강이었다. 각성의 불길은 뺐다(나중에 스킬로 — 범위형 각성은 모두 맞는 수를 늘리는 쪽으로 맞춘다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Dice/Type/Explosion", fileName = "SO_Dice_")]
public sealed class SO_DiceType_Explosion : SO_DiceType
{
    [Header("범위 폭발")]
    [Tooltip("맞은 자리를 중심으로 터지는 반경.")]
    public float ExplosionRadius = 0.9f;
    [Tooltip("눈에 따른 폭발 반경 성장률.")]
    public float ExplosionRadiusGrowth = 0.1f;
    [Min(0f)] public float MaxExplosionRadius = 2f;
    [Tooltip("폭발에 휘말린 다른 몬스터가 받는 피해(공격력 대비). 효과라 치명타는 없다.")]
    [Range(0f, 2f)] public float ExplosionDamageRatio = 0.7f;

    [Header("각성 (눈 20)")]
    [Tooltip("폭발 반경 증가 비율(1 = 2배, 상한을 넘어도 된다).")]
    [Min(0f)] public float AwakenExplosionRadiusBonus = 1f;
}
