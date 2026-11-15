using UnityEngine;

/// <summary>
/// 고정 시점 카메라 배치. 기울기·화각은 설정대로 두고, 맵 전체가 여백을 뺀 화면 영역에 들어오는 가장 가까운 거리를 이분 탐색한다.
/// 원근 카메라라 칸마다 투영 크기가 달라 식 하나로 풀기 어렵기 때문이다.
///
/// 옆 탁자(규칙 책상): 거리(칸 크기)는 맵만으로 정하고, 남는 좌우 여백 안에서만 카메라를 옆으로 민다 — 맵 + 탁자 묶음이
/// 가운데에 오도록 하되 맵이 여백을 넘지 않는 데까지만. 탁자 때문에 칸이 작아지지 않게 하려는 것이다(사용자 2026-11-12).
///
/// 화면 방향 맞춤: 화면의 긴 쪽과 맵의 긴 쪽이 다르면 카메라를 돌린다. 맵 데이터·칸 선택은 카메라 방향과 무관하므로
/// 세로 화면에서도 같은 맵을 그대로 쓰고 보는 방향만 바뀐다. 화면 크기가 바뀌면(회전·폴더블) 다시 부르면 된다.
/// </summary>
public static class MapCameraFitter
{
    private static Vector3[] _groupCorners;
    private static readonly Vector3[] _mainCorners = new Vector3[8];
    private static readonly Vector3[] _sideCorners = new Vector3[8];

    private struct Margins
    {
        public float Top, Bottom, Side;
    }

    /// <param name="emptyHand">빈 손 구도로 넘어간 정도(0~1, HandFraming.Weight).</param>
    /// <param name="sideTable">맵 옆에 놓인 탁자의 상자(맵 루트 기준). 있으면 남는 여백 안에서 묶음 가운데로 민다.</param>
    /// <param name="mainTable">맵을 받친 당구대 전체 상자(쿠션 포함, 맵 루트 기준). 옆으로 밀 때 이것이 여백을 넘지 않게 한다 — 칸만 보면 쿠션이 화면 끝에 걸린다.</param>
    public static void Fit(Camera camera, Transform mapRoot, MapData map, SO_MapCameraConfig config, SO_MapViewConfig viewConfig,
        float emptyHand = 0f, Bounds? sideTable = null, Bounds? mainTable = null)
    {
        if (camera == null) return;

        bool portraitScreen = camera.aspect < 1f;
        bool mapLongOnX = map.Width >= map.Height;
        bool rotate = config.AutoOrient && portraitScreen == mapLongOnX;
        float yaw = config.YawAngle + (rotate ? config.OrientYawOffset : 0f);

        Margins margins;
        config.GetMargins(portraitScreen, emptyHand, out margins.Top, out margins.Bottom, out margins.Side);

        camera.orthographic = false;
        camera.fieldOfView = config.FieldOfView;
        camera.transform.rotation = mapRoot.rotation * Quaternion.Euler(config.GetTilt(emptyHand), yaw, 0f);

        Vector3[] corners = GetMapCorners(mapRoot, map, config, viewConfig);
        Vector3 center = mapRoot.TransformPoint(new Vector3((map.Width - 1) * 0.5f, 0f, (map.Height - 1) * 0.5f));

        float near = config.SearchMinDistance, far = config.SearchMaxDistance;
        for (int i = 0; i < config.SearchIterations; i++)
        {
            float distance = (near + far) * 0.5f;
            if (PlaceAndCheck(camera, corners, center, distance, config, margins)) far = distance;
            else near = distance;
        }
        PlaceAndCheck(camera, corners, center, far, config, margins);
        if (sideTable.HasValue)
            ShiftTowardSideTable(camera, mapRoot, mainTable.HasValue ? GetBoxCorners(mapRoot, mainTable.Value, _mainCorners) : corners,
                sideTable.Value, far, config, margins);
    }

    // 맵 + 탁자 묶음의 가로 가운데를 화면 가운데로 옮기되, 맵이 옆 여백(Side)을 넘지 않는 데까지만 민다.
    // 원근이라 한 번에 정확히 맞지 않아 RecenterPasses만큼 되풀이한다. 가로만 민다 — 세로 화면(돌린 카메라)에서는 탁자가 위아래라 거의 0이다.
    private static void ShiftTowardSideTable(Camera camera, Transform mapRoot, Vector3[] mapCorners, Bounds table, float distance,
        SO_MapCameraConfig config, Margins margins)
    {
        // 손패 구도가 바뀌는 동안 매 프레임 불리므로 배열을 다시 쓴다.
        if (_groupCorners == null || _groupCorners.Length != mapCorners.Length + 8) _groupCorners = new Vector3[mapCorners.Length + 8];
        Vector3[] all = _groupCorners;
        for (int i = 0; i < mapCorners.Length; i++) all[i] = mapCorners[i];
        GetBoxCorners(mapRoot, table, _sideCorners);
        for (int c = 0; c < 8; c++) all[mapCorners.Length + c] = _sideCorners[c];

        Transform t = camera.transform;
        float frustumWidth = 2f * distance * Mathf.Tan(config.FieldOfView * 0.5f * Mathf.Deg2Rad) * camera.aspect;
        for (int pass = 0; pass < config.RecenterPasses; pass++)
        {
            GetViewportBounds(camera, all, out Vector2 groupMin, out Vector2 groupMax);
            GetViewportBounds(camera, mapCorners, out Vector2 mapMin, out Vector2 mapMax);
            float dx = (groupMin.x + groupMax.x) * 0.5f - 0.5f;
            // 오른쪽으로 밀면(dx > 0) 맵이 왼쪽으로 가므로 왼쪽 여백까지, 반대면 오른쪽 여백까지만.
            dx = Mathf.Clamp(dx, -Mathf.Max(0f, 1f - margins.Side - mapMax.x), Mathf.Max(0f, mapMin.x - margins.Side));
            t.position += t.right * (dx * frustumWidth);
        }
    }

    private static bool PlaceAndCheck(Camera camera, Vector3[] corners, Vector3 center, float distance,
        SO_MapCameraConfig config, Margins margins)
    {
        Transform t = camera.transform;
        t.position = center - t.forward * distance;

        // 위아래 여백이 달라 화면 중앙이 아니라 여백 영역의 중앙에 맵을 맞춘다.
        float targetY = (margins.Bottom + (1f - margins.Top)) * 0.5f;
        float frustumHeight = 2f * distance * Mathf.Tan(config.FieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector2 min, max;
        for (int pass = 0; pass < config.RecenterPasses; pass++)
        {
            GetViewportBounds(camera, corners, out min, out max);
            float dx = (min.x + max.x) * 0.5f - 0.5f;
            float dy = (min.y + max.y) * 0.5f - targetY;
            t.position += t.right * (dx * frustumHeight * camera.aspect) + t.up * (dy * frustumHeight);
        }

        GetViewportBounds(camera, corners, out min, out max);
        return max.x - min.x <= 1f - margins.Side * 2f
            && max.y - min.y <= 1f - margins.Top - margins.Bottom;
    }

    private static void GetViewportBounds(Camera camera, Vector3[] corners, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 v = camera.WorldToViewportPoint(corners[i]);
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }
    }

    private static Vector3[] GetBoxCorners(Transform mapRoot, Bounds box, Vector3[] result)
    {
        for (int c = 0; c < 8; c++)
            result[c] = mapRoot.TransformPoint(new Vector3((c & 1) == 0 ? box.min.x : box.max.x, (c & 2) == 0 ? box.min.y : box.max.y,
                (c & 4) == 0 ? box.min.z : box.max.z));
        return result;
    }

    private static Vector3[] GetMapCorners(Transform mapRoot, MapData map, SO_MapCameraConfig config, SO_MapViewConfig viewConfig)
    {
        float x0 = -0.5f, x1 = map.Width - 0.5f;
        float z0 = -0.5f, z1 = map.Height - 0.5f;
        float y0 = -viewConfig.GroundThickness, y1 = viewConfig.MaxTileTop + config.CharacterHeadroom;
        Vector3[] corners =
        {
            new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),
            new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1),
        };
        for (int i = 0; i < corners.Length; i++) corners[i] = mapRoot.TransformPoint(corners[i]);
        return corners;
    }
}
