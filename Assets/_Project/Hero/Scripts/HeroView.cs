using UnityEngine;

/// <summary>
/// 영웅 표시. 위치는 HeroMotor를 따라가기만 하고, 움직이는 방향으로 몸을 돌린다.
/// 모델에 동작 묶음이 있으면 이동 속도로 서기·달리기를 고르고(HeroAnimation), 연출이 앉기·점프를 시킨다.
/// </summary>
public sealed class HeroView
{
    private readonly Transform _parent;
    private readonly SO_HeroConfig _config;
    private readonly HeroStats _stats;
    private readonly Material _outline;
    private GameObject _instance;
    private HeroAnimation _animation;
    // 판 위 크기(ModelHeight로 맞춘 뒤). 연출이 배수를 곱한다.
    private Vector3 _baseScale = Vector3.one;
    // 다시 만들어도(캐릭터 변경·설정 수정) 연출이 시킨 동작을 이어 간다.
    private HeroPose _pose = HeroPose.Locomotion;

    /// <summary>모델 키(발 → 머리, 칸). 로비 카메라가 영웅을 화면에 얼마나 크게 담을지 정할 때 쓴다.</summary>
    public float Height { get; private set; } = 1f;

    /// <summary>지금 보이는 키(월드) — 연출이 곱한 크기(로비에서 크게)까지 넣은 값.</summary>
    public float WorldHeight
    {
        get
        {
            float scale = _baseScale.x > 0f ? _instance.transform.localScale.x / _baseScale.x : 1f;
            return Height * scale * (_parent != null ? _parent.lossyScale.x : 1f);
        }
    }

    /// <summary>모델 원점(발밑, 월드).</summary>
    public Vector3 FootWorld => _instance.transform.position;

    /// <summary>
    /// 몸 가운데(월드). Humanoid면 엉덩이 뼈 — 동작이 몸을 원점에서 옆으로 옮겨 서는 경우가 있어(VRoid 대기 동작 등) 원점보다 정확하다.
    /// Humanoid가 아니면 원점에서 키 절반 위.
    /// </summary>
    public Vector3 BodyWorld => _hips != null ? _hips.position : FootWorld + Vector3.up * (WorldHeight * 0.5f);

    // 몸 가운데를 잴 뼈(Humanoid가 아니면 null). 모델을 다시 만들 때 다시 찾는다.
    private Transform _hips;
    // 카드를 던지는 손(Humanoid가 아니거나 동작 묶음이 없으면 null).
    private Transform _hand;

    /// <summary>던진 카드가 나가는 자리(맵 로컬). 던지는 손이 없으면 null — 쓰는 쪽이 정한 높이를 쓴다.</summary>
    public Vector3? ThrowPoint => _hand != null && _parent != null ? _parent.InverseTransformPoint(_hand.position) : (Vector3?)null;

    /// <summary>지금 발 높이(맵 로컬). 주사위·칩 위를 밟고 지나가면 그 윗면으로 오르내린다(기획서 10.8).</summary>
    public float Floor { get; private set; }

    /// <summary>주사위에 오르내리느라 깡총 뛰는 중인지.</summary>
    public bool IsHopping => _hopTime >= 0f;

    // 깡총: 뛰기 시작한 발 높이 → 내려앉을 발 높이, 지난 시간(뛰지 않으면 음수).
    private float _hopFrom;
    private float _hopTo;
    private float _hopTime = -1f;
    // 놓을 칸으로 뛰어들 때는 내려앉을 높이를 처음부터 정해 둔다 — 발밑이 아직 앞 칸이라 따라가면 칸 경계에서 높이가 튄다.
    private bool _hopFixed;

    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.CharacterOutlineMaterial). 없으면 null.</param>
    public HeroView(Transform parent, SO_HeroConfig config, HeroStats stats, Material outline)
    {
        _parent = parent;
        _config = config;
        _stats = stats;
        _outline = outline;
        Rebuild();
    }

    /// <summary>캐릭터나 그 프리팹·색·동작이 바뀌었을 때 다시 만든다. 위치·방향은 이어받는다.</summary>
    public void Rebuild()
    {
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        if (_instance != null)
        {
            position = _instance.transform.localPosition;
            rotation = _instance.transform.localRotation;
        }
        Destroy();

        SO_HeroCharacter character = _stats.Character;
        _instance = HeroModel.Create(character, _parent, _outline, out float height);
        _instance.name = "Hero";
        _instance.transform.SetLocalPositionAndRotation(position, rotation);
        _baseScale = _instance.transform.localScale;
        Height = height;
        Animator animator = _instance.GetComponentInChildren<Animator>();
        _hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        _hand = animator != null && animator.isHuman && _config.Animations != null ? animator.GetBoneTransform(_config.Animations.AttackHand) : null;
        _animation = new HeroAnimation(_instance, _config.Animations);
        _animation.SetPose(_pose);
    }

    /// <summary>연출(로비·뛰어들기)이 몸을 직접 놓는다. scale은 판 위 크기의 배수(로비에서는 크게 앉아 있다가 뛰어들며 작아진다).</summary>
    public void Place(Vector3 localPosition, Quaternion localRotation, float scale)
    {
        Transform t = _instance.transform;
        t.SetLocalPositionAndRotation(localPosition, localRotation);
        t.localScale = _baseScale * scale;
    }

    public void SetPose(HeroPose pose)
    {
        _pose = pose;
        _animation?.SetPose(pose);
    }

    /// <summary>점프 동작의 진행도(0~1, 클립 시각). 연출이 포물선과 맞춰 넘긴다.</summary>
    public void SetJumpProgress(float normalized) => _animation?.SetJumpProgress(normalized);

    /// <summary>공격 동작을 시작한다(표시만). 동작이 끝날 때까지 던지는 손이 target(맵 로컬 x, z)을 향하게 몸을 돌린다.</summary>
    public void PlayAttack(float interval, Vector2 target)
    {
        _aimTarget = target;
        _animation?.PlayAttack(interval);
    }

    // 공격 중 노리는 자리(맵 로컬 x, z).
    private Vector2 _aimTarget;

    /// <summary>연출이 몸을 붙잡고 있는 동안에도 동작은 흘러야 해서 따로 부른다(서기·달리기는 멈춰 있는 것으로 본다).</summary>
    public void Animate(float deltaTime) => _animation?.Tick(deltaTime, 0f, 0f);

    /// <param name="floor">발밑 윗면 높이(주사위·칩 위면 그 윗면). deltaTime이 0이면 바로 맞춘다.</param>
    public void Sync(Vector2 position, Vector2 moveDelta, float deltaTime, float floor)
    {
        Transform t = _instance.transform;
        UpdateFloor(floor, deltaTime);
        t.localPosition = new Vector3(position.x, Floor, position.y);
        float strafe = Turn(position, moveDelta, deltaTime);
        _animation?.Tick(deltaTime, deltaTime > 0f ? moveDelta.magnitude / deltaTime : 0f, strafe);
    }

    // 몸은 가는 방향을 본다. 공격 중이면 던지는 손이 적을 향하게: 서 있으면 몸 전체를 돌리고,
    // 걷는 중이면 상체를 AimMaxTwist까지 비틀고 모자라는 만큼만 몸을 AimMaxBodyOffset까지 돌린다 — 다리는 몸 기준 옆걸음으로 달린다.
    // 몸 정면 기준 가는 방향(도, + = 오른쪽)을 돌려준다(달리기 섞기용, 서 있으면 0).
    private float Turn(Vector2 position, Vector2 moveDelta, float deltaTime)
    {
        // 0이 아니어도 아주 작은 이동(목적지에 거의 닿았을 때·벽에 비벼질 때)으로는 돌지 않는다 — 떨림으로 방향이 흔들리지 않게.
        // 기준은 Unity의 정규화 하한(kEpsilon)이라 조정 대상이 아니다.
        bool moving = moveDelta.sqrMagnitude > Vector3.kEpsilon;
        SO_HeroAnimationSet set = _config.Animations;
        bool aiming = set != null && _animation != null && _animation.IsAttacking;
        if (!moving && !aiming)
        {
            _animation?.SetTwist(0f);
            return 0f;
        }

        Transform t = _instance.transform;
        float bodyYaw = t.localEulerAngles.y;
        float moveYaw = moving ? Yaw(moveDelta) : bodyYaw;
        float targetYaw = moveYaw;
        // 던지는 손이 적을 향하는 몸 방향.
        float throwYaw = 0f;
        if (aiming)
        {
            Vector2 toTarget = _aimTarget - position;
            throwYaw = toTarget.sqrMagnitude > Vector3.kEpsilon ? Yaw(toTarget) - set.AttackAimYaw : bodyYaw;
            if (!moving) targetYaw = throwYaw;
            else
            {
                float need = Mathf.DeltaAngle(moveYaw, throwYaw);
                float excess = need - Mathf.Clamp(need, -set.AimMaxTwist, set.AimMaxTwist);
                targetYaw = moveYaw + Mathf.Clamp(excess, -set.AimMaxBodyOffset, set.AimMaxBodyOffset);
            }
        }
        t.localRotation = Quaternion.RotateTowards(t.localRotation, Quaternion.Euler(0f, targetYaw, 0f), _config.TurnSpeed * deltaTime);
        float bodyNow = t.localEulerAngles.y;
        float strafe = moving ? Mathf.DeltaAngle(bodyNow, moveYaw) : 0f;

        if (!aiming)
        {
            _animation?.SetTwist(0f);
            return strafe;
        }
        float twist = Mathf.DeltaAngle(bodyNow, throwYaw);
        _animation.SetTwist(Mathf.Clamp(twist, -set.AimMaxTwist, set.AimMaxTwist));
        return strafe;
    }

    // 맵 로컬 (x, z) 방향의 몸 방향(도, +z가 0, +x가 90).
    private static float Yaw(Vector2 direction) => Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

    /// <summary>높이 차와 상관없이 깡총 뛴다 — 내려앉을 곳은 발밑을 따라간다(주사위에서 주사위로 건너갈 때, 영웅은 토끼라 같은 높이여도 뛴다).</summary>
    public void Hop()
    {
        _hopFrom = Floor;
        _hopFixed = false;
        _hopTime = 0f;
    }

    /// <summary>높이 차와 상관없이 깡총 뛰어 landFloor 높이에 내려앉는다(놓을 칸으로 뛰어들 때 — 얹기·칩처럼 낮아도 놓았다는 것이 보이게).</summary>
    public void Hop(float landFloor)
    {
        _hopFrom = Floor;
        _hopTo = landFloor;
        _hopFixed = true;
        _hopTime = 0f;
    }

    // 낮은 단차(칩)는 걸어 오르고, 큰 단차(주사위)는 깡총 뛰어 오르내린다 — 주사위 높이를 미끄러지듯 오르면 떠 있는 것처럼 보인다.
    private void UpdateFloor(float floor, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            _hopTime = -1f;
            Floor = floor;
            return;
        }
        if (!IsHopping && Mathf.Abs(floor - Floor) > _config.HopThreshold)
        {
            _hopFrom = Floor;
            _hopFixed = false;
            _hopTime = 0f;
        }
        if (!IsHopping)
        {
            Floor = Mathf.MoveTowards(Floor, floor, _config.ClimbSpeed * deltaTime);
            return;
        }

        // 뛰는 동안 내려앉을 곳이 바뀌면(옆 칸으로 넘어감) 그쪽으로 내려앉는다.
        if (!_hopFixed) _hopTo = floor;
        _hopTime += deltaTime;
        float p = Mathf.Clamp01(_hopTime / _config.HopDuration);
        // 높은 쪽보다 HopHeight만큼 솟는 포물선: 두 높이 사이를 잇는 직선 + 가운데가 솟는 혹.
        float bump = _config.HopHeight + Mathf.Abs(_hopTo - _hopFrom) * 0.5f;
        Floor = Mathf.Lerp(_hopFrom, _hopTo, p) + 4f * bump * p * (1f - p);
        if (p >= 1f) _hopTime = -1f;
    }

    public void Destroy()
    {
        _animation?.Dispose();
        _animation = null;
        if (_instance != null) Object.Destroy(_instance);
        _instance = null;
    }
}
