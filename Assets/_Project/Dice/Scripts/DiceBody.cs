using UnityEngine;

/// <summary>
/// 주사위 몸체 만들기. 놓인 주사위(DiceView)와 배치 고스트(PlacementPreview)가 같은 모양을 쓰도록 한곳에 둔다.
/// 빈 오브젝트(피벗 = 주사위 중심) 아래에 모델을 돌려 넣고, 메시 경계가 가운데 오도록 옮기고 가장 긴 변이 Size가 되도록 키운다
/// — 모델마다 피벗·크기·단위가 달라도 SO에 모델만 넣으면 되게. 굴릴 때 중심을 축으로 돌아야 자연스럽다.
/// </summary>
public static class DiceBody
{
    /// <param name="halfHeight">몸체 중심에서 윗면까지. 칸 위에 세우고 눈 글자를 윗면에 놓는 데 쓴다.</param>
    /// <param name="outline">툰 아웃라인 머티리얼(OutlineMesh). 고스트·판 주사위처럼 선이 필요 없으면 null.</param>
    public static GameObject Build(Transform parent, SO_DiceConfig.FaceModel model, string name, out float halfHeight, Material outline = null)
    {
        GameObject root = new GameObject(name);
        Transform body = root.transform;
        body.SetParent(parent, false);
        halfHeight = model.Size * 0.5f;
        if (model.Model == null) return root;

        Transform mesh = Object.Instantiate(model.Model, body, false).transform;
        mesh.localRotation = Quaternion.Euler(model.Rotation);
        OutlineMesh.AttachToModel(mesh.gameObject, outline);
        Renderer[] renderers = mesh.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return root;

        // 경계는 월드 축 기준이라 몸체 로컬로 옮겨 잰다(몸체는 아직 돌지 않았고, 맵은 돌리거나 키우지 않는다).
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 center = body.InverseTransformPoint(bounds.center);
        Vector3 size = body.InverseTransformVector(bounds.size);
        float longest = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        if (longest <= 0f) return root;

        // 모델 피벗을 기준으로 고르게 키우므로, 키운 뒤의 경계 중심은 center × scale이다.
        float scale = model.Size / longest;
        mesh.localScale *= scale;
        mesh.localPosition -= center * scale;
        halfHeight = Mathf.Abs(size.y) * scale * 0.5f;
        return root;
    }
}
