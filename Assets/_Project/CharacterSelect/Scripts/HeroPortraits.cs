using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 캐릭터 얼굴(기획서 12.3). 캐릭터에 그림(SO_HeroCharacter.Portrait)이 있으면 그것을 쓰고,
/// 없으면 3D 모델을 멀리 떨어진 자리에 세우고 따로 둔 카메라로 한 번 찍은 사진을 쓴다(그림이 나오기 전까지의 자리 표시).
/// 그림·사진 모두 배경이 투명이라 잠긴 캐릭터는 같은 것을 검게 칠해(RawImage 색) 실루엣으로 쓴다.
/// 찍은 사진은 캐릭터마다 기억해 두고 창을 다시 열 때 다시 쓴다. 설정·캐릭터가 바뀌면 Clear로 버리고 다음에 다시 찍는다.
/// </summary>
public sealed class HeroPortraits : IDisposable
{
    // 찍는 자리. 판·로비 카메라에 걸리지 않게 멀리 떨어뜨린다(조정 값이 아니라 격리용이라 SO로 빼지 않는다).
    private static readonly Vector3 StudioPosition = new Vector3(0f, -1000f, 0f);
    // 사진 카메라만 보는 층. 층 이름 설정 없이 쓸 수 있게 마지막 층을 쓴다.
    private const int StudioLayer = 31;

    private readonly SO_CharacterSelectConfig _config;
    private readonly Material _outline;
    private readonly Dictionary<string, RenderTexture> _cache = new Dictionary<string, RenderTexture>();

    /// <param name="outline">툰 아웃라인 머티리얼(판 위 영웅과 같은 모습으로 찍으려고). 없으면 null.</param>
    public HeroPortraits(SO_CharacterSelectConfig config, Material outline)
    {
        _config = config;
        _outline = outline;
        _config.Changed += Clear;
    }

    public void Dispose()
    {
        _config.Changed -= Clear;
        Clear();
    }

    public Texture Get(SO_HeroCharacter character)
    {
        if (character == null) return null;
        if (character.Portrait != null) return character.Portrait;
        if (character.Prefab == null) return null;
        string id = MetaProfile.IdOf(character);
        if (_cache.TryGetValue(id, out RenderTexture cached) && cached != null && cached.IsCreated()) return cached;
        RenderTexture texture = Render(character);
        _cache[id] = texture;
        return texture;
    }

    /// <summary>찍어 둔 사진을 버린다(다음 Get에서 다시 찍는다).</summary>
    public void Clear()
    {
        foreach (RenderTexture texture in _cache.Values)
        {
            if (texture == null) continue;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }
        _cache.Clear();
    }

    private RenderTexture Render(SO_HeroCharacter character)
    {
        GameObject studio = new GameObject("PortraitStudio");
        studio.transform.position = StudioPosition;

        GameObject model = HeroModel.Create(character, studio.transform, _outline, out float height);
        // 모델 앞(+Z)을 카메라 쪽으로 두고 조금 돌린다.
        model.transform.localRotation = Quaternion.Euler(0f, _config.PortraitYaw, 0f);
        // 동작이 없으면 기본 자세(모델 그대로)로 찍는다.
        HeroAnimation animation = new HeroAnimation(model, character.Animations);
        animation.SetPose(HeroPose.Locomotion);
        animation.Tick(_config.PortraitPoseTime, 0f, 0f);
        SetLayer(studio.transform, StudioLayer);

        // 위 끝(키 × PortraitTop)부터 키 × 비율만큼을 세로로 꽉 채운다(발밑이 모델 원점).
        float frameHeight = height * _config.PortraitBodyRatio;
        float viewHeight = frameHeight * (1f + _config.PortraitMargin * 2f);
        Vector3 center = StudioPosition + Vector3.up * (height * _config.PortraitTop - frameHeight * 0.5f);
        float distance = viewHeight * 0.5f / Mathf.Tan(_config.PortraitFieldOfView * 0.5f * Mathf.Deg2Rad);

        Camera camera = new GameObject("PortraitCamera").AddComponent<Camera>();
        camera.transform.SetParent(studio.transform, false);
        camera.transform.SetPositionAndRotation(center + Vector3.forward * distance, Quaternion.LookRotation(Vector3.back));
        camera.enabled = false;
        camera.fieldOfView = _config.PortraitFieldOfView;
        camera.nearClipPlane = distance * 0.1f;
        camera.farClipPlane = distance * 3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << StudioLayer;
        // 투명 배경을 지키려고 끈다: HDR 중간 텍스처(알파 없음)와 후처리가 알파를 지운다.
        camera.allowHDR = false;
        camera.allowMSAA = false;
        UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;

        // 장면 조명(로비는 어둡다)만으로는 얼굴이 그늘지므로 찍는 자리에 키·림 라이트를 둔다. 멀리 떨어진 자리라 게임 화면에는 닿지 않는다.
        AddLight(studio.transform, center, viewHeight, _config.KeyLightOffset, _config.KeyLightColor, _config.KeyLightIntensity);
        AddLight(studio.transform, center, viewHeight, _config.RimLightOffset, _config.RimLightColor, _config.RimLightIntensity);

        Vector2Int size = _config.PortraitSize;
        RenderTexture texture = new RenderTexture(Mathf.Max(size.x, 16), Mathf.Max(size.y, 16), 24, RenderTextureFormat.ARGB32)
        {
            name = $"Portrait_{character.name}",
        };
        texture.Create();

        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = texture };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else
        {
            camera.targetTexture = texture;
            camera.Render();
            camera.targetTexture = null;
        }

        animation.Dispose();
        UnityEngine.Object.Destroy(studio);
        return texture;
    }

    // offset은 찍는 범위 높이의 배수이고 카메라 기준(x = 카메라 오른쪽, z = 카메라 쪽)이다. 카메라가 -Z를 보므로 오른쪽은 -X다.
    // 거리가 범위 높이에 비례하므로 모델 키와 상관없이 같은 밝기로 닿게 세기에 거리 제곱을 곱한다.
    private static void AddLight(Transform parent, Vector3 center, float viewHeight, Vector3 offset, Color color, float intensity)
    {
        if (intensity <= 0f) return;
        Vector3 position = center + new Vector3(-offset.x, offset.y, offset.z) * viewHeight;
        float distance = Vector3.Distance(position, center);
        Light light = new GameObject("PortraitLight").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.SetPositionAndRotation(position, Quaternion.LookRotation(center - position));
        light.type = LightType.Spot;
        light.spotAngle = 60f;
        light.range = distance * 2f;
        light.color = color;
        light.intensity = intensity * distance * distance;
        light.shadows = LightShadows.None;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
    }
}
