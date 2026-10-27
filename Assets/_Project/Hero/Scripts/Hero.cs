using System;
using UnityEngine;

/// <summary>
/// 영웅 한 명. 목적지 칸 중심으로 곧게 걸어간다(이동·충돌은 HeroMotor).
/// 판 안은 주사위·칩·목표물·출현 구역까지 모두 밟고 지나가므로(기획서 10.8) 막힌 곳 없는 직사각형이라 길찾기가 필요 없다
/// — 판 안 벽(TileType.Wall)을 만들게 되면 그때 길찾기를 다시 둔다.
/// 어디로 갈지는 탭 이동(HeroTapInput)이 정한다.
/// </summary>
public sealed class Hero : IDisposable
{
    private readonly SO_HeroConfig _config;
    private readonly HeroStats _stats;
    private readonly Func<MapData> _getMap;
    private readonly Func<Vector2, float> _getFloor;
    private readonly HeroMotor _motor;
    private readonly HeroView _view;

    private bool _hasDestination;
    private Vector2Int _destination;
    // 지난 프레임에 서 있던 칸. 주사위 칸을 드나들면 깡총 뛴다.
    private Vector2Int _lastCell;

    /// <param name="getFloor">맵 로컬 (x, z) 자리의 밟고 서는 높이(주사위·칩·목표물이 있으면 그 윗면).</param>
    /// <param name="outline">툰 아웃라인 머티리얼. 없으면 null.</param>
    public Hero(Transform parent, SO_HeroConfig config, HeroStats stats, Func<MapData> getMap, Func<Vector2, float> getFloor, Material outline)
    {
        _config = config;
        _stats = stats;
        _getMap = getMap;
        _getFloor = getFloor;
        _motor = new HeroMotor(config);
        _view = new HeroView(parent, config, stats, outline);
        Core.Loop.OnGameUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnGameUpdate -= Tick;
        _view.Destroy();
    }

    /// <summary>맵 로컬 (x, z). 공격(HeroAttack)이 사거리 기준으로 쓴다.</summary>
    public Vector2 Position => _motor.Position;

    /// <summary>모델 키(칸).</summary>
    public float Height => _view.Height;

    /// <summary>지금 보이는 키(월드, 연출 크기 포함).</summary>
    public float ViewHeight => _view.WorldHeight;

    /// <summary>몸이 서 있는 발밑(월드). 높이는 모델 원점, 가로 자리는 몸 가운데 — 동작이 몸을 원점에서 옮겨 세워도 몸 아래를 가리킨다.</summary>
    public Vector3 ViewFoot
    {
        get
        {
            Vector3 body = _view.BodyWorld;
            return new Vector3(body.x, _view.FootWorld.y, body.z);
        }
    }

    /// <summary>몸 가운데(월드).</summary>
    public Vector3 ViewBody => _view.BodyWorld;

    /// <summary>지금 발 높이(맵 로컬) — 주사위 위에 서 있으면 그 윗면. 공격선이 여기서 나간다.</summary>
    public float Floor => _view.Floor;

    /// <summary>
    /// 로비·뛰어들기 연출이 몸을 붙잡고 있다. 그동안은 걷지 않고(탭 이동도 받지 않는다) 몸을 PlaceView로만 놓는다.
    /// 위치(Position)는 판 안 시작 자리 그대로라 놓아주면 그 자리에서 이어 걷는다.
    /// </summary>
    public bool Held { get; set; }

    /// <summary>붙잡힌 동안 몸을 놓는다(맵 로컬). scale은 판 위 크기의 배수 — 놓아주기 전에 1로 되돌린다.</summary>
    public void PlaceView(Vector3 localPosition, Quaternion localRotation, float scale) => _view.Place(localPosition, localRotation, scale);

    /// <summary>지금 캐릭터의 동작 묶음(없으면 null). 연출이 점프 클립 구간을 맞출 때 쓴다.</summary>
    public SO_HeroAnimationSet Animations => _stats.Character.Animations;

    /// <summary>연출이 시키는 동작(앉기·점프). 판에서는 Locomotion — 이동 속도로 서기·달리기를 고른다.</summary>
    public void SetPose(HeroPose pose) => _view.SetPose(pose);

    /// <summary>점프 동작의 진행도(0~1). 연출이 포물선과 맞춰 넘긴다.</summary>
    public void SetJumpProgress(float normalized) => _view.SetJumpProgress(normalized);

    /// <summary>공격 동작(표시만) — target(맵 로컬 x, z)을 향해 던진다. 판정은 HeroAttack이 SO_HeroAnimationSet.AttackWindup 뒤에 한다.</summary>
    public void PlayAttack(float interval, Vector2 target) => _view.PlayAttack(interval, target);

    /// <summary>던진 카드가 나가는 자리(맵 로컬, 던지는 손). 손을 못 찾으면 null.</summary>
    public Vector3? ThrowPoint => _view.ThrowPoint;

    public void RebuildView() => _view.Rebuild();

    /// <summary>새 판: 목표물에서 가장 가까운 바닥에 세운다(목표물 위가 아니라). 로비의 뛰어들기도 이 자리로 내려앉는다.</summary>
    public void Respawn()
    {
        MapData map = _getMap();
        Vector2 goalCenter = map.GoalRect.center - new Vector2(0.5f, 0.5f);
        ClearDestination();
        _motor.Teleport(HeroTerrain.FindNearest(map, goalCenter, HeroTerrain.Ground));
        _lastCell = GridCircle.CellOf(_motor.Position);
        // 붙잡혀 있으면 몸은 연출 자리에 둔다 — 여기서 시작 자리로 옮기면 연출이 다시 놓기 전 한 프레임 튄다.
        if (!Held) _view.Sync(_motor.Position, Vector2.zero, 0f, _getFloor(_motor.Position));
    }

    /// <summary>목적지 칸으로 걸어간다. 판 밖이면 false이고 하던 이동을 그대로 한다.</summary>
    public bool MoveTo(Vector2Int cell)
    {
        MapData map = _getMap();
        if (Held || map == null || !HeroTerrain.IsPassable(map, cell.x, cell.y)) return false;
        _hasDestination = true;
        _destination = cell;
        return true;
    }

    /// <summary>깡총 뛰는 중인지(주사위에 오르내리는 중). 영웅 오버드라이브는 내려앉은 뒤부터 센다.</summary>
    public bool IsHopping => _view.IsHopping;

    private void ClearDestination() => _hasDestination = false;

    // 게임 시간을 따른다 — 카드를 집는 동안의 감속(게임 속도 50%)을 영웅도 똑같이 받는다.
    private void Tick(float deltaTime)
    {
        MapData map = _getMap();
        if (map == null) return;

        // 붙잡혀 있으면 몸은 연출이 놓고, 동작만 흘린다.
        if (Held)
        {
            _view.Animate(deltaTime);
            return;
        }
        Vector2 delta = _hasDestination ? WalkToDestination(deltaTime) : Vector2.zero;

        if (delta.sqrMagnitude > 0f) _motor.Move(map, delta);
        HopAcrossDice();
        _view.Sync(_motor.Position, delta, deltaTime, _getFloor(_motor.Position));
    }

    // 영웅은 토끼다 — 주사위 칸을 드나들 때는 높이가 같아도(주사위 → 주사위) 칸마다 깡총 뛴다. 높이 차로 뛰는 것(HeroView)만으로는
    // 같은 높이 주사위끼리 건널 때 미끄러지듯 지나간다. 칩처럼 낮은 칸끼리는 걸어 지나간다.
    private void HopAcrossDice()
    {
        Vector2Int cell = GridCircle.CellOf(_motor.Position);
        if (cell == _lastCell) return;
        Vector2Int last = _lastCell;
        _lastCell = cell;
        if (_view.IsHopping) return;
        // 판 바닥 윗면은 높이 0이다(SO_MapViewConfig.GetTileTop) — 바닥에서 HopThreshold보다 높으면 주사위 칸이다.
        if (_getFloor(cell) > _config.HopThreshold || _getFloor(last) > _config.HopThreshold) _view.Hop();
    }

    // 목적지 칸 중심으로 곧게 걷는다. 도착 거리 안에 들면 중심에 딱 맞춰 선다(칸 사이에 어중간하게 서지 않게).
    private Vector2 WalkToDestination(float deltaTime)
    {
        Vector2 to = (Vector2)_destination - _motor.Position;
        float distance = to.magnitude;
        if (distance <= _config.ArriveDistance)
        {
            ClearDestination();
            return to;
        }
        return to / distance * Mathf.Min(_stats.MoveSpeed * deltaTime, distance);
    }
}
