using UnityEngine;

/// <summary>
/// 체크(판 전체): 판 위의 모든 적을 잠깐 멈춘다(포커의 체크 — 차례를 넘겨 판을 세운다). 새는 순간을 막는 위기 탈출 카드다.
/// 얼림과 같은 상태(MonsterStatus.Hold)로 거는데, 얼림 면역을 무시한다 — 칩이 막 얼렸다 풀린 적만 계속 걸어가면 카드가 헛돈다.
/// 거대(보스)는 이동 방해 저항만큼 짧게 멈춘다. 쓴 뒤 나오는 적은 걸리지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/Skill/Check", fileName = "SO_SkillCard_Check")]
public sealed class SO_SkillCard_Check : SO_SkillCard
{
    [Tooltip("멈추는 시간(초). 저항이 있으면 곱한다.")]
    [Min(0f)] public float Duration = 3f;

    public override SkillTarget Target => SkillTarget.Board;
}
