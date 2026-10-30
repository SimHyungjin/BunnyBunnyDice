using UnityEngine;

/// <summary>
/// 사이드 팟(칩 리더 전용, 칸 대상): 칩이 한 장 있는 칸에 이 카드의 칩을 한 장 더 얹는다 — 한 칸 두 장까지, 같은 종류는 못 얹는다(2026-10-31 사용자).
/// 칩 색마다 한 장씩(7장) 있고 후보 자리에는 그중 한 장이 나온다(SO_HeroCharacter.ExclusiveCards) — 색이 보여 고를지 판단한다.
/// 두 장의 효과가 모두 걸린다(ChipSet). 특성(칩이 세다)과 같은 테마를 "한 칸에 더"로 푼다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Side Pot", fileName = "SO_SkillCard_SidePot_")]
public sealed class SO_SkillCard_SidePot : SO_SkillCard, ICardTextTokens
{
    [Tooltip("얹는 칩. 카드 색(IconColor)도 이 칩 색으로 맞춘다.")]
    public SO_ChipCard Chip;

    public override SkillTarget Target => SkillTarget.Cell;

    // {Chip} = 얹는 칩 이름(번역).
    public bool TryGetToken(string key, HeroStats hero, out string text)
    {
        text = null;
        if (key != nameof(Chip) || Chip == null) return false;
        text = App.Localize(Chip.DisplayName);
        return true;
    }
}
