using System;
using UnityEngine;

/// <summary>
/// 각성이 켜지는 순간 그 주사위 발밑에서 고리가 한 번 퍼진다(표시만). 켜져 있는 동안의 표시(눈·외곽선 색)는 DiceView가 맡는다
/// — 순간만 눈에 띄게 하고 유지 상태는 조용히 두려고 둘을 나눈다.
/// </summary>
public sealed class DiceAwakenEffect : IDisposable
{
    private readonly SO_DiceConfig _config;
    private readonly DiceField _field;
    private readonly AttackEffects _effects;

    public DiceAwakenEffect(SO_DiceConfig config, DiceField field, AttackEffects effects)
    {
        _config = config;
        _field = field;
        _effects = effects;
        _field.AwakenRevealed += OnRevealed;
    }

    public void Dispose()
    {
        _field.AwakenRevealed -= OnRevealed;
    }

    private void OnRevealed(Vector3 position)
    {
        _effects.ShowBurst(position, _config.AwakenRingRadius, _config.AwakenRingColor);
    }
}
