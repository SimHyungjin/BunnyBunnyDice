using UnityEngine;

/// <summary>
/// 경로를 한 번 달리는 빛 하나. 머리가 지나간 자리에 바닥에 깔린 꼬리를 남기고, 끝에 닿으면 머리를 감춘 뒤 꼬리가 다 사라지면 끝난다.
/// 끝난 빛은 PathLane이 다시 내보낼 때 재사용한다(꼬리 렌더러를 매번 만들지 않게).
/// </summary>
public sealed class PathRunner
{
    private readonly SO_MapPathViewConfig _config;
    private readonly GameObject _root;
    private readonly GameObject _head;
    private readonly TrailRenderer _trail;

    private PathPolyline _line;
    private float _distance;
    private float _fadeTimer;
    private bool _running;

    /// <summary>달리는 중이거나 꼬리가 아직 남아 있다.</summary>
    public bool IsActive { get; private set; }

    public PathRunner(Transform parent, SO_MapPathViewConfig config, Color color)
    {
        _config = config;
        _root = new GameObject("PathRunner");
        _root.transform.SetParent(parent, false);
        // 꼬리를 바닥에 눕힌다 — 꼬리는 Z축을 향하므로 Z를 위로 돌리면 카메라를 향해 서지 않고 바닥에 그린 선처럼 보인다.
        _root.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        _head = Object.Instantiate(config.HeadPrefab, _root.transform, false);

        _trail = _root.AddComponent<TrailRenderer>();
        _trail.sharedMaterial = config.TrailMaterial;
        _trail.alignment = LineAlignment.TransformZ;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.time = config.TrailTime;
        _trail.widthMultiplier = config.TrailWidth;
        _trail.numCornerVertices = config.TrailCornerVertices;
        // 머리 쪽이 굵고 진하며 끝으로 갈수록 가늘고 흐려진다. 색은 머티리얼 색(RendererTint)이 정한다.
        _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        _trail.startColor = Color.white;
        _trail.endColor = new Color(1f, 1f, 1f, 0f);

        RendererTint.Apply(_root, color);
        _root.SetActive(false);
    }

    /// <summary>line 위 distance 지점에서 달리기 시작한다.</summary>
    public void Launch(PathPolyline line, float distance)
    {
        _line = line;
        _distance = distance;
        _running = true;
        IsActive = true;

        _root.SetActive(true);
        _trail.emitting = false;
        _root.transform.localPosition = line.PointAt(distance);
        // 이전에 달린 끝점에서 순간 이동한 자리가 꼬리로 이어지지 않게 지운다.
        _trail.Clear();
        _trail.emitting = true;
        _head.SetActive(true);
    }

    public void Tick(float deltaTime)
    {
        if (!IsActive) return;

        if (!_running)
        {
            // 끝에 닿은 프레임에도 꼬리가 끝점까지 그려지도록 한 프레임 늦게 끊는다.
            _trail.emitting = false;
            _fadeTimer -= deltaTime;
            if (_fadeTimer > 0f) return;
            IsActive = false;
            _root.SetActive(false);
            return;
        }

        _distance += _config.RunSpeed * deltaTime;
        if (_distance >= _line.Length)
        {
            _distance = _line.Length;
            _running = false;
            _fadeTimer = _config.TrailTime;
            _head.SetActive(false);
        }
        _root.transform.localPosition = _line.PointAt(_distance);
    }

    public void Destroy() => Object.Destroy(_root);
}
