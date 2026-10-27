using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 범용 카메라 서비스: 대상 추적, 화면 흔들림, 수동 배치.
/// 장르 특화 로직(모드 전환·컷인 등)은 넣지 않는다 — 그런 건 게임 코드에서 이 API를 조합해 만든다.
///
/// LateUpdate 타이밍에 추적/셰이크를 적용한다. 시간은 unscaled라 히트스톱·슬로모션 중에도
/// 카메라는 매끄럽게 반응하고, 셰이크도 멈추지 않아 임팩트가 강조된다.
/// </summary>
public sealed class CameraManager : Manager
{
    private Camera _camera;

    // Follow
    private Transform _target;
    private Vector3 _offset;
    private float _followSpeed = 12f;
    private Func<Vector3, Vector3> _followPositionConstraint;

    // Shake
    private float _shakeTimer, _shakeDuration, _shakeAmplitude, _shakeFrequency;
    private float _shakeSeedX, _shakeSeedY;
    private Vector3 _shakeOffset;

    public override Type[] Dependencies => new[] { typeof(LoopManager) };

    /// <summary>현재 메인 카메라. 없어지면(씬 재로드 등) 다시 찾는다.</summary>
    public Camera MainCamera
    {
        get
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }
    }

    protected override UniTask OnInitializeAsync()
    {
        _camera = Camera.main;
        Core.Loop.OnLateUpdate += LateTick;
        return UniTask.CompletedTask;
    }

    #region Follow

    /// <summary>대상을 따라가되, 지금의 카메라-대상 오프셋을 그대로 유지한다.</summary>
    public void SetFollow(Transform target, float followSpeed = 12f, bool snap = false)
    {
        Camera cam = MainCamera;
        Vector3 offset = (cam != null && target != null)
            ? cam.transform.position - target.position
            : Vector3.zero;
        SetFollow(target, offset, followSpeed, snap);
    }

    /// <summary>대상을 지정한 오프셋으로 따라간다. (시점은 offset과 카메라 회전으로 결정 — 탑다운·사이드·아이소 다 커버)</summary>
    public void SetFollow(Transform target, Vector3 offset, float followSpeed = 12f, bool snap = true,
        Func<Vector3, Vector3> positionConstraint = null)
    {
        _target = target;
        _offset = offset;
        _followSpeed = Mathf.Max(0f, followSpeed);
        _followPositionConstraint = positionConstraint;

        if (snap && target != null)
        {
            Camera cam = MainCamera;
            if (cam != null)
            {
                Vector3 goal = target.position + offset;
                cam.transform.position = (positionConstraint != null ? positionConstraint(goal) : goal) + _shakeOffset;
            }
        }
    }

    public void ClearFollow()
    {
        _target = null;
        _followPositionConstraint = null;
    }

    /// <summary>지정한 대상을 따라가는 중일 때만 해제한다. (남이 건 추적을 실수로 끄지 않도록)</summary>
    public void ClearFollow(Transform target)
    {
        if (_target == target) ClearFollow();
    }

    #endregion

    #region View

    /// <summary>카메라를 수동으로 배치한다. 추적을 멈추고 이 위치/회전으로 둔다.</summary>
    public void SetView(Vector3 position, Quaternion rotation)
    {
        ClearFollow();
        Camera cam = MainCamera;
        if (cam != null) cam.transform.SetPositionAndRotation(position, rotation);
    }

    public void SetView(Vector3 position, Quaternion rotation, bool orthographic, float orthographicSize)
    {
        SetView(position, rotation);
        Camera cam = MainCamera;
        if (cam == null) return;

        cam.orthographic = orthographic;
        if (orthographic) cam.orthographicSize = orthographicSize;
    }

    #endregion

    #region Shake

    /// <summary>화면을 흔든다. unscaled 시간이라 히트스톱 중에도 흔들려 임팩트가 강조된다.</summary>
    public void Shake(float amplitude, float duration, float frequency = 25f)
    {
        if (amplitude <= 0f || duration <= 0f) return;

        // 진행 중인 셰이크보다 약하면 무시한다 (강한 임팩트가 약한 잔존 셰이크에 덮이지 않게).
        float remaining = _shakeTimer > 0f && _shakeDuration > 0f
            ? _shakeAmplitude * (_shakeTimer / _shakeDuration)
            : 0f;
        if (amplitude < remaining) return;

        _shakeAmplitude = amplitude;
        _shakeDuration  = duration;
        _shakeTimer     = duration;
        _shakeFrequency = frequency > 0f ? frequency : 25f;
        _shakeSeedX = UnityEngine.Random.value * 1000f;
        _shakeSeedY = UnityEngine.Random.value * 1000f;
    }

    #endregion

    private void LateTick(float dt)
    {
        Camera cam = MainCamera;
        if (cam == null) return;

        // 지난 프레임 셰이크를 빼서 base 위치를 복원한다 (외부에서 카메라를 옮겼을 수도 있으므로).
        Vector3 basePos = cam.transform.position - _shakeOffset;

        if (_target != null)
        {
            Vector3 goal = _target.position + _offset;
            basePos = _followSpeed > 0f
                ? Vector3.Lerp(basePos, goal, 1f - Mathf.Exp(-_followSpeed * dt))
                : goal;
        }

        _shakeOffset = ComputeShake(cam, dt);
        Vector3 finalPosition = basePos + _shakeOffset;
        cam.transform.position = _target != null && _followPositionConstraint != null
            ? _followPositionConstraint(finalPosition) : finalPosition;
    }

    // dt는 Core가 넘겨준 실제 시간(상한이 걸린 unscaled)이다. 창 전환 직후 튄 프레임에서 셰이크가 한 번에 끝나지 않게 한다.
    private Vector3 ComputeShake(Camera cam, float dt)
    {
        if (_shakeTimer <= 0f) return Vector3.zero;

        _shakeTimer = Mathf.Max(0f, _shakeTimer - dt);

        float t   = _shakeDuration > 0f ? _shakeTimer / _shakeDuration : 0f;
        float amp = _shakeAmplitude * (t * t);   // 끝으로 갈수록 잦아듦

        float time = Time.unscaledTime * _shakeFrequency;
        float nx = (Mathf.PerlinNoise(_shakeSeedX, time) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(_shakeSeedY, time) - 0.5f) * 2f;

        // 카메라 로컬 축 기준이라 어떤 시점에서도 화면 좌우/상하로 흔들린다.
        return (cam.transform.right * nx + cam.transform.up * ny) * amp;
    }

    public override void Clear()
    {
        ClearFollow();
        _camera = null;              // 새 씬에서 Camera.main을 다시 찾도록
        _shakeTimer = 0f;
        _shakeOffset = Vector3.zero;
        // Loop 구독은 유지한다 — 씬을 넘어 살아있는 매니저.
    }
}
