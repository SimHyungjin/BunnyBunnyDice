using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 몬스터 머리 위 체력바. 모든 바를 메시 하나에 담아 매 프레임 다시 채운다 — 몬스터가 수백 마리여도 그리기 한 번이고,
/// 마리마다 오브젝트(UI·스프라이트)를 두지 않아 MonsterRenderer를 GPU 인스턴싱으로 바꿀 때도 그대로 쓸 수 있다.
/// 바는 카메라를 마주 보게 카메라의 오른쪽·위 방향으로 펼친다. 상태는 읽기만 한다(MonsterField의 배열).
/// 움직임이 끝난 뒤 그리도록 LateUpdate에서 채운다.
/// 바 아래에 딱 붙여 걸린 상태 이상을 작은 네모로 보인다(위는 솟아오르는 피해 숫자와 겹친다) — 걸린 것만 왼쪽부터 채우고 순서는 늘 취약·둔화·얼림·화상, 색은 거는 칩 색.
/// 눈에 띌 필요는 없고 보면 알 수 있으면 된다(칩이 일하는지, 디버프가 끊기는 곳이 어딘지).
/// 몸 색으로 보이던 이전 방식은 여러 개가 겹치면 하나만 보였고 특성 색(몸 색)을 덮었다.
/// </summary>
public sealed class MonsterHealthBars : IDisposable
{
    private readonly SO_MonsterConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly MonsterField _monsters;
    private readonly Func<Camera> _getCamera;
    private readonly Transform _root;
    private readonly MeshRenderer _renderer;
    private readonly Mesh _mesh;
    // 매 프레임 다시 채우는 버퍼. 목록을 재사용해 할당 없이 메시에 넘긴다.
    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Color> _colors = new List<Color>();
    private readonly List<int> _triangles = new List<int>();

    public MonsterHealthBars(Transform parent, SO_MonsterConfig config, SO_MapViewConfig viewConfig, MonsterField monsters,
        Func<Camera> getCamera)
    {
        _config = config;
        _viewConfig = viewConfig;
        _monsters = monsters;
        _getCamera = getCamera;

        GameObject go = new GameObject("MonsterHealthBars");
        _root = go.transform;
        _root.SetParent(parent, false);
        _mesh = new Mesh { name = "MonsterHealthBars" };
        _mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        // 피해 숫자(DamageNumbers, 1)보다 늘 위에 그린다 — 숫자가 많이 떠도 중요한 것은 남은 체력이다(2026-10-28 사용자).
        // 둘 다 판 전체를 덮는 투명 메시 하나라 거리로 정렬하면 프레임마다 앞뒤가 깜빡여 순서를 못 박는다.
        _renderer.sortingOrder = 2;
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnLateUpdate -= LateTick;
        if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
    }

    private void LateTick(float _)
    {
        _vertices.Clear();
        _colors.Clear();
        _triangles.Clear();

        Camera camera = _getCamera();
        // 머티리얼은 매번 넣는다 — 실행 중 설정을 고치면 바로 반영되게. 비우면 끈다.
        _renderer.sharedMaterial = _config.HealthBarMaterial;
        bool visible = camera != null && _config.HealthBarMaterial != null;
        _renderer.enabled = visible;
        if (visible) Fill(camera);

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
    }

    private void Fill(Camera camera)
    {
        // 메시는 맵 아래 로컬 좌표라 카메라 방향도 로컬로 옮긴다.
        Vector3 right = _root.InverseTransformDirection(camera.transform.right);
        Vector3 up = _root.InverseTransformDirection(camera.transform.up);
        float ground = _viewConfig.GetTileTop(TileType.Ground);
        float border = _config.HealthBarBorder;
        float halfThickness = _config.HealthBarThickness * 0.5f;

        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            MonsterState state = states[i];
            if (!state.IsAlive || state.MaxHealth <= 0f) continue;
            float ratio = Mathf.Clamp01(state.Health / state.MaxHealth);

            float scale = _monsters.KindOf(i).VisualScale;
            float halfWidth = _config.HealthBarWidth * scale * 0.5f;
            Vector3 center = new Vector3(state.Position.x, ground + _config.HealthBarHeight * scale, state.Position.y);
            // 상태 네모는 바를 숨겨도 보인다(다치지 않았어도 둔화·얼림은 걸려 있을 수 있다).
            AddStatuses(state, center, right, up, halfWidth, halfThickness, border);
            if (_config.HideFullHealthBar && ratio >= 1f) continue;

            AddQuad(center - right * halfWidth, center + right * halfWidth, up * halfThickness, _config.HealthBarBackColor);
            // 채움은 바탕 테두리 안에서 왼쪽부터 줄어든다.
            Vector3 left = center - right * (halfWidth - border);
            float inner = Mathf.Max(halfWidth - border, 0f) * 2f;
            Color fill = Color.Lerp(_config.HealthBarLowColor, _config.HealthBarFullColor, ratio);
            AddQuad(left, left + right * (inner * ratio), up * Mathf.Max(halfThickness - border, 0f), fill);
        }
    }

    // 네모를 걸린 것만 바 왼쪽 끝부터 틈 없이 붙여 놓는다(바에도, 네모끼리도 딱 붙는다 — 색으로 구분된다).
    // 테두리(바탕 색)를 둘러 밝은 바닥·몬스터 위에서도 하양(취약)이 보이게.
    private void AddStatuses(in MonsterState state, Vector3 center, Vector3 right, Vector3 up, float halfWidth, float halfThickness, float border)
    {
        if (!state.IsVulnerable && !state.IsSlowed && !state.IsFrozen && !state.IsPoisoned) return;
        float slot = halfWidth * 2f / StatusCount;
        float half = slot * _config.StatusSlotFill * 0.5f;
        Vector3 rowCenter = center - up * (halfThickness * (1f + 2f * _config.StatusRowGap) + half);
        int shown = 0;
        for (int s = 0; s < StatusCount; s++)
        {
            Color color;
            switch (s)
            {
                case 0: if (!state.IsVulnerable) continue; color = _config.VulnerableColor; break;
                case 1: if (!state.IsSlowed) continue; color = _config.SlowColor; break;
                case 2: if (!state.IsFrozen) continue; color = _config.FreezeColor; break;
                default: if (!state.IsPoisoned) continue; color = _config.PoisonColor; break;
            }
            Vector3 c = rowCenter + right * (-halfWidth + half * (1f + 2f * shown));
            shown++;
            AddQuad(c - right * half, c + right * half, up * half, _config.HealthBarBackColor);
            float inner = Mathf.Max(half - border, 0f);
            AddQuad(c - right * inner, c + right * inner, up * inner, color);
        }
    }

    // 취약·둔화·얼림·화상(AddStatuses의 자리 순서).
    private const int StatusCount = 4;

    // from → to를 가로로, 위아래로 halfUp만큼 펼친 사각형. 바탕 다음에 채움을 넣어 같은 메시 안에서 채움이 위에 그려진다.
    private void AddQuad(Vector3 from, Vector3 to, Vector3 halfUp, Color color)
    {
        int start = _vertices.Count;
        _vertices.Add(from - halfUp);
        _vertices.Add(from + halfUp);
        _vertices.Add(to + halfUp);
        _vertices.Add(to - halfUp);
        for (int i = 0; i < 4; i++) _colors.Add(color);
        _triangles.Add(start);
        _triangles.Add(start + 1);
        _triangles.Add(start + 2);
        _triangles.Add(start);
        _triangles.Add(start + 2);
        _triangles.Add(start + 3);
    }
}
