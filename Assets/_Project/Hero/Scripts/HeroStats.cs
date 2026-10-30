using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅의 실행 중 능력치(기획서 10.2~10.4). 값 = 캐릭터 한계값 × 단계 비율 × (1 + 이번 판 강화 % 합).
/// 이동·공격은 SO가 아니라 여기서 읽는다 — 같은 캐릭터라도 단계(그리고 판 안 강화)에 따라 값이 달라서다.
/// 값을 복사해 두지 않고 읽을 때마다 계산한다: 캐릭터 SO를 실행 중 고치면(GameTuning) 바로 따라가게.
/// 캐릭터·단계는 세이브에서 받는다(MetaProfile.Bind — 바꾸면 세이브에 남는다). 여기서는 세이브를 모르고, 만들 때는 첫 캐릭터·전 능력치 6단계다.
/// </summary>
public sealed class HeroStats
{
    // 단계는 6면 주사위 눈이다(10.3) — 조정 값이 아니라 규칙이라 SO로 빼지 않는다.
    public const int MinStage = 1;
    public const int MaxStage = 6;

    public static readonly int StatCount = Enum.GetValues(typeof(HeroStat)).Length;

    private readonly SO_HeroConfig _config;
    private readonly int[] _stages = new int[StatCount];
    // 이번 판에 고른 영웅 강화 카드의 % 합. 새 판에서 비운다(단계는 캐릭터 것이라 남는다).
    private readonly float[] _bonuses = new float[StatCount];
    // 그대로 더하는 강화(치명타 카드 — SO_HeroCard.Flat).
    private readonly float[] _flats = new float[StatCount];

    /// <summary>캐릭터가 바뀌었다 — 겉모습을 다시 만든다.</summary>
    public event Action CharacterChanged;

    /// <summary>능력치 단계가 바뀌었다(테스트 창, 나중에 리롤) — 단계를 보여 주는 곳(로비 패널)이 다시 그린다.</summary>
    public event Action StagesChanged;

    public SO_HeroCharacter Character { get; private set; }

    public HeroStats(SO_HeroConfig config)
    {
        _config = config;
        for (int i = 0; i < _stages.Length; i++) _stages[i] = MaxStage;
        for (int i = 0; i < config.Characters.Count; i++)
        {
            if (config.Characters[i] == null) continue;
            Character = config.Characters[i];
            break;
        }
        if (Character == null) Debug.LogError("[HeroStats] SO_HeroConfig.Characters가 비어 있다.");
    }

    public void SetCharacter(SO_HeroCharacter character)
    {
        if (character == null || character == Character) return;
        Character = character;
        CharacterChanged?.Invoke();
    }

    public int GetStage(HeroStat stat) => _stages[(int)stat];

    public void SetStage(HeroStat stat, int stage)
    {
        int clamped = Mathf.Clamp(stage, MinStage, MaxStage);
        if (_stages[(int)stat] == clamped) return;
        _stages[(int)stat] = clamped;
        StagesChanged?.Invoke();
    }

    /// <summary>단계를 통째로 넣는다(세이브에서 불러올 때). HeroStat 순서이고, 바뀐 것이 있으면 StagesChanged를 한 번 부른다.</summary>
    public void LoadStages(int[] stages)
    {
        bool changed = false;
        for (int i = 0; i < _stages.Length && i < stages.Length; i++)
        {
            int clamped = Mathf.Clamp(stages[i], MinStage, MaxStage);
            if (_stages[i] == clamped) continue;
            _stages[i] = clamped;
            changed = true;
        }
        if (changed) StagesChanged?.Invoke();
    }

    /// <summary>이번 판 강화 % 합(0.3 = +30%).</summary>
    public float GetBonus(HeroStat stat) => _bonuses[(int)stat];

    /// <summary>이번 판 그대로 더한 강화 합(치명타 확률 0.2 = +20%p).</summary>
    public float GetFlat(HeroStat stat) => _flats[(int)stat];

    public void Apply(SO_HeroCard card)
    {
        if (card.Flat) _flats[(int)card.Stat] += card.Amount;
        else _bonuses[(int)card.Stat] += card.Amount;
    }

    /// <summary>
    /// 이 카드를 골라도 바뀌는 것이 있는지. 치명타 확률이 이미 100%면 레벨업 후보에 내지 않는다
    /// — 넘친 몫을 치명타 피해로 옮기긴 하지만 확률 카드 한 장의 몫이 치명타 피해 카드보다 훨씬 작아 고르면 손해인 함정 카드가 된다.
    /// </summary>
    public bool IsUseful(SO_HeroCard card) =>
        card.Stat != HeroStat.CritChance || Get(HeroStat.CritChance) < 1f;

    /// <summary>새 판: 강화를 비운다.</summary>
    public void ResetBonuses()
    {
        for (int i = 0; i < _bonuses.Length; i++) _bonuses[i] = 0f;
        for (int i = 0; i < _flats.Length; i++) _flats[i] = 0f;
    }

    /// <summary>강화 전 값(캐릭터 한계값 × 단계 비율). 강화 %의 기준이다.</summary>
    public float GetBase(HeroStat stat)
    {
        // 투사체는 단계가 없다 — 단계 비율을 곱하면 1장이 0.4장이 된다.
        if (stat == HeroStat.Projectile) return Character.GetMax(stat);
        float t = (GetStage(stat) - MinStage) / (float)(MaxStage - MinStage);
        return Character.GetMax(stat) * Mathf.Lerp(_config.StageFloorRatio, 1f, t);
    }

    public float Get(HeroStat stat) => Compute(stat, null);

    /// <summary>이 강화 카드를 고르면 stat이 될 값. 카드에 "지금 → 고른 뒤"를 보이는 데 쓴다(치명타 확률 카드는 넘친 몫이 치명타 피해에 붙는다).</summary>
    public float GetAfter(SO_HeroCard card, HeroStat stat) => Compute(stat, card);

    public float Attack => Get(HeroStat.Attack);
    public float AttackInterval => 1f / Mathf.Max(Get(HeroStat.AttackSpeed), MinAttackSpeed);
    public float MoveSpeed => Get(HeroStat.MoveSpeed);
    public float Range => Get(HeroStat.Range);
    public float CritChance => Get(HeroStat.CritChance);
    public float CritMultiplier => Get(HeroStat.CritDamage);
    /// <summary>한 번에 던지는 카드 수(1 이상).</summary>
    public int Projectiles => Mathf.Max(1, Mathf.RoundToInt(Get(HeroStat.Projectile)));

    // extra 카드를 더 받았다고 치고 stat을 계산한다(미리보기용, 지금 값은 extra 없음).
    private float Compute(HeroStat stat, SO_HeroCard extra)
    {
        switch (stat)
        {
            case HeroStat.CritChance:
                return Mathf.Clamp01(Raw(HeroStat.CritChance, extra));
            case HeroStat.CritDamage:
                // 단계로 깎은 배율이 1 밑이면 치명타가 오히려 약해지므로 막는다(한계값을 ×2.5 이상으로 두면 1단계도 ×1 — SO_HeroCharacter).
                // 확률이 100%를 넘으면 더 오를 곳이 없으므로 넘친 몫을 피해 배율로 옮긴다(기획서 10.4, B안).
                float overflow = Mathf.Max(Raw(HeroStat.CritChance, extra) - 1f, 0f);
                return Mathf.Max(Raw(HeroStat.CritDamage, extra), 1f) + overflow * _config.CritOverflowToDamage;
            default:
                return Raw(stat, extra);
        }
    }

    // 상한·전환 전 값.
    private float Raw(HeroStat stat, SO_HeroCard extra)
    {
        float bonus = GetBonus(stat);
        float flat = GetFlat(stat);
        if (extra != null && extra.Stat == stat)
        {
            if (extra.Flat) flat += extra.Amount;
            else bonus += extra.Amount;
        }
        return GetBase(stat) * (1f + bonus) + flat;
    }

    // 0으로 나누지 않게 하는 안전값(SO_HeroCharacter.AttackSpeed의 Min과 같다).
    private const float MinAttackSpeed = 0.1f;
}
