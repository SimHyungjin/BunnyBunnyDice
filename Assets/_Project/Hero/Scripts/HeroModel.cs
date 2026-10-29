using UnityEngine;

/// <summary>
/// 캐릭터 모델 하나를 만든다(크기 맞추기·아웃라인). 판 위 영웅(HeroView)과 선택 창 얼굴 사진(HeroPortraits)이 같이 써서
/// 두 곳의 모습이 어긋나지 않게 한다. 동작(HeroAnimation)은 쓰는 쪽이 붙인다.
/// </summary>
public static class HeroModel
{
    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.CharacterOutlineMaterial). 없으면 null.</param>
    /// <param name="height">맞춘 뒤의 키(칸).</param>
    public static GameObject Create(SO_HeroCharacter character, Transform parent, Material outline, out float height)
    {
        GameObject instance = Object.Instantiate(character.Prefab, parent, false);

        // 모델마다 키가 달라 재서 정한 높이로 맞춘다(캐릭터끼리 판 위 크기가 같게).
        float measured = MeasureHeight(instance);
        if (character.ModelHeight > 0f) instance.transform.localScale *= character.ModelHeight / measured;
        height = character.ModelHeight > 0f ? character.ModelHeight : measured;

        // 툰 아웃라인을 다른 물체와 같은 화면 비율 굵기로 붙인다.
        OutlineMesh.AttachToModel(instance, outline);
        return instance;
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
