using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅이 던진 카드가 손에서 적까지 날아가는 표시. 맞히기는 손을 떠나는 순간 HeroAttack이 끝냈고 여기는 보여 주기만 한다
/// (AI 시뮬이 화면 없이 같은 결과를 내게). 공속이 끝없이 오르므로 여러 장이 동시에 날 수 있다.
/// 카드는 맞은 적을 따라간다(유도) — 손에서 몸 바깥쪽으로 튀어 나갔다가 휘어 적의 지금 자리에 꽂힌다(2차 베지어, 끝점을 매 프레임 옮긴다).
/// 날아가는 사이 적이 죽으면 마지막으로 본 자리로 간다.
/// 카드는 바닥과 나란히 누운 판 하나를 돌리고, 꼬리는 TrailRenderer다. 날아간 뒤 꼬리가 사라질 때까지 기다렸다 재사용한다.
/// 코어 풀(PoolManager)을 쓰지 않는 이유: 코어 풀은 Addressables 프리팹을 꺼내는데, 카드는 프리팹 없이 코드로 만든다(AttackEffects와 같다).
/// </summary>
public sealed class HeroCardShots : IDisposable
{
    private sealed class Card
    {
        public GameObject Object;
        public Transform Body;
        public TrailRenderer Trail;
        public Vector3 From;
        public Vector3 Control;
        public Vector3 To;
        public int TargetId;
        public float Time;
        public float Spin;
    }

    private readonly SO_HeroCardShotConfig _config;
    private readonly MonsterField _monsters;
    private readonly SO_MonsterConfig _monsterConfig;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly Transform _root;
    private readonly Mesh _mesh;
    private readonly List<Card> _active = new List<Card>();
    private readonly Stack<Card> _idle = new Stack<Card>();

    public HeroCardShots(Transform parent, SO_HeroCardShotConfig config, MonsterField monsters, SO_MonsterConfig monsterConfig,
        SO_MapViewConfig viewConfig)
    {
        _config = config;
        _monsters = monsters;
        _monsterConfig = monsterConfig;
        _viewConfig = viewConfig;
        _root = new GameObject("HeroCardShots").transform;
        _root.SetParent(parent, false);
        _mesh = CreateCardMesh();
        for (int i = 0; i < config.InitialCapacity; i++) _idle.Push(CreateCard());
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        UnityEngine.Object.Destroy(_mesh);
    }

    /// <summary>
    /// 맵 로컬 from(손)에서 targetId 몬스터까지 카드를 날린다. launch는 처음 튀어 나가는 쪽(바닥과 나란한 방향, 길이 상관없음 — 0이면 곧게).
    /// 한 번에 여러 장(count)이면 index마다 SpreadAngle만큼 벌려 부채꼴로 튀어 나간다. 치명타면 금색으로 크게.
    /// </summary>
    public void Throw(Vector3 from, Vector3 launch, int targetId, bool critical, int index, int count)
    {
        if (count > 1) launch = Quaternion.AngleAxis((index - (count - 1) * 0.5f) * _config.SpreadAngle, Vector3.up) * launch;
        Card card = _idle.Count > 0 ? _idle.Pop() : CreateCard();
        card.From = from;
        card.TargetId = targetId;
        // 던지는 순간 대상은 살아 있다(HeroAttack이 막 맞혔다). 못 찾으면 손 자리 — 그 자리에서 사라진다.
        card.To = from;
        TryTargetPoint(targetId, ref card.To);
        float distance = Vector3.Distance(from, card.To);
        card.Control = from + (launch.sqrMagnitude > 0f ? launch.normalized : Vector3.zero) * (distance * _config.CurveBend);
        card.Time = 0f;
        card.Spin = 0f;
        card.Object.SetActive(true);
        card.Body.gameObject.SetActive(true);
        card.Object.transform.localPosition = from;

        // 머티리얼·크기는 던질 때마다 넣는다 — 실행 중 설정을 고치면 만들어 둔 카드에도 바로 반영되게.
        float scale = critical ? _config.CritSizeScale : 1f;
        card.Body.localScale = new Vector3(_config.CardSize.x * scale, 1f, _config.CardSize.y * scale);
        card.Body.GetComponent<MeshRenderer>().sharedMaterial = _config.CardMaterial;
        card.Trail.sharedMaterial = _config.TrailMaterial;
        card.Trail.time = _config.TrailTime;
        card.Trail.widthMultiplier = _config.TrailWidth * scale;
        // 앞 비행의 끝점에서 이어지는 꼬리가 남지 않게.
        card.Trail.Clear();
        card.Trail.emitting = true;

        Color color = (critical ? _config.CritColor : _config.Color) * _config.Intensity;
        color.a = 1f;
        RendererTint.Apply(card.Object, color);
        _active.Add(card);
    }

    public void Clear()
    {
        for (int i = 0; i < _active.Count; i++) Release(_active[i]);
        _active.Clear();
    }

    // 게임 시간을 따른다 — 공격 판정과 같은 빠르기로 날아간다.
    private void Tick(float _)
    {
        float deltaTime = Time.deltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Card card = _active[i];
            card.Time += deltaTime;
            float t = card.Time / _config.FlightTime;
            if (t < 1f)
            {
                // 살아 있으면 끝점이 적을 따라간다. 죽었으면(치워졌으면) 마지막으로 본 자리 그대로.
                TryTargetPoint(card.TargetId, ref card.To);
                float u = 1f - t;
                card.Object.transform.localPosition = u * u * card.From + 2f * u * t * card.Control + t * t * card.To;
                card.Spin += _config.SpinSpeed * deltaTime;
                card.Body.localRotation = Quaternion.Euler(0f, card.Spin, 0f);
                continue;
            }

            // 닿았다: 카드는 숨기고 꼬리가 다 사라질 때까지 둔다.
            if (card.Body.gameObject.activeSelf)
            {
                card.Object.transform.localPosition = card.To;
                card.Body.gameObject.SetActive(false);
                card.Trail.emitting = false;
            }
            if (card.Time < _config.FlightTime + _config.TrailTime) continue;

            Release(card);
            // 순서는 상관없으므로 마지막 것을 옮겨 와 지운다.
            _active[i] = _active[_active.Count - 1];
            _active.RemoveAt(_active.Count - 1);
        }
    }

    // 몬스터 Id의 맞는 자리(맵 로컬)를 point에 넣는다. 슬롯은 치우기로 당겨지므로 Id로 찾는다. 못 찾으면(죽어 치워졌으면) 그대로 둔다.
    private void TryTargetPoint(int id, ref Vector3 point)
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (states[i].Id != id) continue;
            Vector2 p = states[i].Position;
            point = new Vector3(p.x, _viewConfig.GetTileTop(TileType.Ground) + _monsterConfig.HitHeight, p.y);
            return;
        }
    }

    private Card CreateCard()
    {
        GameObject go = new GameObject("HeroCard");
        go.transform.SetParent(_root, false);
        go.SetActive(false);

        GameObject body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer renderer = body.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        // 머리 쪽이 굵고 진하며 끝으로 갈수록 가늘고 흐려진다(PathRunner와 같다). 색은 머티리얼 색(RendererTint)이 정한다.
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.startColor = Color.white;
        trail.endColor = new Color(1f, 1f, 1f, 0f);
        return new Card { Object = go, Body = body.transform, Trail = trail };
    }

    private void Release(Card card)
    {
        card.Trail.emitting = false;
        card.Object.SetActive(false);
        _idle.Push(card);
    }

    // 바닥과 나란히 누운 1×1 판(가로 x, 세로 z). 크기는 Body의 scale이 정한다.
    // 앞뒷면을 다 둔다 — 도는 동안 뒤집혀 보여도(카메라가 비스듬해도) 사라지지 않게.
    private static Mesh CreateCardMesh()
    {
        Mesh mesh = new Mesh { name = "HeroCard" };
        Vector3[] vertices =
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
        };
        Vector2[] uv =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
        };
        Vector3[] normals =
        {
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
        };
        int[] triangles = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
