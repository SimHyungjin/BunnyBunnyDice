using UnityEngine;

/// <summary>
/// 시드로 다시 만들 수 있는 난수 한 줄. UnityEngine.Random과 같은 모양의 호출(Value·Range·InsideUnitCircle)만 둔다
/// — 바꿔 끼울 때 호출하는 쪽 식을 고치지 않게.
/// System.Random을 감싸는 이유: UnityEngine.Random은 전역 한 줄이라 연출도 같이 뽑아 가서, 표시를 고치면 판 결과가 바뀐다.
/// </summary>
public sealed class RandomStream
{
    private System.Random _random;

    public RandomStream(int seed) => Reset(seed);

    public void Reset(int seed) => _random = new System.Random(seed);

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
