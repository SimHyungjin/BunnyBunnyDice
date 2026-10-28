using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 표시. 슬롯 순서가 MonsterField의 상태 배열과 같고, 매 프레임 상태를 읽어 오브젝트를 옮기기만 한다.
/// 표시를 상태·규칙과 떼어 둔 이유: 수가 많아지면 이 클래스만 GPU 인스턴싱(오브젝트 없이 그리기)으로 바꾸려고.
/// 오브젝트는 종류별로 재사용한다 — 웨이브마다 수십~수백 마리를 만들고 지우지 않게.
/// 코어 풀(PoolManager)을 쓰지 않는 이유: 코어 풀은 Addressables 주소로 꺼내는데 몬스터 프리팹은 종류 SO가 직접 참조하고,
/// 이 클래스는 통째로 GPU 인스턴싱(오브젝트 없음)으로 바뀔 예정이라 오브젝트 풀은 그때까지만 쓴다.
/// </summary>
public sealed class MonsterRenderer
{
    private readonly SO_MonsterConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly Transform _root;
    // 모양 번호(MonsterState.TypeIndex)별로 쉬고 있는 오브젝트.
    private readonly List<Stack<GameObject>> _idle = new List<Stack<GameObject>>();
    private GameObject[] _active;
    private int[] _activeType;
    // 포켓에서 나오는 연출(표시만): 슬롯마다 나온 뒤 지난 시간·출발점·원래 크기. 출발점이 없으면(분열 조각) 연출하지 않는다.
    private float[] _age;
    private Vector3[] _origin;
    private bool[] _emerging;
    private float[] _scale;
    // 스킬로 옮겨진 몬스터의 미끄러짐(레이크·백스핀, 표시만): 판정은 새 자리에서 바로 하고 몸체만 옛 자리에서 따라온다
    // — 순간이동하면 무슨 일이 일어났는지 보이지 않는다. 남은 거리(옛 자리 − 새 자리)·지난 시간·걸리는 시간(0이면 없음)·가속 여부.
    private Vector3[] _slideOffset;
    private float[] _slideAge;
    private float[] _slideDuration;
    private bool[] _slideAccelerate;
    // 죽는 연출(표시만): 치울 때 바로 쉬게 하지 않고 떠나는 연출을 한 뒤 쉬게 한다. 걸리는 시간이 0이면 연출 없이 사라진다(목표물 도착).
    // 날아가기(포켓 스킬 처치·보스)는 이 구멍으로, 아니면(일반 사망) 제자리에서 톡 튀며 작아진다.
    private bool[] _leaveFly;
    private Vector3[] _leaveTarget;
    private float[] _leaveDuration;
    private float[] _leaveHeight;
    private float[] _leaveShrink;

    // 슬롯에서 빠진 뒤 떠나는 연출 중인 오브젝트. 끝나면 쉬게 한다.
    private struct Leaving
    {
        public GameObject Object;
        public int Type;
        public bool Fly;
        public Vector3 From;
        public Vector3 Hole;
        public float Scale;
        public float Age;
        public float Duration;
        public float Height;
        public float Shrink;
    }

    private readonly List<Leaving> _leaving = new List<Leaving>();

    // 툰 아웃라인 머티리얼. 없으면 null.
    private readonly Material _outline;

    public MonsterRenderer(Transform parent, SO_MonsterConfig config, SO_MapViewConfig viewConfig, int capacity, Material outline)
    {
        _config = config;
        _outline = outline;
        _viewConfig = viewConfig;
        _root = new GameObject("Monsters").transform;
        _root.SetParent(parent, false);
        _active = new GameObject[capacity];
        _activeType = new int[capacity];
        _age = new float[capacity];
        _origin = new Vector3[capacity];
        _emerging = new bool[capacity];
        _scale = new float[capacity];
        _slideOffset = new Vector3[capacity];
        _slideAge = new float[capacity];
        _slideDuration = new float[capacity];
        _slideAccelerate = new bool[capacity];
        _leaveFly = new bool[capacity];
        _leaveTarget = new Vector3[capacity];
        _leaveDuration = new float[capacity];
        _leaveHeight = new float[capacity];
        _leaveShrink = new float[capacity];
    }

    /// <summary>
    /// slot(= 지금까지의 마릿수)에 새 몬스터 오브젝트를 둔다. 처음부터 길 쪽을 보게 바로 맞춘다.
    /// origin이 있으면 그 자리(포켓 구멍)에서 솟아 나오는 연출을 한다.
    /// </summary>
    public void Add(int slot, MonsterKind kind, in MonsterState state, Vector3? origin = null)
    {
        if (slot >= _active.Length)
        {
            System.Array.Resize(ref _active, _active.Length * 2);
            System.Array.Resize(ref _activeType, _activeType.Length * 2);
            System.Array.Resize(ref _age, _age.Length * 2);
            System.Array.Resize(ref _origin, _origin.Length * 2);
            System.Array.Resize(ref _emerging, _emerging.Length * 2);
            System.Array.Resize(ref _scale, _scale.Length * 2);
            System.Array.Resize(ref _slideOffset, _slideOffset.Length * 2);
            System.Array.Resize(ref _slideAge, _slideAge.Length * 2);
            System.Array.Resize(ref _slideDuration, _slideDuration.Length * 2);
            System.Array.Resize(ref _slideAccelerate, _slideAccelerate.Length * 2);
            System.Array.Resize(ref _leaveFly, _leaveFly.Length * 2);
            System.Array.Resize(ref _leaveTarget, _leaveTarget.Length * 2);
            System.Array.Resize(ref _leaveDuration, _leaveDuration.Length * 2);
            System.Array.Resize(ref _leaveHeight, _leaveHeight.Length * 2);
            System.Array.Resize(ref _leaveShrink, _leaveShrink.Length * 2);
        }
        while (_idle.Count <= state.TypeIndex) _idle.Add(new Stack<GameObject>());

        GameObject go;
        Stack<GameObject> idle = _idle[state.TypeIndex];
        if (idle.Count > 0)
        {
            go = idle.Pop();
            go.SetActive(true);
        }
        else
        {
            go = Object.Instantiate(kind.Base.Prefab, _root, false);
            go.name = $"Monster_{kind.Name}";
            // 오브젝트는 재사용하므로 만들 때 한 번만 붙인다.
            OutlineMesh.AttachToModel(go, _outline);
        }
        // 몸 무늬는 특성 색뿐이다 — 상태 이상은 체력바 아래에 따로 보인다(MonsterHealthBars).
        // 꺼낼 때마다 색·크기를 다시 읽는 이유: 플레이 중 특성 SO를 고치면 다음에 나오는 몬스터부터 반영되게.
        kind.GetBallColors(out Color band, out Color cap);
        RendererTint.Apply(go, Color.white, GetBallTexture(band, cap));
        go.transform.localScale = Vector3.one * kind.VisualScale;

        _active[slot] = go;
        _activeType[slot] = state.TypeIndex;
        _scale[slot] = kind.VisualScale;
        _age[slot] = 0f;
        _emerging[slot] = origin.HasValue && _config.EmergeDuration > 0f;
        _origin[slot] = origin ?? Vector3.zero;
        _slideDuration[slot] = 0f;
        _leaveDuration[slot] = 0f;
        Place(go.transform, state);
        if (_emerging[slot]) Emerge(slot, go.transform);
        Vector2 facing = state.Waypoint - state.Position;
        if (facing.sqrMagnitude > 0f) go.transform.localRotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y));
    }

    /// <summary>
    /// slot의 몸체를 지금 보이는 자리에서 to(새 자리)까지 duration 동안 미끄러지게 한다(표시만 — 상태는 이미 to에 있다).
    /// accelerate면 점점 빨라지고(끌려 들어감), 아니면 점점 느려진다(튕겨 나감). 이미 미끄러지는 중이면 지금 보이는 자리에서 다시 시작한다.
    /// </summary>
    public void Slide(int slot, Vector2 to, float duration, bool accelerate)
    {
        if (duration <= 0f) return;
        Vector3 shown = _active[slot].transform.localPosition;
        _slideOffset[slot] = new Vector3(shown.x - to.x, 0f, shown.z - to.y);
        _slideAge[slot] = 0f;
        _slideDuration[slot] = duration;
        _slideAccelerate[slot] = accelerate;
    }

    /// <summary>
    /// slot이 치워질 때 쉬게 하지 않고 hole(맵 로컬, 구멍 가운데)로 날아 들어가게 한다(포켓 스킬).
    /// 판 주사위가 포켓으로 날아가는 연출(BoardRollPresenter)과 같은 궤적이다 — height까지 솟았다가, 끝의 shrinkPortion 동안 작아지며 구멍에 꽂힌다.
    /// </summary>
    public void Sink(int slot, Vector3 hole, float duration, float height, float shrinkPortion)
    {
        _leaveFly[slot] = true;
        _leaveTarget[slot] = hole;
        _leaveDuration[slot] = duration;
        _leaveHeight[slot] = height;
        _leaveShrink[slot] = shrinkPortion;
    }

    /// <summary>slot이 치워질 때 제자리에서 hop만큼 톡 튀며 작아져 사라지게 한다(일반 사망) — 짧고 제자리라 많이 죽어도 판이 어지럽지 않다.</summary>
    public void Pop(int slot, float duration, float hop)
    {
        _leaveFly[slot] = false;
        _leaveDuration[slot] = duration;
        _leaveHeight[slot] = hop;
    }

    /// <summary>치워질 때의 연출(Sink·Pop)이 이미 정해졌는지.</summary>
    public bool IsLeaving(int slot) => _leaveDuration[slot] > 0f;

    /// <summary>slot을 비우고 마지막 슬롯을 그 자리로 옮긴다(상태 배열의 지우기와 같은 순서). 떠나는 연출이 정해져 있으면 그 연출 뒤에 쉬게 한다.</summary>
    public void RemoveAt(int slot, int last)
    {
        if (_leaveDuration[slot] > 0f) StartLeave(slot);
        else Release(slot);
        _active[slot] = _active[last];
        _activeType[slot] = _activeType[last];
        _age[slot] = _age[last];
        _origin[slot] = _origin[last];
        _emerging[slot] = _emerging[last];
        _scale[slot] = _scale[last];
        _slideOffset[slot] = _slideOffset[last];
        _slideAge[slot] = _slideAge[last];
        _slideDuration[slot] = _slideDuration[last];
        _slideAccelerate[slot] = _slideAccelerate[last];
        _leaveFly[slot] = _leaveFly[last];
        _leaveTarget[slot] = _leaveTarget[last];
        _leaveDuration[slot] = _leaveDuration[last];
        _leaveHeight[slot] = _leaveHeight[last];
        _leaveShrink[slot] = _leaveShrink[last];
        _active[last] = null;
    }

    public void Clear(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Release(i);
            _active[i] = null;
        }
        for (int i = 0; i < _leaving.Count; i++) Rest(_leaving[i].Object, _leaving[i].Type);
        _leaving.Clear();
    }

    public void Sync(MonsterState[] states, int count, float deltaTime)
    {
        float maxTurn = _config.TurnSpeed * deltaTime;
        for (int i = 0; i < count; i++)
        {
            Transform t = _active[i].transform;
            Place(t, states[i]);
            if (_emerging[i])
            {
                _age[i] += deltaTime;
                Emerge(i, t);
            }
            if (_slideDuration[i] > 0f) SlideStep(i, t, deltaTime);
            Vector2 heading = states[i].Heading;
            if (heading.sqrMagnitude <= 0f) continue;
            Quaternion target = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.y));
            t.localRotation = Quaternion.RotateTowards(t.localRotation, target, maxTurn);
        }
        TickLeaving(deltaTime);
    }

    private void SlideStep(int slot, Transform t, float deltaTime)
    {
        _slideAge[slot] += deltaTime;
        float progress = Mathf.Clamp01(_slideAge[slot] / _slideDuration[slot]);
        float eased = _slideAccelerate[slot] ? progress * progress : 1f - (1f - progress) * (1f - progress);
        t.localPosition += _slideOffset[slot] * (1f - eased);
        if (progress >= 1f) _slideDuration[slot] = 0f;
    }

    private void StartLeave(int slot)
    {
        GameObject go = _active[slot];
        if (go == null) return;
        _leaving.Add(new Leaving
        {
            Object = go,
            Type = _activeType[slot],
            Fly = _leaveFly[slot],
            From = go.transform.localPosition,
            Hole = _leaveTarget[slot],
            Scale = go.transform.localScale.x,
            Duration = _leaveDuration[slot],
            Height = _leaveHeight[slot],
            Shrink = _leaveShrink[slot],
        });
    }

    // 날아가기: 판 주사위가 포켓으로 날아가는 궤적 — 높은 포물선으로 구멍 안 깊이(EmergeDepth — 나올 때와 같은 구멍 깊이)까지,
    // 끝 무렵 나올 때의 크기(EmergeStartScale)로 줄어들며 쏙 들어간다.
    // 톡 사라지기: 제자리에서 낮게 튀며 점점 빨리 작아진다.
    private void TickLeaving(float deltaTime)
    {
        for (int i = _leaving.Count - 1; i >= 0; i--)
        {
            Leaving sink = _leaving[i];
            sink.Age += deltaTime;
            float progress = Mathf.Clamp01(sink.Age / sink.Duration);
            if (progress >= 1f)
            {
                Rest(sink.Object, sink.Type);
                _leaving[i] = _leaving[_leaving.Count - 1];
                _leaving.RemoveAt(_leaving.Count - 1);
                continue;
            }
            _leaving[i] = sink;

            Transform t = sink.Object.transform;
            if (!sink.Fly)
            {
                t.localPosition = sink.From + Vector3.up * (sink.Height * 4f * progress * (1f - progress));
                t.localScale = Vector3.one * (sink.Scale * (1f - progress * progress));
                continue;
            }
            Vector3 flat = Vector3.Lerp(sink.From, sink.Hole, progress);
            float baseHeight = Mathf.Lerp(sink.From.y, sink.Hole.y + _config.EmergeDepth, progress);
            t.localPosition = new Vector3(flat.x, baseHeight + sink.Height * 4f * progress * (1f - progress), flat.z);
            float shrink = Mathf.Clamp01((progress - (1f - sink.Shrink)) / sink.Shrink);
            t.localScale = Vector3.one * (sink.Scale * Mathf.Lerp(1f, _config.EmergeStartScale, shrink));
        }
    }

    // 구멍(출발점)에서 실제 자리까지 끌어오며 커진다. 실제 자리는 그동안에도 움직이므로 매 프레임 지금 자리를 향해 섞는다.
    // 처음엔 빠르고 끝에서 느려지게(ease-out) 해 튕겨 나와 자리를 잡는 느낌을 준다.
    private void Emerge(int slot, Transform t)
    {
        float duration = _config.EmergeDuration;
        float progress = duration > 0f ? Mathf.Clamp01(_age[slot] / duration) : 1f;
        if (progress >= 1f)
        {
            _emerging[slot] = false;
            t.localScale = Vector3.one * _scale[slot];
            return;
        }

        float eased = 1f - (1f - progress) * (1f - progress);
        Vector3 from = _origin[slot] + Vector3.up * _config.EmergeDepth;
        Vector3 position = Vector3.LerpUnclamped(from, t.localPosition, eased);
        position.y += Mathf.Sin(progress * Mathf.PI) * _config.EmergeHop;
        t.localPosition = position;
        t.localScale = Vector3.one * (_scale[slot] * Mathf.Lerp(_config.EmergeStartScale, 1f, eased));
    }

    // 띠 공 무늬 텍스처: 세로 한 줄(구 메시의 V = 남극 → 북극). 띠는 적도 둘레, 나머지는 양끝 색.
    // 프리팹의 몸통 구체는 극이 옆(진행 방향에 수직)을 향하게 돌려 두어, 굴러가는 축과 띠의 축이 같다 — 굴려도 겉모습이 변하지 않아 구르기 연출이 필요 없다.
    // 셰이더를 따로 두지 않고 Lit 텍스처로 칠하는 이유: 판 위 조명(팔로우 스포트)·그림자를 다른 물체와 똑같이 받게.
    // 같은 색 조합은 하나를 같이 쓴다(조합은 웨이브마다 몇 가지뿐). 띠 각도도 키에 넣어 GameTuning에서 고치면 새로 만든다.
    private const int BallTextureHeight = 128;
    private readonly Dictionary<(Color, Color, float), Texture2D> _ballTextures = new Dictionary<(Color, Color, float), Texture2D>();
    private readonly Color[] _ballPixels = new Color[BallTextureHeight];

    private Texture2D GetBallTexture(Color band, Color cap)
    {
        float halfAngle = _config.BandHalfAngle;
        var key = (band, cap, halfAngle);
        if (_ballTextures.TryGetValue(key, out Texture2D texture)) return texture;

        for (int y = 0; y < BallTextureHeight; y++)
        {
            // V는 위도에 비례한다(-90° → 0, 90° → 1).
            float latitude = ((y + 0.5f) / BallTextureHeight - 0.5f) * 180f;
            _ballPixels[y] = Mathf.Abs(latitude) < halfAngle ? band : cap;
        }
        texture = new Texture2D(1, BallTextureHeight, TextureFormat.RGBA32, false)
        {
            name = "MonsterBall",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels(_ballPixels);
        texture.Apply(false, true);
        _ballTextures.Add(key, texture);
        return texture;
    }

    private void Place(Transform t, in MonsterState state) =>
        // 몬스터가 걷는 칸(바닥·출현 구역·목표물)은 윗면 높이가 모두 바닥과 같다.
        t.localPosition = new Vector3(state.Position.x, _viewConfig.GetTileTop(TileType.Ground), state.Position.y);

    private void Release(int slot)
    {
        if (_active[slot] != null) Rest(_active[slot], _activeType[slot]);
    }

    private void Rest(GameObject go, int type)
    {
        go.SetActive(false);
        _idle[type].Push(go);
    }
}
