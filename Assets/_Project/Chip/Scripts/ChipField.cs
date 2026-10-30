using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 칩 층(기획서 7 — 칩 = 칩 카드). 칩은 칸 위에 따로 놓이는 물건이고 몬스터 길을 막는다.
/// 한 칸에는 칩만, 주사위만, 칩 위 주사위가 올 수 있고, 칩 위 주사위가 그 칩의 효과를 받는다.
/// 판 시작 칩은 맵 생성이 자리를 정하고(MapGenerator, 판 주사위 눈의 합) 여기서 종류를 칩 카드 가중치로 뽑는다.
/// 판 중에는 칩 카드를 빈 칸이나 칩 없는 주사위 칸에 놓으면 칩이 생긴다.
/// 맵은 길을 막는지만 알고(MapData.HasChip), 어떤 강화인지와 표시는 여기서 든다 — 주사위(DiceField)와 같은 나눔.
/// 칩은 한 장이고 색이 곧 강화 종류다(카드 IconColor — 카드와 칩이 같은 색이라 이어진다). 윗면에 카지노 칩처럼 링을 인쇄한다.
/// 사이드 팟(칩 리더 전용)으로 칩 칸에 다른 종류 한 장을 더 얹을 수 있다(한 칸 두 장까지). 두 장이면 칩 두 장을 실제로 쌓고(맵이 장수를 든다 — MapData.ChipLayers)
/// 그 위 주사위도 그만큼 올라앉는다 — 처음엔 높이를 그대로 두려고 반 두께 둘로 쌓았는데 아래 칩이 거의 안 보여 색이 바뀐 것처럼 보였다(2026-10-31 사용자).
/// 효과는 ChipSet으로 읽는다(두 장 + 세기 — IChipScale).
/// </summary>
public sealed class ChipField
{
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    /// <summary>한 칸에 쌓을 수 있는 칩 장수(사이드 팟 규칙 — 조정 값이 아니라 규칙이다).</summary>
    public const int MaxLayers = 2;

    private struct Chip
    {
        public Vector2Int Cell;
        public SO_ChipCard Card;
        // 사이드 팟으로 얹은 두 번째 칩. 없으면 null.
        public SO_ChipCard Extra;
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
    private readonly TraitHooks _traits;

    /// <summary>판 중에 칩이 놓였다. 빈 칸이면 길이 막혔을 수 있고, 주사위 칸이면 주사위가 칩 위로 올라앉는다.</summary>
    public event Action<Vector2Int> ChipPlaced;

    /// <summary>칩 칸에 한 장이 더 얹혔다(사이드 팟). 길은 그대로고 그 위 주사위가 한 장만큼 올라앉는다.</summary>
    public event Action<Vector2Int> ChipStacked;

    /// <summary>칩이 없어졌다(캐시 아웃). 빈 칸의 칩은 벽이었으므로 길이 바뀐다.</summary>
    public event Action<Vector2Int> ChipRemoved;

    /// <summary>칩 표시들(피벗은 칸 바닥 가운데). 시작 칩이 왼쪽 열부터 앞에 온다. 판 주사위 연출이 숨겼다가 하나씩 떨어뜨린다.</summary>
    public IReadOnlyList<Transform> Views => _views;

    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.OutlineMaterial). 없으면 null.</param>
    /// <param name="traits">칩 세기 갈고리(IChipScale)를 묻는다. 없으면 null.</param>
    public ChipField(Transform parent, SO_MapViewConfig config, Material outline, TraitHooks traits)
    {
        _traits = traits;
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

    /// <summary>지금 칩 효과의 세기(특성이 바꾸면 그 값, 아니면 1).</summary>
    public float Strength
    {
        get
        {
            IChipScale scale = _traits?.Find<IChipScale>();
            return scale != null ? scale.ChipStrength : 1f;
        }
    }

    /// <summary>cell의 칩 묶음(두 장까지 + 세기). 칩이 없으면 비어 있다.</summary>
    public ChipSet GetSet(Vector2Int cell)
    {
        int index = IndexOf(cell);
        return index >= 0 ? new ChipSet(_chips[index].Card, _chips[index].Extra, Strength) : new ChipSet(null, null, Strength);
    }

    /// <summary>칩 카드 한 장(아직 놓지 않은 카드 — 끄는 동안의 미리보기·봇 판단)의 효과를 지금 세기로.</summary>
    public ChipSet Preview(SO_ChipCard card) => new ChipSet(card, null, Strength);

    /// <summary>cell의 칩에 card를 얹었을 때의 묶음(사이드 팟을 끄는 동안의 사거리 미리보기).</summary>
    public ChipSet PreviewExtra(Vector2Int cell, SO_ChipCard card)
    {
        int index = IndexOf(cell);
        return index >= 0 ? new ChipSet(_chips[index].Card, card, Strength) : Preview(card);
    }

    /// <summary>cell에 card를 두 번째 칩으로 얹을 수 있는지 — 칩이 한 장 있고 그 칩과 종류가 다르다.</summary>
    public bool CanAddExtra(Vector2Int cell, SO_ChipCard card)
    {
        int index = IndexOf(cell);
        return index >= 0 && card != null && _chips[index].Extra == null && !SameKind(_chips[index].Card, card);
    }

    // 종류(클래스 — 칩 종류마다 하나)로 비교한다. 판의 칩과 카드의 칩이 같은 에셋이어도 실행 중 복사본(ConfigTuning)은 따로 만들어져
    // 물건으로 비교하면 다르다고 나왔다 — 같은 색 칩이 얹혔다(2026-10-31 사용자).
    private static bool SameKind(SO_ChipCard a, SO_ChipCard b) => a != null && b != null && a.GetType() == b.GetType();

    /// <summary>판정(CanAddExtra)을 통과한 칸에만 부른다. 칩 칸은 이미 벽이라 길은 바뀌지 않는다.</summary>
    public void AddExtra(Vector2Int cell, SO_ChipCard card)
    {
        int index = IndexOf(cell);
        if (index < 0) return;
        Chip chip = _chips[index];
        chip.Extra = card;
        _chips[index] = chip;
        _map?.SetChipLayers(cell.x, cell.y, MaxLayers);
        if (chip.View != null)
        {
            _views.Remove(chip.View.GameObject.transform);
            chip.View.Dispose();
            BuildView(index);
        }
        ChipStacked?.Invoke(cell);
    }

    private int IndexOf(Vector2Int cell)
    {
        for (int i = 0; i < _chips.Count; i++)
        {
            if (_chips[i].Cell == cell) return i;
        }
        return -1;
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

    /// <summary>cell의 칩을 없앤다(캐시 아웃 — 두 장이면 둘 다). 칩이 없으면 false.</summary>
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
    // 두 장이면 칩 두 장을 쌓는다(아래 = 처음 칩, 위 = 얹은 칩). 윗면 링은 맨 위 칩에 인쇄한다.
    private void BuildView(int index)
    {
        Chip chip = _chips[index];
        Color color = chip.Card != null ? chip.Card.IconColor : Color.gray;
        float height = _config.ChipHeight;
        float outer = _config.ChipDiameter * _config.ChipRingOuter;
        float inner = _config.ChipDiameter * _config.ChipRingInner;
        _builder.Add(_chipMesh, Matrix4x4.identity, color);
        if (chip.Extra != null)
        {
            color = chip.Extra.IconColor;
            _builder.Add(_chipMesh, Matrix4x4.Translate(Vector3.up * height), color);
        }
        Vector3 top = Vector3.up * height * (chip.Extra != null ? MaxLayers : 1);
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
