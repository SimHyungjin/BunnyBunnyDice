using UnityEngine;

/// <summary>
/// 칩 리더: 칩이 있는 칸에 칩을 놓아 바꿀 수 있다(2026-11-01 사용자). 바뀐 칩은 사라진다 — 손패로 돌려주면 공짜로 계속 바꿔 끼운다.
/// 몬스터 특성이 주기로 돌아(분노 → 재생 → 보호막 → 단단함 → 보스) 다음 웨이브에 맞춰 칩을 바꾸는 판단이 생긴다.
/// 전에는 "모든 칩 x1.5"였는데 고를 거리 없이 그냥 셌고, 사이드 팟(한 칸 두 장)과 곱해지면 칩 칸 하나가 3배를 넘었다.
/// 세기는 전용 카드(빅 스택 — 2배 칩)가 맡는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Chip Swap", fileName = "SO_HeroTrait_ChipSwap")]
public sealed class SO_HeroTrait_ChipSwap : SO_HeroTrait, IChipReplace
{
}
