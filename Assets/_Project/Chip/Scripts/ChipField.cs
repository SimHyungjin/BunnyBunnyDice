using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 칩 층(기획서 9.7 — 칩 = 칩 카드). 칩은 칸 위에 따로 놓이는 물건이고 몬스터 길을 막는다.
/// 한 칸에는 칩만, 주사위만, 칩 위 주사위가 올 수 있고, 칩 위 주사위가 그 칩의 효과를 받는다.
/// 판 시작 칩은 맵 생성이 자리를 정하고(MapGenerator, 판 주사위 눈의 합) 여기서 종류를 칩 카드 가중치로 뽑는다.
/// 판 중에는 칩 카드를 빈 칸이나 칩 없는 주사위 칸에 놓으면 칩이 생긴다.
/// 맵은 길을 막는지만 알고(MapData.HasChip), 어떤 강화인지와 표시는 여기서 든다 — 주사위(DiceField)와 같은 나눔.
/// 칩은 한 장이고 색이 곧 강화 종류다(카드 IconColor — 카드와 칩이 같은 색이라 이어진다). 윗면에 카지노 칩처럼 링을 인쇄한다.
/// </summary>
public sealed class ChipField
{
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private struct Chip
    {
        public Vector2Int Cell;
        public SO_ChipCard Card;
        public PaletteMesh View;
    }

    private readonly SO_MapViewConfig _config;
    private readonly Transform _root;
    private readonly Material _outline;
    private readonly PaletteMeshBuilder _builder = new PaletteMeshBuilder();
    // 시작 칩은 왼쪽 열부터 — 판 주사위 연출이 이 순서로 떨어뜨려 왼쪽에서 오른쪽으로 쓸고 지나간다. 판 중에 놓인 칩은 뒤에 붙는다.
    private readonly List<Chip> _chips = new List<Chip>();
    private readonly List<Transform> _views = new List<Transform>();
    private readonly List<SO_ChipCard> _pool = new List<SO_ChipCard>();
    private Mesh _chipMesh;
    private Mesh _disc;
    private MapData _map;

    /// <summary>판 중에 칩이 놓였다. 빈 칸이면 길이 막혔을 수 있고, 주사위 칸이면 주사위가 칩 위로 올라앉는다.</summary>
    public event Action<Vector2Int> ChipPlaced;

    /// <summary>칩이 없어졌다(폴드). 빈 칸의 칩은 벽이었으므로 길이 바뀐다.</summary>
    public event Action<Vector2Int> ChipRemoved;

    /// <summary>칩 표시들(피벗은 칸 바닥 가운데). 시작 칩이 왼쪽 열부터 앞에 온다. 판 주사위 연출이 숨겼다가 하나씩 떨어뜨린다.</summary>
    public IReadOnlyList<Transform> Views => _views;

    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.OutlineMaterial). 없으면 null.</param>
    public ChipField(Transform parent, SO_MapViewConfig config, Material outline)
    {
        _config = config;
        _outline = outline;
        _root = new GameObject("Chips").transform;
        _root.SetParent(parent, false);
    }

    /// <summary>cell에 놓인 칩의 강화. 칩이 없으면 null.</summary>
    public SO_ChipCard Get(Vector2Int cell)
    {
        // 칩은 칸 수(최대 120)만큼만 있어 그냥 훑는다.
        for (int i = 0; i < _chips.Count; i++)
        {
            if (_chips[i].Cell == cell) return _chips[i].Card;
        }
        return null;
    }

    /// <summary>
    /// 새 판: 맵의 칩 칸마다 종류를 뽑는다. 카드마다의 가중치(Weight)로 — 레벨업 출현과 같은 희귀도.
    /// 같은 시드면 같은 칩이 나온다(맵 생성과 같은 이유로 System.Random). 표시는 RebuildViews가 만든다(맵 표시와 함께).
    /// </summary>
    public void Build(MapData map, IReadOnlyList<SO_ChipCard> candidates, int seed)
    {
        Clear();
        _map = map;
        _pool.Clear();
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            SO_ChipCard card = candidates[i];
            if (card == null || card.Weight <= 0f) continue;
            _pool.Add(card);
            total += card.Weight;
        }

        System.Random random = new System.Random(seed);
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                if (!map.HasChip(x, y)) continue;
                _chips.Add(new Chip { Cell = new Vector2Int(x, y), Card = Pick(random, total) });
            }
        }
    }

    /// <summary>판정(DicePlacement.CheckChip)을 통과한 칸에만 부른다.</summary>
    public void Place(Vector2Int cell, SO_ChipCard card)
    {
        if (_map == null) return;
        _map.SetChip(cell.x, cell.y, true);
        _chips.Add(new Chip { Cell = cell, Card = card });
        BuildView(_chips.Count - 1);
        ChipPlaced?.Invoke(cell);
    }

    /// <summary>cell의 칩을 없앤다(폴드). 칩이 없으면 false.</summary>
    public bool Remove(Vector2Int cell)
    {
        for (int i = 0; i < _chips.Count; i++)
        {
            if (_chips[i].Cell != cell) continue;
            PaletteMesh view = _chips[i].View;
            if (view != null)
            {
                _views.Remove(view.GameObject.transform);
                view.Dispose();
            }
            _chips.RemoveAt(i);
            _map?.SetChip(cell.x, cell.y, false);
            ChipRemoved?.Invoke(cell);
            return true;
        }
        return false;
    }

    /// <summary>칩은 그대로 두고 표시만 다시 만든다(칩 모양 설정이 바뀌었을 때).</summary>
    public void RebuildViews()
    {
        ClearViews();
        if (_chipMesh != null) UnityEngine.Object.Destroy(_chipMesh);
        if (_disc != null) UnityEngine.Object.Destroy(_disc);
        float height = _config.ChipHeight;
        _chipMesh = ChipMesh.CreateChip(_config.ChipDiameter, height, height * _config.ChipBevelRatio, _config.ChipSegments);
        _disc = ChipMesh.CreateDisc(1f, _config.ChipSegments);
        for (int i = 0; i < _chips.Count; i++) BuildView(i);
    }

    /// <summary>표시만 판 치우기에 넘긴다(칩은 그대로 — 곧 새 판이 온다).</summary>
    public void DetachViews(BoardSweep sweep)
    {
        for (int i = 0; i < _chips.Count; i++)
        {
            Chip chip = _chips[i];
            PaletteMesh view = chip.View;
            if (view == null) continue;
            // 판 주사위 연출 전이라 숨겨 둔 칩(로비 테이블)은 날릴 것이 없다.
            if (view.GameObject.activeSelf) sweep.Add(view.GameObject.transform, view.Dispose);
            else view.Dispose();
            chip.View = null;
            _chips[i] = chip;
        }
        _views.Clear();
    }

    public void Clear()
    {
        ClearViews();
        _chips.Clear();
        _map = null;
    }

    // 무게대로 고른다. Random은 1을 포함하지 않지만 실수 오차로 끝까지 못 고르면 마지막 것을 쓴다.
    private SO_ChipCard Pick(System.Random random, float total)
    {
        if (_pool.Count == 0) return null;
        double roll = random.NextDouble() * total;
        for (int i = 0; i < _pool.Count; i++)
        {
            if (roll < _pool[i].Weight) return _pool[i];
            roll -= _pool[i].Weight;
        }
        return _pool[_pool.Count - 1];
    }

    // 칩 한 장 + 윗면 링(링 색 원판 위에 칩 색 원판, 겹쳐 깜빡이지 않게 조금씩 띄운다). 판 위로 그림자를 드리워야 입체감이 난다.
    private void BuildView(int index)
    {
        Chip chip = _chips[index];
        Color color = chip.Card != null ? chip.Card.IconColor : Color.gray;
        float height = _config.ChipHeight;
        float outer = _config.ChipDiameter * _config.ChipRingOuter;
        float inner = _config.ChipDiameter * _config.ChipRingInner;
        Vector3 top = Vector3.up * height;
        _builder.Add(_chipMesh, Matrix4x4.identity, color);
        _builder.Add(_disc, Matrix4x4.TRS(top + Vector3.up * _config.PrintLift, Quaternion.identity, new Vector3(outer, 1f, outer)), _config.ChipRingColor);
        _builder.Add(_disc, Matrix4x4.TRS(top + Vector3.up * _config.PrintLift * 2f, Quaternion.identity, new Vector3(inner, 1f, inner)), color);

        PaletteMesh view = _builder.Build($"Chip_{chip.Cell.x}_{chip.Cell.y}", _root, MapView.GetTemplateMaterial(_config.BlockPrefab),
            ShadowCastingMode.On);
        view.Material.SetFloat(SmoothnessId, _config.ChipSmoothness);
        view.GameObject.transform.localPosition = new Vector3(chip.Cell.x, 0f, chip.Cell.y);
        // 칩과 그 위 주사위가 같은 색이어도 둘 사이에 선이 그어져 따로 읽힌다.
        OutlineMesh.AttachToOwned(view.GameObject.GetComponent<MeshFilter>(), _outline);
        chip.View = view;
        _chips[index] = chip;
        _views.Add(view.GameObject.transform);
    }

    private void ClearViews()
    {
        for (int i = 0; i < _chips.Count; i++)
        {
            Chip chip = _chips[i];
            chip.View?.Dispose();
            chip.View = null;
            _chips[i] = chip;
        }
        _views.Clear();
    }
}
