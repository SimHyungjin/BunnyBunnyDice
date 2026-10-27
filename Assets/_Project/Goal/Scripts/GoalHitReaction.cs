using System;
using UnityEngine;

/// <summary>
/// 몬스터가 목표물에 닿을 때의 반응(표시만): 목표물 표식이 찌그러졌다 튀어 오르고, 잠깐 빛나고, 바닥에 고리가 퍼진다.
/// 체력이 깎이는 순간이 HUD 숫자만이 아니라 판 위에서 보이게 하려는 것이다. 화면 흔들림은 쓰지 않는다(타워 디펜스라 판이 흔들리면 배치가 불편하다).
/// 연달아 닿으면 처음부터 다시 재생한다. 카드를 집는 동안의 감속과 상관없이 같은 빠르기로 보이게 실제 시간으로 진행한다.
/// </summary>
public sealed class GoalHitReaction : IDisposable
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly SO_GoalConfig _config;
    private readonly MapView _view;
    private readonly AttackEffects _effects;
    private float _time;
    private bool _playing;

    public GoalHitReaction(SO_GoalConfig config, MapView view, AttackEffects effects)
    {
        _config = config;
        _view = view;
        _effects = effects;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        Apply(1f);
    }

    public void Play()
    {
        Transform goal = _view.Goal;
        if (goal == null) return;
        _time = 0f;
        _playing = true;
        if (_config.HitBurstRadius > 0f) _effects.ShowBurst(goal.localPosition, _config.HitBurstRadius, _config.HitColor);
        Apply(0f);
    }

    private void Tick(float deltaTime)
    {
        if (!_playing) return;
        _time += deltaTime;
        float progress = _config.HitDuration > 0f ? _time / _config.HitDuration : 1f;
        if (progress >= 1f)
        {
            _playing = false;
            progress = 1f;
        }
        Apply(progress);
    }

    // progress 0 = 막 닿음, 1 = 끝(원래 모습).
    private void Apply(float progress)
    {
        Transform goal = _view.Goal;
        if (goal == null) return;

        // 줄어드는 진동: 먼저 눌렸다가(세로 ↓ 가로 ↑) 튀어 오르며 잦아든다. 부피가 대략 유지되게 가로는 세로 변화의 절반만큼 반대로.
        float fade = (1f - progress) * (1f - progress);
        float wave = Mathf.Sin(progress * Mathf.PI * 2f * _config.HitBounces);
        float y = 1f - _config.HitSquash * wave * fade;
        float xz = 1f + (1f - y) * 0.5f;
        goal.localScale = new Vector3(xz, y, xz);

        Material flash = _view.GoalFlashMaterial;
        if (flash != null) flash.SetColor(EmissionColorId, _config.HitColor * (_config.HitFlashIntensity * fade));
    }
}
