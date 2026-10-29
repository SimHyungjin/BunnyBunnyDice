using UnityEngine;

/// <summary>
/// 777: 레벨업 후보에 육면체가 나오지 않고, 주사위를 놓을 때(얹기 포함) 늘 7이 나온다(2026-10-29 사용자). 리롤·올인은 평소처럼 굴린다.
/// 이십면체가 놓자마자 각성할 일이 없는 대신 판이 고르다 — 각성은 합성이나 전용 카드(세븐스 헤븐)로 한다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Sevens", fileName = "SO_HeroTrait_Sevens")]
public sealed class SO_HeroTrait_Sevens : SO_HeroTrait, IDicePipRoller, ILevelUpFaces
{
    [Tooltip("놓을 때 나오는 눈. 면 수보다 크면 면 수.")]
    [Min(1)] public int PlacePips = 7;
    [Tooltip("레벨업 후보에서 빼는 면체.")]
    [Min(1)] public int ExcludeFaces = 6;

    public int RollPips(int faces, bool placing) => placing ? Mathf.Min(PlacePips, faces) : DiceRules.Roll(faces);

    public float ScaleFaces(int faces) => faces == ExcludeFaces ? 0f : 1f;
}
