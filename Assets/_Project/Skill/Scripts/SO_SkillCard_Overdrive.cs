using UnityEngine;

/// <summary>
/// 오버드라이브(주사위 대상·지속): 주사위 하나가 한동안 공격력·공격 속도 몇 배로 쏜다(기획서 8). 다시 쓰면 시간이 새로 시작한다(겹쳐 세지지 않음).
/// 몰려오는 순간에 맞춰 쓰는 기술이라 쏘는 빠르기 자체가 눈에 보이는 연출이다.
/// 영웅이 올라탄 주사위(영웅 오버드라이브, 기획서 10.5)와 같은 상태다 — 이 카드는 영웅의 오버드라이브를 멀리서 쓰는 것이다 — 배율은 이 카드 값 하나만 쓰고(SO_HeroConfig.Overdrive), 둘은 겹치지 않는다. 영웅이 올라타 있으면 카드 시간을 먼저 쓴다(그동안 눈이 깎이지 않는다).
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Overdrive", fileName = "SO_SkillCard_Overdrive")]
public sealed class SO_SkillCard_Overdrive : SO_SkillCard
{
    [Tooltip("공격력 배율. 영웅이 올라탄 주사위도 이 값을 쓴다.")]
    [Min(1f)] public float DamageScale = 3f;
    [Tooltip("공격 속도 배율. 3이면 세 배 빠르게 쏜다. 영웅이 올라탄 주사위도 이 값을 쓴다.")]
    [Min(1f)] public float SpeedScale = 3f;
    [Tooltip("지속 시간(초).")]
    [Min(0.1f)] public float Duration = 6f;
    [Tooltip("지속되는 동안 주사위 밑에 두는 고리 반경(칸) — 어느 주사위가 빨라졌는지 보이게.")]
    [Min(0f)] public float RingRadius = 0.55f;
    [Tooltip("쓰는 순간 주사위에서 퍼지는 고리 반경(칸).")]
    [Min(0f)] public float BurstRadius = 1.2f;

    public override SkillTarget Target => SkillTarget.Dice;
}
