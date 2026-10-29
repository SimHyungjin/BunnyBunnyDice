using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 출현 구역 하나의 경로 표시. 경로 전체에 흐린 바탕선을 늘 그려 두고, 그 위로 출현 구역에서 일정 간격으로 빛(PathRunner)을 내보낸다.
/// 끊기지 않게 보이는 것은 바탕선이 맡고, 빛은 방향(어디서 어디로)만 보여 준다 — 빛 꼬리를 길게 늘여 이으면 꼬리끼리 겹친 곳이
/// 가산 블렌딩으로 하얗게 뜨고, 길이 바뀔 때 빛을 중간에 미리 깔면 엉뚱한 자리에서 줄이 튀어나와 보였다.
/// 처음 드러날 때(Reveal)는 바탕선이 첫 빛을 따라 자라며 그려진다 — 판 주사위 연출 뒤에 선이 통째로 뚝 생기면 거슬려서.
/// 주사위 배치로 길이 바뀔 때는 바로 통째로 그린다(새 길을 곧바로 보여 주는 게 우선).
/// </summary>
public sealed class PathLane
{
    private readonly Transform _parent;
    private readonly SO_MapPathViewConfig _config;
    private readonly Color _color;
    private readonly PathPolyline _line;
    private readonly GameObject _baseLine;
    private readonly LineRenderer _baseRenderer;
    // 바탕선이 그려진 길이(처음부터). 0 미만이면 다 그려진 것이다.
    private float _revealed = -1f;
    // 바탕선은 바닥에 눕힌 오브젝트에 그린다(선은 Z축을 향한다). 점은 이 회전을 되돌려 부모 로컬 좌표 그대로 놓이게 한다.
    private static readonly Quaternion LineRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
    private static readonly Quaternion LineInverse = Quaternion.Inverse(LineRotation);
    private readonly List<PathRunner> _runners = new List<PathRunner>();
    private float _sendTimer;

    public PathLane(Transform parent, SO_MapPathViewConfig config, Color color, PathPolyline line)
    {
        _parent = parent;
        _config = config;
        _color = color;
        _line = line;
        _baseLine = CreateBaseLine(parent, config, color, line);
        _baseRenderer = _baseLine.GetComponent<LineRenderer>();

        Send();
        _sendTimer = config.SendInterval;
    }

    public void Tick(float deltaTime)
    {
        _sendTimer -= deltaTime;
        if (_sendTimer <= 0f)
        {
            Send();
            _sendTimer += _config.SendInterval;
        }
        for (int i = 0; i < _runners.Count; i++) _runners[i].Tick(deltaTime);

        if (_revealed < 0f) return;
        // 첫 빛과 같은 빠르기로 자라 빛 머리 바로 뒤까지 그려진다.
        _revealed += _config.RunSpeed * deltaTime;
        UpdateBaseLine();
    }

    /// <summary>처음부터 다시 드러낸다: 빛을 출현 구역에서 새로 내보내고, 바탕선은 그 빛을 따라 자라게 한다.</summary>
    public void Reveal()
    {
        for (int i = 0; i < _runners.Count; i++) _runners[i].Destroy();
        _runners.Clear();
        Send();
        _sendTimer = _config.SendInterval;
        _revealed = 0f;
        UpdateBaseLine();
    }

    // 그려진 길이까지의 점 + 그 지점의 중간 점. 다 그렸으면 경로 전체.
    private void UpdateBaseLine()
    {
        if (_revealed >= _line.Length) _revealed = -1f;
        if (_revealed < 0f)
        {
            _baseRenderer.positionCount = _line.PointCount;
            for (int i = 0; i < _line.PointCount; i++) _baseRenderer.SetPosition(i, LineInverse * _line.GetPoint(i));
            return;
        }

        int count = 0;
        while (count < _line.PointCount && _line.DistanceAt(count) <= _revealed) count++;
        _baseRenderer.positionCount = count + 1;
        for (int i = 0; i < count; i++) _baseRenderer.SetPosition(i, LineInverse * _line.GetPoint(i));
        _baseRenderer.SetPosition(count, LineInverse * _line.PointAt(_revealed));
    }

    public void Destroy()
    {
        for (int i = 0; i < _runners.Count; i++) _runners[i].Destroy();
        _runners.Clear();
        Object.Destroy(_baseLine);
    }

    private void Send()
    {
        for (int i = 0; i < _runners.Count; i++)
        {
            if (_runners[i].IsActive) continue;
            _runners[i].Launch(_line, 0f);
            return;
        }

        PathRunner runner = new PathRunner(_parent, _config, _color);
        runner.Launch(_line, 0f);
        _runners.Add(runner);
    }

    private static GameObject CreateBaseLine(Transform parent, SO_MapPathViewConfig config, Color color, PathPolyline line)
    {
        GameObject go = new GameObject("PathBaseLine");
        go.transform.SetParent(parent, false);
        // 빛 꼬리와 같이 바닥에 눕힌다.
        go.transform.localRotation = LineRotation;

        LineRenderer renderer = go.AddComponent<LineRenderer>();
        renderer.sharedMaterial = config.TrailMaterial;
        renderer.useWorldSpace = false;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.widthMultiplier = config.BaseLineWidth;
        renderer.numCornerVertices = config.TrailCornerVertices;
        // 색은 머티리얼 색(RendererTint)이 정하고, 정점 투명도로 빛 꼬리보다 흐리게 한다.
        Color faint = new Color(1f, 1f, 1f, config.BaseLineAlpha);
        renderer.startColor = faint;
        renderer.endColor = faint;

        renderer.positionCount = line.PointCount;
        for (int i = 0; i < line.PointCount; i++) renderer.SetPosition(i, LineInverse * line.GetPoint(i));

        RendererTint.Apply(go, color);
        return go;
    }
}
