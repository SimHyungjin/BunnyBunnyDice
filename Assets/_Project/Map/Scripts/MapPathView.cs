using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 출현 구역마다 목표물까지의 예상 경로를, 빛(PathRunner)이 꼬리를 남기며 끊임없이 달리는 것으로 보여준다(PathLane).
/// 몬스터가 실제로 걷는 길(MonsterField와 같은 GridRouteField)을 받아 그린다.
/// 주사위 배치로 길이 바뀌면 통째로 다시 만든다 — 바탕선이 곧바로 새 길을 보여 주고 옛 길의 빛은 남지 않는다.
/// </summary>
public sealed class MapPathView : IDisposable
{
    private readonly SO_MapPathViewConfig _config;
    private readonly Transform _root;
    private readonly List<Vector2> _path = new List<Vector2>();
    private readonly List<PathLane> _lanes = new List<PathLane>();

    public MapPathView(Transform parent, SO_MapPathViewConfig config)
    {
        _config = config;
        _root = new GameObject("Paths").transform;
        _root.SetParent(parent, false);
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        Clear();
    }

    public void Rebuild(MapData map, GridRouteField route)
    {
        // 길이 바뀔 때(주사위 배치·설정 변경)만 불리고 출현 구역은 2~3곳이라, 재사용하지 않고 새로 만든다.
        Clear();

        for (int i = 0; i < map.Spawns.Count; i++)
        {
            Vector2 spawn = map.Spawns[i];
            if (!route.TryGetPath(spawn, _path)) continue;

            // 같은 길을 지나는 경로는 비켜 놓지 않고 그대로 겹친다 — 몬스터가 실제로 같은 선을 걷기 때문이다.
            PathPolyline line = new PathPolyline(spawn, _path, new Vector3(0f, _config.Lift, 0f));
            _lanes.Add(new PathLane(_root, _config, _config.GetColor(i), line));
        }
    }

    /// <summary>경로 표시를 숨기거나 보인다. 판 주사위 연출 동안은 숨긴다 — 굴림 결과(열린 포켓)가 미리 드러나지 않게.</summary>
    public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

    /// <summary>경로마다 첫 빛을 출현 구역에서 새로 내보내고, 바탕선이 그 빛을 따라 자라며 그려지게 한다(판 주사위 연출 뒤 처음 드러날 때).</summary>
    public void Reveal()
    {
        for (int i = 0; i < _lanes.Count; i++) _lanes[i].Reveal();
    }

    private void Clear()
    {
        for (int i = 0; i < _lanes.Count; i++) _lanes[i].Destroy();
        _lanes.Clear();
    }

    // 실제 시간을 따른다 — 길 표시는 판 정보라 멈춘 동안에도 흘러야 길을 읽으며 계획한다.
    // 꼬리(TrailRenderer)도 Unity 시간(Time.timeScale은 늘 1 — GameSpeed)을 따르므로 둘이 어긋나지 않는다.
    private void Tick(float deltaTime)
    {
        for (int i = 0; i < _lanes.Count; i++) _lanes[i].Tick(deltaTime);
    }
}
