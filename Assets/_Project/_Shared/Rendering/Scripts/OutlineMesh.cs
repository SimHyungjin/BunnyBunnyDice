using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 툰 아웃라인(셰이더 Def/Outline) 붙이기. 아웃라인은 물체 머티리얼 뒤의 두 번째 머티리얼이라 같은 메시를 한 번 더 그린다
/// (서브메시가 하나인 메시에 머티리얼이 둘이면 Unity가 마지막 서브메시를 다시 그린다).
/// 각진 모델은 같은 위치에 법선이 다른 정점이 겹쳐 있어 부풀리면 모서리가 갈라지므로, 위치마다 법선을 평균 낸 부드러운 법선을 UV3에 넣는다.
/// 모델 원본 메시는 건드리지 않고 사본을 만들어 쓰며, 같은 원본은 사본 하나를 같이 쓴다(주사위가 수십 개여도 면체마다 하나).
/// 모델 FBX는 Read/Write가 켜져 있어야 정점을 읽을 수 있다.
/// </summary>
public static class OutlineMesh
{
    // 스킨드 메시 옆에 붙이는 아웃라인 렌더러의 이름 끝. 다시 붙일 때 이것에는 또 붙이지 않는다.
    private const string SkinnedOutlineSuffix = "_Outline";

    private static readonly int OutlineWidthModeId = Shader.PropertyToID("_OutlineWidthMode");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int ClipMapId = Shader.PropertyToID("_ClipMap");
    private static readonly int ClipMapStId = Shader.PropertyToID("_ClipMap_ST");
    private static readonly int ClipCutoffId = Shader.PropertyToID("_ClipCutoff");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

    private static readonly Dictionary<Mesh, Mesh> SmoothedCopies = new Dictionary<Mesh, Mesh>();
    private static readonly Dictionary<(Mesh, ulong), Mesh> FilteredCopies = new Dictionary<(Mesh, ulong), Mesh>();
    private static readonly Dictionary<Vector3, Vector3> NormalSums = new Dictionary<Vector3, Vector3>();
    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector3> Normals = new List<Vector3>();
    private static readonly List<Vector3> Smoothed = new List<Vector3>();

    /// <summary>root 아래 모든 메시 렌더러에 아웃라인을 붙인다. 모델 메시는 부드러운 법선을 넣은 사본으로 바꾼다. outline이 null이면 아무 일도 없다.</summary>
    public static void AttachToModel(GameObject root, Material outline)
    {
        if (outline == null) return;
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh source = filters[i].sharedMesh;
            if (source == null || !source.isReadable) continue;
            filters[i].sharedMesh = GetSmoothedCopy(source);
            Attach(filters[i].GetComponent<Renderer>(), outline);
        }

        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++) AttachToSkinned(skinned[i], outline);
    }

    /// <summary>
    /// 스킨드 메시(캐릭터)는 같은 뼈를 쓰는 렌더러를 옆에 하나 더 두고 서브메시마다 아웃라인을 그린다.
    /// - 머티리얼 끝에 붙이는 방식(Attach)은 마지막 서브메시만 다시 그려, 머티리얼이 여럿인 캐릭터는 일부만 테두리가 생긴다.
    /// - 부드러운 법선(UV3)을 넣지 않는다: UV3은 뼈를 따라 돌지 않아 팔다리를 굽히면 테두리가 엉뚱한 쪽으로 부푼다.
    ///   셰이더는 UV3이 없으면 실제(뼈로 변형된) 법선을 쓴다. 캐릭터는 대개 매끈한 메시라 모서리가 갈라질 일이 적다.
    /// </summary>
    private static void AttachToSkinned(SkinnedMeshRenderer source, Material outline)
    {
        if (source.sharedMesh == null || source.name.EndsWith(SkinnedOutlineSuffix)) return;

        // 그려지는 서브메시만(재질 수만큼 — 꺼 둔 아웃라인 껍데기는 테두리도 그리지 않는다).
        Material[] sourceMaterials = source.sharedMaterials;
        int count = Mathf.Min(source.sharedMesh.subMeshCount, sourceMaterials.Length);
        ulong skipped = 0;
        for (int i = 0; i < count && i < 64; i++)
        {
            if (!WantsOutline(sourceMaterials[i])) skipped |= 1UL << i;
        }
        Mesh mesh = skipped == 0 ? source.sharedMesh : GetFilteredCopy(source.sharedMesh, skipped);
        if (mesh == null) return;

        GameObject child = new GameObject(source.name + SkinnedOutlineSuffix);
        child.transform.SetParent(source.transform, false);
        SkinnedMeshRenderer copy = child.AddComponent<SkinnedMeshRenderer>();
        copy.sharedMesh = mesh;
        copy.bones = source.bones;
        copy.rootBone = source.rootBone;
        copy.localBounds = source.localBounds;
        copy.updateWhenOffscreen = source.updateWhenOffscreen;
        copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        copy.receiveShadows = false;

        Material[] materials = new Material[count];
        for (int i = 0; i < materials.Length; i++) materials[i] = outline;
        copy.sharedMaterials = materials;

        // 알파로 깎은 재질은 선도 같은 모양으로 깎는다 — 레이스 치맛단·소매 끝에 판 테두리가 검은 가시처럼 삐져나오지 않게.
        // 머티리얼을 복제하지 않고 블록으로 넣어, 조명 설정이 공용 아웃라인의 색·굵기를 바꾸면 그대로 따라온다.
        for (int i = 0; i < count; i++)
        {
            Material material = sourceMaterials[i];
            if (!IsCutout(material)) continue;
            PropertyBlock.Clear();
            PropertyBlock.SetTexture(ClipMapId, material.mainTexture);
            PropertyBlock.SetVector(ClipMapStId, new Vector4(material.mainTextureScale.x, material.mainTextureScale.y,
                material.mainTextureOffset.x, material.mainTextureOffset.y));
            PropertyBlock.SetFloat(ClipCutoffId, material.GetFloat(CutoffId));
            copy.SetPropertyBlock(PropertyBlock, i);
        }
    }

    // 알파 테스트 큐에 있고 컷오프·텍스처가 있는 재질(Unity 표준 이름 _Cutoff — URP Lit·MToon 모두).
    private static bool IsCutout(Material material)
    {
        if (material == null || material.mainTexture == null || !material.HasProperty(CutoffId)) return false;
        int queue = material.renderQueue;
        return queue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest && queue <= (int)UnityEngine.Rendering.RenderQueue.GeometryLast;
    }

    // 테두리를 그리면 안 되는 재질.
    // - 반투명(속눈썹·눈썹·눈 하이라이트): 껍데기는 깊이를 써서 뒤를 가리고, 알파로 그린 모양이 아니라 판 테두리가 그려진다.
    // - 모델이 테두리를 끈 재질(VRM MToon의 _OutlineWidthMode = 0, 머리카락 등): 알파로 깎은 판이라 판 모서리가 들쭉날쭉 그려진다.
    //   모델을 만든 도구(VRoid)가 고른 값을 따른다 — 같은 모델이라도 부위마다 어울리는지가 다르다.
    private static bool WantsOutline(Material material)
    {
        if (material == null) return false;
        if (material.renderQueue > (int)UnityEngine.Rendering.RenderQueue.GeometryLast) return false;
        if (material.HasProperty(OutlineWidthModeId) && material.GetFloat(OutlineWidthModeId) == 0f) return false;
        return true;
    }

    // 뺄 서브메시의 삼각형을 비운 사본. 슬롯을 비워(null) 두면 분홍 에러 셰이더로 그려지므로 메시 쪽에서 지운다.
    // 같은 원본·같은 조합은 사본 하나를 같이 쓴다(영웅을 다시 만들어도 쌓이지 않게). 읽을 수 없는 메시는 null(테두리 없음).
    private static Mesh GetFilteredCopy(Mesh source, ulong skipped)
    {
        if (FilteredCopies.TryGetValue((source, skipped), out Mesh copy) && copy != null) return copy;
        if (!source.isReadable) return null;
        copy = Object.Instantiate(source);
        copy.name = source.name + SkinnedOutlineSuffix;
        for (int i = 0; i < copy.subMeshCount && i < 64; i++)
        {
            if ((skipped & (1UL << i)) != 0) copy.SetIndices(System.Array.Empty<int>(), MeshTopology.Triangles, i);
        }
        FilteredCopies[(source, skipped)] = copy;
        return copy;
    }

    /// <summary>
    /// root 아래 아웃라인 자리(outline 머티리얼 번호)에만 색을 덮어쓴다. null이면 공용 색으로 돌린다 — 공용 머티리얼은 그대로라
    /// 조명 설정이 바꾸는 굵기·평소 색은 따라온다. 알파로 깎은 재질의 깎기 값(AttachToSkinned가 넣은 블록)은 지우지 않고 남긴다.
    /// </summary>
    public static void Tint(GameObject root, Material outline, Color? color)
    {
        if (root == null || outline == null) return;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                // 머티리얼이 아니라 셰이더로 찾는다 — 누가 렌더러 머티리얼을 복제해도(VRM 런타임은 처음 뜰 때 모두 복제한다,
                // HeroModel 참고) 아웃라인 자리를 놓치지 않게.
                if (materials[m] == null || materials[m].shader != outline.shader) continue;
                // 블록에서 속성 하나만 지우는 방법이 없어, 깎기 값을 꺼내 두고 비운 뒤 다시 넣는다.
                renderers[i].GetPropertyBlock(PropertyBlock, m);
                Texture clip = PropertyBlock.HasTexture(ClipMapId) ? PropertyBlock.GetTexture(ClipMapId) : null;
                Vector4 clipSt = PropertyBlock.GetVector(ClipMapStId);
                float clipCutoff = PropertyBlock.GetFloat(ClipCutoffId);
                PropertyBlock.Clear();
                if (clip != null)
                {
                    PropertyBlock.SetTexture(ClipMapId, clip);
                    PropertyBlock.SetVector(ClipMapStId, clipSt);
                    PropertyBlock.SetFloat(ClipCutoffId, clipCutoff);
                }
                if (color.HasValue) PropertyBlock.SetColor(OutlineColorId, color.Value);
                renderers[i].SetPropertyBlock(PropertyBlock, m);
            }
        }
    }

    /// <summary>
    /// 코드로 만든(직접 가진) 메시에 부드러운 법선을 넣고 렌더러에 아웃라인을 붙인다. 사본을 만들지 않는다.
    /// </summary>
    public static void AttachToOwned(MeshFilter filter, Material outline)
    {
        if (outline == null || filter == null || filter.sharedMesh == null) return;
        BakeSmoothNormals(filter.sharedMesh);
        Attach(filter.GetComponent<Renderer>(), outline);
    }

    /// <summary>렌더러 머티리얼 끝에 아웃라인을 붙인다(이미 있으면 그대로).</summary>
    public static void Attach(Renderer renderer, Material outline)
    {
        if (renderer == null || outline == null) return;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == outline) return;
        }
        Material[] next = new Material[materials.Length + 1];
        for (int i = 0; i < materials.Length; i++) next[i] = materials[i];
        next[materials.Length] = outline;
        renderer.sharedMaterials = next;
    }

    private static Mesh GetSmoothedCopy(Mesh source)
    {
        if (SmoothedCopies.TryGetValue(source, out Mesh copy) && copy != null) return copy;
        copy = Object.Instantiate(source);
        copy.name = source.name + "_Outline";
        BakeSmoothNormals(copy);
        SmoothedCopies[source] = copy;
        return copy;
    }

    // 위치가 같은 정점들의 법선을 더해 정규화한다. 면 법선을 더하는 것과 같아 각진 모서리에서 껍데기가 이어진다.
    private static void BakeSmoothNormals(Mesh mesh)
    {
        mesh.GetVertices(Vertices);
        mesh.GetNormals(Normals);
        if (Normals.Count != Vertices.Count) return;

        NormalSums.Clear();
        for (int i = 0; i < Vertices.Count; i++)
        {
            NormalSums.TryGetValue(Vertices[i], out Vector3 sum);
            NormalSums[Vertices[i]] = sum + Normals[i];
        }
        Smoothed.Clear();
        for (int i = 0; i < Vertices.Count; i++) Smoothed.Add(NormalSums[Vertices[i]].normalized);
        mesh.SetUVs(3, Smoothed);
    }
}
