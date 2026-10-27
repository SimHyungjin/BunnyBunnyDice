using UnityEngine;

/// <summary>
/// 반사용 큐브맵을 코드로 만든다: 위는 밝고 아래로 갈수록 어두운 바탕에, 머리 위 사각 조명판(소프트박스) 하나.
/// 카메라 배경이 단색이라 광택·금속 재질이 비칠 것이 없으면 금은 누렇게, 광택 레일은 그냥 검게 보인다.
/// 제품 사진처럼 밝은 조명판이 곡면에 길게 비쳐야 재질이 읽힌다. 화면에는 그려지지 않고 반사에만 쓴다.
/// HDR(Half) 형식이라 조명판을 1보다 밝게 둬 블룸을 받을 만큼 반짝이게 할 수 있다.
/// </summary>
public static class StudioReflection
{
    public static Cubemap Create(SO_LightingConfig config)
    {
        int size = Mathf.Max(8, config.ReflectionSize);
        Cubemap cubemap = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "StudioReflection" };
        Color[] pixels = new Color[size * size];

        for (int face = 0; face < 6; face++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 면 안의 위치(-1~1). 큐브맵 면은 위쪽 행부터 저장된다.
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = Sample(config, Direction((CubemapFace)face, u, v));
                }
            }
            cubemap.SetPixels(pixels, (CubemapFace)face);
        }
        // 밉맵은 거친 재질일수록 흐린 반사를 쓰게 한다(정확한 흐림 계산은 아니지만 거칠수록 조명판이 퍼져 보인다).
        cubemap.Apply(true, false);
        return cubemap;
    }

    private static Color Sample(SO_LightingConfig config, Vector3 direction)
    {
        Vector3 d = direction.normalized;
        Color color = d.y >= 0f
            ? Color.Lerp(config.ReflectionHorizon, config.ReflectionTop, d.y)
            : Color.Lerp(config.ReflectionHorizon, config.ReflectionBottom, -d.y);

        // 머리 위 조명판: 위쪽 방향을 높이 1 평면에 투영해 사각형 안이면 밝게.
        if (d.y > 0f)
        {
            float px = d.x / d.y, pz = d.z / d.y;
            Vector2 half = config.SoftboxHalfSize;
            if (Mathf.Abs(px) < half.x && Mathf.Abs(pz) < half.y) color = config.SoftboxColor * config.SoftboxIntensity;
        }
        return color;
    }

    // 큐브맵 면과 면 안 위치(u 오른쪽, v 아래쪽)를 방향으로. Unity(DirectX) 큐브맵 면 배치를 따른다.
    private static Vector3 Direction(CubemapFace face, float u, float v)
    {
        switch (face)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
            default: return new Vector3(-u, -v, -1f);
        }
    }
}
