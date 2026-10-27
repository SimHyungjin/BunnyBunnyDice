using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// 설정대로 조명과 후처리를 맞춘다. 해는 씬에 있는 Directional Light를 쓰고(씬 환경), 후처리 볼륨은 코드로 만든다
/// — 볼륨 프로필을 에셋으로 두면 값이 SO와 프로필 두 곳에 나뉘므로, 프로필은 설정값으로 그때그때 채운다.
/// </summary>
public sealed class SceneLighting : IDisposable
{
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineDepthBiasId = Shader.PropertyToID("_OutlineDepthBias");

    private readonly SO_LightingConfig _config;
    private readonly Volume _volume;
    private readonly VolumeProfile _profile;
    private readonly Tonemapping _tonemapping;
    private readonly ColorAdjustments _colorAdjustments;
    private readonly Bloom _bloom;
    private readonly Vignette _vignette;
    private Cubemap _reflection;
    private float _dim = 1f;

    /// <summary>
    /// 게임 물체에 붙이는 툰 아웃라인 머티리얼(모두 이 하나를 같이 쓴다). 셰이더가 없으면 null.
    /// 실행 중에 만든 것이라 색·굵기를 바꿔도 에셋이 더러워지지 않고, Apply가 바꾸면 붙은 물체 모두에 바로 반영된다.
    /// </summary>
    public Material OutlineMaterial { get; }

    /// <summary>캐릭터(영웅)용 아웃라인 — 색·굵기는 OutlineMaterial과 같고 깊이 당김만 작다(얇은 귀·팔다리를 덮지 않게). 셰이더가 없으면 null.</summary>
    public Material CharacterOutlineMaterial { get; }

    public SceneLighting(SO_LightingConfig config)
    {
        _config = config;
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _tonemapping = _profile.Add<Tonemapping>(true);
        _colorAdjustments = _profile.Add<ColorAdjustments>(true);
        _bloom = _profile.Add<Bloom>(true);
        _vignette = _profile.Add<Vignette>(true);

        // 씬 오브젝트라 씬을 벗어나면 함께 사라진다.
        _volume = new GameObject("PostProcess").AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.sharedProfile = _profile;

        if (config.OutlineShader != null)
        {
            OutlineMaterial = new Material(config.OutlineShader) { name = "Outline" };
            CharacterOutlineMaterial = new Material(config.OutlineShader) { name = "CharacterOutline" };
        }
    }

    /// <summary>
    /// 방 밝기 배율(해·주변광, 1 = 설정 그대로). 영웅 스포트라이트가 무대처럼 보이게 방을 어둡게 한다(HeroSpotlight).
    /// 설정을 고쳐 Apply가 다시 불려도 이 배율은 유지된다.
    /// </summary>
    public void SetDim(float dim)
    {
        if (Mathf.Approximately(_dim, dim)) return;
        _dim = dim;
        ApplyDim();
    }

    private void ApplyDim()
    {
        Light sun = FindSun();
        if (sun != null) sun.intensity = _config.SunIntensity * _dim;
        RenderSettings.ambientSkyColor = _config.AmbientSky * _dim;
        RenderSettings.ambientEquatorColor = _config.AmbientEquator * _dim;
        RenderSettings.ambientGroundColor = _config.AmbientGround * _dim;
    }

    public void Apply(Camera camera)
    {
        Light sun = FindSun();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(_config.SunRotation);
            sun.color = _config.SunColor;
            sun.shadowStrength = _config.ShadowStrength;
        }

        // 반사 환경은 값이 바뀔 수 있어 Apply마다 다시 만든다(64칸 큐브맵이라 가볍다).
        if (_reflection != null) Object.Destroy(_reflection);
        _reflection = StudioReflection.Create(_config);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = _reflection;
        RenderSettings.reflectionIntensity = _config.ReflectionIntensity;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        ApplyDim();

        _volume.priority = _config.VolumePriority;
        _tonemapping.mode.value = _config.Tonemapping;
        _colorAdjustments.postExposure.value = _config.PostExposure;
        _colorAdjustments.contrast.value = _config.Contrast;
        _colorAdjustments.saturation.value = _config.Saturation;
        _bloom.intensity.value = _config.BloomIntensity;
        _bloom.threshold.value = _config.BloomThreshold;
        _bloom.scatter.value = _config.BloomScatter;
        _vignette.intensity.value = _config.VignetteIntensity;
        _vignette.color.value = _config.VignetteColor;
        _vignette.smoothness.value = _config.VignetteSmoothness;

        if (OutlineMaterial != null)
        {
            OutlineMaterial.SetColor(OutlineColorId, _config.OutlineColor);
            OutlineMaterial.SetFloat(OutlineWidthId, _config.OutlineWidth);
            OutlineMaterial.SetFloat(OutlineDepthBiasId, _config.OutlineDepthBias);
            CharacterOutlineMaterial.SetColor(OutlineColorId, _config.OutlineColor);
            CharacterOutlineMaterial.SetFloat(OutlineWidthId, _config.OutlineWidth);
            CharacterOutlineMaterial.SetFloat(OutlineDepthBiasId, _config.CharacterOutlineDepthBias);
        }

        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _config.BackgroundColor;
            // 카메라에서 후처리가 꺼져 있으면 볼륨이 있어도 아무것도 적용되지 않는다.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }

    public void Dispose()
    {
        if (_profile != null) Object.Destroy(_profile);
        if (_reflection != null) Object.Destroy(_reflection);
        if (OutlineMaterial != null) Object.Destroy(OutlineMaterial);
        if (CharacterOutlineMaterial != null) Object.Destroy(CharacterOutlineMaterial);
    }

    private static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i].type == LightType.Directional) return lights[i];
        return null;
    }
}
