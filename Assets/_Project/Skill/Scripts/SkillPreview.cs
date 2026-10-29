using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 대상·판 전체 스킬 카드를 끄는 동안의 표시(기획서 5.3). 주사위 대상 스킬은 주사위 고스트(PlacementPreview)를 쓴다.
/// 적 대상: 판 전체를 연한 초록으로 깐다(모든 적이 대상 — 손가락이 판 밖이면 깔지 않는다) + 레이크 모음점·체크메이트 왕 밑 고리(카드 색).
/// 적마다 고리를 그리지 않는다 — 판 위 모든 적이 대상이라(2026-10-29) 판 색이 그 뜻을 대신하고, 수백 개를 매 프레임 다시 그리게 된다.
/// 처음엔 손가락 밑 고리였는데 "그 자리에 쓰는" 범위 스킬처럼 보였다(2026-10-29 사용자).
/// 판 전체: 영향을 받을 주사위 칸마다 칸 하이라이트(주사위가 없으면 하이라이트 없이 버려진다).
/// 왕은 체력이 바뀌며 달라지므로 손가락이 멈춰 있어도 매 프레임 다시 고른다 — 놓는 순간의 판정(SkillCaster)과 같은 함수를 지난다.
/// </summary>
public sealed class SkillPreview : IDisposable
{
    // 고리 하나. 메시는 코드로 만든 것이라 오브젝트와 함께 지워지지 않으므로 따로 지운다.
    private sealed class Ring
    {
        public GameObject Object;
        public MeshRenderer Renderer;
        public Mesh Mesh;
    }

    private readonly Transform _root;
    private readonly SO_PlacementPreviewConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly SkillCaster _caster;
    private readonly MonsterField _monsters;
    private readonly DiceField _diceField;
    private readonly Func<MapData> _getMap;
    private readonly List<int> _targets = new List<int>();
    private readonly List<Ring> _marks = new List<Ring>();
    private readonly List<GameObject> _highlights = new List<GameObject>();
    private Ring _board;
    // 판 색 메시 버퍼(할당 없이 다시 채운다).
    private readonly Vector3[] _boardVertices = new Vector3[4];
    private readonly Color[] _boardColors = new Color[4];
    private static readonly int[] BoardTriangles = { 0, 2, 1, 1, 2, 3 };
    private SO_AreaSkillCard _areaCard;
    private Vector2 _center;

    public SkillPreview(Transform parent, SO_PlacementPreviewConfig config, SO_MapViewConfig viewConfig, SkillCaster caster,
        MonsterField monsters, DiceField diceField, Func<MapData> getMap)
    {
        _config = config;
        _viewConfig = viewConfig;
        _caster = caster;
        _monsters = monsters;
        _diceField = diceField;
        _getMap = getMap;
        _root = new GameObject("SkillPreview").transform;
        _root.SetParent(parent, false);
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnLateUpdate -= LateTick;
        Reset();
    }

    /// <summary>적 대상 스킬 표시를 띄운다. center(맵 로컬 x, z)는 손가락 자리 — 판 위인지와 레이크 모음점을 정한다. 숨길 때까지 매 프레임 다시 그린다.</summary>
    public void ShowArea(SO_AreaSkillCard card, Vector2 center)
    {
        HideHighlights();
        _areaCard = card;
        _center = center;
        DrawArea();
    }

    /// <summary>판 전체 스킬: 영향을 받을 주사위 칸을 모두 밝힌다.</summary>
    public void ShowBoard(SO_SkillCard card)
    {
        HideArea();
        HideHighlights();
        // 주사위를 바꾸는 카드만 칸을 밝힌다(올인·세븐스 헤븐 = 모든 주사위, 리스핀 = 눈이 1인 주사위). 영웅·경험치에 거는 카드는 밝힐 칸이 없다.
        bool all = card is SO_SkillCard_AllIn || card is SO_SkillCard_SevensHeaven;
        bool lowest = card is SO_SkillCard_Respin;
        if (!all && !lowest) return;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        int used = 0;
        for (int i = 0; i < placed.Count; i++)
        {
            if (lowest && placed[i].Dice.Pips != 1) continue;
            GameObject highlight = GetHighlight(used++);
            highlight.transform.localPosition = new Vector3(placed[i].Cell.x, ground, placed[i].Cell.y);
            RendererTint.Apply(highlight, _config.ValidColor);
            highlight.SetActive(true);
        }
    }

    public void Hide()
    {
        HideArea();
        HideHighlights();
    }

    /// <summary>만들어 둔 표시를 버린다. 설정(칸 하이라이트 프리팹 등)이 바뀌면 다음에 새로 만든다.</summary>
    public void Reset()
    {
        _areaCard = null;
        if (_board != null) DestroyRing(_board);
        _board = null;
        for (int i = 0; i < _marks.Count; i++) DestroyRing(_marks[i]);
        _marks.Clear();
        for (int i = 0; i < _highlights.Count; i++) UnityEngine.Object.Destroy(_highlights[i]);
        _highlights.Clear();
    }

    // 끄는 동안 적이 움직이므로 왕을 매 프레임 다시 고른다.
    private void LateTick(float _)
    {
        if (_areaCard != null) DrawArea();
    }

    private void DrawArea()
    {
        if (_config.RangeMaterial == null) return;
        float y = _viewConfig.GetTileTop(TileType.Ground) + _config.RangeLift;
        MapData map = _getMap();
        bool valid = map != null && _caster.CanCast(_areaCard, _center);
        if (!valid)
        {
            HideMarks();
            return;
        }

        if (_board == null) _board = CreateRing("SkillBoard");
        FillBoard(_board, map, y, _config.SkillBoardColor);

        int used = 0;
        Color mark = _areaCard.IconColor;
        if (_caster.TryGetGatherPoint(_areaCard, _center, out Vector2 gather))
            FillRing(GetMark(used++), gather, y, _config.SkillGatherMarkRadius, _config.SkillMarkWidth, mark);
        if (_areaCard is SO_SkillCard_Checkmate)
        {
            _caster.CollectTargets(_areaCard, _targets);
            int king = _caster.FindKing(_areaCard, _targets);
            if (king >= 0) FillRing(GetMark(used++), _monsters.States[king].Position, y, _config.SkillKingMarkRadius, _config.SkillMarkWidth, mark);
        }
        for (int i = used; i < _marks.Count; i++) _marks[i].Object.SetActive(false);
    }

    private void HideArea()
    {
        _areaCard = null;
        HideMarks();
    }

    private void HideMarks()
    {
        if (_board != null) _board.Object.SetActive(false);
        for (int i = 0; i < _marks.Count; i++) _marks[i].Object.SetActive(false);
    }

    // 판 전체를 덮는 납작한 사각형(정점 색). 칸 중심이 정수 좌표라 판은 −0.5 ~ 크기 − 0.5다.
    private void FillBoard(Ring ring, MapData map, float y, Color color)
    {
        float right = map.Width - 0.5f;
        float top = map.Height - 0.5f;
        _boardVertices[0] = new Vector3(-0.5f, 0f, -0.5f);
        _boardVertices[1] = new Vector3(right, 0f, -0.5f);
        _boardVertices[2] = new Vector3(-0.5f, 0f, top);
        _boardVertices[3] = new Vector3(right, 0f, top);
        for (int i = 0; i < _boardColors.Length; i++) _boardColors[i] = color;
        ring.Renderer.sharedMaterial = _config.RangeMaterial;
        ring.Mesh.Clear();
        ring.Mesh.vertices = _boardVertices;
        ring.Mesh.colors = _boardColors;
        ring.Mesh.triangles = BoardTriangles;
        ring.Mesh.RecalculateBounds();
        ring.Object.transform.localPosition = new Vector3(0f, y, 0f);
        ring.Object.SetActive(true);
    }

    private void HideHighlights()
    {
        for (int i = 0; i < _highlights.Count; i++) _highlights[i].SetActive(false);
    }

    private void FillRing(Ring ring, Vector2 center, float y, float radius, float width, Color color)
    {
        // 재질·굵기는 그릴 때마다 넣는다 — 실행 중 설정을 고치면 바로 반영되게.
        ring.Renderer.sharedMaterial = _config.RangeMaterial;
        RingMesh.Fill(ring.Mesh, radius, width, Mathf.Max(_config.RangeSegments, 8), color);
        ring.Object.transform.localPosition = new Vector3(center.x, y, center.y);
        ring.Object.SetActive(true);
    }

    private Ring GetMark(int index)
    {
        while (_marks.Count <= index) _marks.Add(CreateRing("SkillMark"));
        return _marks[index];
    }

    private GameObject GetHighlight(int index)
    {
        while (_highlights.Count <= index)
        {
            GameObject highlight = UnityEngine.Object.Instantiate(_config.HighlightPrefab, _root, false);
            float size = 1f - _viewConfig.Gap;
            highlight.transform.localScale = new Vector3(size, 1f, size);
            _highlights.Add(highlight);
        }
        return _highlights[index];
    }

    // 바닥에 눕힌 고리 메시(RingMesh) — 사거리 고리와 같은 방식.
    private Ring CreateRing(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_root, false);
        Ring ring = new Ring { Object = go, Mesh = RingMesh.Create(name) };
        go.AddComponent<MeshFilter>().sharedMesh = ring.Mesh;
        ring.Renderer = go.AddComponent<MeshRenderer>();
        ring.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.Renderer.receiveShadows = false;
        return ring;
    }

    private static void DestroyRing(Ring ring)
    {
        UnityEngine.Object.Destroy(ring.Object);
        UnityEngine.Object.Destroy(ring.Mesh);
    }
}
