using UnityEngine;

/// <summary>
/// 영웅 강화 카드 한 종류(기획서 5장·10.4). 손패에 들어가지 않고 레벨업에서 고르는 즉시 이번 판 동안 영웅 능력치 하나를 올린다.
/// 오르는 양은 보통 지금 값이 아니라 캐릭터 기본값(한계값 × 단계)의 %다 — 기본값이 높은 캐릭터일수록 이득이 커서 영웅 위주 캐릭터의 근거가 된다(토끼공듀, 10.3).
/// 치명타 두 장은 값을 그대로 더한다(Flat — 확률 +20%p, 피해 +1배). 기본 치명타 확률이 10%라 기본값의 %로는 아무리 올려도 약했다(2026-10-26).
/// 같은 카드를 여러 번 고르면 더한다(곱하지 않는다) — 몇 장 모았는지로 세기가 바로 읽히게.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroUpgrade/Hero Card", fileName = "SO_HeroCard_")]
public class SO_HeroCard : SO_Card
{
    [Header("영웅 강화")]
    [Tooltip("올리는 능력치. 사거리는 올리지 않는다(2026-10-25 — 캐릭터마다 고정).")]
    public HeroStat Stat = HeroStat.Attack;
    [Tooltip("올리는 양. Flat이 꺼져 있으면 기본값 대비 비율(1 = +100%), 켜져 있으면 값에 그대로 더한다(치명타 확률 0.2 = +20%p, 치명타 피해 1 = +1배).")]
    [Min(0f)] public float Amount = 1f;
    [Tooltip("Amount를 기본값의 %가 아니라 값에 그대로 더한다.")]
    public bool Flat;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        if (Stat == HeroStat.Range) Debug.LogWarning($"[SO_HeroCard] {name}: 사거리는 강화 카드로 올리지 않는다(기획서 10.4).", this);
        base.OnValidate();
    }
#endif
}
