using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>영웅이 지금 해야 할 동작. 서기·달리기는 이동 속도로 저절로 고른다.</summary>
public enum HeroPose
{
    Locomotion,
    Sit,
    Jump,
}

/// <summary>
/// 영웅 모델의 동작 재생(표시만). Animator Controller 없이 Playables 섞기 하나로 서기·달리기·앉기·점프를 섞는다
/// — 어느 동작인지는 판정·연출 코드가 이미 알고 있어서 상태 기계를 따로 두면 같은 판단이 두 곳에 생긴다.
/// 그래프는 직접 갱신한다(받은 시간으로 — 게임 시간 규칙). 점프는 연출이 진행도를 넘겨 그 시각의 자세를 놓는다(포물선과 맞추려고).
/// 모델에 Animator가 없거나 Humanoid가 아니거나 묶음이 없으면 아무것도 하지 않는다(임시 모델).
/// 달리기는 앞·왼옆·오른옆 셋을 몸 기준 이동 방향으로 섞는다 — 공격 중 몸이 적을 향해 가는 방향과 어긋나면 옆걸음으로 달린다.
/// 공격은 위에 얹는 상체 층이다 — 다리는 아래 층(서기·달리기)을 그대로 따라 걸으면서 던진다.
/// 공격 판정은 HeroAttack이 하고 여기는 보여 주기만 한다(AI 시뮬과 같은 결과가 나오게).
/// </summary>
public sealed class HeroAnimation : IDisposable
{
    private const int IdleInput = 0;
    private const int RunInput = 1;
    private const int RunLeftInput = 2;
    private const int RunRightInput = 3;
    private const int SitInput = 4;
    private const int JumpInput = 5;
    private const int InputCount = 6;

    private readonly SO_HeroAnimationSet _set;
    private readonly PlayableGraph _graph;
    private readonly AnimationMixerPlayable _mixer;
    private readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[InputCount];
    private readonly float[] _times = new float[InputCount];
    private readonly float[] _weights = new float[InputCount];
    private readonly float[] _goals = new float[InputCount];
    // 달리기 셋이 함께 쓰는 진행도(0~1). 클립 길이가 달라도 발이 같은 박자로 섞이게 시각 대신 진행도를 나눠 쓴다.
    private float _runPhase;
    private readonly bool _valid;

    private const int BaseLayer = 0;
    private const int AttackLayer = 1;
    private readonly AnimationLayerMixerPlayable _layers;
    private readonly AnimationClipPlayable _attack;
    private readonly AvatarMask _upperBody;
    // 공격 클립 시각(재생 중이 아니면 음수)과 재생 배율, 공격 층 무게.
    private float _attackTime = -1f;
    private float _attackRate = 1f;
    private float _attackWeight;

    // 조준 비틀기: 허리·가슴·목 뼈(없으면 null)와 지금·목표 각도(도, + = 오른쪽).
    private readonly Transform[] _twistBones = new Transform[3];
    private readonly Transform _root;
    private float _twist;
    private float _twistTarget;

    private HeroPose _pose = HeroPose.Locomotion;
    private float _jumpProgress;

    public HeroAnimation(GameObject model, SO_HeroAnimationSet set)
    {
        _set = set;
        Animator animator = model.GetComponentInChildren<Animator>();
        if (set == null || animator == null || !animator.isHuman) return;

        // 위치는 코드가 옮긴다 — 클립의 루트 이동이 몸을 끌고 가지 않게.
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        _graph = PlayableGraph.Create("HeroAnimation");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        _mixer = AnimationMixerPlayable.Create(_graph, InputCount);
        _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
        _graph.Connect(_mixer, 0, _layers, BaseLayer);
        _layers.SetInputWeight(BaseLayer, 1f);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Hero", animator);
        output.SetSourcePlayable(_layers);

        if (set.Attack != null)
        {
            _attack = AnimationClipPlayable.Create(_graph, set.Attack);
            _attack.SetSpeed(0);
            _graph.Connect(_attack, 0, _layers, AttackLayer);
            _upperBody = CreateUpperBodyMask();
            _layers.SetLayerMaskFromAvatarMask(AttackLayer, _upperBody);
        }
        _root = animator.transform;
        _twistBones[0] = animator.GetBoneTransform(HumanBodyBones.Spine);
        _twistBones[1] = animator.GetBoneTransform(HumanBodyBones.Chest);
        _twistBones[2] = animator.GetBoneTransform(HumanBodyBones.Neck);

        AnimationClip[] clips = { set.Idle, set.Run, set.RunLeft, set.RunRight, set.Sit, set.Jump };
        for (int i = 0; i < InputCount; i++)
        {
            if (clips[i] == null) continue;
            _clips[i] = AnimationClipPlayable.Create(_graph, clips[i]);
            // 시각은 직접 놓는다(되풀이·점프 진행도). 그래프가 시간을 흘리지 않게 빠르기 0.
            _clips[i].SetSpeed(0);
            _graph.Connect(_clips[i], 0, _mixer, i);
        }
        _weights[IdleInput] = 1f;
        _valid = true;
        Apply();
    }

    public void Dispose()
    {
        if (_graph.IsValid()) _graph.Destroy();
        if (_upperBody != null) UnityEngine.Object.Destroy(_upperBody);
    }

    // 상체만: 다리·발 IK·루트(몸통 위치·방향)는 아래 층 것을 쓴다 — 루트까지 넘기면 클립의 허리 비틀기가 다리째 돌린다.
    // 어느 부위를 막을지는 구조라 조정값이 아니어서 에셋 대신 코드로 만든다.
    private static AvatarMask CreateUpperBodyMask()
    {
        AvatarMask mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        return mask;
    }

    /// <summary>공격 동작을 처음부터 시작한다. 카드가 손을 떠나는 프레임은 SO_HeroAnimationSet.AttackWindup 뒤 — 판정이 그 시각에 맞힌다.</summary>
    public void PlayAttack(float interval)
    {
        if (!_valid || !_attack.IsValid()) return;
        _attackRate = _set.AttackRate(interval);
        _attackTime = 0f;
    }

    /// <summary>공격 동작이 재생 중인지. 그동안 몸이 적을 향한다(HeroView).</summary>
    public bool IsAttacking => _attackTime >= 0f;

    /// <summary>
    /// 상체를 비틀 각도(도, + = 오른쪽). 공격 층 무게만큼만 걸린다 — 공격이 끝나면 저절로 풀린다.
    /// 클립 위에 덧붙이는 것이라 마스크·클립을 바꾸지 않고 어느 공격 동작에도 쓴다.
    /// </summary>
    public void SetTwist(float degrees) => _twistTarget = degrees;

    public void SetPose(HeroPose pose)
    {
        if (_pose == pose) return;
        _pose = pose;
        // 점프는 처음부터 — 연출이 진행도를 넘기기 전 한 프레임 이전 시각이 보이지 않게.
        if (pose == HeroPose.Jump) _jumpProgress = 0f;
    }

    /// <summary>점프 클립의 진행도(0~1). Jump 동작일 때 그 시각의 자세를 놓는다.</summary>
    public void SetJumpProgress(float normalized) => _jumpProgress = Mathf.Clamp01(normalized);

    /// <param name="moveSpeed">이번 프레임 이동 속도(초당 칸). 서기·달리기 고르기와 달리기 빠르기에 쓴다.</param>
    /// <param name="strafe">몸 정면 기준 가는 방향(도, + = 오른쪽). 0이면 앞으로, ±90이면 옆걸음.</param>
    public void Tick(float deltaTime, float moveSpeed, float strafe)
    {
        if (!_valid) return;

        SetGoals(moveSpeed, strafe);
        float step = _set.FadeDuration > 0f ? deltaTime / _set.FadeDuration : 1f;
        for (int i = 0; i < InputCount; i++)
            _weights[i] = Mathf.MoveTowards(_weights[i], _clips[i].IsValid() ? _goals[i] : 0f, step);

        float runRate = Mathf.Clamp(moveSpeed / _set.RunReferenceSpeed, _set.RunSpeedRange.x, _set.RunSpeedRange.y);
        Advance(IdleInput, deltaTime);
        AdvanceRun(deltaTime * runRate);
        Advance(SitInput, deltaTime);
        if (_clips[JumpInput].IsValid()) _times[JumpInput] = _jumpProgress * _set.Jump.length;
        AdvanceAttack(deltaTime);
        _twist = Mathf.MoveTowardsAngle(_twist, _twistTarget, _set.AimTurnSpeed * deltaTime);

        Apply();
        _graph.Evaluate(0f);
        ApplyTwist();
    }

    // 그래프가 뼈를 쓴 뒤에 덧돌린다. 다음 Evaluate가 뼈를 다시 쓰므로 쌓이지 않는다.
    // 부모(허리)를 돌리면 자식(가슴·목)도 따라 돌아 몫이 더해진다 — 몫의 합만큼 상체가 돈다.
    private void ApplyTwist()
    {
        float angle = _twist * _attackWeight;
        if (Mathf.Approximately(angle, 0f)) return;
        Vector3 up = _root.up;
        Vector3 shares = _set.AimTwistShares;
        for (int i = 0; i < _twistBones.Length; i++)
        {
            if (_twistBones[i] == null) continue;
            _twistBones[i].rotation = Quaternion.AngleAxis(angle * shares[i], up) * _twistBones[i].rotation;
        }
    }

    private void SetGoals(float moveSpeed, float strafe)
    {
        for (int i = 0; i < InputCount; i++) _goals[i] = 0f;
        switch (_pose)
        {
            case HeroPose.Sit: _goals[SitInput] = 1f; return;
            case HeroPose.Jump: _goals[JumpInput] = 1f; return;
        }
        if (moveSpeed <= _set.MoveThreshold || !_clips[RunInput].IsValid())
        {
            _goals[IdleInput] = 1f;
            return;
        }

        // 옆 몫은 0°에서 0, 90°에서 1. 뒤로 가는 클립은 없어서 90°를 넘으면 옆걸음 그대로 둔다(몸을 그 이상 돌리지 않는다 — HeroView).
        // 옆 클립이 없으면 앞 달리기로 대신한다.
        int side = strafe >= 0f ? RunRightInput : RunLeftInput;
        float sideShare = _clips[side].IsValid() ? Mathf.Clamp01(Mathf.Abs(strafe) / 90f) : 0f;
        _goals[RunInput] = 1f - sideShare;
        _goals[side] = sideShare;
    }

    // 달리기 셋은 진행도 하나로 함께 흐른다. 한 바퀴 빠르기는 앞 달리기(제 길이)와 옆걸음(제 길이 ÷ RunSideRate) 사이를
    // 지금 섞인 비율대로 잇는다 — 옆걸음만 빠르게 돌려도 발 박자가 어긋나지 않게.
    private void AdvanceRun(float deltaTime)
    {
        if (!_clips[RunInput].IsValid()) return;
        float forward = _weights[RunInput];
        float side = _weights[RunLeftInput] + _weights[RunRightInput];
        float sideShare = forward + side > 0f ? side / (forward + side) : 0f;
        int sideInput = _weights[RunRightInput] >= _weights[RunLeftInput] ? RunRightInput : RunLeftInput;
        float forwardSpeed = 1f / _set.Run.length;
        float sideSpeed = _clips[sideInput].IsValid() ? _set.RunSideRate / _clips[sideInput].GetAnimationClip().length : forwardSpeed;
        _runPhase = Mathf.Repeat(_runPhase + deltaTime * Mathf.Lerp(forwardSpeed, sideSpeed, sideShare), 1f);
        for (int i = RunInput; i <= RunRightInput; i++)
            if (_clips[i].IsValid()) _times[i] = _runPhase * _clips[i].GetAnimationClip().length;
    }

    // 되풀이하는 동작: 섞여 있지 않은 동안에도 흘려 둔다(다시 섞일 때 같은 자세에서 시작하지 않게).
    private void Advance(int input, float deltaTime)
    {
        if (!_clips[input].IsValid()) return;
        float length = _clips[input].GetAnimationClip().length;
        _times[input] = length > 0f ? Mathf.Repeat(_times[input] + deltaTime, length) : 0f;
    }

    // 한 번 재생하고 끝난다. 끝나기 AttackFade 전부터 층을 꺼 아래 층(서기·달리기) 팔로 돌아간다.
    private void AdvanceAttack(float deltaTime)
    {
        if (!_attack.IsValid()) return;
        float length = _set.Attack.length;
        if (_attackTime >= 0f)
        {
            _attackTime += deltaTime * _attackRate;
            if (_attackTime >= length) _attackTime = -1f;
        }
        bool on = _attackTime >= 0f && (length - _attackTime) / _attackRate > _set.AttackFade;
        _attackWeight = Mathf.MoveTowards(_attackWeight, on ? 1f : 0f, deltaTime / _set.AttackFade);
    }

    private void Apply()
    {
        if (_attack.IsValid())
        {
            if (_attackTime >= 0f) _attack.SetTime(_attackTime);
            _layers.SetInputWeight(AttackLayer, _attackWeight);
        }

        // 섞는 도중 합이 1이 안 되면 Humanoid는 T자세 쪽으로 끌려가므로 합으로 나눠 맞춘다.
        float sum = 0f;
        for (int i = 0; i < InputCount; i++) sum += _weights[i];
        for (int i = 0; i < InputCount; i++)
        {
            if (!_clips[i].IsValid()) continue;
            _clips[i].SetTime(_times[i]);
            _mixer.SetInputWeight(i, sum > 0f ? _weights[i] / sum : 0f);
        }
    }
}
