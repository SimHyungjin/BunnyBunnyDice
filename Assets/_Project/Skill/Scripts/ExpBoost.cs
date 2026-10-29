using System;
using UnityEngine;

/// <summary>
/// 한동안 처치 경험치에 곱하는 배율(더블 전용 카드 — 더블 업). 게임 시간으로 센다(멈춤·배속을 따른다).
/// 다시 걸면 시간이 새로 시작하고 배율은 큰 쪽을 남긴다.
/// </summary>
public sealed class ExpBoost : IDisposable
{
    private float _time;
    private float _scale = 1f;

    /// <summary>지금 처치 경험치에 곱할 값(없으면 1).</summary>
    public float Scale => _time > 0f ? _scale : 1f;

    public ExpBoost() => Core.Loop.OnGameUpdate += Tick;

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    public void Activate(float scale, float duration)
    {
        _scale = _time > 0f ? Mathf.Max(_scale, scale) : scale;
        _time = duration;
    }

    /// <summary>판이 바뀌면 끝낸다.</summary>
    public void Clear() => _time = 0f;

    private void Tick(float deltaTime) => _time = Mathf.Max(_time - deltaTime, 0f);
}
