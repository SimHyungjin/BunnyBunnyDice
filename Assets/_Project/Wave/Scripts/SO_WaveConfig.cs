using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 설정(잠정). 원본의 웨이브 공식(기획서 6장)을 정하기 전까지 쓰는 단순한 형태다 — 수는 웨이브마다 일정하게 늘고,
/// 몬스터는 기본 종류 하나에 특성(기획서 9.11)을 붙여 만든다. 앞쪽 웨이브는 목록대로(특성을 하나씩 소개),
/// 목록이 끝나면 특성을 무작위로 조합한다(WaveComposition). 시간은 초다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Wave/Wave Config", fileName = nameof(SO_WaveConfig))]
public sealed class SO_WaveConfig : SO_Config
{
    /// <summary>몬스터 한 가지 = 기본 종류에 붙일 특성들. 비우면 특성 없는 몬스터.</summary>
    [Serializable]
    public sealed class MonsterVariant
    {
        public List<SO_MonsterTrait> Traits = new List<SO_MonsterTrait>();
    }

    [Serializable]
    public sealed class WaveEntry
    {
        [Tooltip("일반 몬스터들. 여러 가지면 번갈아 나온다. 비우면 특성 없는 몬스터만.")]
        public List<MonsterVariant> Monsters = new List<MonsterVariant>();
        [Tooltip("일반 몬스터 뒤에 보스가 나온다.")]
        public bool HasBoss;
        public MonsterVariant Boss = new MonsterVariant();
    }

    [Header("시간")]
    [Tooltip("판 시작부터 첫 웨이브까지, 그리고 웨이브의 몬스터를 다 잡은 뒤 다음 웨이브까지 쉬는 시간. 주사위를 놓고 미로를 고칠 여유.")]
    [Min(0f)] public float WaveDelay = 5f;
    [Tooltip("같은 웨이브 안에서 몬스터가 하나씩 나오는 간격. 출현 구역을 돌아가며 나온다.")]
    [Min(0.05f)] public float SpawnInterval = 0.8f;

    [Header("수")]
    [Tooltip("1웨이브 일반 몬스터 수(보스 제외).")]
    [Min(1)] public int BaseCount = 6;
    [Tooltip("웨이브마다 늘어나는 수.")]
    [Min(0)] public int CountPerWave = 2;
    [Tooltip("보스 웨이브의 보스 수.")]
    [Min(1)] public int BossCount = 1;

    [Header("체력")]
    [Tooltip("웨이브마다 몬스터 기본 체력(SO_MonsterType.Health)에 더하는 비율(직선). 0.15면 2웨이브 115%, 3웨이브 130%.")]
    [Min(0f)] public float HealthGrowth = 0f;
    // 무한 모드는 언젠가 반드시 져야 한다 — 플레이어 성장(카드·합성·각성)이 곱으로 커지므로 직선으로는 따라잡지 못한다(기획서 11.1).
    [Tooltip("웨이브마다 체력에 곱하는 비율(복리). 0.1이면 10웨이브 236%, 20웨이브 611%, 50웨이브 약 107배. 직선과 곱해진다.")]
    [Min(0f)] public float HealthCompound = 0.1f;

    [Header("몬스터")]
    [Tooltip("모든 몬스터의 기본 종류. 특성이 그 위에 붙는다.")]
    public SO_MonsterType Monster;
    [Tooltip("웨이브 순서대로 쓴다(1웨이브 = 첫 번째). 특성을 하나씩 소개하는 구간이다.")]
    public List<WaveEntry> Waves = new List<WaveEntry>();

    [Header("목록 뒤 (무작위)")]
    [Tooltip("일반 몬스터에 무작위로 붙는 특성 후보.")]
    public List<SO_MonsterTrait> RandomTraits = new List<SO_MonsterTrait>();
    [Tooltip("한 웨이브에 섞여 나오는 일반 몬스터 가짓수.")]
    [Min(1)] public int RandomVariants = 2;
    [Tooltip("일반 몬스터 한 가지에 붙는 특성 수의 범위.")]
    [Min(0)] public int RandomTraitMin = 1;
    [Min(0)] public int RandomTraitMax = 2;
    [Tooltip("이 웨이브마다 보스가 나온다(5면 5·10·15…). 목록 안 웨이브는 목록의 HasBoss를 따른다.")]
    [Min(1)] public int BossInterval = 5;
    [Tooltip("보스에 늘 붙는 특성(거대). 여기에 무작위 후보에서 BossRandomTraits개를 더 붙인다.")]
    public List<SO_MonsterTrait> BossTraits = new List<SO_MonsterTrait>();
    [Min(0)] public int BossRandomTraits = 1;

    [Header("보스 (기획서 11.3)")]
    [Tooltip("보스 체력 = 그 웨이브 일반 몬스터 체력 합 × 이 값. 특성의 체력 배율은 보스에 쓰지 않는다 — 몬스터 수가 늘어도 보스가 무리에 묻히지 않게.")]
    [Range(0f, 2f)] public float BossHealthShare = 0.2f;
    [Tooltip("보스를 잡으면 받는 카드 고르기 횟수(레벨은 오르지 않는다).")]
    [Min(0)] public int BossRewardChoices = 1;

    [Header("잭팟 보스 (기획서 11.3)")]
    [Tooltip("보스가 잭팟 보스로 바뀔 확률. 잭팟 보스는 금색이고 잡으면 보상이 크다. 보스 웨이브가 시작될 때 보스 포켓이 금색으로 바뀐다.")]
    [Range(0f, 1f)] public float JackpotChance = 0.25f;
    [Tooltip("잭팟 보스에 더 붙는 특성(금색 겉모습).")]
    public List<SO_MonsterTrait> JackpotTraits = new List<SO_MonsterTrait>();
    [Tooltip("잭팟 보스를 잡으면 받는 카드 고르기 횟수(보스 보상 대신).")]
    [Min(0)] public int JackpotRewardChoices = 2;

    public int GetCount(int wave) => BaseCount + CountPerWave * (wave - 1);

    public float GetHealthScale(int wave) =>
        (1f + HealthGrowth * (wave - 1)) * Mathf.Pow(1f + HealthCompound, wave - 1);
}
