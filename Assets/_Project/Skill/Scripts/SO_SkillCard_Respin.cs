using UnityEngine;

/// <summary>
/// 리스핀(더블 오어 낫싱 전용, 판 전체): 판의 눈이 1인 주사위만 다시 굴린다(2026-10-29 사용자). 특성이 1 아니면 최대만 내므로
/// 절반쯤이 최대로 뒤집힌다 — 특성이 강해 카드는 약하게 둔다. 전부 다시 굴리면 어느 쪽이 최대인지만 바뀌어 이득이 없었다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Respin", fileName = "SO_SkillCard_Respin")]
public sealed class SO_SkillCard_Respin : SO_SkillCard
{
    public override SkillTarget Target => SkillTarget.Board;
}
