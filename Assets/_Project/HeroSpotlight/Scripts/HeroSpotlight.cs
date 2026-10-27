using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 영웅을 따라다니는 스포트라이트(표시만) — 시상식 팔로우 스포트처럼 테이블 위 높은 곳에 고정된 조명기(SourceOffset)가 고개만 돌려 영웅을 따라간다.
/// 로비에서는 무대 조명이고, 게임 중에는 판 위에서 영웅을 놓치지 않게 하는 표시다.
/// 로비 값과 게임 값을 섞어 쓴다 — 섞는 비율은 로비 카메라가 게임 자리로 옮겨 간 정도(LobbyStage.LobbyWeight)라 뛰어드는 동안 자연히 넘어간다.
/// 조명기 자리는 둘이 같다 — 뛰어드는 영웅을 같은 빛줄기가 끊김 없이 따라간다.
///
/// - 키 라이트(조명기)·림 라이트(뒤, 로비만): 해(씬 Directional)는 판을 위한 각도라 카메라를 마주 보는 로비 캐릭터는 역광이 되어 얼굴이 그늘진다.
///   해를 돌리면 판 조명이 바뀌므로 영웅을 비추는 스포트라이트를 따로 둔다.
/// - 방 어둡게: 무대처럼 보이게 해·주변광을 낮춘다(SceneLighting.SetDim).
/// - 발밑 빛 웅덩이·빛줄기: 실제 조명으로 바닥을 밝히면 같은 빛을 받는 영웅이 하얗게 날아가므로, 바닥 쪽은 빛을 더하는 판으로 따로 그린다.
///   예전엔 영웅 위로 곧은 원기둥을 세웠더니 판 위에서 기둥이 서 있는 것처럼 보였다 — 조명기에서 비스듬히 내려오는 빛줄기라야 "비춘다"로 읽힌다.
///
/// 조명기 자리는 카메라 기준(오른쪽·위·카메라가 보는 쪽)이라 화면을 세로로 돌려도 같은 쪽에서 온다. 크기는 지금 보이는 영웅 키의 배수다.
/// 밝기는 "영웅에 닿는 밝기"로 받아 거리 제곱을 곱해 넣는다(URP 조명은 거리 제곱으로 약해진다).
/// 원점이 아니라 몸 가운데를 따라간다(Hero.ViewBody) — 대기 동작이 몸을 원점 옆으로 옮겨 세우면 조명이 한쪽으로 치우쳐 보인다.
/// </summary>
public sealed class HeroSpotlight : IDisposable
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int TopRatioId = Shader.PropertyToID("_TopRatio");
    private static readonly int BottomFadeId = Shader.PropertyToID("_BottomFade");
    private static readonly int TopBrightnessId = Shader.PropertyToID("_TopBrightness");
    private static readonly int FloorYId = Shader.PropertyToID("_FloorY");
    private static readonly int FloorFadeId = Shader.PropertyToID("_FloorFade");
    private static readonly int EdgePowerId = Shader.PropertyToID("_EdgePower");

    // 빛줄기 둘레를 나누는 수. 조정 대상이 아니다 — 흐린 가산 면이라 이 정도면 각이 보이지 않는다.
    private const int BeamSegments = 32;

    private readonly Func<SO_HeroSpotlightConfig> _getConfig;
    private readonly SceneLighting _lighting;
    private readonly Hero _hero;
    private readonly Transform _mapRoot;
    private readonly Func<MapData> _getMap;
    private readonly Func<float> _getLobbyWeight;
    private readonly Func<Camera> _getCamera;
    private readonly Light _key;
    private readonly Light _rim;
    private readonly Transform _pool;
    private readonly Material _poolMaterial;
    private readonly Transform _beam;
    private readonly Material _beamMaterial;
    private readonly Mesh _beamMesh;

    /// <param name="getConfig">설정(실행 중 복사본이 바뀌어도 따라가게 매번 읽는다).</param>
    /// <param name="getLobbyWeight">1 = 로비, 0 = 게임.</param>
    public HeroSpotlight(Transform parent, Func<SO_HeroSpotlightConfig> getConfig, SceneLighting lighting, Hero hero,
        Func<MapData> getMap, Func<float> getLobbyWeight, Func<Camera> getCamera)
    {
        _mapRoot = parent;
        _getMap = getMap;
        _getConfig = getConfig;
        _lighting = lighting;
        _hero = hero;
        _getLobbyWeight = getLobbyWeight;
        _getCamera = getCamera;

        SO_HeroSpotlightConfig config = getConfig();
        _key = CreateSpot(parent, "HeroKeyLight");
        _rim = CreateSpot(parent, "HeroRimLight");
        if (config.PoolShader != null)
        {
            _poolMaterial = new Material(config.PoolShader) { name = "HeroLightPool" };
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(pool.GetComponent<Collider>());
            _pool = SetUpGlow(pool, parent, "HeroLightPool", _poolMaterial);
        }
        if (config.BeamShader != null)
        {
            _beamMaterial = new Material(config.BeamShader) { name = "HeroLightBeam" };
            _beamMesh = BuildBeamMesh();
            GameObject beam = new GameObject();
            beam.AddComponent<MeshFilter>().sharedMesh = _beamMesh;
            beam.AddComponent<MeshRenderer>();
            _beam = SetUpGlow(beam, parent, "HeroLightBeam", _beamMaterial);
        }
        // 영웅·카메라가 이번 프레임 자리를 잡은 뒤에 따라간다.
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnLateUpdate -= LateTick;
        if (_key != null) Object.Destroy(_key.gameObject);
        if (_rim != null) Object.Destroy(_rim.gameObject);
        if (_pool != null) Object.Destroy(_pool.gameObject);
        if (_poolMaterial != null) Object.Destroy(_poolMaterial);
        if (_beam != null) Object.Destroy(_beam.gameObject);
        if (_beamMaterial != null) Object.Destroy(_beamMaterial);
        if (_beamMesh != null) Object.Destroy(_beamMesh);
        _lighting.SetDim(1f);
    }

    private void LateTick(float deltaTime)
    {
        Camera camera = _getCamera();
        MapData map = _getMap();
        if (camera == null || map == null) return;
        SO_HeroSpotlightConfig config = _getConfig();
        HeroSpotlightLook look = HeroSpotlightLook.Lerp(config.Game, config.Lobby, _getLobbyWeight());

        Vector3 body = _hero.ViewBody;
        Vector3 foot = _hero.ViewFoot;
        float height = _hero.ViewHeight;
        Quaternion view = camera.transform.rotation;
        Vector3 source = SourcePosition(config, map, camera);

        AimKey(source, body, height, config.KeyLightColor, look);
        AimRim(_rim, body, view, height, look.RimLightOffset, config.RimLightColor, look.RimLightBrightness, look.RimLightAngle);
        _lighting.SetDim(look.RoomDim);
        PlacePool(config, look, source, foot, height);
        PlaceBeam(config, look, source, foot, height);
    }

    // 판 가운데 바닥에서 카메라 기준으로 띄운 고정 자리. 카메라의 수평 방향만 쓴다 — 로비와 게임 카메라는 같은 쪽을 보므로(LobbyStage)
    // 뛰어드는 동안 카메라가 기울어도 조명기는 제자리다.
    private Vector3 SourcePosition(SO_HeroSpotlightConfig config, MapData map, Camera camera)
    {
        Vector3 center = _mapRoot.TransformPoint(new Vector3((map.Width - 1) * 0.5f, 0f, (map.Height - 1) * 0.5f));
        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        Vector3 offset = config.SourceOffset;
        return center + (right * offset.x + Vector3.up * offset.y + forward * offset.z) * _mapRoot.lossyScale.x;
    }

    // 조명기에서 몸 가운데로. 원뿔 폭은 영웅 자리에서의 빛 원 반지름으로 정한다 — 거리가 바뀌어도 영웅 둘레만 비춘다.
    private void AimKey(Vector3 source, Vector3 body, float height, Color color, HeroSpotlightLook look)
    {
        _key.enabled = look.KeyLightBrightness > 0f;
        if (!_key.enabled) return;
        float distance = Mathf.Max(Vector3.Distance(source, body), 0.01f);
        float angle = 2f * Mathf.Atan(look.KeyLightRadius * height / distance) * Mathf.Rad2Deg;
        _key.transform.SetPositionAndRotation(source, Quaternion.LookRotation(body - source));
        _key.color = color;
        _key.intensity = look.KeyLightBrightness * distance * distance;
        _key.range = distance * 2f;
        _key.spotAngle = angle;
        _key.innerSpotAngle = angle * 0.6f;
    }

    // 발밑에 깐다. 비스듬히 오는 빛이 바닥에 닿은 모양처럼 조명기 반대쪽으로 길쭉한 타원이다(낮게 올수록 길다).
    // 가운데는 발에 둔다 — 실제 빛 원은 조금 밀려나지만, 무대 조명은 사람을 가운데 두고 떨어져 보여야 읽힌다.
    private void PlacePool(SO_HeroSpotlightConfig config, HeroSpotlightLook look, Vector3 source, Vector3 foot, float height)
    {
        if (_pool == null) return;
        bool visible = look.PoolBrightness > 0f && look.PoolRadius > 0f;
        _pool.gameObject.SetActive(visible);
        if (!visible) return;
        Vector3 away = foot - source;
        float sine = away.magnitude > 0f ? -away.y / away.magnitude : 1f;
        away.y = 0f;
        float stretch = Mathf.Min(1f / Mathf.Max(sine, 0.01f), config.PoolMaxStretch);
        // 사각 판은 바닥을 보게 눕히고 세로 축을 조명기 반대쪽으로 돌린다 — 그 축만 늘인다.
        Quaternion rotation = away.sqrMagnitude > 0f ? Quaternion.LookRotation(Vector3.down, away) : Quaternion.Euler(90f, 0f, 0f);
        float diameter = look.PoolRadius * height * 2f / ParentScale(_pool);
        _pool.SetPositionAndRotation(foot + Vector3.up * (config.PoolLift * height), rotation);
        _pool.localScale = new Vector3(diameter, diameter * stretch, diameter);
        _poolMaterial.SetColor(ColorId, config.KeyLightColor * look.PoolBrightness);
        _poolMaterial.SetFloat(SoftnessId, config.PoolSoftness);
    }

    // 조명기에서 발까지 비스듬한 원뿔. 바닥 쪽 반지름이 웅덩이와 같아 빛이 내려와 바닥에 고인 것처럼 이어진다.
    // 원뿔 밑면은 축에 수직이라 비스듬하면 반쪽이 바닥 위로 떠 끊긴 자리가 보인다 — 밑면을 축을 따라 바닥 밑까지 늘여 바닥이 잘라 주게 한다.
    private void PlaceBeam(SO_HeroSpotlightConfig config, HeroSpotlightLook look, Vector3 source, Vector3 foot, float height)
    {
        if (_beam == null) return;
        bool visible = look.BeamBrightness > 0f && look.PoolRadius > 0f;
        _beam.gameObject.SetActive(visible);
        if (!visible) return;
        float radius = look.PoolRadius * height;
        Vector3 axis = source - foot;
        float length = axis.magnitude;
        if (length <= 0f) return;
        axis /= length;
        float sine = Mathf.Max(axis.y, 0.01f);
        float cosine = Mathf.Sqrt(Mathf.Max(1f - sine * sine, 0f));
        float sink = radius * cosine / sine;
        _beam.SetPositionAndRotation(foot - axis * sink, Quaternion.FromToRotation(Vector3.up, axis));
        _beam.localScale = new Vector3(radius, length + sink, radius) / ParentScale(_beam);
        _beamMaterial.SetColor(ColorId, config.KeyLightColor * look.BeamBrightness);
        _beamMaterial.SetFloat(TopRatioId, config.BeamTopRatio);
        _beamMaterial.SetFloat(BottomFadeId, look.BeamBottom);
        _beamMaterial.SetFloat(TopBrightnessId, look.BeamTop);
        _beamMaterial.SetFloat(EdgePowerId, config.BeamEdgeSoftness);
        _beamMaterial.SetFloat(FloorYId, foot.y);
        _beamMaterial.SetFloat(FloorFadeId, Mathf.Max(config.BeamFloorFade * height, 0.0001f));
    }

    private static float ParentScale(Transform t) => t.parent != null ? t.parent.lossyScale.x : 1f;

    // offset: x = 카메라 오른쪽, y = 위, z = 카메라 쪽(음수면 영웅 뒤). 영웅 키 배수.
    private static void AimRim(Light light, Vector3 focus, Quaternion view, float height, Vector3 offset, Color color, float brightness, float angle)
    {
        light.enabled = brightness > 0f;
        if (!light.enabled) return;
        Vector3 position = focus
            + view * Vector3.right * (offset.x * height)
            + Vector3.up * (offset.y * height)
            - view * Vector3.forward * (offset.z * height);
        float distance = Mathf.Max(Vector3.Distance(position, focus), 0.01f);
        light.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
        light.color = color;
        light.intensity = brightness * distance * distance;
        light.range = distance * 2f;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.6f;
    }

    private static Light CreateSpot(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        // 그림자는 끈다 — 해의 그림자와 겹쳐 판에 그림자가 두 겹 생긴다.
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    // 빛 표현용 판(웅덩이·빛줄기): 그림자를 만들지도 받지도 않는다.
    private static Transform SetUpGlow(GameObject go, Transform parent, string name, Material material)
    {
        go.name = name;
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    // 반지름 1·높이 1(바닥 0 → 위 1)의 뚜껑 없는 원기둥. 윗면을 좁히는 것은 셰이더가 한다.
    private static Mesh BuildBeamMesh()
    {
        Vector3[] vertices = new Vector3[(BeamSegments + 1) * 2];
        int[] triangles = new int[BeamSegments * 6];
        for (int i = 0; i <= BeamSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / BeamSegments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x, 0f, z);
            vertices[i * 2 + 1] = new Vector3(x, 1f, z);
        }
        for (int i = 0; i < BeamSegments; i++)
        {
            int b = i * 2;
            int t = i * 6;
            triangles[t] = b;
            triangles[t + 1] = b + 1;
            triangles[t + 2] = b + 2;
            triangles[t + 3] = b + 2;
            triangles[t + 4] = b + 1;
            triangles[t + 5] = b + 3;
        }
        Mesh mesh = new Mesh { name = "HeroLightBeam" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        // 셰이더가 윗면을 좁히기 전 크기라 경계가 실제보다 크다 — 화면 밖 판정에서 잘리지 않는 쪽이라 괜찮다.
        mesh.RecalculateBounds();
        return mesh;
    }
}
