using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 웨이브 설정. 수는 웨이브마다 일정하게 늘고, 몬스터는 기본 종류 하나에 특성(기획서 9.1)을 붙여 만든다 — 웨이브는 주기(CycleTraits)를 되풀이한다(WaveComposition).
/// 판은 두 부로 나뉜다: 1부는 체력·방어력이 자라고, SecondActAfterWave 웨이브 보스를 넘기면 2부 — 체력이 멈추고 속도·경험치·매 웨이브 보스로 판이 바뀐다(기획서 9.2·9.4). 시간은 초다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Wave/Wave Config", fileName = nameof(SO_WaveConfig))]
public sealed class SO_WaveConfig : SO_Config
{
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
    [Tooltip("한 웨이브 일반 몬스터 수의 상한(0 = 없음). 넘는 웨이브는 이 수로 나오고, 줄어든 수만큼 한 마리 체력을 키워 웨이브 체력 합은 그대로다 " +
             "— 나오는 시간이 1분(300 × 0.2초)을 넘지 않게, 길 위 밀도(범위 공격이 맞히는 수)가 후반에도 그대로이게(2026-10-29).")]
    [Min(0)] public int MaxCount = 300;

    [Header("체력 (1부)")]
    [Tooltip("웨이브마다 몬스터 기본 체력(SO_MonsterType.Health)에 더하는 비율(직선). 0.15면 2웨이브 115%, 3웨이브 130%.")]
    [Min(0f)] public float HealthGrowth = 0f;
    // 무한 모드는 언젠가 반드시 져야 한다 — 플레이어 성장(카드·합성·각성)이 곱으로 커지므로 직선으로는 따라잡지 못한다(기획서 11.1).
    [Tooltip("웨이브마다 체력에 곱하는 비율(복리). 0.1이면 10웨이브 236%, 20웨이브 611%, 50웨이브 약 107배. 직선과 곱해진다. 2부에서 멈춘다.")]
    [Min(0f)] public float HealthCompound = 0.1f;

    // 최대 체력 피해를 뺀 뒤(2026-11-03) 후반을 고정 피해로 맞추는 두 번째 축. 방어력은 한 방마다 빠지므로
    // 작은 타격을 많이 넣는 범위형(파편·체인·난사 — 맞히는 수로 곱해져 후반에 폭주하던 쪽)을 더 누르고, 큰 한 방(빨강·파랑)에 역할을 준다.
    [Tooltip("모든 몬스터의 방어력이 웨이브마다 이만큼 는다(1웨이브 0, 2부에서 멈춤). 특성의 방어력 배율(단단함 2배)이 곱해진다.")]
    [Min(0f)] public float ArmorPerWave = 1f;

    [Header("몬스터")]
    [Tooltip("모든 몬스터의 기본 종류. 특성이 그 위에 붙는다.")]
    public SO_MonsterType Monster;

    [Header("웨이브 구성 (주기)")]
    [Tooltip("보스 사이 웨이브마다 차례로 붙는 특성(분노 → 재생 → 보호막 → 단단함 → 보스). 비우면 특성 없는 몬스터만 나온다. " +
             "웨이브마다 특성 하나가 뚜렷하게 나와야 다음 웨이브를 보고 색을 준비할 이유가 생긴다(2026-10-31). 주기 웨이브는 한 종류만 나온다(2026-11-02). " +
             "보스 웨이브의 일반 몬스터는 이 특성들을 하나씩 단 몬스터가 마리마다 무작위로 섞여 나온다.")]
    public List<SO_MonsterTrait> CycleTraits = new List<SO_MonsterTrait>();
    [Tooltip("이 웨이브부터 주기 특성에 보조 특성(CycleExtraPool에서 무작위)을 더 붙인다.")]
    [Min(1)] public int CycleExtraFromWave = 6;
    [Tooltip("더 붙는 보조 특성 수. 웨이브 하나에 한 번 뽑아 그 웨이브 몬스터 모두가 같은 조합이다.")]
    [Min(0)] public int CycleExtraTraits = 1;
    [Tooltip("보조 특성의 세기(1 = 특성 SO 값 그대로). 0.5면 재생·방어력·보호막 대수·분노를 절반만 받는다 — 웨이브의 주인공 특성이 뚜렷하게(2026-11-02 사용자). " +
             "목표물 피해·경험치는 줄이지 않는다(MonsterKind).")]
    [Range(0f, 1f)] public float CycleExtraStrength = 0.5f;
    [Tooltip("보조 특성 후보.")]
    [FormerlySerializedAs("RandomTraits")]
    public List<SO_MonsterTrait> CycleExtraPool = new List<SO_MonsterTrait>();

    [Header("보스 (기획서 9.4)")]
    [Tooltip("이 웨이브마다 보스가 나온다(5면 5·10·15…). 2부는 SecondActBossEveryWave를 따른다.")]
    [Min(1)] public int BossInterval = 5;
    [Tooltip("보스 웨이브의 보스 수.")]
    [Min(1)] public int BossCount = 1;
    [Tooltip("보스에 늘 붙는 특성(거대).")]
    public List<SO_MonsterTrait> BossTraits = new List<SO_MonsterTrait>();
    [Tooltip("보스에 BossExtraStrength 세기로 더 붙는 특성(주기 특성들). 거대처럼 보스다움을 만드는 특성은 BossTraits(세기 1)에 둔다.")]
    public List<SO_MonsterTrait> BossExtraTraits = new List<SO_MonsterTrait>();
    // 퍼뎀을 뺀 뒤(2026-11-03) 재생 4%·단단함 2배를 그대로 받으면 고정 피해로는 보스를 깎을 수 없다 — 웨이브 보조 특성과 같은 세기 체계로 낮춘다.
    [Tooltip("BossExtraTraits의 세기(1 = 특성 SO 값 그대로). 0.5면 재생·방어력 배율·보호막·분노를 절반만 받는다.")]
    [Range(0f, 1f)] public float BossExtraStrength = 0.5f;
    [Tooltip("보스 체력 = 그 웨이브 일반 몬스터 체력 합 × 이 값. 특성의 체력 배율은 보스에 쓰지 않는다 — 몬스터 수가 늘어도 보스가 무리에 묻히지 않게. " +
             "퍼뎀을 뺀 뒤(2026-11-03) 거대의 피해 저항(0.9)을 없애고 몫을 올렸다 — 보스는 한 마리라 범위형이 맞힐 대상이 하나뿐이고 단단함 방어력이 작은 타격을 깎아, 저항 없이도 단일 고화력·영웅·스킬의 웨이브가 된다.")]
    [Range(0f, 2f)] public float BossHealthShare = 0.5f;
    // 웨이브 체력 합은 마릿수를 따라 크는데 보스는 한 마리라 범위형이 맞힐 대상이 늘지 않는다 — 몫만 두면 마릿수가 느는 만큼 보스만 혼자 단단해졌다
    // (색 시험 2026-11-03: 화력이 고정인데 범위형은 25웨이브, 빨강도 35웨이브부터 보스를 못 깎음. 11-04~13 상한이 빠졌을 때 사용자 판도 25웨이브부터 보스 절반이 샜다).
    // 초반은 웨이브 크기를 따라 작게, 뒤로는 이 상한에서 멈춘다.
    [Tooltip("보스 체력 상한 = 그 웨이브 일반 몬스터 한 마리 체력 × 이 값(몹 몇 마리분). 0이면 상한 없음.")]
    [Min(0f)] public float BossHealthMaxCount = 50f;
    [Tooltip("보스를 잡으면 받는 카드 고르기 횟수(레벨은 오르지 않는다).")]
    [Min(0)] public int BossRewardChoices = 1;

    [Header("잭팟 보스 (기획서 9.4)")]
    [Tooltip("보스가 잭팟 보스로 바뀔 확률. 잭팟 보스는 금색이고 잡으면 보상이 크다. 보스 웨이브가 시작될 때 보스 포켓이 금색으로 바뀐다.")]
    [Range(0f, 1f)] public float JackpotChance = 0.25f;
    [Tooltip("잭팟 보스에 더 붙는 특성(금색 겉모습).")]
    public List<SO_MonsterTrait> JackpotTraits = new List<SO_MonsterTrait>();
    [Tooltip("잭팟 보스를 잡으면 받는 카드 고르기 횟수(보스 보상 대신).")]
    [Min(0)] public int JackpotRewardChoices = 2;

    // 체력 멈춤·속도·경험치·매 웨이브 보스가 같은 기점에서 바뀌어야 "판이 바뀐다"가 된다 — 따로 두면 값이 어긋나 기점이 흐려진다(2026-11-14 사용자).
    [Header("2부 (기획서 9.2·9.4)")]
    [Tooltip("이 웨이브(보스)까지가 1부, 다음 웨이브부터 2부다(0 = 2부 없음 — 체력·방어력이 끝없이 는다). 체력·방어력·몹 수 상한 보정은 이 웨이브 값에서 멈춘다.")]
    [FormerlySerializedAs("GrowthEndWave"), FormerlySerializedAs("HealthGrowthEndWave")]
    [Min(0)] public int SecondActAfterWave = 50;
    // 50웨이브쯤이면 주사위 피해 대부분이 최대 체력 비례라 체력을 더 불려도 압박이 되지 않는다 — 비례 피해가 같이 커진다.
    // 이동 속도는 사거리 안에 머무는 시간을 줄여 고정·비례 피해를 똑같이 누르고, 칸·눈 상한으로 화력이 멈추면 언젠가 반드시 이긴다(2026-11-03 사용자).
    [Tooltip("2부에서 웨이브마다 이동 속도에 더하는 비율(직선). 0.03이면 60웨이브 1.3배, 80웨이브 1.9배. 나온 때 정해져 그 몬스터는 끝까지 같은 속도다.")]
    [Min(0f)] public float SpeedGrowth = 0.03f;
    [Tooltip("2부의 처치 경험치 배율. 마릿수 상한으로 웨이브 경험치가 늘지 않는 후반에 레벨업이 잦아져 덜 심심하게(2026-10-29 — 사용자 결정, 총량을 맞추지 않는 깔끔한 값).")]
    [Min(1)] public int ExpBoostScale = 2;
    // 5웨이브마다면 보스 웨이브에 스킬을 아껴 두었다 쏟는 운영으로 보스가 다 잡혔다(2026-11-14 사용자 판 — 보스 웨이브에만 스킬 1~3번).
    // 매 웨이브 보스가 나오면 스킬만으로는 다 못 막는다. 보스가 아닌 차례의 일반 몬스터 구성은 그대로다(주기 웨이브 + 보스).
    [Tooltip("2부는 매 웨이브 보스가 나온다. 일반 몬스터 구성은 그 웨이브 차례 그대로이고 보스만 더해진다.")]
    public bool SecondActBossEveryWave = true;

    /// <summary>이 웨이브가 2부인지.</summary>
    public bool IsSecondAct(int wave) => SecondActAfterWave > 0 && wave > SecondActAfterWave;

    /// <summary>이 웨이브에 보스가 나오는지.</summary>
    public bool HasBoss(int wave) => wave % BossInterval == 0 || (SecondActBossEveryWave && IsSecondAct(wave));

    /// <summary>이 웨이브의 일반 몬스터 수(상한 MaxCount 적용).</summary>
    public int GetCount(int wave)
    {
        int count = GetUncappedCount(wave);
        return MaxCount > 0 ? Mathf.Min(count, MaxCount) : count;
    }

    /// <summary>
    /// 한 마리 체력 배율. 상한으로 줄어든 수만큼 키워 웨이브 체력 합(상한이 없을 때와 같은 값)을 지킨다.
    /// 2부에서는 1부 마지막 웨이브 값에 멈춘다 — 직선·복리뿐 아니라 수 상한 보정(뒤로 갈수록 상한 없는 수가 늘어 한 마리가 커진다)도 같이 멈춰야 체력이 정말 멈춘다.
    /// </summary>
    public float GetHealthScale(int wave)
    {
        wave = GrowthWave(wave);
        return (1f + HealthGrowth * (wave - 1)) * Mathf.Pow(1f + HealthCompound, wave - 1) * GetUncappedCount(wave) / Mathf.Max(GetCount(wave), 1);
    }

    /// <summary>이 웨이브 몬스터의 기본 방어력(특성 배율 전).</summary>
    public float GetArmor(int wave) => ArmorPerWave * (GrowthWave(wave) - 1);

    private int GrowthWave(int wave) => IsSecondAct(wave) ? SecondActAfterWave : wave;

    /// <summary>이 웨이브 몬스터의 이동 속도 배율(1 = 그대로).</summary>
    public float GetSpeedScale(int wave) => IsSecondAct(wave) ? 1f + SpeedGrowth * (wave - SecondActAfterWave) : 1f;

    /// <summary>이 웨이브의 처치 경험치 배율.</summary>
    public int GetExpScale(int wave) => IsSecondAct(wave) ? ExpBoostScale : 1;

    private int GetUncappedCount(int wave) => BaseCount + CountPerWave * (wave - 1);
}
