using System;
using UnityEngine;

/// <summary>
/// 로비의 3D 쪽(기획서 12.2): 영웅이 판 크기보다 크게(HeroScale) 판 앞 난간에 걸터앉아 있고 카메라가 테이블을 크게 비춘다.
/// 게임 시작(Enter)이면 영웅이 일어서며 웅크렸다가 판 안 시작 자리로 뛰어들며 판 위 크기로 작아지고, 카메라는 로비 자리에서 게임 자리로
/// 옮겨 간다. 둘 다 끝나면 Entered. 크게 두는 이유: 판 위 크기로는 로비 화면 절반을 채우려면 카메라가 너무 붙어 테이블이 벽처럼 보인다.
///
/// 로비로(Exit)는 같은 연출을 거꾸로 재생한다 — 시간을 끝에서 0으로 되감으면 착지 → 공중(커지며 난간으로) → 일어서기 역(앉기) 순이 된다.
/// 모든 자세가 지금 시각에서 계산되므로 따로 짤 것이 없고, 들어갈 때와 나올 때 길·박자가 늘 같다. 끝나면 Exited(로비 화면을 띄울 때).
///
/// 로비를 따로 씬으로 두지 않는 이유: 로비 뒤의 테이블이 곧 판이라, 씬을 나누면 뛰어드는 도중에 불러오기로 끊긴다.
///
/// 몸의 자리는 코드가 옮기고(포물선), 자세는 동작이 맡는다(HeroAnimation). 점프 클립 하나를 웅크림·공중·착지 세 구간으로 나눠
/// 연출의 세 박자에 맞춰 재생한다 — 그래야 박자를 SO로 바꿔도 발이 땅을 떠나는 순간과 몸이 뜨는 순간이 맞는다.
///
/// 카메라: 게임 자리는 매번 MapCameraFitter가 계산한 것을 받아(ApplyCamera 직전에 맞춰 둔 카메라) 그 위에 로비 자리를 섞는다.
/// 그래서 옮겨 가는 도중에 화면이 돌거나 맵이 다시 만들어져도 도착점이 따라온다. 로비 자리는 게임 카메라와 같은 방향을 보고
/// (앞 가장자리 = 게임 화면의 아래쪽), 영웅 키가 화면에서 정한 비율이 되는 거리에 둔다.
/// 모든 자세는 지금 시각에서 바로 계산한다(쌓는 상태가 없어 도중에 맵·화면이 바뀌어도 어긋나지 않는다). 실제 시간으로 진행한다.
/// </summary>
public sealed class LobbyStage : IDisposable
{
    private readonly SO_LobbyConfig _config;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly Hero _hero;
    private readonly Transform _mapRoot;
    private readonly Func<MapData> _getMap;
    private readonly Func<Camera> _getCamera;
    private readonly Action _fitCamera;

    // -1이면 로비에 앉아 있다. 0 이상이면 뛰어들기 시작 후 흐른 시간(로비로 돌아가는 중이면 거꾸로 줄어든다).
    private float _time = -1f;
    private bool _exiting;
    // 게임 카메라가 보는 수평 방향(맵 로컬). ApplyCamera가 섞기 전의 게임 자리 회전에서 잰다 — 섞은 뒤의 카메라에서 재면 로비 쪽으로 기운 방향이 된다.
    private Vector3 _gameForward = Vector3.forward;

    /// <summary>뛰어들기와 카메라 이동이 끝났다(영웅을 놓아준 뒤).</summary>
    public event Action Entered;

    /// <summary>로비로 돌아오기(거꾸로 재생)가 끝났다 — 영웅이 난간에 앉았다.</summary>
    public event Action Exited;

    /// <param name="fitCamera">게임 자리로 카메라를 맞추는 동작. 맞춘 뒤 ApplyCamera를 부르는 쪽이다(GameScene.FitCamera).</param>
    public LobbyStage(SO_LobbyConfig config, SO_MapViewConfig viewConfig, Hero hero, Transform mapRoot,
        Func<MapData> getMap, Func<Camera> getCamera, Action fitCamera)
    {
        _config = config;
        _viewConfig = viewConfig;
        _hero = hero;
        _mapRoot = mapRoot;
        _getMap = getMap;
        _getCamera = getCamera;
        _fitCamera = fitCamera;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
    }

    /// <summary>
    /// 로비다운 정도: 1 = 로비 카메라, 0 = 게임 카메라. 카메라가 옮겨 가는 만큼 줄어든다(되돌아올 때는 늘어난다).
    /// 영웅 스포트라이트가 로비 모습과 게임 모습을 이 비율로 섞는다(HeroSpotlight).
    /// </summary>
    public float LobbyWeight => IsActive ? 1f - Smooth(CameraProgress()) : 0f;

    /// <summary>로비에 있거나 뛰어드는 중이다(판이 아직 시작되지 않았다).</summary>
    public bool IsActive { get; private set; }

    /// <summary>게임 시작을 눌러 뛰어드는 중이다.</summary>
    public bool IsEntering => IsActive && _time >= 0f && !_exiting;

    /// <summary>판에서 난간으로 돌아가는 중이다.</summary>
    public bool IsExiting => IsActive && _exiting;

    /// <summary>로비로: 영웅을 난간에 앉히고 카메라를 로비 자리에 둔다.</summary>
    public void Show()
    {
        IsActive = true;
        _time = -1f;
        _exiting = false;
        _hero.Held = true;
        _hero.SetPose(HeroPose.Sit);
        _fitCamera();
        PlaceHero();
    }

    /// <summary>게임 시작: 뛰어들기를 시작한다. 로비에 있지 않거나 이미 뛰는 중이면 무시한다.</summary>
    public void Enter()
    {
        if (!IsActive || _time >= 0f) return;
        _time = 0f;
        _hero.SetPose(HeroPose.Jump);
    }

    /// <summary>
    /// 로비로: 판 위 시작 자리에서 난간으로 뛰어오르고 카메라가 로비 자리로 빠진다(뛰어들기를 거꾸로). 이미 로비에 있으면 무시한다.
    /// 영웅 위치는 새 판의 시작 자리를 쓴다(맵을 다시 만들기 전에 불러도 된다 — 자세는 매 프레임 다시 계산한다).
    /// </summary>
    public void Exit()
    {
        if (IsActive) return;
        IsActive = true;
        _exiting = true;
        _time = EnterDuration;
        _hero.Held = true;
        _hero.SetPose(HeroPose.Jump);
    }

    /// <summary>
    /// 카메라가 게임 자리에 맞춰진 직후 부른다. 로비면 로비 자리로, 뛰어드는 중이면 그 사이로 옮긴다. 판 중에는 아무것도 하지 않는다.
    /// </summary>
    public void ApplyCamera()
    {
        Camera camera = _getCamera();
        MapData map = _getMap();
        if (!IsActive || camera == null || map == null) return;

        Transform t = camera.transform;
        Vector3 gamePosition = t.position;
        Quaternion gameRotation = t.rotation;
        float gameFov = camera.fieldOfView;

        GetLobbyCamera(camera, map, gameRotation, out Vector3 lobbyPosition, out Quaternion lobbyRotation);

        float blend = Smooth(CameraProgress());
        t.SetPositionAndRotation(Vector3.Lerp(lobbyPosition, gamePosition, blend), Quaternion.Slerp(lobbyRotation, gameRotation, blend));
        camera.fieldOfView = Mathf.Lerp(_config.FieldOfView, gameFov, blend);
    }

    private void Tick(float deltaTime)
    {
        if (!IsActive) return;
        if (_time >= 0f) _time += _exiting ? -deltaTime : deltaTime;
        if (_exiting && _time <= 0f)
        {
            // 되감기 끝: 난간에 앉은 로비 모습(Show와 같다).
            _time = -1f;
            _exiting = false;
            _hero.SetPose(HeroPose.Sit);
            _fitCamera();
            PlaceHero();
            Exited?.Invoke();
            return;
        }

        // 로비에 앉아 있을 때는 화면·설정이 바뀔 때만 다시 맞추면 되지만, 계산이 싸고 영웅 모델(키)이 바뀌는 경우도 있어 늘 맞춘다.
        _fitCamera();
        PlaceHero();

        if (!_exiting && _time >= 0f && _time >= EnterDuration)
        {
            IsActive = false;
            _time = -1f;
            // 판 안 시작 자리에 선 자세로 놓고 놓아준다 — 이후로는 Hero가 자기 위치를 따라 몸을 놓는다.
            MapData map = _getMap();
            if (map != null) _hero.PlaceView(GroundFoot(), LandFacing(map), 1f);
            _hero.SetPose(HeroPose.Locomotion);
            _hero.Held = false;
            Entered?.Invoke();
        }
    }

    private float JumpStart => _config.StandUpDuration;
    private float JumpEnd => JumpStart + _config.JumpDuration;
    private float EnterDuration =>
        Mathf.Max(JumpEnd + _config.LandDuration, JumpStart + _config.CameraDelay + _config.CameraDuration);

    private float CameraProgress()
    {
        if (_time < 0f) return 0f;
        return Mathf.Clamp01((_time - JumpStart - _config.CameraDelay) / _config.CameraDuration);
    }

    private void PlaceHero()
    {
        MapData map = _getMap();
        if (map == null) return;

        float big = _config.HeroScale;
        Vector3 seat = SeatFoot(map);
        Vector3 from = EdgeFoot(map, _config.HeroStandHeight);
        Vector3 to = GroundFoot();
        // 로비에서는 카메라를 마주 본다(카메라는 게임 카메라와 같은 방향을 본다).
        Quaternion facing = FacingAlong(-_gameForward);
        SO_HeroAnimationSet animations = _hero.Animations;
        float takeoff = animations != null ? animations.JumpTakeoff : 0f;
        float land = animations != null ? animations.JumpLand : 1f;

        if (_time < 0f)
        {
            _hero.PlaceView(seat, facing, big);
            return;
        }

        if (_time < JumpStart)
        {
            // 일어서기: 앉은 높이에서 난간 위로 올라서며 점프 클립의 웅크림 구간을 재생한다.
            float s = _config.StandUpDuration > 0f ? Mathf.Clamp01(_time / _config.StandUpDuration) : 1f;
            _hero.PlaceView(Vector3.Lerp(seat, from, Smooth(s)), facing, big);
            _hero.SetJumpProgress(s * takeoff);
            return;
        }

        if (_time < JumpEnd)
        {
            // 포물선. 높은 쪽 자리에서 더 솟고, 날아가는 방향으로 몸을 돌린다.
            float u = Mathf.Clamp01((_time - JumpStart) / _config.JumpDuration);
            Vector3 flat = Vector3.Lerp(from, to, u);
            float baseHeight = Mathf.Lerp(from.y, to.y, u);
            float apex = Mathf.Abs(from.y - to.y) + _config.JumpHeight;
            Vector3 position = new Vector3(flat.x, baseHeight + apex * 4f * u * (1f - u), flat.z);
            // 뛰어오른 직후에 날아가는 방향으로 돌고(처음 1/3 동안), 나는 동안 판 위 크기로 줄어든다.
            float scale = Mathf.Lerp(big, 1f, Smooth(u));
            _hero.PlaceView(position, Quaternion.Slerp(facing, LandFacing(map), Mathf.Clamp01(u * 3f)), scale);
            _hero.SetJumpProgress(Mathf.Lerp(takeoff, land, u));
            return;
        }

        // 착지: 클립의 착지 구간을 재생한다.
        float l = _config.LandDuration > 0f ? Mathf.Clamp01((_time - JumpEnd) / _config.LandDuration) : 1f;
        _hero.PlaceView(to, LandFacing(map), 1f);
        _hero.SetJumpProgress(Mathf.Lerp(land, 1f, l));
    }

    // 날아간 방향(난간 → 시작 자리)을 본다. 두 자리가 겹치면 판 안쪽을 본다.
    private Quaternion LandFacing(MapData map)
    {
        Vector3 travel = GroundFoot() - EdgeFoot(map, 0f);
        travel.y = 0f;
        return FacingAlong(travel.sqrMagnitude > 0f ? travel : _gameForward);
    }

    // 로비에 앉아 있을 때 발(모델 원점) 위치, 맵 로컬.
    // 앉기 동작은 발 위에 엉덩이가 떠 있으므로, 다리를 늘어뜨리게 발을 난간보다 키 비율만큼 내린다(크기가 바뀌어도 같은 자세).
    private Vector3 SeatFoot(MapData map) =>
        EdgeFoot(map, _config.HeroStandHeight - _config.HeroSitDrop * _hero.Height * _config.HeroScale);

    // 판 안 시작 자리(Hero.Respawn이 정한 위치)의 발 위치, 맵 로컬.
    private Vector3 GroundFoot()
    {
        Vector2 position = _hero.Position;
        return new Vector3(position.x, _viewConfig.GetTileTop(TileType.Ground), position.y);
    }

    // 난간 위 발(모델 원점) 위치, 맵 로컬. 앞 가장자리 = 게임 카메라가 보는 방향의 반대쪽 가장자리.
    private Vector3 EdgeFoot(MapData map, float height)
    {
        Vector3 center = new Vector3((map.Width - 1) * 0.5f, 0f, (map.Height - 1) * 0.5f);
        // 맵은 축에 맞춘 사각형이라 forward 방향으로 가장자리까지의 거리는 두 반폭의 투영 합이다.
        float extent = Mathf.Abs(_gameForward.x) * map.Width * 0.5f + Mathf.Abs(_gameForward.z) * map.Height * 0.5f;
        Vector3 right = new Vector3(_gameForward.z, 0f, -_gameForward.x);
        Vector3 foot = center - _gameForward * (extent + _config.HeroEdgeOffset) + right * _config.HeroSideOffset;
        foot.y = height;
        return foot;
    }

    private void GetLobbyCamera(Camera camera, MapData map, Quaternion gameRotation, out Vector3 position, out Quaternion rotation)
    {
        Vector3 worldForward = gameRotation * Vector3.forward;
        Vector3 localForward = _mapRoot.InverseTransformDirection(worldForward);
        localForward.y = 0f;
        _gameForward = localForward.sqrMagnitude > 0f ? localForward.normalized : Vector3.forward;

        bool portrait = camera.aspect < 1f;
        float tableWidth = portrait ? _config.PortraitTableScreenWidth : _config.TableScreenWidth;
        Vector2 viewport = portrait ? _config.PortraitHeroViewport : _config.HeroViewport;

        Vector3 flatForward = _mapRoot.TransformDirection(_gameForward);
        rotation = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(_config.CameraTilt, 0f, 0f);

        // 거리는 테이블 폭으로 정한다 — 영웅 키로 정하면 영웅을 키울수록 카메라가 물러나 판만 작아진다.
        // 테이블 폭 = 판의 화면 가로 방향 길이(축에 맞춘 사각형의 투영).
        float width = Mathf.Abs(_gameForward.z) * map.Width + Mathf.Abs(_gameForward.x) * map.Height;
        float tan = Mathf.Tan(_config.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float distance = width / (tableWidth * 2f * tan * camera.aspect);
        float frustumHeight = 2f * distance * tan;

        // 화면 자리를 맞추는 점은 일어선 영웅의 몸(앉아도, 일어서도 크게 벗어나지 않게).
        float height = _hero.Height * _config.HeroScale;
        Vector3 focus = _mapRoot.TransformPoint(EdgeFoot(map, _config.HeroStandHeight) + Vector3.up * (height * _config.HeroFocusHeight));

        // 영웅이 화면 가운데가 아니라 정한 자리에 오도록 카메라를 반대쪽으로 민다.
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        position = focus - rotation * Vector3.forward * distance
            + right * ((0.5f - viewport.x) * frustumHeight * camera.aspect)
            + up * ((0.5f - viewport.y) * frustumHeight);
    }

    private static Quaternion FacingAlong(Vector3 direction) =>
        direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity;

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
