using UnityEngine;

/// <summary>
/// 하이 롤러: 타워 주사위를 굴리면 높은 눈이 더 자주 나온다(2026-10-30 사용자 — 레벨업 면체를 기울이던 것에서 바꿨다. 면체는 카드 고를 때만 티가 나 체감이 약했다).
/// 눈 k의 가중치 = 1 + (k - 1) × Strength. 1이면 눈 = 가중치(1:2:3…)라 각성 확률이 면체와 상관없이 거의 두 배가 된다.
/// 놓기·얹기·리롤·올인 모두에 건다 — 다시 굴리는 스킬도 높게 나와야 이름과 맞는다.
/// </summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Pip Bias", fileName = "SO_HeroTrait_PipBias")]
public sealed class SO_HeroTrait_PipBias : SO_HeroTrait, IDicePipRoller
{
    [Tooltip("높은 눈 쪽으로 기우는 정도. 0 = 고르게, 1 = 눈이 곧 가중치(1:2:3…).")]
    [Min(0f)] public float Strength = 1f;

    public int RollPips(int faces, bool placing)
    {
        // 가중치가 등차라 합은 공식으로 구한다.
        float total = faces + Strength * faces * (faces - 1) * 0.5f;
        float roll = GameRandom.Dice.Value * total;
        for (int pips = 1; pips < faces; pips++)
        {
            float weight = 1f + (pips - 1) * Strength;
            if (roll < weight) return pips;
            roll -= weight;
        }
        return faces;
    }
}
