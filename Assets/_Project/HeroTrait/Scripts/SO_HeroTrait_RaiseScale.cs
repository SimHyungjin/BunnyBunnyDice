using UnityEngine;

/// <summary>
/// 하이 스테이크: 레이즈 배율이 커진다(2026-10-30 사용자 — x3·x3 → x5·x5). 레이즈 카드와 영웅이 올라탄 주사위 둘 다.
/// 영웅이 올라탄 주사위의 눈이 깎이는 대가는 그대로 둔다 — 대가를 없애면 한 자리에 서 있기만 하면 되는 캐릭터가 된다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Raise Scale", fileName = "SO_HeroTrait_RaiseScale")]
public sealed class SO_HeroTrait_RaiseScale : SO_HeroTrait, IRaiseScale
{
    [Tooltip("레이즈 공격력 배율(카드 값 대신).")]
    [Min(1f)] public float DamageScale = 5f;
    [Tooltip("레이즈 공격 속도 배율(카드 값 대신).")]
    [Min(1f)] public float SpeedScale = 5f;

    public void Scale(ref float damageScale, ref float speedScale)
    {
        damageScale = DamageScale;
        speedScale = SpeedScale;
    }
}
