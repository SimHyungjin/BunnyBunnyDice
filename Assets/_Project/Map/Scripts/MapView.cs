using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지형 표시. 칸 (x, y)는 부모 기준 로컬 (x, 0, y)에 놓인다.
/// 판 바닥은 테이블 펠트(EnvironmentView)가 대신하므로 여기서는 목표물 표식만 그린다.
/// 칩은 판 중에 놓이고 종류가 있어 ChipField가 그린다.
/// 출현 구역은 테이블의 열린 포켓(EnvironmentView)이 보여 주므로 여기서는 그리지 않는다. 둘레 벽은 펠트에 인쇄된 경계 선으로 보이므로 그리지 않는다.
/// 목표물 표식은 목표물 영역 정중앙에 둥근 물건 하나다 — 판정 영역이 1×2여도(판이 짝수 칸이면 정중앙이 칸 경계라서) 눈에는 한 점짜리 목표로 보이게.
/// 프리팹이 있으면 합치지 않고 그대로 세우고(합치면 표식 자체의 텍스처·투명도가 사라진다), 없으면 큰 잭팟 칩을 만든다.
/// 적이 닿을 때 혼자 튀고 빛나야 해서(GoalHitReaction) Goal 오브젝트를 영역 정중앙에 두고 그 안에 바닥 기준으로 만든다.
/// </summary>
public sealed class MapView
{
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly SO_MapViewConfig _config;
    private readonly Transform _root;
    private readonly Material _outline;
    private readonly PaletteMeshBuilder _builder = new PaletteMeshBuilder();
    private PaletteMesh _goalMesh;
    private Transform _goal;
    private Mesh _disc;

    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.OutlineMaterial). 없으면 null.</param>
    public MapView(Transform parent, SO_MapViewConfig config, Material outline)
    {
        _config = config;
        _outline = outline;
        _root = new GameObject("Terrain").transform;
        _root.SetParent(parent, false);
    }

    public void Build(MapData map)
    {
        Clear();
        Material lit = GetTemplateMaterial(_config.BlockPrefab);

        // 칩 모양 설정이 실행 중에 바뀔 수 있어 Build마다 다시 만든다(몇백 정점이라 가볍다).
        if (_disc != null) Object.Destroy(_disc);
        _disc = ChipMesh.CreateDisc(1f, _config.ChipSegments);

        // 목표물은 바닥과 같은 높이라 한눈에 안 들어오므로 영역 정중앙에 표식을 세운다.
        RectInt goal = map.GoalRect;
        _goal = new GameObject("Goal").transform;
        _goal.SetParent(_root, false);
        _goal.localPosition = new Vector3(goal.center.x - 0.5f, 0f, goal.center.y - 0.5f);
        if (_config.GoalMarkerPrefab != null)
        {
            GameObject marker = Object.Instantiate(_config.GoalMarkerPrefab, _goal, false);
            OutlineMesh.AttachToModel(marker, _outline);
            return;
        }
        AddGoalChip(Vector3.zero);
        _goalMesh = _builder.Build("GoalChip", _goal, lit, ShadowCastingMode.On);
        OutlineMesh.AttachToOwned(_goalMesh.GameObject.GetComponent<MeshFilter>(), _outline);
        Material material = _goalMesh.Material;
        material.SetFloat(SmoothnessId, _config.ChipSmoothness);
        // 빛나기(GoalHitReaction)는 발광 색만 바꾼다. 팔레트를 발광 텍스처로도 써서 칩 색 그대로 빛나게 하고, 평소엔 0이다.
        material.EnableKeyword("_EMISSION");
        material.SetTexture(EmissionMapId, material.GetTexture(BaseMapId));
        material.SetColor(EmissionColorId, Color.black);
    }

    /// <summary>목표물 표식(영역 정중앙, 바닥 높이). 맵을 다시 만들면 바뀐다.</summary>
    public Transform Goal => _goal;

    /// <summary>잭팟 칩의 머티리얼(발광 켜짐). 표식 프리팹을 쓰면 null — 프리팹은 튀기만 한다.</summary>
    public Material GoalFlashMaterial => _goalMesh?.Material;

    // 목표물 대용 표식: 강화 칩보다 크고 밝은 잭팟 칩 한 장 + 윗면 링. 모양 언어는 강화 칩과 같고 크기·색으로 구분된다.
    private void AddGoalChip(Vector3 center)
    {
        float height = _config.GoalChipHeight;
        float diameter = _config.GoalChipDiameter;
        Mesh chip = ChipMesh.CreateChip(diameter, height, height * _config.ChipBevelRatio, _config.ChipSegments);
        _builder.Add(chip, Matrix4x4.Translate(center), _config.GoalChipColor);
        Object.Destroy(chip);

        Vector3 top = center + Vector3.up * height;
        float outer = diameter * _config.GoalRingOuter, inner = diameter * _config.GoalRingInner;
        _builder.Add(_disc, Matrix4x4.TRS(top + Vector3.up * _config.PrintLift, Quaternion.identity, new Vector3(outer, 1f, outer)), _config.GoalRingColor);
        _builder.Add(_disc, Matrix4x4.TRS(top + Vector3.up * _config.PrintLift * 2f, Quaternion.identity, new Vector3(inner, 1f, inner)), _config.GoalChipColor);
    }

    public void Clear()
    {
        _goalMesh?.Dispose();
        _goalMesh = null;
        if (_goal != null) Object.Destroy(_goal.gameObject);
        _goal = null;
    }

    /// <summary>합친 메시의 머티리얼은 블록 프리팹의 머티리얼(URP Lit)을 복제해 쓴다.</summary>
    public static Material GetTemplateMaterial(GameObject prefab) => prefab.GetComponentInChildren<Renderer>(true).sharedMaterial;
}
