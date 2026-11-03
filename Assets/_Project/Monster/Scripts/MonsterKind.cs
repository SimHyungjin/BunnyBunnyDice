using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실제로 나오는 몬스터 하나의 모양 = 기본 종류 + 특성 목록. 값은 들고 있지 않고 읽을 때마다 SO에서 계산한다
/// — 플레이 중 특성 SO를 고치면 이미 걷고 있는 몬스터에도 바로 반영되게(특성은 많아야 두세 개라 계산이 싸다).
/// 같은 조합은 같은 인스턴스를 쓴다(WaveSpawner가 모아 둔다) — 표시가 종류별로 오브젝트를 재사용하기 때문이다.
///
/// 특성마다 세기(TraitSlot.Strength)가 있다. 세기를 받는 값: 배율(체력·속도·크기 — 1에서 벗어난 몫만), 저항(이동 방해·피해), 방어력, 재생, 보호막 대수, 분노.
/// 받지 않는 값: 목표물 피해·경험치(보상과 위협의 단위라 반으로 나누면 어중간하다), 보호막 피해 비율(보호막은 대수로 약해진다), 색.
/// </summary>
public sealed class MonsterKind
{
    private readonly List<SO_MonsterTrait> _traits;
    private readonly List<float> _strengths;

    public SO_MonsterType Base { get; }
    public IReadOnlyList<SO_MonsterTrait> Traits => _traits;
    /// <summary>보스 웨이브의 보스. 특성으로 두지 않는 이유: 보스는 거대 + 무작위 특성이라, 거대 특성에 붙이면 보스가 아닌 거대도 같이 받는다.</summary>
    public bool IsBoss { get; }
    /// <summary>
    /// 잭팟 보스(기획서 6.5): 보스가 확률로 된다. 겉모습(금색)과 보상(카드 고르기 횟수)만 보스와 다르다.
    /// 특성으로 두지 않는 이유는 보스와 같다 — 보상은 이 표시가 정한다.
    /// </summary>
    public bool IsJackpot { get; }

    public MonsterKind(SO_MonsterType type, List<TraitSlot> slots, bool isBoss, bool isJackpot = false)
    {
        Base = type;
        _traits = new List<SO_MonsterTrait>(slots.Count);
        _strengths = new List<float>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            _traits.Add(slots[i].Trait);
            _strengths.Add(slots[i].Strength);
        }
        IsBoss = isBoss;
        IsJackpot = isJackpot;
    }

    /// <summary>i번째 특성의 세기(1 = SO 값 그대로).</summary>
    public float GetStrength(int index) => _strengths[index];

    // 배율은 1에서 벗어난 몫만 세기를 받는다(체력 2배 특성의 세기 0.5 = 1.5배).
    private float Scaled(float scale, int index) => 1f + (scale - 1f) * _strengths[index];

    public string Name
    {
        get
        {
            string name = Base.name;
            for (int i = 0; i < _traits.Count; i++)
            {
                name += "_" + _traits[i].name;
                if (_strengths[i] != 1f) name += "x" + _strengths[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (IsBoss) name += "_Boss";
            if (IsJackpot) name += "_Jackpot";
            return name;
        }
    }

    public float HealthScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= Scaled(_traits[i].HealthScale, i);
            return scale;
        }
    }

    public float MoveSpeed
    {
        get
        {
            float speed = Base.MoveSpeed;
            for (int i = 0; i < _traits.Count; i++) speed *= Scaled(_traits[i].SpeedScale, i);
            return speed;
        }
    }

    /// <summary>둔화 비율·얼림 시간에 곱하는 값. 저항은 겹치면 곱으로 줄어든다(둘 다 0.5면 0.25 — 면역은 되지 않는다).</summary>
    public float ImpairScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= 1f - _traits[i].ImpairResist * _strengths[i];
            return scale;
        }
    }

    /// <summary>받는 피해에 곱하는 값(1 − 피해 저항의 곱).</summary>
    public float DamageScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= 1f - _traits[i].DamageResist * _strengths[i];
            return scale;
        }
    }

    /// <summary>웨이브 방어력에 곱하는 배율(배율이라 1에서 벗어난 몫만 세기를 받는다 — 보조 단단함은 1.5배).</summary>
    public float ArmorScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= Scaled(_traits[i].ArmorScale, i);
            return scale;
        }
    }

    /// <summary>방어력의 고정 몫. 실제 방어력은 GetArmor로 읽는다.</summary>
    public float Armor
    {
        get
        {
            float armor = Base.Armor;
            for (int i = 0; i < _traits.Count; i++) armor += _traits[i].Armor * _strengths[i];
            return armor;
        }
    }

    /// <summary>방어력의 체력 비례 몫(겹치면 더한다).</summary>
    public float ArmorHealthRatio
    {
        get
        {
            float ratio = 0f;
            for (int i = 0; i < _traits.Count; i++) ratio += _traits[i].ArmorHealthRatio * _strengths[i];
            return ratio;
        }
    }

    /// <summary>방어력 = 웨이브 방어력 × 배율 + 고정 몫 + 체력 비례 몫 × 자기 최대 체력.</summary>
    /// <param name="waveArmor">나온 웨이브의 방어력(MonsterState.WaveArmor).</param>
    public float GetArmor(double maxHealth, float waveArmor) => waveArmor * ArmorScale + Armor + ArmorHealthRatio * (float)maxHealth;

    public float RegenRatio
    {
        get
        {
            float ratio = 0f;
            for (int i = 0; i < _traits.Count; i++) ratio += _traits[i].RegenRatio * _strengths[i];
            return ratio;
        }
    }

    public int GoalDamage
    {
        get
        {
            int damage = Base.GoalDamage;
            for (int i = 0; i < _traits.Count; i++) damage += _traits[i].GoalDamageBonus;
            return damage;
        }
    }

    public int Exp
    {
        get
        {
            int exp = Base.Exp;
            for (int i = 0; i < _traits.Count; i++) exp += _traits[i].ExpBonus;
            return exp;
        }
    }

    /// <summary>wave 웨이브에 나올 때의 보호막 횟수(고정 몫 + 웨이브 비례 몫, 겹치면 더한다). 나올 때 MonsterState.Shield에 넣는다.</summary>
    public int GetShieldHits(int wave)
    {
        float hits = 0f;
        for (int i = 0; i < _traits.Count; i++) hits += (_traits[i].ShieldHits + _traits[i].ShieldPerWave * wave) * _strengths[i];
        return Mathf.RoundToInt(hits);
    }

    /// <summary>분노: 잃은 체력 비율에 곱해 이동 속도에 더하는 값(겹치면 더한다).</summary>
    public float EnrageSpeedBonus
    {
        get
        {
            float bonus = 0f;
            for (int i = 0; i < _traits.Count; i++) bonus += _traits[i].EnrageSpeedBonus * _strengths[i];
            return bonus;
        }
    }

    /// <summary>보호막이 남은 동안 받는 피해 비율(겹치면 곱한다).</summary>
    public float ShieldDamageRatio
    {
        get
        {
            float ratio = 1f;
            for (int i = 0; i < _traits.Count; i++) ratio *= _traits[i].ShieldDamageRatio;
            return ratio;
        }
    }

    public float VisualScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= Scaled(_traits[i].VisualScale, i);
            return scale;
        }
    }

    /// <summary>
    /// 띠 공의 두 색(띠·양끝). 색이 있는 특성(Tint 알파 &gt; 0)마다 기본 색에 섞은 색 하나 — 없으면 기본 색, 하나면 그 색 단색(띠 = 양끝),
    /// 둘이면 띠 = 앞, 양끝 = 뒤. 셋째부터는 보이지 않는다(웨이브 설정이 색 있는 특성을 둘까지로 둔다).
    /// 색 섞기 대신 나눠 칠하는 이유: 섞으면 두 특성이 겹친 몹이 제3의 색이 되어 어느 특성인지 읽을 수 없다.
    /// 센 특성(웨이브의 주인공 특성)이 띠 — 몸통 대부분이 그 색이라 웨이브 성격이 한눈에 보인다. 세기가 같으면 에셋 이름 순서:
    /// 조합 순서는 무작위라, 그대로 쓰면 같은 조합이 띠·양끝이 뒤바뀌어 다른 몹처럼 보인다.
    /// </summary>
    public void GetBallColors(out Color band, out Color cap)
    {
        int first = -1;
        int second = -1;
        for (int i = 0; i < _traits.Count; i++)
        {
            if (_traits[i].Tint.a <= 0f) continue;
            if (first < 0 || ComesBefore(i, first))
            {
                second = first;
                first = i;
            }
            else if (second < 0 || ComesBefore(i, second)) second = i;
        }

        band = first >= 0 ? Blend(_traits[first]) : Base.Color;
        cap = second >= 0 ? Blend(_traits[second]) : band;
    }

    private bool ComesBefore(int a, int b) =>
        _strengths[a] != _strengths[b] ? _strengths[a] > _strengths[b] : string.CompareOrdinal(_traits[a].name, _traits[b].name) < 0;

    private Color Blend(SO_MonsterTrait trait)
    {
        Color tint = trait.Tint;
        return Color.Lerp(Base.Color, new Color(tint.r, tint.g, tint.b, 1f), tint.a);
    }

    /// <summary>같은 기본 종류와 같은 특성·세기 모음인지(순서는 보지 않는다). 같은 두 특성이어도 주인공·보조가 바뀌면 다른 모양이다.</summary>
    public bool Matches(SO_MonsterType type, List<TraitSlot> slots, bool isBoss, bool isJackpot = false)
    {
        if (IsBoss != isBoss || IsJackpot != isJackpot || Base != type || _traits.Count != slots.Count) return false;
        for (int i = 0; i < slots.Count; i++)
        {
            int index = _traits.IndexOf(slots[i].Trait);
            if (index < 0 || _strengths[index] != slots[i].Strength) return false;
        }
        return true;
    }
}
