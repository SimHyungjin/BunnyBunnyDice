using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판 밖의 2D 6면 주사위(로비 능력치 칸·결과 화면 보상). 눈을 그리고, 굴림 연출을 한다: 빙글 돌다 감속 → 착지(톡) → 올랐으면 반짝 / 그대로면 흐린 눈 → 원래 눈.
/// 도는 동안은 눈을 지운다(빈 주사위) — 지나가는 눈이 보이면 어느 것이 진짜 결과인지 헷갈렸다. 결과 눈은 착지하는 순간 처음 나타난다.
/// 결과는 부르는 쪽이 먼저 정한다(판 주사위 연출과 같은 원칙) — 여기는 보여 주기만 한다.
///
/// 구르는 동안 다시 Roll을 부르면 착지하지 않고 계속 구른다(리롤 연타 — 6이 될 때까지 계속 굴리는 사람을 위해). 착지 눈만 마지막 것으로 바뀐다.
/// 움직임은 회전·크기만 쓴다(자리는 AspectRatioFitter가 정하므로 건드리지 않는다).
/// </summary>
public sealed class UI_MetaDie : MonoBehaviour
{
    // 단계별로 켜는 눈 자리. _pips 순서: 왼위·오른위·왼가운데·오른가운데·왼아래·오른아래·가운데.
    private static readonly bool[][] Faces =
    {
        new[] { false, false, false, false, false, false, true },
        new[] { true, false, false, false, false, true, false },
        new[] { true, false, false, false, false, true, true },
        new[] { true, true, false, false, true, true, false },
        new[] { true, true, false, false, true, true, true },
        new[] { true, true, true, true, true, true, false },
    };

    private enum Phase { None, Delay, Tumble, Land, Flash, FailHold, Shake }

    [Tooltip("주사위 몸(올랐을 때 반짝이는 색).")]
    [SerializeField] private Graphic _body;
    [Tooltip("눈 자리 7개. 순서: 왼위·오른위·왼가운데·오른가운데·왼아래·오른아래·가운데.")]
    [SerializeField] private Graphic[] _pips;

    private SO_MetaDiceConfig _config;
    private Phase _phase;
    private float _phaseStart;
    private float _tumbleEnd;
    // 회전(도, 시계 방향). 감속은 지금 각도에서 180° 단위 자세까지 부드럽게 간다.
    // 90° 단위로 멈추면 2·3(대각선)·6(줄)의 눈 모양이 돌아간 채로 보이다가, 착지 때 회전을 0으로 되돌리며 뒤집혀 보였다 — 모든 눈은 180°에서 같은 모양이다.
    private const float RestAngle = 180f;
    private float _angle;
    private float _decelStart = -1f;
    private float _decelDuration;
    private float _decelFrom;
    private float _decelTo;
    private int _restFace = HeroStats.MaxStage;
    private int _landFace;
    private bool _success;
    private Action _landed;
    private Color _bodyColor;
    private Color[] _pipColors;
    private bool _cached;

    /// <summary>굴리는 중(착지 뒤 반짝·고개 젓기까지). 이동안은 SetFace가 그리지 않는다.</summary>
    public bool IsBusy => _phase != Phase.None;

    /// <summary>아직 착지하지 않았다(다시 Roll하면 이어서 구른다).</summary>
    public bool IsTumbling => _phase == Phase.Delay || _phase == Phase.Tumble;

    /// <summary>쉬는 눈. 굴리는 중이면 기억만 하고 연출이 끝나면 이 눈으로 쉰다.</summary>
    public void SetFace(int face)
    {
        _restFace = Mathf.Clamp(face, 1, Faces.Length);
        if (!IsBusy) Draw(_restFace, 1f);
    }

    /// <param name="landFace">착지 눈.</param>
    /// <param name="success">true = 착지 눈으로 반짝이며 쉰다. false = 착지 눈을 흐리게 보였다가 쉬는 눈(SetFace)으로 돌아온다.</param>
    /// <param name="delay">구르기 전 기다림(여러 개를 차례로 굴릴 때). 이어서 구를 때는 쓰지 않는다.</param>
    /// <param name="landed">착지한 순간(Finish로 건너뛰어도 한 번 불린다). 이어서 구르면 마지막 것만 불린다.</param>
    public void Roll(SO_MetaDiceConfig config, int landFace, bool success, float delay = 0f, Action landed = null)
    {
        Cache();
        _config = config;
        _landFace = Mathf.Clamp(landFace, 1, Faces.Length);
        _success = success;
        _landed = landed;
        float now = Time.unscaledTime;
        if (IsTumbling)
        {
            _tumbleEnd = Mathf.Max(_tumbleEnd, now + config.TumbleDuration);
            return;
        }
        ResetPose();
        _phase = delay > 0f ? Phase.Delay : Phase.Tumble;
        _phaseStart = now + delay;
        _tumbleEnd = _phaseStart + config.TumbleDuration;
        _angle = 0f;
        _decelStart = -1f;
        // 기다렸다 구르면(차례로 굴리기) 기다리는 동안은 지금 눈이 보이고, 구르기 시작할 때 지운다.
        if (_phase == Phase.Tumble) HidePips();
    }

    /// <summary>연출을 건너뛰어 쉬는 모습으로 놓는다(착지 전이면 착지 알림도 부른다).</summary>
    public void Finish()
    {
        if (!IsBusy) return;
        bool landed = IsTumbling;
        _phase = Phase.None;
        ResetPose();
        Draw(_success ? _landFace : _restFace, 1f);
        if (landed) InvokeLanded();
    }

    private void Awake() => Cache();

    private void OnDisable() => Finish();

    private void Cache()
    {
        if (_cached) return;
        _cached = true;
        if (_body != null) _bodyColor = _body.color;
        _pipColors = new Color[_pips.Length];
        for (int i = 0; i < _pips.Length; i++) _pipColors[i] = _pips[i].color;
    }

    private void Update()
    {
        if (_phase == Phase.None) return;
        float now = Time.unscaledTime;
        float elapsed = now - _phaseStart;
        switch (_phase)
        {
            case Phase.Delay:
                if (elapsed < 0f) break;
                _phase = Phase.Tumble;
                HidePips();
                break;
            case Phase.Tumble:
                Spin(now);
                break;
            case Phase.Land:
            {
                float t = Mathf.Clamp01(elapsed / _config.LandDuration);
                transform.localScale = Vector3.one * (1f + (_config.LandPopScale - 1f) * Mathf.Sin(t * Mathf.PI));
                if (t < 1f) break;
                transform.localScale = Vector3.one;
                _phase = _success ? Phase.Flash : Phase.FailHold;
                _phaseStart = now;
                break;
            }
            case Phase.Flash:
            {
                float t = Mathf.Clamp01(elapsed / _config.SuccessDuration);
                if (_body != null) _body.color = Color.Lerp(_config.SuccessColor, _bodyColor, t);
                if (t >= 1f) _phase = Phase.None;
                break;
            }
            case Phase.FailHold:
                if (elapsed < _config.FailHold) break;
                Draw(_restFace, 1f);
                _phase = Phase.Shake;
                _phaseStart = now;
                break;
            case Phase.Shake:
            {
                float t = Mathf.Clamp01(elapsed / _config.FailShakeDuration);
                // 좌우로 세 번 젓고 잦아든다.
                float angle = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * _config.FailShakeAngle;
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (t < 1f) break;
                transform.localRotation = Quaternion.identity;
                _phase = Phase.None;
                break;
            }
        }
    }

    private void Spin(float now)
    {
        float remaining = _tumbleEnd - now;
        bool decelerating = remaining <= _config.DecelDuration;
        float bounce = 1f;
        if (!decelerating)
        {
            // 감속 중에 다시 눌렸으면(이어서 구르기) 최고 속도로 돌아간다.
            _decelStart = -1f;
            _angle += _config.SpinSpeed * Time.unscaledDeltaTime;
        }
        else
        {
            if (_decelStart < 0f)
            {
                // 이대로 감속하면 멈출 자리(3차 감속의 이동 거리 = 속도 × 시간 ÷ 3)에서 가장 가까운 바로 선 자세(RestAngle 단위)로 맞춘다.
                _decelStart = now;
                _decelDuration = Mathf.Max(remaining, 0.01f);
                _decelFrom = _angle;
                float natural = _angle + _config.SpinSpeed * _decelDuration / 3f;
                _decelTo = Mathf.Round(natural / RestAngle) * RestAngle;
                if (_decelTo <= _angle) _decelTo += RestAngle;
            }
            float u = Mathf.Clamp01((now - _decelStart) / _decelDuration);
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            _angle = Mathf.Lerp(_decelFrom, _decelTo, e);
            bounce = 1f - u;
            if (u >= 1f)
            {
                Land(now);
                return;
            }
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, -_angle);
        transform.localScale = Vector3.one * (1f + _config.TumbleBounce * bounce);
    }

    private void Land(float now)
    {
        transform.localRotation = Quaternion.identity;
        Draw(_landFace, _success ? 1f : _config.FailAlpha);
        _phase = Phase.Land;
        _phaseStart = now;
        InvokeLanded();
    }

    private void InvokeLanded()
    {
        Action landed = _landed;
        _landed = null;
        landed?.Invoke();
    }

    private void ResetPose()
    {
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        if (_body != null && _cached) _body.color = _bodyColor;
    }

    /// <summary>눈을 지운다(빈 주사위). 굴리기 전 기다리는 동안 결과 눈이 미리 보이지 않게 할 때.</summary>
    public void HidePips()
    {
        for (int i = 0; i < _pips.Length; i++) _pips[i].enabled = false;
    }

    private void Draw(int face, float alpha)
    {
        Cache();
        bool[] on = Faces[face - 1];
        for (int i = 0; i < _pips.Length && i < on.Length; i++)
        {
            _pips[i].enabled = on[i];
            Color color = _pipColors[i];
            color.a *= alpha;
            _pips[i].color = color;
        }
    }
}
