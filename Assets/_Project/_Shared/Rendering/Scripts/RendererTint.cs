using UnityEngine;

/// <summary>
/// 머티리얼을 복제하지 않고 렌더러 색만 바꾼다. 임시 블록(같은 머티리얼 하나를 공유)을 종류별 색으로 구분할 때 쓴다
/// — 색마다 머티리얼 에셋을 만들거나 런타임에 복제하면 정리할 것이 늘어난다.
/// </summary>
public static class RendererTint
{
    // URP Lit·Unlit의 기본 색 속성.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static MaterialPropertyBlock _block;

    public static void Apply(GameObject target, Color color) => Apply(target, color, null);

    /// <summary>색과 함께 기본 텍스처도 바꾼다(몬스터 띠 공 무늬). texture가 null이면 머티리얼의 텍스처를 그대로 쓴다.</summary>
    public static void Apply(GameObject target, Color color, Texture texture)
    {
        _block ??= new MaterialPropertyBlock();
        _block.Clear();
        _block.SetColor(BaseColorId, color);
        if (texture != null) _block.SetTexture(BaseMapId, texture);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(_block);
    }
}
