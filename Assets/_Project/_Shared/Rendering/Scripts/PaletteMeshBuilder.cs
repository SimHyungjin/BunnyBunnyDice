using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 단색 블록 여러 개를 메시 하나로 합친다. 블록마다 다른 색은 작은 팔레트 텍스처에 모으고, 각 블록의 UV가 자기 색 픽셀을 가리키게 한다.
/// 블록 수백 개를 따로 그리면 그리기 호출이 블록 수만큼 생기고, 색을 MaterialPropertyBlock으로 바꾸면 SRP Batcher로도 묶이지 않는다.
/// 합치면 그리기 호출이 하나가 되고, 머티리얼은 원본(URP Lit)을 복제해 쓰므로 조명·그림자는 그대로다.
///
/// 단색(질감 없는) 메시 전용이다. 질감 있는 아트 블록으로 바꾸면 이 방식 대신 같은 머티리얼끼리 합치는 방식으로 교체해야 한다.
/// </summary>
public sealed class PaletteMeshBuilder
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<int> _colorIndices = new List<int>();
    private readonly List<int> _triangles = new List<int>();
    private readonly List<Color32> _palette = new List<Color32>();
    private readonly Dictionary<Color32, int> _paletteLookup = new Dictionary<Color32, int>();
    // 같은 메시(큐브 등)를 수백 번 넣으므로 정점 배열은 메시마다 한 번만 꺼낸다.
    // 합칠 때마다(Build) 비운다 — 테이블 조각·칩처럼 매번 새로 만들고 지우는 메시가 키로 쌓이지 않게.
    private readonly Dictionary<Mesh, MeshData> _meshCache = new Dictionary<Mesh, MeshData>();
    private readonly List<MeshFilter> _filterBuffer = new List<MeshFilter>();

    private sealed class MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Triangles;
    }

    public bool IsEmpty => _vertices.Count == 0;

    /// <summary>프리팹 안의 모든 메시를 placement 위치(부모 로컬)에 놓은 것처럼 넣는다.</summary>
    public void AddPrefab(GameObject prefab, Matrix4x4 placement, Color color)
    {
        prefab.GetComponentsInChildren(true, _filterBuffer);
        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        for (int i = 0; i < _filterBuffer.Count; i++)
        {
            MeshFilter filter = _filterBuffer[i];
            if (filter.sharedMesh == null) continue;
            Add(filter.sharedMesh, placement * toRoot * filter.transform.localToWorldMatrix, color);
        }
    }

    public void Add(Mesh mesh, Matrix4x4 matrix, Color color)
    {
        MeshData data = GetMeshData(mesh);
        int colorIndex = GetColorIndex(color);
        int baseVertex = _vertices.Count;
        // 늘림이 축마다 다를 수 있어 법선은 역전치 행렬로 옮긴다.
        Matrix4x4 normalMatrix = matrix.inverse.transpose;

        for (int i = 0; i < data.Vertices.Length; i++)
        {
            _vertices.Add(matrix.MultiplyPoint3x4(data.Vertices[i]));
            _normals.Add(normalMatrix.MultiplyVector(data.Normals[i]).normalized);
            _colorIndices.Add(colorIndex);
        }
        for (int i = 0; i < data.Triangles.Length; i++) _triangles.Add(baseVertex + data.Triangles[i]);
    }

    /// <summary>모은 블록으로 오브젝트를 만들고 쌓아 둔 데이터를 비운다. template은 복제해 팔레트 텍스처를 입힌다.</summary>
    public PaletteMesh Build(string objectName, Transform parent, Material template, ShadowCastingMode shadows)
    {
        // 1줄짜리 팔레트. 점 필터라 이웃 색이 섞이지 않는다.
        int width = Mathf.Max(1, _palette.Count);
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false, false)
        {
            name = objectName + "_Palette",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(_palette.Count > 0 ? _palette.ToArray() : new[] { new Color32(255, 255, 255, 255) });
        texture.Apply(false, true);

        Vector2[] uvs = new Vector2[_colorIndices.Count];
        for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2((_colorIndices[i] + 0.5f) / width, 0.5f);

        Mesh mesh = new Mesh { name = objectName, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.uv = uvs;
        mesh.SetTriangles(_triangles, 0);
        mesh.RecalculateBounds();

        Material material = new Material(template) { name = objectName + "_Material" };
        material.SetTexture(BaseMapId, texture);
        material.SetColor(BaseColorId, Color.white);

        GameObject go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = shadows;

        Reset();
        return new PaletteMesh(go, mesh, texture, material);
    }

    private void Reset()
    {
        _vertices.Clear();
        _normals.Clear();
        _colorIndices.Clear();
        _triangles.Clear();
        _palette.Clear();
        _paletteLookup.Clear();
        _meshCache.Clear();
    }

    private MeshData GetMeshData(Mesh mesh)
    {
        if (_meshCache.TryGetValue(mesh, out MeshData data)) return data;
        data = new MeshData { Vertices = mesh.vertices, Normals = mesh.normals, Triangles = mesh.triangles };
        _meshCache[mesh] = data;
        return data;
    }

    private int GetColorIndex(Color color)
    {
        Color32 key = color;
        if (_paletteLookup.TryGetValue(key, out int index)) return index;
        index = _palette.Count;
        _palette.Add(key);
        _paletteLookup[key] = index;
        return index;
    }
}

/// <summary>합쳐서 만든 오브젝트와 그것만을 위해 만든 메시·텍스처·머티리얼. 다시 만들 때 Dispose로 함께 치운다.</summary>
public sealed class PaletteMesh : IDisposable
{
    private readonly GameObject _gameObject;
    private readonly Mesh _mesh;
    private readonly Texture2D _texture;
    private readonly Material _material;

    /// <summary>이 메시만 쓰는 복제 머티리얼. 광택·발광 세기처럼 메시 전체에 한 값인 속성은 여기에 직접 넣는다.</summary>
    public Material Material => _material;
    public GameObject GameObject => _gameObject;

    public PaletteMesh(GameObject gameObject, Mesh mesh, Texture2D texture, Material material)
    {
        _gameObject = gameObject;
        _mesh = mesh;
        _texture = texture;
        _material = material;
    }

    public void Dispose()
    {
        if (_gameObject != null) Object.Destroy(_gameObject);
        if (_mesh != null) Object.Destroy(_mesh);
        if (_texture != null) Object.Destroy(_texture);
        if (_material != null) Object.Destroy(_material);
    }
}
