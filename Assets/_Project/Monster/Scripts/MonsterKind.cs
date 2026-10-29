using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실제로 나오는 몬스터 하나의 모양 = 기본 종류 + 특성 목록. 값은 들고 있지 않고 읽을 때마다 SO에서 계산한다
/// — 플레이 중 특성 SO를 고치면 이미 걷고 있는 몬스터에도 바로 반영되게(특성은 많아야 두세 개라 계산이 싸다).
/// 같은 조합은 같은 인스턴스를 쓴다(WaveSpawner가 모아 둔다) — 표시가 종류별로 오브젝트를 재사용하기 때문이다.
/// </summary>
public sealed class MonsterKind
{
    private readonly List<SO_MonsterTrait> _traits;
    // 분열해서 나온 몹이면 나뉜 특성. 나뉜 몹은 그 특성이 빠진 채 목표물 피해·경험치를 이 특성에서 받는다.
    private readonly SO_MonsterTrait _splitFrom;
    private MonsterKind _piece;

    public SO_MonsterType Base { get; }
    public IReadOnlyList<SO_MonsterTrait> Traits => _traits;
    public bool IsPiece => _splitFrom != null;
    /// <summary>보스 웨이브의 보스. 특성으로 두지 않는 이유: 보스는 거대 + 무작위 특성이라, 거대 특성에 붙이면 보스가 아닌 거대도 같이 받는다.</summary>
    public bool IsBoss { get; }
    /// <summary>
    /// 잭팟 몬스터(현상금을 보너스로 바꾼 것, 기획서 6장): 잡으면 보상, 목표물에 닿으면 피해 없이 빠져나간다.
    /// 특성으로 두지 않는 이유는 보스와 같다 — 겉모습·속도는 잭팟용 특성이 정하고, 보상·피해 없음은 이 표시가 정한다.
    /// </summary>
    public bool IsJackpot { get; }

    public MonsterKind(SO_MonsterType type, List<SO_MonsterTrait> traits, bool isBoss, bool isJackpot = false)
        : this(type, traits, null, isBoss, isJackpot) { }

    private MonsterKind(SO_MonsterType type, List<SO_MonsterTrait> traits, SO_MonsterTrait splitFrom, bool isBoss, bool isJackpot)
    {
        Base = type;
        _traits = traits;
        _splitFrom = splitFrom;
        IsBoss = isBoss;
        IsJackpot = isJackpot;
    }

    public string Name
    {
        get
        {
            string name = Base.name;
            for (int i = 0; i < _traits.Count; i++) name += "_" + _traits[i].name;
            if (IsBoss) name += "_Boss";
            if (IsJackpot) name += "_Jackpot";
            return IsPiece ? name + "_Piece" : name;
        }
    }

    public float HealthScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= _traits[i].HealthScale;
            return scale;
        }
    }

    public float MoveSpeed
    {
        get
        {
            float speed = Base.MoveSpeed;
            for (int i = 0; i < _traits.Count; i++) speed *= _traits[i].SpeedScale;
            return speed;
        }
    }

    /// <summary>둔화 비율·얼림 시간에 곱하는 값. 저항은 겹치면 곱으로 줄어든다(둘 다 0.5면 0.25 — 면역은 되지 않는다).</summary>
    public float ImpairScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= 1f - _traits[i].ImpairResist;
            return scale;
        }
    }

    /// <summary>받는 피해에 곱하는 값(1 − 피해 저항의 곱).</summary>
    public float DamageScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= 1f - _traits[i].DamageResist;
            return scale;
        }
    }

    public float Armor
    {
        get
        {
            float armor = Base.Armor;
            for (int i = 0; i < _traits.Count; i++) armor += _traits[i].Armor;
            return armor;
        }
    }

    public float RegenRatio
    {
        get
        {
            float ratio = 0f;
            for (int i = 0; i < _traits.Count; i++) ratio += _traits[i].RegenRatio;
            return ratio;
        }
    }

    public int GoalDamage
    {
        get
        {
            if (IsPiece) return _splitFrom.PieceGoalDamage;
            int damage = Base.GoalDamage;
            for (int i = 0; i < _traits.Count; i++) damage += _traits[i].GoalDamageBonus;
            return damage;
        }
    }

    public int Exp
    {
        get
        {
            if (IsPiece) return _splitFrom.PieceExp;
            int exp = Base.Exp;
            for (int i = 0; i < _traits.Count; i++) exp += _traits[i].ExpBonus;
            return exp;
        }
    }

    public float VisualScale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _traits.Count; i++) scale *= _traits[i].VisualScale;
            return scale;
        }
    }

    /// <summary>
    /// 띠 공의 두 색(띠·양끝). 색이 있는 특성(Tint 알파 &gt; 0)마다 기본 색에 섞은 색 하나 — 없으면 기본 색, 하나면 그 색 단색(띠 = 양끝),
    /// 둘이면 띠 = 앞, 양끝 = 뒤. 셋째부터는 보이지 않는다(웨이브 설정이 색 있는 특성을 둘까지로 둔다).
    /// 색 섞기 대신 나눠 칠하는 이유: 섞으면 두 특성이 겹친 몹이 제3의 색이 되어 어느 특성인지 읽을 수 없다.
    /// 앞뒤를 에셋 이름 순서로 정하는 이유: 조합 순서는 무작위라, 그대로 쓰면 같은 조합이 띠·양끝이 뒤바뀌어 다른 몹처럼 보인다.
    /// </summary>
    public void GetBallColors(out Color band, out Color cap)
    {
        SO_MonsterTrait first = null;
        SO_MonsterTrait second = null;
        for (int i = 0; i < _traits.Count; i++)
        {
            SO_MonsterTrait trait = _traits[i];
            if (trait.Tint.a <= 0f) continue;
            if (first == null || string.CompareOrdinal(trait.name, first.name) < 0)
            {
                second = first;
                first = trait;
            }
            else if (second == null || string.CompareOrdinal(trait.name, second.name) < 0) second = trait;
        }

        band = first != null ? Blend(first) : Base.Color;
        cap = second != null ? Blend(second) : band;
    }

    private Color Blend(SO_MonsterTrait trait)
    {
        Color tint = trait.Tint;
        return Color.Lerp(Base.Color, new Color(tint.r, tint.g, tint.b, 1f), tint.a);
    }

    /// <summary>나뉘는 특성(없으면 null). 여러 개면 첫 번째만 쓴다.</summary>
    public SO_MonsterTrait SplitTrait
    {
        get
        {
            for (int i = 0; i < _traits.Count; i++)
                if (_traits[i].SplitCount > 0) return _traits[i];
            return null;
        }
    }

    /// <summary>나뉜 몹의 모양: 나뉘는 특성만 뺀다(재생하며 분열하는 몹의 조각은 계속 재생한다). 한 번 만들어 둔다.</summary>
    public MonsterKind GetPiece()
    {
        if (_piece != null) return _piece;
        SO_MonsterTrait split = SplitTrait;
        List<SO_MonsterTrait> traits = new List<SO_MonsterTrait>();
        for (int i = 0; i < _traits.Count; i++)
            if (_traits[i].SplitCount <= 0) traits.Add(_traits[i]);
        // 보스의 조각은 보스가 아니다(작게 나뉜 몹이다).
        _piece = new MonsterKind(Base, traits, split, false, false);
        return _piece;
    }

    /// <summary>같은 기본 종류와 같은 특성 모음인지(순서는 보지 않는다).</summary>
    public bool Matches(SO_MonsterType type, List<SO_MonsterTrait> traits, bool isBoss, bool isJackpot = false)
    {
        if (IsPiece || IsBoss != isBoss || IsJackpot != isJackpot || Base != type || _traits.Count != traits.Count) return false;
        for (int i = 0; i < traits.Count; i++)
            if (!_traits.Contains(traits[i])) return false;
        return true;
    }
}
