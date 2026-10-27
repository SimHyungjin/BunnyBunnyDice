using UnityEngine;

/// <summary>
/// 칸 좌표로 정해지는 밝기 흔들림. 같은 종류 블록이 똑같은 색으로 깔리면 단조로워 보여 칸마다 조금씩 다르게 한다.
/// 난수가 아니라 좌표 해시를 쓰는 이유: 설정을 바꿔 블록을 다시 만들어도 같은 칸은 같은 색이라 화면이 깜빡이지 않는다.
/// </summary>
public static class ColorJitter
{
    /// <param name="amount">최대 밝기 변화 비율(0.05 = ±5%).</param>
    public static Color Apply(Color color, float amount, int x, int y)
    {
        if (amount <= 0f) return color;
        float k = 1f + (Hash01(x, y) * 2f - 1f) * amount;
        return new Color(color.r * k, color.g * k, color.b * k, color.a);
    }

    // 정수 좌표 → [0, 1]. 곱하는 수들은 비트를 고르게 섞기 위한 큰 소수다(조정 대상이 아니다).
    private static float Hash01(int x, int y)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0xFFFFFF;
        }
    }
}
