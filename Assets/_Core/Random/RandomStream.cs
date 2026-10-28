using UnityEngine;

/// <summary>
/// 시드로 다시 만들 수 있는 난수 한 줄. UnityEngine.Random과 같은 모양의 호출(Value·Range·InsideUnitCircle)만 둔다
/// — 바꿔 끼울 때 호출하는 쪽 식을 고치지 않게.
/// System.Random을 감싸는 이유: UnityEngine.Random은 전역 한 줄이라 연출도 같이 뽑아 가서, 표시를 고치면 결과가 바뀐다.
/// 재현이 필요한 난수(판 결과·시뮬레이션·리플레이)에 쓰고, 연출은 UnityEngine.Random을 그대로 쓴다.
/// 어떤 줄을 둘지는 게임이 정한다(게임 쪽 정적 클래스가 시스템마다 한 줄씩 들고, 판 시작 때 Reset(DeriveSeed(판 시드, 줄 번호))).
/// </summary>
public sealed class RandomStream
{
    private System.Random _random;

    public RandomStream(int seed) => Reset(seed);

    public void Reset(int seed) => _random = new System.Random(seed);

    /// <summary>
    /// 시드 하나에서 줄마다 서로 동떨어진 시드를 낸다(splitmix64 한 걸음).
    /// 줄을 나누는 이유: 한 줄을 같이 쓰면 한 시스템이 한 번 더 굴리는 것만으로 다른 시스템의 수열이 밀린다.
    /// 그대로 seed + 번호를 쓰지 않는 이유: System.Random은 이웃한 시드의 첫 값들이 비슷해 줄끼리·판끼리 닮는다.
    /// </summary>
    public static int DeriveSeed(int seed, int stream)
    {
        unchecked
        {
            ulong z = (ulong)(uint)seed + (ulong)stream * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)z;
        }
    }

    /// <summary>[0, 1). UnityEngine.Random.value와 달리 1은 나오지 않는다.</summary>
    public float Value => (float)_random.NextDouble();

    public double NextDouble() => _random.NextDouble();

    /// <summary>정수 [min, max). max ≤ min이면 min.</summary>
    public int Range(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);

    /// <summary>정수 [0, count).</summary>
    public int Next(int count) => count <= 0 ? 0 : _random.Next(count);

    /// <summary>반지름 1 원 안의 고른 점.</summary>
    public Vector2 InsideUnitCircle()
    {
        // 넓이에 고르게: 반지름은 제곱근을 씌운다(그냥 쓰면 가운데에 몰린다).
        float angle = Value * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(Value);
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }
}
