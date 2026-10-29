using UnityEngine;

/// <summary>
/// 화면 좌표 → 칸. 벽·칩 윗면을 먼저 보고, 맞은 칸이 그 지형이 아니면 바닥면으로 다시 본다
/// — 기울어진 카메라에서는 솟은 블록이 뒤쪽 바닥 칸을 가리므로, 눈에 보이는 윗면을 우선해야 손가락 아래 칸과 일치한다.
/// 콜라이더 없이 평면 계산만 한다(블록 수만큼 콜라이더를 둘 필요가 없고, 나중의 물리 판정에도 섞이지 않는다).
/// </summary>
public sealed class MapPicker
{
    private readonly Transform _mapRoot;
    private readonly SO_MapViewConfig _viewConfig;

    public MapPicker(Transform mapRoot, SO_MapViewConfig viewConfig)
    {
        _mapRoot = mapRoot;
        _viewConfig = viewConfig;
    }

    public bool TryScreenToCell(Camera camera, MapData map, Vector2 screenPosition, out Vector2Int cell)
    {
        cell = default;
        if (camera == null || map == null) return false;

        Ray worldRay = camera.ScreenPointToRay(screenPosition);
        Ray ray = new Ray(_mapRoot.InverseTransformPoint(worldRay.origin), _mapRoot.InverseTransformDirection(worldRay.direction));

        // 높은 윗면부터 본다. 벽을 가리키면 벽 칸을 돌려준다(배치 불가로 보여야 손가락 아래 모습과 맞는다).
        if (TryRayToCell(ray, map, _viewConfig.WallHeight, out cell) && map.Get(cell.x, cell.y) == TileType.Wall) return true;
        if (TryRayToCell(ray, map, _viewConfig.ChipHeight, out cell) && map.HasChip(cell.x, cell.y)) return true;
        return TryRayToCell(ray, map, 0f, out cell);
    }

    /// <summary>
    /// 화면 좌표 → 판 바닥 위 점(맵 로컬 x, z). 적 대상 스킬처럼 칸이 아니라 자리를 고를 때 쓴다(기획서 8).
    /// 원은 적이 걷는 바닥에 그리므로 솟은 윗면을 보지 않고 바닥면으로만 본다. 맵 밖이면 false.
    /// </summary>
    public bool TryScreenToPoint(Camera camera, MapData map, Vector2 screenPosition, out Vector2 point)
    {
        point = default;
        if (camera == null || map == null) return false;

        Ray worldRay = camera.ScreenPointToRay(screenPosition);
        Ray ray = new Ray(_mapRoot.InverseTransformPoint(worldRay.origin), _mapRoot.InverseTransformDirection(worldRay.direction));
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float enter)) return false;

        Vector3 hit = ray.GetPoint(enter);
        point = new Vector2(hit.x, hit.z);
        Vector2Int cell = GridCircle.CellOf(point);
        return map.InBounds(cell.x, cell.y);
    }

    private static bool TryRayToCell(Ray localRay, MapData map, float planeHeight, out Vector2Int cell)
    {
        cell = default;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
        if (!plane.Raycast(localRay, out float enter)) return false;

        Vector3 hit = localRay.GetPoint(enter);
        cell = new Vector2Int(Mathf.RoundToInt(hit.x), Mathf.RoundToInt(hit.z));
        return map.InBounds(cell.x, cell.y);
    }
}
