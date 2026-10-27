using UnityEngine;

/// <summary>
/// 카지노 바닥 카펫 무늬를 코드로 만든다(이음매 없이 반복): 바탕 + 마름모 격자 선 + 마름모 가운데 작은 장식 마름모.
/// 테이블 바깥이 검은 빈 공간이면 판이 허공에 떠 보여서, 무늬 있는 바닥을 깔아 "방 안의 테이블"로 보이게 한다.
/// 무늬는 칸 하나(0~1) 안의 마름모 거리(|u−½| + |v−½|)로 그린다 — 가장자리에서 서로 맞물려 반복해도 이어진다.
/// </summary>
public static class CarpetTexture
{
    public static Texture2D Create(SO_EnvironmentConfig config)
    {
        int size = Mathf.Max(16, config.CarpetTextureSize);
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float diamond = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                Color color = config.CarpetBaseColor;
                // 격자 선: 마름모 테두리(거리 0.5) 근처.
                if (Mathf.Abs(diamond - 0.5f) < config.CarpetLineWidth) color = config.CarpetLineColor;
                // 가운데 장식.
                else if (diamond < config.CarpetAccentSize) color = config.CarpetAccentColor;
                pixels[y * size + x] = color;
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
        {
            name = "CarpetTexture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
        };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }
}
