using UnityEngine;

/// <summary>
/// 더블 오어 낫싱: 타워 주사위를 굴리면 가장 높은 눈 아니면 부서진다(놓기·얹기·리롤·올인 모두, 2026-11-01 사용자 — 미친 캐릭터로 둔다).
/// 놓기는 주사위가 생기지 않고, 얹기·리롤·올인은 그 칸의 주사위가 통째로 사라진다. 예외를 두면 규칙이 길어져 하나로 묶었다.
/// 전에는 "1 아니면 최대"였는데 얹기가 눈을 더하는 규칙이라 1이 손해가 아니었다 — d20을 얹을 때마다 50% 각성에 실패해도 +1이라 지나치게 셌다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Min Max", fileName = "SO_HeroTrait_MinMax")]
public sealed class SO_HeroTrait_MinMax : SO_HeroTrait, IDicePipRoller
{
    [Tooltip("가장 높은 눈이 나올 확률(나머지는 부서진다).")]
    [Range(0f, 1f)] public float HighChance = 0.5f;

    public int RollPips(int faces) => GameRandom.Dice.Value < HighChance ? faces : 0;
}
