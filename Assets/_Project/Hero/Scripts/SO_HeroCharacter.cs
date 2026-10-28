using System;
using UnityEngine;

/// <summary>영웅 능력치 종류(기획서 10.2). HeroStats가 단계·값을 이 순서의 배열로 둔다.</summary>
public enum HeroStat
{
    Attack,
    AttackSpeed,
    MoveSpeed,
    Range,
    CritChance,
    CritDamage,
    // 한 번에 던지는 카드 수. 단계·캐릭터 차이 없이 1장에서 시작해 강화 카드(투사체 +1)로만 는다 — 로비 능력치 주사위 칸에는 없다.
    // 강화 카드·표시·봇이 능력치 하나로 다루게 같은 목록에 둔다(맨 뒤 — 세이브의 단계 배열 순서를 지키려고).
    Projectile,
}

/// <summary>
/// 영웅 캐릭터 하나(기획서 10.3). 캐릭터마다 다른 것 — 이름·겉모습·능력치 한계값 — 만 둔다.
/// 조작·충돌처럼 모든 캐릭터가 같이 쓰는 값은 SO_HeroConfig에 둔다(캐릭터를 바꿔도 조작감이 달라지지 않게).
/// 여기 적는 값은 6단계(한계값)이고, 실제 값은 얻을 때 굴린 단계에 따라 깎인다(HeroStats). 특성은 SO_HeroTrait 하나를 참조한다.
/// 전용 카드는 특성이 레벨업 목록에 더한다(SO_HeroTrait_ExtraCards, 10.1).
/// </summary>
[CreateAssetMenu(menuName = "Def/Hero/Hero Character", fileName = "SO_HeroCharacter_")]
public sealed class SO_HeroCharacter : SO_Config
{
    [Localized] public string DisplayName = "";
    [Tooltip("특성 하나(기획서 10.3). 비우면 특성 없음 — 능력치로 승부하는 캐릭터.")]
    public SO_HeroTrait Trait;

    [Header("표시")]
    [Tooltip("발밑이 피벗인 프리팹(모델 FBX를 바로 넣어도 된다). 앞(+Z)이 이동 방향을 향하도록 돌린다.")]
    public GameObject Prefab;
    [Tooltip("모델 키(칸). 모델마다 내보낸 단위가 달라 키를 재서 이 높이로 맞춘다. 0이면 프리팹 크기 그대로.")]
    [Min(0f)] public float ModelHeight = 0f;
    [Tooltip("공격선 색. 몸 색 입히기를 켜면 몸 색이기도 하다.")]
    public Color Color = new Color(0.95f, 0.85f, 0.3f);
    [Tooltip("몸에 Color를 입힌다(임시 단색 모델용). 텍스처가 있는 모델은 끈다 — 색이 텍스처에 곱해져 칠이 탁해진다.")]
    public bool TintModel = true;
    [Tooltip("툰 아웃라인(다른 물체와 같은 화면 비율 굵기)을 붙인다. 모델이 자기 아웃라인 껍데기를 갖고 있으면 아래 ShellMaterial로 껍데기를 끄고 켠다 — 껍데기 위에 붙이면 뒷면만 그리는 아웃라인이 뒤집힌 껍데기에서는 앞면을 그려 몸을 통째로 덮는다.")]
    public bool AddOutline = true;
    [Tooltip("모델 안에 든 아웃라인 껍데기의 재질(Blender 등에서 만든 뒤집힌 메시). 지정하면 그 부분을 그리지 않는다 — 굵기가 모델 크기를 따라가 다른 물체의 아웃라인과 맞지 않는다. 메시의 마지막 재질이어야 한다.")]
    public Material ShellMaterial;

    [Header("초상화 (캐릭터 선택 창)")]
    [Tooltip("배경이 투명한 그림. 비우면 3D 모델을 찍은 사진을 쓴다(HeroPortraits). 잠긴 캐릭터는 이 그림을 검게 칠해 실루엣으로 쓴다.")]
    public Texture2D Portrait;
    [Tooltip("칸에 맞춰 자를 때 남길 세로 자리(그림 높이 비율, 0 = 아래 · 1 = 위). 칸 아래쪽은 이름 판에 가려지므로 얼굴이 그 위에 오게 맞춘다.")]
    [Range(0f, 1f)] public float PortraitFocusY = 0.5f;

    [Header("한계 능력치 (6단계 값)")]
    [Tooltip("한 번 맞힐 때의 피해. 스킬 위력의 기준이기도 하다(10.2).")]
    [Min(0f)] public float Attack = 20f;
    [Tooltip("초당 공격 횟수. 간격이 아니라 횟수로 두는 이유: 판 안 강화가 %로 오르므로 '높을수록 좋다'로 맞춘다.")]
    [Min(0.1f)] public float AttackSpeed = 2f;
    [Tooltip("초당 칸 수.")]
    [Min(0f)] public float MoveSpeed = 3f;
    [Tooltip("사거리(칸). 영웅 중심에서 몬스터 중심까지. 주사위보다 짧게 두어 몸을 데려가야 하게 한다.")]
    [Min(0f)] public float Range = 1.6f;
    [Tooltip("치명타 확률(0~1). 영웅 공격과 스킬에만 쓴다 — 주사위 치명타와 따로다.")]
    [Range(0f, 1f)] public float CritChance = 0.1f;
    [Tooltip("치명타 피해 배율(6단계 한계값). 1단계 = 이 값 × 하한 비율(0.4)이므로 ×2.5 이상으로 둔다 — 낮으면 낮은 단계에서 ×1 밑이 되어 치명타가 아무 일도 안 한다.")]
    [Min(1f)] public float CritDamage = 2.5f;

    public override void SwapNested(Func<SO_Config, SO_Config> swap)
    {
        Trait = (SO_HeroTrait)swap(Trait);
    }

    public float GetMax(HeroStat stat)
    {
        switch (stat)
        {
            case HeroStat.Attack: return Attack;
            case HeroStat.AttackSpeed: return AttackSpeed;
            case HeroStat.MoveSpeed: return MoveSpeed;
            case HeroStat.Range: return Range;
            case HeroStat.CritChance: return CritChance;
            case HeroStat.CritDamage: return CritDamage;
            case HeroStat.Projectile: return 1f;
            default: return 0f;
        }
    }
}
