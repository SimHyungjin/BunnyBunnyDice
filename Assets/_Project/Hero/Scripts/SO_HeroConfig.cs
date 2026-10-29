using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 영웅 캐릭터가 같이 쓰는 설정. 위치·크기는 칸 단위(한 칸 = 1)다.
/// 캐릭터마다 다른 이름·겉모습·능력치는 SO_HeroCharacter(기획서 10.3) — 여기에는 캐릭터를 바꿔도 같아야 하는 이동·충돌만 둔다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Hero/Hero Config", fileName = nameof(SO_HeroConfig))]
public sealed class SO_HeroConfig : SO_Config
{
    [Header("캐릭터")]
    [Tooltip("고를 수 있는 캐릭터. 로비가 생기기 전까지는 첫 캐릭터로 시작하고 테스트 창(F1)에서 바꾼다. 여기서 참조하므로 의존성으로 함께 로드된다.")]
    public List<SO_HeroCharacter> Characters = new List<SO_HeroCharacter>();
    [Tooltip("능력치 1단계 값 = 한계값 × 이 비율. 6단계 = 한계값, 사이는 고르게(기획서 10.3). 1/6로 두면 낮은 단계 캐릭터를 쓸 수 없다.")]
    [Range(0f, 1f)] public float StageFloorRatio = 0.4f;

    [Header("능력치 규칙")]
    [Tooltip("치명타 확률이 100%를 넘은 몫을 치명타 피해 배율로 옮기는 비율(기획서 10.4). 1이면 넘친 10%p → 배율 +0.1.")]
    [Min(0f)] public float CritOverflowToDamage = 1f;

    [Header("표시")]
    [Tooltip("이동 방향으로 도는 빠르기(도/초).")]
    public float TurnSpeed = 720f;
    [Tooltip("모델 크기가 마지막으로 흔들림 뼈(VRM 스프링 본)를 만든 크기에서 이 비율 넘게 바뀌면 다시 만든다(HeroSprings). " +
             "스프링 본은 뼈 길이를 만들 때 크기로 고정해, 로비에서 4배로 키우면 머리·옷이 쉬지 않고 휘날렸다(2026-10-29). 작을수록 자주 다시 만든다.")]
    [Range(0.01f, 1f)] public float SpringRebuildScaleStep = 0.1f;
    [Tooltip("영웅을 따라다니는 스포트라이트(로비·게임). 비우면 없다.")]
    public SO_HeroSpotlightConfig Spotlight;
    [Tooltip("동작 묶음(Humanoid). 모든 캐릭터가 같이 쓴다 — 공격 준비 시간(AttackWindup)이 판정 시각을 정하므로, 캐릭터마다 다르면 겉모습이 판 결과를 바꾼다. "
        + "비우거나 모델이 Humanoid가 아니면 움직이지 않는 모델로 둔다.")]
    public SO_HeroAnimationSet Animations;

    [Header("이동")]
    [Tooltip("충돌 반지름. 0.5에 가까울수록 한 칸 통로를 지나기 빡빡해진다.")]
    [Range(0.05f, 0.45f)] public float Radius = 0.25f;
    [Tooltip("탭 이동에서 경유점에 이만큼 가까워지면 도착으로 본다.")]
    public float ArriveDistance = 0.05f;
    [Tooltip("낮은 단차(칩)를 밟고 지나갈 때 발 높이가 오르내리는 빠르기(칸/초). 칸 경계에서 높이가 뚝 튀지 않게.")]
    [Min(0.1f)] public float ClimbSpeed = 4f;
    [Tooltip("발 높이가 이보다 크게 바뀌면(주사위에 오르내림) 걸어 오르지 않고 깡총 뛴다(칸). 칩 높이보다 크고 주사위보다 작게.")]
    [Min(0f)] public float HopThreshold = 0.3f;
    [Tooltip("깡총 한 번의 시간(초).")]
    [Min(0.05f)] public float HopDuration = 0.3f;
    [Tooltip("깡총 뛸 때 높은 쪽 발 높이보다 더 솟는 높이(칸).")]
    [Min(0f)] public float HopHeight = 0.3f;

    [Header("영웅 레이즈 (기획서 10.5)")]
    [Tooltip("영웅이 올라탄 주사위는 이 카드의 레이즈 상태가 된다(배율·고리 크기·색을 이 카드에서 읽는다 — 값을 한 곳에만 둔다). 지속 시간은 쓰지 않는다.")]
    public SO_SkillCard_Raise Raise;
    [Tooltip("올라탄 시간이 이만큼(초) 쌓일 때마다 그 주사위 눈이 1 영구로 깎인다. 시간은 주사위마다 쌓이고 적이 판에 있을 때만 흐른다.")]
    [Min(0.1f)] public float RaisePipInterval = 3f;
    [Tooltip("각성 주사위(눈 20)의 첫 눈은 이만큼(초) 쌓여야 깎인다 — 깎이면(20 → 19) 각성이 꺼지므로 더 오래 버티게 한다. 그 뒤로는 RaisePipInterval마다.")]
    [Min(0.1f)] public float RaiseAwakenPipInterval = 10f;

    [Header("공격")]
    [Tooltip("공격선이 나가는 높이(바닥 윗면에서).")]
    public float AttackHeight = 0.6f;
    [Tooltip("던지는 순간 처음 노린 적을 놓지 않는 사거리 배수. 준비 동작 동안 적이 걸어 사거리 끝을 살짝 넘어도 그 적을 맞힌다 — 다른 적으로 바꿀 때는 원래 사거리.")]
    [Min(1f)] public float AttackKeepRangeScale = 1.2f;
    [Tooltip("던진 카드가 날아가는 모습(표시만).")]
    public SO_HeroCardShotConfig CardShot;

    public override void SwapNested(Func<SO_Config, SO_Config> swap)
    {
        for (int i = 0; i < Characters.Count; i++) Characters[i] = (SO_HeroCharacter)swap(Characters[i]);
        if (Raise != null) Raise = (SO_SkillCard_Raise)swap(Raise);
        if (Spotlight != null) Spotlight = (SO_HeroSpotlightConfig)swap(Spotlight);
        if (CardShot != null) CardShot = (SO_HeroCardShotConfig)swap(CardShot);
        if (Animations != null) Animations = (SO_HeroAnimationSet)swap(Animations);
    }
}
