using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 바닥 칸 선. 평소에는 숨기고(또는 아주 옅게) 카드를 끌어 놓는 동안에만 드러낸다.
/// 판 전체에 늘 깔린 균일한 격자는 화면을 프로토타입처럼 보이게 하고, 칸을 세야 하는 것은 놓을 자리를 고를 때뿐이기 때문이다.
///
/// 선은 메시 하나로 합치고 투명 머티리얼의 알파만 바꿔 서서히 나타나고 사라지게 한다(선마다 오브젝트 없음).
/// 집는 동안 게임 속도가 느려지므로 전환은 실제 시간(unscaled)으로 진행한다.
/// </summary>
public sealed class MapGridView : IDisposable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly SO_MapViewConfig _config;
    private readonly Transform _root;
    private readonly PaletteMeshBuilder _builder = new PaletteMeshBuilder();
    private PaletteMesh _mesh;
    private bool _shown;
    private float _alpha;

    public MapGridView(Transform parent, SO_MapViewConfig config)
    {
        _config = config;
        _root = new GameObject("Grid").transform;
        _root.SetParent(parent, false);
        _alpha = config.GridIdleAlpha;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        Clear();
    }

    /// <summary>카드를 끌어 놓는 중인지. 켜면 선이 드러나고 끄면 평소 밝기로 돌아간다.</summary>
    public void SetShown(bool shown) => _shown = shown;

    public void Build(MapData map)
    {
        Clear();
        if (_config.GridMaterial == null) return;

        float width = _config.GridLineWidth;
        float thickness = _config.GridLineLift * 2f;
        if (width <= 0f || thickness <= 0f) return;

        // 칸끼리 맞닿은 변마다 얇은 선을 펠트 위에 살짝 띄워 깐다(같은 높이면 펠트와 겹쳐 깜빡인다).
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (!IsGridCell(map, x, y)) continue;
                if (x + 1 < map.Width && IsGridCell(map, x + 1, y))
                    Add(new Vector3(x + 0.5f, 0f, y), new Vector3(width, thickness, 1f));
                if (y + 1 < map.Height && IsGridCell(map, x, y + 1))
                    Add(new Vector3(x, 0f, y + 0.5f), new Vector3(1f, thickness, width));
            }
        }

        _mesh = _builder.Build("GridMesh", _root, _config.GridMaterial, ShadowCastingMode.Off);
        _mesh.Material.renderQueue = (int)RenderQueue.Transparent;
        ApplyAlpha();
    }

    public void Clear()
    {
        _mesh?.Dispose();
        _mesh = null;
    }

    private void Tick(float deltaTime)
    {
        float target = _shown ? _config.GridShownAlpha : _config.GridIdleAlpha;
        if (Mathf.Approximately(_alpha, target)) return;

        float duration = _config.GridFadeDuration;
        _alpha = duration > 0f
            ? Mathf.MoveTowards(_alpha, target, deltaTime / duration)
            : target;
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        if (_mesh == null) return;
        Color color = _config.GridLineColor;
        color.a = _alpha;
        _mesh.Material.SetColor(BaseColorId, color);
        // 다 사라졌으면 그리지 않는다 — 투명 오브젝트는 알파 0이어도 그리기 비용이 든다.
        _mesh.GameObject.SetActive(_alpha > 0f);
    }

    // 벽이 아닌 모든 칸. 칩 칸도 넣는다 — 칩은 둥글어 칸을 다 덮지 않으므로, 빼면 칩 옆 칸 테두리가 비어 보인다.
    // 더미 밑에 깔린 선은 칩에 가려진다.
    private static bool IsGridCell(MapData map, int x, int y) => map.Get(x, y) != TileType.Wall;

    // 색은 머티리얼(_BaseColor)이 정하므로 팔레트는 흰색 하나다.
    private void Add(Vector3 position, Vector3 scale) =>
        _builder.AddPrefab(_config.BlockPrefab, Matrix4x4.TRS(position, Quaternion.identity, scale), Color.white);
}
