using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 로비 테이블 장식(기획서 12.2): 판 위에 주사위와 칩 더미를 흩어 둔다 — 빈 펠트만 보이면 무슨 게임인지 읽히지 않는다.
/// 표시만 한다. 판 시스템(DiceField·ChipField)을 쓰지 않는 이유: 그쪽은 칸을 막고 길을 다시 만드는 판 규칙이라, 장식이 끼면 판 상태가 섞인다.
/// 게임 시작이면 하나씩 작아지며 사라진다(뒤이어 판 주사위 연출이 판을 새로 채운다). 로비로 돌아올 때는 거꾸로 하나씩 톡톡 생긴다
/// (판 위 물건이 하늘로 날아가는 판 치우기와 박자를 맞춘다).
/// 자리는 판 결과와 상관없는 난수로 뽑는다(판 시드를 쓰지 않는다 — 장식이 판 재현에 끼지 않게). 실제 시간으로 진행한다.
/// </summary>
public sealed class LobbyDecor : IDisposable
{
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private sealed class Piece
    {
        public GameObject Body;
        public PaletteMesh Mesh;
        public Vector3 Scale;
        public float HideAt;
        public float ShowAt;
    }

    private readonly SO_LobbyConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly SO_DiceConfig _diceConfig;
    private readonly SO_LevelUpConfig _levelUpConfig;
    private readonly Material _outline;
    private readonly Transform _root;
    private readonly PaletteMeshBuilder _builder = new PaletteMeshBuilder();
    private readonly List<Piece> _pieces = new List<Piece>();
    private readonly List<Vector2> _spots = new List<Vector2>();
    private readonly List<Color> _colors = new List<Color>();
    private readonly System.Random _random = new System.Random();
    private Mesh _chipMesh;
    private Mesh _disc;
    // -1이면 보이는 중. 0 이상이면 사라지기 시작한 뒤 흐른 시간.
    private float _hideTime = -1f;
    // -1이면 다 나타났다. 0 이상이면 나타나기 시작한 뒤 흐른 시간.
    private float _showTime = -1f;

    public LobbyDecor(Transform parent, SO_LobbyConfig config, SO_MapViewConfig viewConfig, SO_DiceConfig diceConfig,
        SO_LevelUpConfig levelUpConfig, Material outline)
    {
        _config = config;
        _viewConfig = viewConfig;
        _diceConfig = diceConfig;
        _levelUpConfig = levelUpConfig;
        _outline = outline;
        _root = new GameObject("LobbyDecor").transform;
        _root.SetParent(parent, false);
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        Clear();
    }

    /// <summary>새 테이블에 장식을 흩어 둔다(이전 것은 지운다).</summary>
    /// <param name="pop">하나씩 톡톡 생기게(로비로 돌아올 때). 아니면 바로 놓는다(첫 화면·테이블 바꾸기).</param>
    public void Show(MapData map, bool pop = false)
    {
        Clear();
        _hideTime = -1f;
        _showTime = -1f;
        if (map == null) return;

        PickSpots(map, _config.DecorDiceCount + _config.DecorChipStacks);
        int spot = 0;
        for (int i = 0; i < _config.DecorDiceCount && spot < _spots.Count; i++) AddDie(_spots[spot++]);
        for (int i = 0; i < _config.DecorChipStacks && spot < _spots.Count; i++) AddChipStack(_spots[spot++]);
        if (!pop || _pieces.Count == 0) return;
        _showTime = 0f;
        for (int i = 0; i < _pieces.Count; i++)
        {
            _pieces[i].ShowAt = _config.DecorShowDelay + (float)_random.NextDouble() * _config.DecorShowSpread;
            if (_pieces[i].Body != null) _pieces[i].Body.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>게임 시작: 하나씩 작아지며 사라진다.</summary>
    public void Hide()
    {
        if (_hideTime >= 0f || _pieces.Count == 0) return;
        _hideTime = 0f;
        // 아직 생기는 중이면 멈추고 지금 크기에서 사라진다(두 연출이 같은 크기를 다투지 않게).
        _showTime = -1f;
        // 무작위 차례로 — 한쪽부터 쓸려 나가면 연출처럼 보여 판 주사위 연출과 겹친다.
        for (int i = 0; i < _pieces.Count; i++) _pieces[i].HideAt = (float)_random.NextDouble() * _config.DecorHideSpread;
    }

    private void Tick(float deltaTime)
    {
        if (_showTime >= 0f) TickShow(deltaTime);
        if (_hideTime < 0f) return;
        _hideTime += deltaTime;
        bool remaining = false;
        for (int i = 0; i < _pieces.Count; i++)
        {
            Piece piece = _pieces[i];
            if (piece.Body == null) continue;
            float u = _config.DecorHideDuration > 0f ? Mathf.Clamp01((_hideTime - piece.HideAt) / _config.DecorHideDuration) : 1f;
            if (u >= 1f)
            {
                piece.Body.SetActive(false);
                continue;
            }
            remaining = true;
            // 살짝 커졌다가 줄어든다(톡 하고 사라지는 느낌).
            float scale = u < 0.3f ? 1f + 0.15f * (u / 0.3f) : 1.15f * (1f - (u - 0.3f) / 0.7f);
            piece.Body.transform.localScale = piece.Scale * scale;
        }
        if (!remaining)
        {
            Clear();
            _hideTime = -1f;
        }
    }

    // 사라지기의 반대: 0에서 살짝 넘치게 커졌다가 제 크기로.
    private void TickShow(float deltaTime)
    {
        _showTime += deltaTime;
        bool remaining = false;
        for (int i = 0; i < _pieces.Count; i++)
        {
            Piece piece = _pieces[i];
            if (piece.Body == null) continue;
            float u = _config.DecorShowDuration > 0f ? Mathf.Clamp01((_showTime - piece.ShowAt) / _config.DecorShowDuration) : 1f;
            if (u < 1f) remaining = true;
            float scale = u < 0.7f ? 1.15f * (u / 0.7f) : 1.15f - 0.15f * ((u - 0.7f) / 0.3f);
            piece.Body.transform.localScale = piece.Scale * scale;
        }
        if (!remaining) _showTime = -1f;
    }

    // 펠트 안쪽(가장자리 여백을 뺀 곳)에서 서로 떨어진 자리를 뽑는다. 못 찾으면 그만큼 덜 놓는다(장식일 뿐이다).
    private void PickSpots(MapData map, int count)
    {
        _spots.Clear();
        float margin = _config.DecorEdgeMargin;
        float minX = -0.5f + margin, maxX = map.Width - 0.5f - margin;
        float minZ = -0.5f + margin, maxZ = map.Height - 0.5f - margin;
        if (maxX <= minX || maxZ <= minZ) return;

        for (int attempt = 0; attempt < count * 30 && _spots.Count < count; attempt++)
        {
            Vector2 spot = new Vector2(Mathf.Lerp(minX, maxX, (float)_random.NextDouble()), Mathf.Lerp(minZ, maxZ, (float)_random.NextDouble()));
            bool clear = true;
            for (int i = 0; i < _spots.Count && clear; i++)
                if (Vector2.Distance(_spots[i], spot) < _config.DecorMinSpacing) clear = false;
            if (clear) _spots.Add(spot);
        }
    }

    private void AddDie(Vector2 spot)
    {
        if (_diceConfig.Models.Count == 0) return;
        SO_DiceConfig.FaceModel model = _diceConfig.GetModel(_diceConfig.Models[_random.Next(_diceConfig.Models.Count)].Faces);
        GameObject body = DiceBody.Build(_root, model, "DecorDie", out float halfHeight, _outline);
        RendererTint.Apply(body, PickDiceColor());
        Transform t = body.transform;
        t.localScale *= _config.DecorScale;
        t.localPosition = new Vector3(spot.x, Floor + halfHeight * _config.DecorScale, spot.y);
        t.localRotation = Quaternion.Euler(0f, (float)_random.NextDouble() * 360f, 0f) * t.localRotation;
        _pieces.Add(new Piece { Body = body, Scale = t.localScale });
    }

    // 칩 더미: 같은 색 칩을 몇 장 쌓고 한 장씩 조금 어긋나게(카지노 칩 더미처럼).
    private void AddChipStack(Vector2 spot)
    {
        EnsureChipMeshes();
        Color color = PickChipColor();
        int count = _random.Next(1, Mathf.Max(1, _config.DecorChipStackMax) + 1);
        float height = _viewConfig.ChipHeight;
        float outer = _viewConfig.ChipDiameter * _viewConfig.ChipRingOuter;
        float inner = _viewConfig.ChipDiameter * _viewConfig.ChipRingInner;
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(Jitter(), height * i, Jitter());
            Vector3 top = offset + Vector3.up * height;
            _builder.Add(_chipMesh, Matrix4x4.Translate(offset), color);
            _builder.Add(_disc, Matrix4x4.TRS(top + Vector3.up * _viewConfig.PrintLift, Quaternion.identity, new Vector3(outer, 1f, outer)), _viewConfig.ChipRingColor);
            _builder.Add(_disc, Matrix4x4.TRS(top + Vector3.up * _viewConfig.PrintLift * 2f, Quaternion.identity, new Vector3(inner, 1f, inner)), color);
        }

        PaletteMesh mesh = _builder.Build("DecorChips", _root, MapView.GetTemplateMaterial(_viewConfig.BlockPrefab), ShadowCastingMode.On);
        mesh.Material.SetFloat(SmoothnessId, _viewConfig.ChipSmoothness);
        Transform t = mesh.GameObject.transform;
        t.localPosition = new Vector3(spot.x, Floor, spot.y);
        t.localScale = Vector3.one * _config.DecorScale;
        OutlineMesh.AttachToOwned(mesh.GameObject.GetComponent<MeshFilter>(), _outline);
        _pieces.Add(new Piece { Body = mesh.GameObject, Mesh = mesh, Scale = t.localScale });
    }

    private float Floor => _viewConfig.GetTileTop(TileType.Ground);

    private float Jitter() => ((float)_random.NextDouble() - 0.5f) * 2f * _config.DecorChipJitter;

    private Color PickDiceColor()
    {
        _colors.Clear();
        for (int i = 0; i < _diceConfig.Types.Count; i++)
            if (_diceConfig.Types[i] != null) _colors.Add(_diceConfig.Types[i].Color);
        return _colors.Count > 0 ? _colors[_random.Next(_colors.Count)] : Color.gray;
    }

    private Color PickChipColor()
    {
        _colors.Clear();
        for (int i = 0; i < _levelUpConfig.ChipCards.Count; i++)
            if (_levelUpConfig.ChipCards[i] != null) _colors.Add(_levelUpConfig.ChipCards[i].IconColor);
        return _colors.Count > 0 ? _colors[_random.Next(_colors.Count)] : Color.gray;
    }

    // 칩 모양 설정은 판 칩(ChipField)과 같은 값을 쓴다. 장식을 다시 만들 때마다 지금 설정으로 새로 만든다.
    private void EnsureChipMeshes()
    {
        if (_chipMesh != null) return;
        float height = _viewConfig.ChipHeight;
        _chipMesh = ChipMesh.CreateChip(_viewConfig.ChipDiameter, height, height * _viewConfig.ChipBevelRatio, _viewConfig.ChipSegments);
        _disc = ChipMesh.CreateDisc(1f, _viewConfig.ChipSegments);
    }

    private void Clear()
    {
        for (int i = 0; i < _pieces.Count; i++)
        {
            if (_pieces[i].Mesh != null) _pieces[i].Mesh.Dispose();
            else if (_pieces[i].Body != null) Object.Destroy(_pieces[i].Body);
        }
        _pieces.Clear();
        if (_chipMesh != null) Object.Destroy(_chipMesh);
        if (_disc != null) Object.Destroy(_disc);
        _chipMesh = null;
        _disc = null;
    }
}
