using System;
using UnityEngine;

/// <summary>
/// 스킬을 쓰면 컷인을 띄울지 정한다(기획서 12.3, 2026-10-30 사용자): 전용 카드와 적 대상 스킬만 — 자주 쓰는 주사위·판 스킬마다 나오면 과하다.
/// 적 대상 스킬은 영웅 공격력 비례라 "영웅이 쓴다"는 느낌을 준다. 표시만이라 판정·시뮬레이션과 무관하다.
/// </summary>
public sealed class SkillCutIn : IDisposable
{
    private readonly SO_SkillCutInConfig _config;
    private readonly SO_CardHandConfig _handConfig;
    private readonly SkillCaster _skills;
    private readonly HeroStats _stats;
    private readonly UI_SkillCutIn _view;

    /// <param name="handConfig">카드 종류 색(전용·스킬) — 컷인 줄 색.</param>
    public SkillCutIn(SO_SkillCutInConfig config, SO_CardHandConfig handConfig, SkillCaster skills, HeroStats stats, UI_SkillCutIn view)
    {
        _config = config;
        _handConfig = handConfig;
        _skills = skills;
        _stats = stats;
        _view = view;
        _skills.Used += OnUsed;
    }

    public void Dispose()
    {
        _skills.Used -= OnUsed;
    }

    private void OnUsed(SO_SkillCard card)
    {
        if (_view == null || !(card.Exclusive || card is SO_EnemySkillCard)) return;
        Color kind = card.Exclusive ? _handConfig.ExclusiveCardColor : _handConfig.SkillCardColor;
        _view.Show(_stats.Character != null ? _stats.Character.Portrait : null, App.Localize(card.DisplayName), kind, _config);
    }
}
