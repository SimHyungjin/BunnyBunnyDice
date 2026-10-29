using System;
using UnityEngine;

/// <summary>
/// 브레이크 샷 표시(표시만): 쓰는 순간 영웅 발밑에서 고리가 한 번 퍼지고, 강화 중에는 영웅 외곽선이 카드 색이 된다.
/// 각성 표시와 같은 원칙 — 켜지는 순간만 눈에 띄게, 켜져 있는 동안은 조용히(영웅은 판 위를 계속 뛰어다녀 머무는 고리는 어지럽다).
/// 강화가 걸렸는지는 HeroAttack이 센다(게임 시간) — 여기서는 매 프레임 그 상태를 따라간다.
/// </summary>
public sealed class HeroBreakShotView : IDisposable
{
    private readonly Hero _hero;
    private readonly HeroAttack _attack;
    private readonly SkillCaster _skills;
    private readonly AttackEffects _effects;
    private Color _color = Color.white;
    private bool _wasActive;

    public HeroBreakShotView(Hero hero, HeroAttack attack, SkillCaster skills, AttackEffects effects)
    {
        _hero = hero;
        _attack = attack;
        _skills = skills;
        _effects = effects;
        _skills.Used += OnUsed;
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        _skills.Used -= OnUsed;
        Core.Loop.OnLateUpdate -= LateTick;
    }

    // 다시 써서 시간만 새로 시작해도 고리는 매번 띄운다 — 쓴 것이 보이게.
    private void OnUsed(SO_SkillCard card)
    {
        if (!(card is SO_SkillCard_BreakShot breakShot)) return;
        _color = breakShot.IconColor;
        Vector2 position = _hero.Position;
        _effects.ShowBurst(new Vector3(position.x, _hero.Floor, position.y), breakShot.BurstRadius, _color);
        // 쓴 뒤 첫 LateTick을 기다리지 않고 바로 색을 바꾼다(색이 바뀌었을 수도 있다).
        _hero.SetOutlineTint(_color);
        _wasActive = true;
    }

    private void LateTick(float _)
    {
        bool active = _attack.IsOverdriven;
        if (active == _wasActive) return;
        _wasActive = active;
        _hero.SetOutlineTint(active ? _color : (Color?)null);
    }
}
