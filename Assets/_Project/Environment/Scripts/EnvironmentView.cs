using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 판 바깥 환경 표시(당구대): 테이블 모델을 맵 가운데에 놓고, 열린 포켓(MapData.Spawns) 구멍 안에 빛나는 원판을 둔다.
/// 테이블 밑(바닥 높이)에는 무늬 카펫을 넓게 깐다 — 바깥이 검은 빈 공간이면 판이 허공에 떠 보인다.
/// 모델의 펠트가 곧 판 바닥이다(MapView는 목표물 표식, ChipField는 칩만 그린다).
///
/// 모델 방향은 모델마다 다를 수 있어(내보내기 축 설정) 놓은 뒤 경계를 재서 긴 축이 맵의 긴 축과 다르면 90° 돌린다.
/// 수평 위치는 경계 가운데를 맵 가운데에 맞추고, 높이는 피벗(펠트 윗면 = 0)을 그대로 쓴다.
/// </summary>
public sealed class EnvironmentView : IDisposable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private readonly SO_EnvironmentConfig _config;
    private readonly Transform _root;
    private readonly PaletteMeshBuilder _builder = new PaletteMeshBuilder();
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private GameObject _table;
    private readonly List<PaletteMesh> _glows = new List<PaletteMesh>();
    private readonly List<Vector2Int> _glowCells = new List<Vector2Int>();
    // 판 주사위 연출 동안은 열린 포켓 빛을 숨긴다(주사위가 빠진 뒤 켜진다). 다시 만들어도 이 상태를 따른다.
    private bool _glowVisible = true;
    // 보스 포켓과 잭팟 예고 중인지 — 다시 만들어도(설정 변경) 색을 잃지 않게 든다.
    private Vector2Int _bossPocket = new Vector2Int(-1, -1);
    private bool _jackpot;
    private PaletteMesh _floor;
    // 테이블 모델 밑면 높이. 카펫을 이 높이에 깐다.
    private float _tableBottom;

    public EnvironmentView(Transform parent, SO_EnvironmentConfig config)
    {
        _config = config;
        _root = new GameObject("Environment").transform;
        _root.SetParent(parent, false);
    }

    public void Dispose() => Clear();

    public void Build(MapData map, int seed)
    {
        Clear();
        PlaceTable(map);
        BuildPocketGlow(map);
        BuildFloor(map);
    }

    public void Clear()
    {
        if (_table != null) Object.Destroy(_table);
        _table = null;
        for (int i = 0; i < _glows.Count; i++) _glows[i].Dispose();
        _glows.Clear();
        _glowCells.Clear();
        _floor?.Dispose();
        _floor = null;
    }

    private void PlaceTable(MapData map)
    {
        if (_config.TablePrefab == null) return;
        _table = Object.Instantiate(_config.TablePrefab, _root, false);
        _table.GetComponentsInChildren(true, _renderers);
        if (_config.TableMaterial != null)
            for (int i = 0; i < _renderers.Count; i++) _renderers[i].sharedMaterial = _config.TableMaterial;

        _tableBottom = _config.FloorHeight;
        Bounds bounds = GetLocalBounds();
        bool tableLongOnX = bounds.size.x >= bounds.size.z;
        bool mapLongOnX = map.Width >= map.Height;
        if (tableLongOnX != mapLongOnX)
        {
            _table.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * _table.transform.localRotation;
            bounds = GetLocalBounds();
        }

        // 내보내기 단위가 틀리면(예: 100배) 판과 안 맞으므로 실제 크기를 남긴다. 펠트 + 쿠션이라 맵보다 사방 약 0.6칸 커야 한다.
        Debug.Log($"[EnvironmentView] table size {bounds.size} (map {map.Width}x{map.Height})");
        Vector3 mapCenter = new Vector3((map.Width - 1) * 0.5f, 0f, (map.Height - 1) * 0.5f);
        Vector3 offset = mapCenter - new Vector3(bounds.center.x, 0f, bounds.center.z);
        _table.transform.localPosition += offset;
        _tableBottom = bounds.min.y;
    }

    // 맵 가운데를 중심으로 넓은 사각 판 하나. 무늬는 위에서 본 좌표로 깔아 칸과 같은 방향으로 반복된다.
    private void BuildFloor(MapData map)
    {
        if (_config.TableMaterial == null || _config.FloorExtent <= 0f) return;

        Vector3 center = new Vector3((map.Width - 1) * 0.5f, _tableBottom, (map.Height - 1) * 0.5f);
        float e = _config.FloorExtent;
        Vector3[] vertices =
        {
            center + new Vector3(-e, 0f, -e), center + new Vector3(-e, 0f, e),
            center + new Vector3(e, 0f, e), center + new Vector3(e, 0f, -e),
        };
        Vector2[] uvs = new Vector2[4];
        float tile = Mathf.Max(0.01f, _config.CarpetTileSize);
        for (int i = 0; i < 4; i++) uvs[i] = new Vector2(vertices[i].x, vertices[i].z) / tile;
        Mesh mesh = new Mesh { name = "Floor", vertices = vertices, uv = uvs, normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up } };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        // 테이블 머티리얼(Lit)을 복제하되 발광은 끈다(테이블 발광 텍스처가 바닥에 비치지 않게).
        Texture2D texture = CarpetTexture.Create(_config);
        Material material = new Material(_config.TableMaterial) { name = "Floor" };
        material.DisableKeyword("_EMISSION");
        material.SetTexture(EmissionMapId, null);
        material.SetColor(EmissionColorId, Color.black);
        material.SetTexture(BaseMapId, texture);
        material.SetColor(BaseColorId, Color.white);
        material.SetFloat(SmoothnessId, _config.CarpetSmoothness);

        GameObject go = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(_root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        // 테이블 그림자를 받아 바닥에 붙어 보이게 한다. 바닥 자체는 그림자를 드리울 것이 없다.
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        _floor = new PaletteMesh(go, mesh, texture, material);
    }

    // 렌더러 경계를 루트 기준 좌표로 모은다(모델을 맵 루트 아래 놓으므로 루트 회전·위치에 영향받지 않게).
    private Bounds GetLocalBounds()
    {
        Bounds result = default;
        bool any = false;
        for (int i = 0; i < _renderers.Count; i++)
        {
            Bounds world = _renderers[i].bounds;
            Vector3 min = world.min, max = world.max;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3((c & 1) == 0 ? min.x : max.x, (c & 2) == 0 ? min.y : max.y, (c & 4) == 0 ? min.z : max.z);
                Vector3 local = _root.InverseTransformPoint(corner);
                if (!any) result = new Bounds(local, Vector3.zero);
                else result.Encapsulate(local);
                any = true;
            }
        }
        return result;
    }

    // 열린 포켓마다 구멍 안에 빛나는 원판. 포켓마다 따로 둔다 — 판 주사위 연출이 주사위가 빠지는 순간 그 포켓만 켠다.
    // 팔레트 색을 발광 텍스처로도 써서 색 그대로 빛나게 한다.
    private void BuildPocketGlow(MapData map)
    {
        if (_config.TableMaterial == null || map.Spawns.Count == 0) return;

        RoundedRectPath circle = new RoundedRectPath();
        circle.Build(new Vector2(-0.5f, -0.5f), new Vector2(0.5f, 0.5f), 0.5f, Mathf.Max(1, _config.PocketSegments / 4));
        Mesh disc = SweepMesh.BuildCap("PocketGlowDisc", circle, 0f, 1f);

        for (int i = 0; i < map.Spawns.Count; i++)
        {
            Vector2Int cell = map.Spawns[i];
            bool corner = IsCorner(map, cell);
            float diameter = (corner ? _config.CornerGlowRadius : _config.SideGlowRadius) * 2f;
            Vector3 center = GetPocketCenter(map, cell) + Vector3.up * _config.PocketGlowDepth;
            _builder.Add(disc, Matrix4x4.TRS(center, Quaternion.identity, new Vector3(diameter, 1f, diameter)), _config.PocketGlowColor);

            PaletteMesh glow = _builder.Build($"PocketGlow_{cell.x}_{cell.y}", _root, _config.TableMaterial, ShadowCastingMode.Off);
            Material material = glow.Material;
            material.SetColor(BaseColorId, Color.black);
            material.SetTexture(EmissionMapId, material.GetTexture(BaseMapId));
            material.SetColor(EmissionColorId, Color.white * _config.PocketGlowIntensity);
            material.EnableKeyword("_EMISSION");
            glow.GameObject.SetActive(_glowVisible);
            _glows.Add(glow);
            _glowCells.Add(cell);
        }
        Object.Destroy(disc);
        _bossPocket = map.BossPocket;
        ApplyBossGlow();
    }

    /// <summary>열린 포켓 빛을 모두 켜거나 끈다. 다시 만들어도 이 상태를 따른다.</summary>
    public void SetPocketGlowVisible(bool visible)
    {
        _glowVisible = visible;
        for (int i = 0; i < _glows.Count; i++) _glows[i].GameObject.SetActive(visible);
    }

    /// <summary>한 포켓의 빛만 켜거나 끈다(판 주사위가 그 포켓에 빠질 때).</summary>
    public void SetPocketGlowVisible(Vector2Int cell, bool visible)
    {
        for (int i = 0; i < _glows.Count; i++)
            if (_glowCells[i] == cell) _glows[i].GameObject.SetActive(visible);
    }

    /// <summary>보스 포켓 빛을 잭팟 색(금색)으로 바꾸거나 보스 색으로 되돌린다(잭팟 보스 웨이브).</summary>
    public void SetPocketJackpot(bool jackpot)
    {
        _jackpot = jackpot;
        ApplyBossGlow();
    }

    // 보스 포켓은 판 내내 보스 색, 잭팟 보스 웨이브 동안은 금색. 빛은 팔레트 색을 발광 텍스처로 쓰므로,
    // 바꿀 때는 발광 텍스처를 빼고 발광 색만 쓴다 — 텍스처를 남긴 채 색을 곱하면 빨강에 금색이 섞여 주황이 된다.
    private void ApplyBossGlow()
    {
        Color color = _jackpot ? _config.JackpotGlowColor : _config.BossGlowColor;
        for (int i = 0; i < _glows.Count; i++)
        {
            if (_glowCells[i] != _bossPocket) continue;
            Material material = _glows[i].Material;
            material.SetTexture(EmissionMapId, null);
            material.SetColor(EmissionColorId, color * _config.PocketGlowIntensity);
        }
    }

    /// <summary>포켓 칸 → 모델의 구멍 가운데(맵 로컬, 펠트 높이). 모서리 포켓은 맵 모서리에서, 가운데 포켓은 맵 가장자리의 그 칸 자리에서 바깥으로 민 곳.</summary>
    public Vector3 GetPocketCenter(MapData map, Vector2Int cell)
    {
        bool longOnX = map.Width >= map.Height;
        if (IsCorner(map, cell))
        {
            float sx = cell.x == 0 ? -1f : 1f, sz = cell.y == 0 ? -1f : 1f;
            float ox = longOnX ? _config.CornerPocketOffset.x : _config.CornerPocketOffset.y;
            float oz = longOnX ? _config.CornerPocketOffset.y : _config.CornerPocketOffset.x;
            return new Vector3(cell.x + sx * (0.5f + ox), 0f, cell.y + sz * (0.5f + oz));
        }

        Vector3 outward = GetOutward(map, cell);
        return new Vector3(cell.x, 0f, cell.y) + outward * (0.5f + _config.SidePocketOffset);
    }

    private static bool IsCorner(MapData map, Vector2Int cell) =>
        (cell.x == 0 || cell.x == map.Width - 1) && (cell.y == 0 || cell.y == map.Height - 1);

    // 가장자리 칸이 맞닿은 맵 밖 방향(모서리가 아닌 칸 기준).
    private static Vector3 GetOutward(MapData map, Vector2Int cell)
    {
        if (cell.y == 0) return Vector3.back;
        if (cell.y == map.Height - 1) return Vector3.forward;
        if (cell.x == 0) return Vector3.left;
        return Vector3.right;
    }
}
