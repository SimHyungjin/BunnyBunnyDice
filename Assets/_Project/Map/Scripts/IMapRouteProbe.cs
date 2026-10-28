using System.Collections.Generic;

/// <summary>
/// 맵 생성기가 묻는 "미로를 지으면 구멍마다 길이 얼마나 되는가"(기획서 3.3). 생성기는 미로 계획을 모르고 이것만 묻는다(MazeRouteProbe가 답한다).
/// </summary>
public interface IMapRouteProbe
{
    /// <summary>
    /// lengths[i] = map.Spawns[i]에서 목표물까지의 길 길이(칸), plan = 그 길을 낸 미로(주사위를 놓을 칸들). 같은 맵·시드면 같은 값.
    /// </summary>
    void Measure(MapData map, int seed, List<float> lengths, List<UnityEngine.Vector2Int> plan);
}
