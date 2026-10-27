using UnityEngine;

/// <summary>
/// 캐릭터 모델 하나를 만든다(크기 맞추기·색·아웃라인). 판 위 영웅(HeroView)과 선택 창 얼굴 사진(HeroPortraits)이 같이 써서
/// 두 곳의 모습이 어긋나지 않게 한다. 동작(HeroAnimation)은 쓰는 쪽이 붙인다.
/// </summary>
public static class HeroModel
{
    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.CharacterOutlineMaterial). 없으면 null.</param>
    /// <param name="height">맞춘 뒤의 키(칸).</param>
    public static GameObject Create(SO_HeroCharacter character, Transform parent, Material outline, out float height)
    {
        GameObject instance = Object.Instantiate(character.Prefab, parent, false);

        // 모델마다 내보낸 단위가 달라(cm·m) 키를 재서 정한 높이로 맞춘다.
        float measured = MeasureHeight(instance);
        if (character.ModelHeight > 0f) instance.transform.localScale *= character.ModelHeight / measured;
        height = character.ModelHeight > 0f ? character.ModelHeight : measured;

        if (character.ShellMaterial != null) HideShell(instance, character.ShellMaterial);
        if (character.TintModel) RendererTint.Apply(instance, character.Color);
        if (character.AddOutline) OutlineMesh.AttachToModel(instance, outline);
        return instance;
    }

    // 모델의 아웃라인 껍데기를 끈다. 재질 수가 서브메시보다 적으면 남는 서브메시는 그려지지 않으므로 끝의 껍데기 재질을 잘라 낸다
    // (메시를 고치지 않아 모델 Read/Write가 필요 없다). 껍데기가 끝이 아니면 다른 부분까지 꺼지므로 건드리지 않는다.
    private static void HideShell(GameObject instance, Material shell)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            int count = materials.Length;
            while (count > 0 && materials[count - 1] == shell) count--;
            if (count == materials.Length || count == 0) continue;
            Material[] kept = new Material[count];
            for (int j = 0; j < count; j++) kept[j] = materials[j];
            renderers[i].sharedMaterials = kept;
        }
    }

    // 렌더러 경계의 높이. 모델을 놓은 직후(서 있는 자세, 눌리지 않음)에 잰다. 렌더러가 없으면 한 칸으로 본다.
    private static float MeasureHeight(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1f;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.size.y > 0f ? bounds.size.y : 1f;
    }
}
