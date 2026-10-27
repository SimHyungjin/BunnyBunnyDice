using System;

/// <summary>
/// 첫 웨이브를 플레이어가 시작한다(2026-10-28 사용자 결정). 판 주사위 연출이 끝나면 웨이브를 바로 내보내지 않고 기다리고,
/// 시작·일시정지 버튼의 ▶(또는 Space)를 누르면 시작한다 — 시작 전에 미로를 계획할 여유. 웨이브 사이는 기다리지 않는다(WaveDelay 0).
/// 봇이 켜져 있으면 기다리지 않는다(사람과 같은 행동을 코드로 부르는 봇이 버튼을 누를 필요가 없게).
/// 기다리는 동안인지는 Waiting으로 알리고, 무엇을 보일지(버튼 글·그리드)는 받는 쪽이 정한다.
/// </summary>
public sealed class WaveStartGate : IDisposable
{
    private readonly Action _begin;
    private readonly Func<bool> _autoStart;

    /// <summary>첫 웨이브 시작을 기다리는 중인지가 바뀌었다.</summary>
    public event Action<bool> WaitingChanged;

    public bool Waiting { get; private set; }

    /// <param name="begin">웨이브를 시작하는 동작(WaveSpawner.Begin).</param>
    /// <param name="autoStart">기다리지 않고 바로 시작할지(봇이 켜져 있으면 true).</param>
    public WaveStartGate(Action begin, Func<bool> autoStart)
    {
        _begin = begin;
        _autoStart = autoStart;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnUpdate -= Tick;

    /// <summary>판 준비가 끝났다 — 시작을 기다린다.</summary>
    public void Arm() => SetWaiting(true);

    /// <summary>기다리는 중이면 웨이브를 시작한다.</summary>
    public void Start()
    {
        if (!Waiting) return;
        SetWaiting(false);
        _begin();
    }

    /// <summary>시작하지 않고 기다림을 접는다(로비로·새 판).</summary>
    public void Cancel() => SetWaiting(false);

    private void SetWaiting(bool waiting)
    {
        if (Waiting == waiting) return;
        Waiting = waiting;
        WaitingChanged?.Invoke(waiting);
    }

    private void Tick(float _)
    {
        if (Waiting && _autoStart()) Start();
    }
}
