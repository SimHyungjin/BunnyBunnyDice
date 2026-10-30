using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한동안 처치 경험치에 곱하는 배율(더블 전용 카드 — 더블 업). 게임 시간으로 센다(멈춤·배속을 따른다).
/// 장마다 따로 돌고 더 얹는 몫을 더한다(x2 두 장 = x3) — 다시 걸면 시간이 새로 시작하던 때는 앞 장이 끝나길 기다렸다 써야 했다(2026-10-31 사용자).
/// 곱하지 않고 더하므로 장 수에 직선으로만 큰다.
/// </summary>
public sealed class ExpBoost : IDisposable
{
    private readonly List<float> _times = new List<float>();
    // 장마다 더 얹는 몫(배율 - 1).
    private readonly List<float> _bonuses = new List<float>();

    /// <summary>지금 처치 경험치에 곱할 값(없으면 1).</summary>
    public float Scale
    {
        get
        {
            float scale = 1f;
            for (int i = 0; i < _bonuses.Count; i++) scale += _bonuses[i];
            return scale;
        }
    }

    public ExpBoost() => Core.Loop.OnGameUpdate += Tick;

    public void Dispose() => Core.Loop.OnGameUpdate -= Tick;

    public void Activate(float scale, float duration)
    {
        if (scale <= 1f || duration <= 0f) return;
        _times.Add(duration);
        _bonuses.Add(scale - 1f);
    }

    /// <summary>판이 바뀌면 끝낸다.</summary>
    public void Clear()
    {
        _times.Clear();
        _bonuses.Clear();
    }

    private void Tick(float deltaTime)
    {
        for (int i = _times.Count - 1; i >= 0; i--)
        {
            _times[i] -= deltaTime;
            if (_times[i] > 0f) continue;
            _times.RemoveAt(i);
            _bonuses.RemoveAt(i);
        }
    }
}
