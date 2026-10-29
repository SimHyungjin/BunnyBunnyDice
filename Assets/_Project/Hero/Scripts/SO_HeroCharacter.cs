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
/// 전용 카드도 캐릭터가 하나 든다(ExclusiveCard) — 레벨업마다 확률로 후보 한 자리에 나온다(CardRoll, 10.3).
/// </summary>
[CreateAssetMenu(menuName = "Def/Hero/Hero Character", fileName = "SO_HeroCharacter_")]
public sealed class SO_HeroCharacter : SO_Config
{
    [Localized] public string DisplayName = "";
    [Tooltip("세이브 키 — 한 번 정하면 바꾸지 않는다. 에셋 이름·표시 이름이 바뀌어도(지금은 특성 이름, 나중에 사람 이름) 해금·단계 기록이 이어지게. 비우면 에셋 이름.")]
    public string Id = "";
    [Tooltip("특성 하나(기획서 10.3). 비우면 특성 없음 — 능력치로 승부하는 캐릭터.")]
    public SO_HeroTrait Trait;
    [Tooltip("전용 카드(기획서 10.1). 레벨업마다 SO_LevelUpConfig.ExclusiveChance로 후보 한 자리에 나온다. 공용 목록에는 넣지 않는다.")]
    public SO_SkillCard ExclusiveCard;

    [Header("표시")]
    [Tooltip("VRoid 모델(VRM을 불러온 프리팹, 발밑이 피벗). 앞(+Z)이 이동 방향을 향하도록 돌린다. 모든 캐릭터가 VRoid 모델을 쓴다(2026-10-29).")]
    public GameObject Prefab;
    [Tooltip("모델 키(칸). 키를 재서 이 높이로 맞춘다. 0이면 프리팹 크기 그대로.")]
    [Min(0f)] public float ModelHeight = 0.9f;
    [Tooltip("공격선 색.")]
    public Color Color = new Color(0.95f, 0.85f, 0.3f);

    [Header("초상화 (캐릭터 선택 창·스킬 컷인)")]
    [Tooltip("배경이 투명한 세로 그림(2:3, 머리~허벅지). 선택 창은 얼굴·손 둘레를 잘라 쓰고(아래 자르기 값), 컷인은 거의 다 쓴다. " +
             "잠긴 캐릭터는 이 그림을 검게 칠해 실루엣으로 쓴다. 비우면 얼굴 칸이 빈다.")]
    public Texture2D Portrait;
    [Tooltip("칸에 맞춰 자를 때 남길 세로 자리(그림 높이 비율, 0 = 아래 · 1 = 위). 칸 아래쪽은 이름 판에 가려지므로 얼굴이 그 위에 오게 맞춘다.")]
    [Range(0f, 1f)] public float PortraitFocusY = 0.5f;
    [Tooltip("자를 때 남길 가로 자리(그림 너비 비율, 0 = 왼쪽 · 1 = 오른쪽). 확대(PortraitZoom)할 때 얼굴이 가운데가 아니면 맞춘다.")]
    [Range(0f, 1f)] public float PortraitFocusX = 0.5f;
    [Tooltip("칸을 꽉 채운 크기에 곱하는 배율(1 = 꽉 채움). 크면 확대, 1보다 작으면 축소(그림 밖은 투명 — 그림 가장자리가 투명해야 한다). " +
             "그림은 컷인까지 쓰려고 세로로 길게(머리~허벅지) 그려서, 선택 칸에서는 얼굴·손이 칸을 채우게 확대한다.")]
    [Min(0.1f)] public float PortraitZoom = 1f;

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
        ExclusiveCard = (SO_SkillCard)swap(ExclusiveCard);
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
