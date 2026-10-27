using System;
using UnityEngine;

/// <summary>
/// 손패가 비었을 때 판을 아래로 넓혀 보이게 하는 비율(0 = 손패 있는 구도, 1 = 빈 손 구도). MapCameraFitter가 아래 여백과 기울기를 이 비율로 섞는다.
/// 손패 자리(아래 여백)는 카드가 있을 때만 필요하다 — 다 쓰고 나면 화면 아래가 통째로 빈다.
///
/// - 다 써서 비면 바로 넘어간다(EmptyHandDelay 0, 사용자 결정 — 빈 손인데 판이 머뭇거리면 굼떠 보인다). 늦추고 싶으면 그 값을 올린다.
/// - 카드를 끄는 중에는 비었다고 보지 않는다(손을 뗀 뒤에 판단) — 놓는 칸이 끄는 도중에 움직이면 안 된다.
/// - 빈 손에 카드가 들어오면 기다리지 않고 바로 돌아간다 — 카드가 판 아래를 가리기 전에 자리를 내준다.
/// 옮기는 빠르기는 카드가 손패 자리로 붙는 빠르기(SO_CardHandConfig.FollowSharpness)와 같다 — 들어오는 카드와 판이 한 박자로 움직인다.
/// 실제 시간이다(게임 배속·멈춤과 무관, 카드도 그렇다).
/// </summary>
public sealed class HandFraming : IDisposable
{
    // 이만큼 가까워지면 목표에 붙인다 — 지수 따라가기는 끝없이 다가가기만 해서, 붙이지 않으면 매 프레임 카메라를 다시 맞춘다. 조정 대상 아님.
    private const float SnapDistance = 0.001f;

    private readonly Func<SO_MapCameraConfig> _getConfig;
    private readonly Func<SO_CardHandConfig> _getHandConfig;
    private readonly Func<int> _getCount;
    private readonly Func<bool> _isBusy;
    private readonly Action _refit;

    private float _emptyTime;

    /// <param name="refit">비율이 바뀐 프레임마다 카메라를 다시 맞추는 동작.</param>
    public HandFraming(Func<SO_MapCameraConfig> getConfig, Func<SO_CardHandConfig> getHandConfig, Func<int> getCount, Func<bool> isBusy,
        Action refit)
    {
        _getConfig = getConfig;
        _getHandConfig = getHandConfig;
        _getCount = getCount;
        _isBusy = isBusy;
        _refit = refit;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
    }

    /// <summary>빈 손 구도로 넘어간 정도(0~1).</summary>
    public float Weight { get; private set; }

    private void Tick(float deltaTime)
    {
        bool empty = _getCount() == 0 && !_isBusy();
        _emptyTime = empty ? _emptyTime + deltaTime : 0f;
        float target = empty && _emptyTime >= _getConfig().EmptyHandDelay ? 1f : 0f;
        if (Weight == target) return;
        float t = 1f - Mathf.Exp(-_getHandConfig().FollowSharpness * deltaTime);
        Weight = Mathf.Abs(target - Weight) <= SnapDistance ? target : Mathf.Lerp(Weight, target, t);
        _refit();
    }
}
