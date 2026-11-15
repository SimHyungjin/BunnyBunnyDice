using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 규칙 책상 표시: 당구대 오른쪽(맵 +X)에 세로를 당구대에 맞춰 붙여 놓는다. 모델(DeskPrefab)이 없으면 상자를 쌓아 만든다 — 상판·펠트·다리는
/// 메시 하나(PaletteMeshBuilder), 둘레 네온 띠는 발광 머티리얼이라 따로 하나.
///
/// 오른쪽 = 맵 +X: 가로 화면에서 카메라는 맵을 돌리지 않고 보므로(MapCameraFitter) 화면 오른쪽이 된다.
/// 당구대 크기를 따라 붙어야 하므로 당구대를 놓은 뒤(EnvironmentView.Build) 그 경계를 받아 만든다.
/// </summary>
public sealed class RulesTableView : IDisposable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly SO_RulesTableConfig _config;
    private readonly RulesScreenView _screen;
    private readonly Transform _root;
    private readonly PaletteMeshBuilder _builder = new PaletteMeshBuilder();
    private GameObject _desk;
    private PaletteMesh _body;
    private PaletteMesh _neon;
    private Vector2 _size;

    /// <summary>상판 윗면 가운데(규칙 화면을 놓는 기준). 맵 루트 아래라 맵을 따라 움직인다.</summary>
    public Transform Surface { get; }

    /// <summary>책상이 차지하는 상자(맵 루트 기준) — 카메라가 책상을 비출 때 쓴다.</summary>
    public Bounds DeskBounds { get; private set; }

    /// <summary>책상을 놓았다(맵을 처음 만들기 전에는 아직 없다).</summary>
    public bool IsBuilt { get; private set; }

    /// <summary>책상 윗면의 규칙 화면(탭 누르기는 RulesScreenTapInput이 넘긴다).</summary>
    public RulesScreenView Screen => _screen;

    public RulesTableView(Transform parent, SO_RulesTableConfig config, SO_DiceConfig diceConfig, SO_DiceStatsConfig diceStatsConfig)
    {
        _config = config;
        _screen = new RulesScreenView(config, diceConfig, diceStatsConfig);
        _root = new GameObject("RulesTable").transform;
        _root.SetParent(parent, false);
        Surface = new GameObject("Surface").transform;
        Surface.SetParent(_root, false);
    }

    public void Dispose()
    {
        Clear();
        _screen.Dispose();
    }

    /// <param name="table">당구대 경계(맵 루트 기준, EnvironmentView.TableBounds).</param>
    public void Build(Bounds table)
    {
        Clear();
        // 세로는 당구대에 맞춘다(사용자 2026-11-12: 세로를 맞추고 딱 붙여서 옆에).
        _size = new Vector2(_config.Width, Mathf.Max(table.size.z - _config.DepthInset * 2f, 0.1f));
        Vector2 size = _size;
        Vector3 top = new Vector3(table.max.x + _config.Gap + size.x * 0.5f, table.max.y + _config.TopOffset, table.center.z);
        Surface.localPosition = top;

        Bounds desk = new Bounds();
        desk.SetMinMax(new Vector3(top.x - size.x * 0.5f, table.min.y, top.z - size.y * 0.5f),
            new Vector3(top.x + size.x * 0.5f, top.y, top.z + size.y * 0.5f));
        DeskBounds = desk;
        IsBuilt = true;

        if (_config.DeskPrefab != null)
        {
            _desk = Object.Instantiate(_config.DeskPrefab, _root, false);
            _desk.transform.localPosition = top;
        }
        else BuildDesk(top, table.min.y);

        // 규칙 화면은 펠트 판 윗면에 놓는다(만든 책상의 펠트는 상판 위로 두께의 10%).
        Vector3 felt = top + Vector3.up * (_config.DeskPrefab != null ? 0f : FeltThickness);
        Vector2 screenArea = _config.DeskPrefab != null ? _size : _size - Vector2.one * (_config.FeltInset * 2f);
        _screen.Build(_root, felt, screenArea);
    }

    private float FeltThickness => _config.TopThickness * 0.1f;

    public void Clear()
    {
        if (_desk != null) Object.Destroy(_desk);
        _desk = null;
        _body?.Dispose();
        _body = null;
        _neon?.Dispose();
        _neon = null;
        _screen.Clear();
    }

    // 상판(먹색) + 안쪽 펠트 판 + 다리 넷, 그리고 상판 윗면 테두리에 깐 네온 띠.
    private void BuildDesk(Vector3 top, float floor)
    {
        if (_config.Material == null) return;
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Vector2 size = _size;
        float t = _config.TopThickness;

        AddBox(cube, top + new Vector3(0f, -t * 0.5f, 0f), new Vector3(size.x, t, size.y), _config.FrameColor);
        // 펠트 판은 상판 위로 아주 얇게 — 같은 높이면 면이 겹쳐 깜빡인다.
        float felt = FeltThickness;
        Vector2 feltSize = size - Vector2.one * (_config.FeltInset * 2f);
        if (feltSize.x > 0f && feltSize.y > 0f)
            AddBox(cube, top + new Vector3(0f, felt * 0.5f, 0f), new Vector3(feltSize.x, felt, feltSize.y),
                _config.ScreenPrefab != null ? _config.ScreenBackground : _config.FeltColor);

        float legHeight = top.y - t - floor;
        if (legHeight > 0f)
        {
            float lx = size.x * 0.5f - _config.LegInset - _config.LegSize * 0.5f;
            float lz = size.y * 0.5f - _config.LegInset - _config.LegSize * 0.5f;
            Vector3 legScale = new Vector3(_config.LegSize, legHeight, _config.LegSize);
            float ly = floor + legHeight * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(top.x + ((i & 1) == 0 ? -lx : lx), ly, top.z + ((i & 2) == 0 ? -lz : lz));
                AddBox(cube, p, legScale, _config.FrameColor);
            }
        }
        _body = _builder.Build("RulesDesk", _root, _config.Material, ShadowCastingMode.On);

        // 네온 띠: 당구대처럼 상판 윗면 테두리(펠트 바깥 틀) 위에 깐 납작한 줄 네 변 — 옆면에 두르면 당구대와 따로 논다(사용자 2026-11-12).
        float n = _config.NeonThickness;
        float inset = _config.NeonInset;
        float h = felt * 0.5f;
        float y = top.y + h * 0.5f;
        float zEdge = size.y * 0.5f - inset - n * 0.5f;
        float xEdge = size.x * 0.5f - inset - n * 0.5f;
        AddBox(cube, new Vector3(top.x, y, top.z - zEdge), new Vector3(size.x - inset * 2f, h, n), _config.NeonColor);
        AddBox(cube, new Vector3(top.x, y, top.z + zEdge), new Vector3(size.x - inset * 2f, h, n), _config.NeonColor);
        AddBox(cube, new Vector3(top.x - xEdge, y, top.z), new Vector3(n, h, size.y - inset * 2f - n * 2f), _config.NeonColor);
        AddBox(cube, new Vector3(top.x + xEdge, y, top.z), new Vector3(n, h, size.y - inset * 2f - n * 2f), _config.NeonColor);
        _neon = _builder.Build("RulesDeskNeon", _root, _config.Material, ShadowCastingMode.Off);
        Material material = _neon.Material;
        material.SetColor(BaseColorId, Color.black);
        material.SetTexture(EmissionMapId, material.GetTexture(BaseMapId));
        material.SetColor(EmissionColorId, Color.white * _config.NeonIntensity);
        material.EnableKeyword("_EMISSION");
    }

    private void AddBox(Mesh cube, Vector3 center, Vector3 size, Color color) =>
        _builder.Add(cube, Matrix4x4.TRS(center, Quaternion.identity, size), color);
}
