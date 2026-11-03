using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격선·폭발 고리 표시. 맞히기는 쏘는 즉시 끝나고(투사체 없음), 여기서는 맞았다는 것만 보이게 잠깐 그렸다 지운다.
/// 폭발·난사는 바닥 높이의 고리가 반경까지 퍼지며 사라진다 — 얼마나 넓게 터졌는지 보이게.
/// 선·고리 오브젝트는 재사용한다 — 주사위 수십 개가 1초에 몇 번씩 쏘므로 매번 만들고 지우지 않게.
/// 코어 풀(PoolManager)을 쓰지 않는 이유: 코어 풀은 Addressables 프리팹을 꺼내는데, 선은 프리팹 없이 코드로 만든다.
/// </summary>
public sealed class AttackEffects : IDisposable
{
    private struct Shot
    {
        public LineRenderer Line;
        public float Remaining;
    }

    // 고리는 LineRenderer가 아니라 바닥에 눕힌 메시다(RingMesh — LineRenderer 고리는 한쪽에 틈이 보였다).
    private sealed class Ring
    {
        public GameObject Object;
        public MeshRenderer Renderer;
        public Mesh Mesh;
    }

    private readonly SO_AttackEffectConfig _config;
    private readonly Transform _root;
    private struct Burst
    {
        public Ring Ring;
        public float Radius;
        public float Duration;
        public float Remaining;
        /// <summary>퍼지는 고리(폭발)인지, 제자리에 머무는 고리(스킬 지속 구역)인지.</summary>
        public bool Expand;
    }

    // 맞은 적 고리(하양 난사 — 범위 안 적마다 하나라 수백 개가 된다). 고리마다 오브젝트·메시를 두면 프레임이 떨어져 메시 하나에 모아 그린다.
    private struct Spark
    {
        public Vector3 Center;
        public float Radius;
        public Color Color;
        public float Remaining;
    }

    private readonly List<Spark> _sparks = new List<Spark>();
    private GameObject _sparkObject;
    private MeshRenderer _sparkRenderer;
    private Mesh _sparkMesh;
    private readonly List<Vector3> _sparkVertices = new List<Vector3>();
    private readonly List<Color> _sparkColors = new List<Color>();
    private readonly List<int> _sparkTriangles = new List<int>();
    private readonly List<Shot> _active = new List<Shot>();
    private readonly Stack<LineRenderer> _idle = new Stack<LineRenderer>();
    private readonly List<Burst> _bursts = new List<Burst>();
    private readonly Stack<Ring> _idleBursts = new Stack<Ring>();
    // ShowHeldRing의 고리. 처음 쓸 때 만든다.
    private Ring _heldRing;
    // 코드로 만든 메시는 오브젝트와 함께 지워지지 않으므로 모아 두었다가 Dispose에서 지운다.
    private readonly List<Mesh> _meshes = new List<Mesh>();

    public AttackEffects(Transform parent, SO_AttackEffectConfig config)
    {
        _config = config;
        _root = new GameObject("AttackEffects").transform;
        _root.SetParent(parent, false);
        for (int i = 0; i < config.InitialCapacity; i++) _idle.Push(CreateLine());
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnGameUpdate -= Tick;
        for (int i = 0; i < _meshes.Count; i++) UnityEngine.Object.Destroy(_meshes[i]);
        _meshes.Clear();
    }

    /// <summary>
    /// 맵 로컬 좌표 from에서 to까지 color로 선을 긋는다. 치명타면 굵게 그려 터진 것이 보이게 한다.
    /// widthScale은 공격 방식이 굵기로 보여 줄 것이 있을 때(각성한 관통의 두꺼운 빔 등) 곱한다.
    /// </summary>
    public void Show(Vector3 from, Vector3 to, Color color, bool critical = false, float widthScale = 1f)
    {
        LineRenderer line = _idle.Count > 0 ? _idle.Pop() : CreateLine();
        line.gameObject.SetActive(true);
        // 머티리얼·굵기는 쏠 때마다 넣는다 — 실행 중 설정을 고치면 만들어 둔 선에도 바로 반영되게.
        line.sharedMaterial = _config.Material;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.widthMultiplier = _config.Width * (critical ? _config.CritWidthScale : 1f) * widthScale;

        Tint(line.gameObject, color);
        SetAlpha(line, 1f);
        _active.Add(new Shot { Line = line, Remaining = _config.Duration });
    }

    /// <summary>맵 로컬 center(바닥 높이)에 radius까지 퍼지는 폭발 고리를 그린다.</summary>
    public void ShowBurst(Vector3 center, float radius, Color color) =>
        AddRing(center, radius, color, _config.BurstDuration, true);

    /// <summary>
    /// 맵 로컬 center(바닥 높이)에 작은 고리가 퍼지며 사라진다 — ShowBurst와 같은 모습이지만 한꺼번에 많이 띄우는 것(맞은 적마다)을
    /// 메시 하나에 모아 그린다.
    /// </summary>
    public void ShowSpark(Vector3 center, float radius, Color color) =>
        _sparks.Add(new Spark { Center = center, Radius = radius, Color = color, Remaining = _config.BurstDuration });

    /// <summary>duration 동안 제자리에 머물며 흐려지는 고리(레이즈·풀 하우스 지속 등).</summary>
    public void ShowRing(Vector3 center, float radius, Color color, float duration) =>
        AddRing(center, radius, color, duration, false);

    /// <summary>
    /// 흐려지지 않고 숨길 때까지 머무는 고리 하나(영웅이 올라탄 주사위 — 레이즈 고리와 같은 모습). 매 프레임 불러도 된다.
    /// 영웅은 한 명이라 하나만 둔다.
    /// </summary>
    public void ShowHeldRing(Vector3 center, float radius, Color color)
    {
        if (_heldRing == null) _heldRing = CreateRing();
        _heldRing.Object.SetActive(true);
        _heldRing.Object.transform.localPosition = center + Vector3.up * _config.BurstLift;
        _heldRing.Renderer.sharedMaterial = _config.Material;
        Tint(_heldRing.Object, color);
        RingMesh.Fill(_heldRing.Mesh, radius, _config.BurstWidth, _config.BurstSegments, Color.white);
    }

    public void HideHeldRing()
    {
        if (_heldRing != null) _heldRing.Object.SetActive(false);
    }

    private void AddRing(Vector3 center, float radius, Color color, float duration, bool expand)
    {
        if (duration <= 0f) return;
        Ring ring = _idleBursts.Count > 0 ? _idleBursts.Pop() : CreateRing();
        ring.Object.SetActive(true);
        ring.Object.transform.localPosition = center + Vector3.up * _config.BurstLift;
        ring.Renderer.sharedMaterial = _config.Material;
        Tint(ring.Object, color);
        Burst burst = new Burst { Ring = ring, Radius = radius, Duration = duration, Remaining = duration, Expand = expand };
        PlaceBurst(burst);
        _bursts.Add(burst);
    }

    public void Clear()
    {
        for (int i = 0; i < _active.Count; i++) Release(_active[i].Line);
        _active.Clear();
        for (int i = 0; i < _bursts.Count; i++) ReleaseBurst(_bursts[i].Ring);
        _bursts.Clear();
        _sparks.Clear();
        if (_sparkObject != null) _sparkObject.SetActive(false);
        HideHeldRing();
    }

    // 게임 시간을 따른다 — 카드를 집는 동안 공격도 느려지므로 선도 같은 빠르기로 사라진다.
    private void Tick(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Shot shot = _active[i];
            shot.Remaining -= deltaTime;
            if (shot.Remaining <= 0f)
            {
                Release(shot.Line);
                // 순서는 상관없으므로 마지막 것을 옮겨 와 지운다.
                _active[i] = _active[_active.Count - 1];
                _active.RemoveAt(_active.Count - 1);
                continue;
            }

            SetAlpha(shot.Line, shot.Remaining / _config.Duration);
            _active[i] = shot;
        }

        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            Burst burst = _bursts[i];
            burst.Remaining -= deltaTime;
            if (burst.Remaining <= 0f)
            {
                ReleaseBurst(burst.Ring);
                _bursts[i] = _bursts[_bursts.Count - 1];
                _bursts.RemoveAt(_bursts.Count - 1);
                continue;
            }
            PlaceBurst(burst);
            _bursts[i] = burst;
        }

        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = _sparks[i];
            spark.Remaining -= deltaTime;
            if (spark.Remaining <= 0f)
            {
                _sparks[i] = _sparks[_sparks.Count - 1];
                _sparks.RemoveAt(_sparks.Count - 1);
                continue;
            }
            _sparks[i] = spark;
        }
        DrawSparks();
    }

    // 모든 작은 고리를 메시 하나로 다시 채운다(퍼지는 크기·투명도는 PlaceBurst와 같은 곡선).
    // 색은 정점 색에 넣고(고리마다 다를 수 있다) 블룸 밝기는 머티리얼 색(흰색 × Intensity)으로 곱한다.
    private void DrawSparks()
    {
        if (_sparks.Count == 0)
        {
            if (_sparkObject != null) _sparkObject.SetActive(false);
            return;
        }
        if (_sparkObject == null) CreateSparkObject();
        // 머티리얼·밝기는 켤 때만 넣는다(실행 중 설정을 고치면 다음에 켤 때 반영) — 매 프레임 넣으면 색 입히기가 렌더러를 찾느라 할당이 생긴다.
        if (!_sparkObject.activeSelf)
        {
            _sparkObject.SetActive(true);
            _sparkRenderer.sharedMaterial = _config.Material;
            Tint(_sparkObject, Color.white);
        }

        int segments = Mathf.Max(_config.SparkSegments, 3);
        float half = _config.BurstWidth * 0.5f;
        _sparkVertices.Clear();
        _sparkColors.Clear();
        _sparkTriangles.Clear();
        for (int s = 0; s < _sparks.Count; s++)
        {
            Spark spark = _sparks[s];
            float t = 1f - spark.Remaining / _config.BurstDuration;
            float radius = spark.Radius * Mathf.Lerp(_config.BurstStartScale, 1f, 1f - (1f - t) * (1f - t));
            float inner = Mathf.Max(radius - half, 0f);
            float outer = radius + half;
            Color color = spark.Color;
            color.a = 1f - t;
            Vector3 center = spark.Center + Vector3.up * _config.BurstLift;
            int start = _sparkVertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                _sparkVertices.Add(center + direction * inner);
                _sparkVertices.Add(center + direction * outer);
                _sparkColors.Add(color);
                _sparkColors.Add(color);
                int a = start + i * 2;
                int b = start + (i + 1) % segments * 2;
                _sparkTriangles.Add(a);
                _sparkTriangles.Add(b);
                _sparkTriangles.Add(a + 1);
                _sparkTriangles.Add(a + 1);
                _sparkTriangles.Add(b);
                _sparkTriangles.Add(b + 1);
            }
        }
        _sparkMesh.Clear();
        _sparkMesh.SetVertices(_sparkVertices);
        _sparkMesh.SetColors(_sparkColors);
        _sparkMesh.SetTriangles(_sparkTriangles, 0);
        _sparkMesh.RecalculateBounds();
    }

    private void CreateSparkObject()
    {
        _sparkObject = new GameObject("Sparks");
        _sparkObject.transform.SetParent(_root, false);
        _sparkMesh = RingMesh.Create("Sparks");
        // 고리 수백 개면 정점이 65535를 넘는다.
        _sparkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _meshes.Add(_sparkMesh);
        _sparkObject.AddComponent<MeshFilter>().sharedMesh = _sparkMesh;
        _sparkRenderer = _sparkObject.AddComponent<MeshRenderer>();
        _sparkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _sparkRenderer.receiveShadows = false;
        _sparkObject.SetActive(false);
    }

    // 폭발은 반경의 일부에서 시작해 반경까지 퍼지며 흐려진다. 머무는 고리는 크기 그대로 흐려지기만 한다.
    // 굵기는 퍼져도 그대로 두려고 크기(scale)가 아니라 메시를 다시 채운다.
    private void PlaceBurst(in Burst burst)
    {
        float t = 1f - burst.Remaining / burst.Duration;
        float radius = burst.Expand ? burst.Radius * Mathf.Lerp(_config.BurstStartScale, 1f, 1f - (1f - t) * (1f - t)) : burst.Radius;
        RingMesh.Fill(burst.Ring.Mesh, radius, _config.BurstWidth, _config.BurstSegments, new Color(1f, 1f, 1f, 1f - t));
    }

    // 정점 색은 0~1로 잘리므로 블룸에 쓸 밝은 색은 머티리얼 색으로 넣고, 정점 색에는 사라지는 투명도만 둔다(PathLane과 같은 방식).
    private void Tint(GameObject target, Color color)
    {
        Color bright = color * _config.Intensity;
        bright.a = 1f;
        RendererTint.Apply(target, bright);
    }

    private LineRenderer CreateLine()
    {
        GameObject go = new GameObject("Shot");
        go.transform.SetParent(_root, false);
        go.SetActive(false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    // 바닥 높이에 눕힌 고리. 위에서 비스듬히 보는 고정 카메라라 눕혀도 충분히 보인다.
    private Ring CreateRing()
    {
        GameObject go = new GameObject("Burst");
        go.transform.SetParent(_root, false);
        go.SetActive(false);
        Mesh mesh = RingMesh.Create("BurstRing");
        _meshes.Add(mesh);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return new Ring { Object = go, Renderer = renderer, Mesh = mesh };
    }

    private void Release(LineRenderer line)
    {
        line.gameObject.SetActive(false);
        _idle.Push(line);
    }

    private void ReleaseBurst(Ring ring)
    {
        ring.Object.SetActive(false);
        _idleBursts.Push(ring);
    }

    private static void SetAlpha(LineRenderer line, float alpha)
    {
        Color color = new Color(1f, 1f, 1f, alpha);
        line.startColor = color;
        line.endColor = color;
    }
}
