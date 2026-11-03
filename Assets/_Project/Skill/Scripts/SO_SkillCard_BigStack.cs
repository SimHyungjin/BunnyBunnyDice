using System;
using UnityEngine;

/// <summary>
/// 빅 스택(칩 리더 전용, 칸 대상): 강화된 칩 한 장을 놓는다(2026-11-01 사용자). 놓는 곳은 칩 카드와 같고(빈 칸·칩 없는 주사위 칸),
/// 특성(칩 교체)이 있으면 칩이 있는 칸에 놓아 바꿀 수도 있다 — 깔아 둔 보통 칩을 센 칩으로 바꿔 끼운다.
/// 칩 색마다 한 장씩(7장) 있고 후보 자리에는 그중 한 장이 나온다(SO_HeroCharacter.ExclusiveCards) — 색이 보여 고를지 판단한다.
/// 세기는 칩마다 다르다 — 모두 2배였을 때 둔화(감속 비율이 머무는 시간에 가파르게 붙음)·화상(최대 체력 비례라 후반에 그대로 두 배)이 후반에 너무 셌다.
/// 보통 칩보다 실효 +25%쯤 되게 칩마다 맞추고(2026-11-03 사용자), 그래서 글에 "n배"를 적지 않고 강화된 칩 글을 실제 수치로 보여 준다({ChipText}).
/// 전에는 사이드 팟(칩 칸에 다른 종류 한 장을 더 얹어 두 효과 모두)이었는데, 특성(모든 칩 x1.5)과 곱해져 칩 칸 하나가 3배를 넘었다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Big Stack", fileName = "SO_SkillCard_BigStack_")]
public sealed class SO_SkillCard_BigStack : SO_SkillCard, ICardTextTokens
{
    [Tooltip("놓는 칩. 카드 색(IconColor)도 이 칩 색으로 맞춘다.")]
    public SO_ChipCard Chip;
    [Tooltip("칩 효과에 곱하는 세기(ChipEffects.Scaled). 디버프는 세기(얼림은 어는 시간), 강화는 늘리는 비율에 곱한다. 지속 시간은 그대로다. 글에 보이는 강화 수치가 둥글게 나오게 고른다.")]
    [Min(1f)] public float Strength = 2f;

    public override SkillTarget Target => SkillTarget.Cell;

    // 실행 중 조정(GameTuning)이 원본 칩 에셋을 고치지 않게 복사본으로 바꿔 끼운다.
    public override void SwapNested(Func<SO_Config, SO_Config> swap) => Chip = (SO_ChipCard)swap(Chip);

    // {Chip} = 놓는 칩 이름(번역), {ChipText} = 그 칩의 글을 세기를 곱한 수치로 채운 것 — 효과와 같은 계산(ChipEffects.Scaled)을 거친다.
    public bool TryGetToken(string key, HeroStats hero, out string text)
    {
        text = null;
        if (Chip == null) return false;
        if (key == nameof(Chip))
        {
            text = App.Localize(Chip.DisplayName);
            return true;
        }
        if (key == "ChipText")
        {
            text = CardText.Fill(Chip.Description, Chip, hero, (field, value) => ChipEffects.Scaled(Chip, field, value, Strength));
            return true;
        }
        return false;
    }
}
