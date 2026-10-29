using UnityEngine;

/// <summary>
/// 777: 육면체가 나오지 않고(레벨업 후보·시작 손패), 주사위를 놓을 때(얹기 포함) 늘 7이 나온다(2026-10-29 사용자). 리롤·올인은 평소처럼 굴린다.
/// 이십면체가 놓자마자 각성할 일이 없는 대신 판이 고르다 — 각성은 합성이나 전용 카드(세븐스 헤븐)로 한다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Sevens", fileName = "SO_HeroTrait_Sevens")]
public sealed class SO_HeroTrait_Sevens : SO_HeroTrait, IDicePipRoller, ILevelUpFaces, IStartHandCard
{
    [Tooltip("놓을 때 나오는 눈. 면 수보다 크면 면 수.")]
    [Min(1)] public int PlacePips = 7;
    [Tooltip("레벨업 후보·시작 손패에서 빼는 면체.")]
    [Min(1)] public int ExcludeFaces = 6;
    [Tooltip("시작 손패의 빼는 면체 카드를 이 면체로 바꾼다(색은 그대로). 7이 기댓값보다 조금 높은 십이면체 — 이십면체면 7이 평균 아래라 특성이 손해가 된다.")]
    [Min(1)] public int StartFaces = 12;

    public int RollPips(int faces, bool placing) => placing ? Mathf.Min(PlacePips, faces) : DiceRules.Roll(faces);

    public float ScaleFaces(int faces) => faces == ExcludeFaces ? 0f : 1f;

    public DiceCard StartCard(DiceCard card) => card.Faces == ExcludeFaces ? new DiceCard(StartFaces, card.Colors) : card;
}
