using UnityEngine;

/// <summary>
/// 드래그 중 손가락 아래 칸에 고스트 주사위와 칸 하이라이트를 띄운다.
/// 고스트는 놓였을 때의 모양 그대로다 — 빈 칸이면 카드의 면체·색, 얹기면 합친 결과의 면체·색(기획서 9.9 드래그 미리보기).
/// 놓을 수 없는 칸에서는 고스트를 숨기고 칸 하이라이트(빨강)만 보인다. 눈은 굴려야 정해지므로 고스트에는 숫자가 없다.
/// 몸체는 면체가 바뀔 때만 다시 만들고, 칸을 옮길 때는 옮기기만 한다.
/// 놓을 수 있는 칸이면 그 자리에서의 사거리를 바닥에 고리로 그린다(기획서 9.9) — 그 칸 칩의 사거리 보너스까지 반영한 실제 사거리.
/// 칩 카드는 칸에 놓이므로 고스트 칩을 띄운다(ShowChip). 주사위 칸이면 칩이 그 밑으로 들어간다.
/// </summary>
public sealed class PlacementPreview
{
    private readonly Transform _parent;
    private readonly SO_PlacementPreviewConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly ChipField _chips;
    private readonly Material _outline;
    private GameObject _ghost;
    private MeshRenderer _ghostChip;
    private Mesh _ghostChipMesh;
    private MeshRenderer _ring;
    private Mesh _ringMesh;
    private float _ghostHalfHeight;
    private int _ghostFaces;
    private GameObject _highlight;

    public PlacementPreview(Transform parent, SO_PlacementPreviewConfig config, SO_MapViewConfig viewConfig, SO_DiceConfig diceConfig,
        SO_DiceStatsConfig statsConfig, ChipField chips, Material outline)
    {
        _parent = parent;
        _config = config;
        _viewConfig = viewConfig;
        _diceConfig = diceConfig;
        _statsConfig = statsConfig;
        _chips = chips;
        _outline = outline;
    }

    /// <param name="dice">놓으면 될 주사위(면체·색만 본다).</param>
    /// <returns>고스트 윗면 위 눈 글자 자리의 높이(맵 로컬). 고스트를 띄우지 않았으면 NaN.</returns>
    public float Show(MapData map, Vector2Int cell, bool valid, in DiceState dice)
    {
        float top = _viewConfig.GetCellTop(map, cell.x, cell.y);
        // 칸 하이라이트는 늘 판 바닥에 깐다 — 칩 칸이면 칩 밑으로 비쳐 칩 윗면을 덮지 않는다.
        ShowHighlight(cell, _viewConfig.GetTileTop(TileType.Ground), valid);
        if (_ghostChip != null) _ghostChip.gameObject.SetActive(false);

        if (!valid)
        {
            if (_ghost != null) _ghost.SetActive(false);
            if (_ring != null) _ring.gameObject.SetActive(false);
            return float.NaN;
        }
        ShowRange(cell, top, ChipEffects.RangeBonus(_chips.Get(cell)), dice);
        if (_ghost == null || _ghostFaces != dice.Faces)
        {
            if (_ghost != null) Object.Destroy(_ghost);
            // 고스트도 놓인 물체와 같은 아웃라인 — 놓였을 때의 모양 그대로 보이게.
            _ghost = DiceBody.Build(_parent, _diceConfig.GetModel(dice.Faces), "PlacementGhost", out _ghostHalfHeight, _outline);
            _ghostFaces = dice.Faces;
        }
        // 놓일 자리보다 조금 띄워, 놓으면 떨어질 주사위라는 것이 보이게.
        float center = top + _ghostHalfHeight + _config.GhostHover;
        _ghost.transform.localPosition = new Vector3(cell.x, center, cell.y);
        RendererTint.Apply(_ghost, _diceConfig.GetColor(dice.Colors));
        _ghost.SetActive(true);
        return center + _ghostHalfHeight + _diceConfig.PipLift;
    }

    /// <summary>
    /// 칩 카드를 끄는 동안. 고스트 칩은 바닥에 놓일 모양 그대로다(카드 색). 주사위 칸이면 칩이 그 밑으로 들어가므로
    /// 주사위는 그대로 두고, 그 주사위가 이 칩을 받았을 때의 사거리를 보인다(사거리 칩이면 늘어난 고리).
    /// </summary>
    /// <param name="dice">칸에 놓인 주사위. 없으면 null.</param>
    public void ShowChip(Vector2Int cell, bool valid, SO_ChipCard card, DiceState? dice)
    {
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        ShowHighlight(cell, ground, valid);
        if (_ghost != null) _ghost.SetActive(false);
        if (!valid)
        {
            if (_ghostChip != null) _ghostChip.gameObject.SetActive(false);
            if (_ring != null) _ring.gameObject.SetActive(false);
            return;
        }

        if (_ghostChip == null) CreateGhostChip();
        RendererTint.Apply(_ghostChip.gameObject, card.IconColor);
        // 놓일 자리보다 조금 띄워, 놓으면 떨어질 칩이라는 것이 보이게(주사위 고스트와 같은 높이만큼).
        _ghostChip.transform.localPosition = new Vector3(cell.x, ground + _config.GhostHover, cell.y);
        _ghostChip.gameObject.SetActive(true);

        if (dice.HasValue) ShowRange(cell, ground + _viewConfig.ChipHeight, ChipEffects.RangeBonus(card), dice.Value);
        else if (_ring != null) _ring.gameObject.SetActive(false);
    }

    /// <summary>칸 대상 스킬(폴드)을 끄는 동안 — 칸 하이라이트만 보인다(없어질 주사위·칩은 그대로 보여 무엇이 사라지는지 알게).</summary>
    public void ShowCell(Vector2Int cell, bool valid)
    {
        ShowHighlight(cell, _viewConfig.GetTileTop(TileType.Ground), valid);
        if (_ghost != null) _ghost.SetActive(false);
        if (_ghostChip != null) _ghostChip.gameObject.SetActive(false);
        if (_ring != null) _ring.gameObject.SetActive(false);
    }

    private void ShowHighlight(Vector2Int cell, float top, bool valid)
    {
        if (_highlight == null)
        {
            _highlight = Object.Instantiate(_config.HighlightPrefab, _parent, false);
            float size = 1f - _viewConfig.Gap;
            _highlight.transform.localScale = new Vector3(size, 1f, size);
        }
        _highlight.transform.localPosition = new Vector3(cell.x, top, cell.y);
        RendererTint.Apply(_highlight, valid ? _config.ValidColor : _config.InvalidColor);
        _highlight.SetActive(true);
    }

    // 칩 모양은 실제 칩(ChipField)과 같은 설정으로 만든다. 설정이 바뀌면 Reset이 버리고 다음에 다시 만든다.
    private void CreateGhostChip()
    {
        GameObject go = new GameObject("PlacementGhostChip");
        go.transform.SetParent(_parent, false);
        float height = _viewConfig.ChipHeight;
        _ghostChipMesh = ChipMesh.CreateChip(_viewConfig.ChipDiameter, height, height * _viewConfig.ChipBevelRatio, _viewConfig.ChipSegments);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = _ghostChipMesh;
        _ghostChip = go.AddComponent<MeshRenderer>();
        _ghostChip.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ghostChip.sharedMaterial = MapView.GetTemplateMaterial(_viewConfig.BlockPrefab);
        OutlineMesh.AttachToOwned(filter, _outline);
    }

    // 사거리는 공격하는 쪽(DiceCombat)과 같은 계산을 지난다 — 미리보기와 실제 사거리가 어긋나지 않게.
    private void ShowRange(Vector2Int cell, float top, float bonus, in DiceState dice)
    {
        if (_config.RangeMaterial == null)
        {
            if (_ring != null) _ring.gameObject.SetActive(false);
            return;
        }
        if (_ring == null) CreateRing();

        DiceStats stats = DiceStats.Compute(dice, _diceConfig.GetDiceType(dice.Colors), _statsConfig);
        float range = stats.GetRange(bonus);
        // 설정은 그릴 때마다 넣는다 — 실행 중 고치면 바로 반영되게.
        _ring.sharedMaterial = _config.RangeMaterial;
        RingMesh.Fill(_ringMesh, range, _config.RangeWidth, Mathf.Max(_config.RangeSegments, 8), _config.RangeColor);
        _ring.transform.localPosition = new Vector3(cell.x, top + _config.RangeLift, cell.y);
        _ring.gameObject.SetActive(range > 0f);
    }

    // 바닥에 눕힌 고리 메시(RingMesh). 카메라를 향하는 선이면 비스듬한 시점에서 굵기가 들쭉날쭉하고, LineRenderer 고리는 한쪽에 틈이 보였다.
    private void CreateRing()
    {
        GameObject go = new GameObject("PlacementRange");
        go.transform.SetParent(_parent, false);
        _ringMesh = RingMesh.Create("PlacementRange");
        go.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
        _ring = go.AddComponent<MeshRenderer>();
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
    }

    public void Hide()
    {
        if (_ghost != null) _ghost.SetActive(false);
        if (_ghostChip != null) _ghostChip.gameObject.SetActive(false);
        if (_ring != null) _ring.gameObject.SetActive(false);
        if (_highlight != null) _highlight.SetActive(false);
    }

    /// <summary>만들어 둔 고스트를 버린다. 설정(모델·크기)이 바뀌면 다음 Show에서 새로 만든다.</summary>
    public void Reset()
    {
        if (_ghost != null) Object.Destroy(_ghost);
        if (_highlight != null) Object.Destroy(_highlight);
        if (_ring != null) Object.Destroy(_ring.gameObject);
        // 코드로 만든 메시는 오브젝트와 함께 지워지지 않는다.
        if (_ringMesh != null) Object.Destroy(_ringMesh);
        if (_ghostChip != null) Object.Destroy(_ghostChip.gameObject);
        if (_ghostChipMesh != null) Object.Destroy(_ghostChipMesh);
        _ghostChip = null;
        _ghostChipMesh = null;
        _ghost = null;
        _ringMesh = null;
        _highlight = null;
        _ring = null;
    }
}
