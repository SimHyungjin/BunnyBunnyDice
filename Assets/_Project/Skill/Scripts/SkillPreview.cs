using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범위·판 전체 스킬 카드를 끄는 동안의 표시(기획서 5.3). 주사위 대상 스킬은 주사위 고스트(PlacementPreview)를 쓴다.
/// 범위: 손가락 아래 원(맵 위면 초록 — 대상이 없어도 쓸 수 있다) + 지금 닿을 적과 모음점 밑의 작은 고리(카드 색).
/// 판 전체: 영향을 받을 주사위 칸마다 칸 하이라이트(주사위가 없으면 하이라이트 없이 버려진다).
/// 적은 끄는 동안에도 걸어 다니므로 손가락이 멈춰 있어도 매 프레임 다시 모은다 — 놓는 순간의 판정(SkillCaster.CanCast)과 같은 함수를 지난다.
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
    private readonly List<int> _targets = new List<int>();
    private readonly List<Ring> _marks = new List<Ring>();
    private readonly List<GameObject> _highlights = new List<GameObject>();
    private Ring _area;
    private SO_AreaSkillCard _areaCard;
    private Vector2 _center;

    public SkillPreview(Transform parent, SO_PlacementPreviewConfig config, SO_MapViewConfig viewConfig, SkillCaster caster,
        MonsterField monsters, DiceField diceField)
    {
        _config = config;
        _viewConfig = viewConfig;
        _caster = caster;
        _monsters = monsters;
        _diceField = diceField;
        _root = new GameObject("SkillPreview").transform;
        _root.SetParent(parent, false);
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnLateUpdate -= LateTick;
        Reset();
    }

    /// <summary>범위 스킬 원을 center(맵 로컬 x, z)에 띄운다. 숨길 때까지 매 프레임 다시 그린다.</summary>
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
        // 주사위 칸을 밝히는 것은 주사위를 바꾸는 올인만 — 체크는 적에게 거는 것이라 밝힐 칸이 없다.
        if (!(card is SO_SkillCard_AllIn)) return;
        IReadOnlyList<PlacedDice> placed = _diceField.Placed;
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        for (int i = 0; i < placed.Count; i++)
        {
            GameObject highlight = GetHighlight(i);
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
        if (_area != null) DestroyRing(_area);
        _area = null;
        for (int i = 0; i < _marks.Count; i++) DestroyRing(_marks[i]);
        _marks.Clear();
        for (int i = 0; i < _highlights.Count; i++) UnityEngine.Object.Destroy(_highlights[i]);
        _highlights.Clear();
    }

    // 끄는 동안 적이 움직이므로 원 안의 대상을 매 프레임 다시 모은다.
    private void LateTick(float _)
    {
        if (_areaCard != null) DrawArea();
    }

    private void DrawArea()
    {
        if (_config.RangeMaterial == null) return;
        float y = _viewConfig.GetTileTop(TileType.Ground) + _config.RangeLift;
        bool valid = _caster.CanCast(_areaCard, _center);

        if (_area == null) _area = CreateRing("SkillArea");
        FillRing(_area, _center, y, _areaCard.Radius, _config.SkillAreaWidth, valid ? _config.ValidColor : _config.InvalidColor);

        _caster.CollectTargets(_areaCard, _center, _targets);
        int used = 0;
        Color mark = _areaCard.IconColor;
        if (valid && _caster.TryGetGatherPoint(_areaCard, _center, _targets, out Vector2 gather))
            FillRing(GetMark(used++), gather, y, _config.SkillMarkRadius, _config.SkillMarkWidth, mark);
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _targets.Count; i++)
            FillRing(GetMark(used++), states[_targets[i]].Position, y, _config.SkillMarkRadius, _config.SkillMarkWidth, mark);
        int king = _caster.FindKing(_areaCard, _targets);
        if (king >= 0) FillRing(GetMark(used++), states[king].Position, y, _config.SkillKingMarkRadius, _config.SkillMarkWidth, mark);
        for (int i = used; i < _marks.Count; i++) _marks[i].Object.SetActive(false);
    }

    private void HideArea()
    {
        _areaCard = null;
        if (_area != null) _area.Object.SetActive(false);
        for (int i = 0; i < _marks.Count; i++) _marks[i].Object.SetActive(false);
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
