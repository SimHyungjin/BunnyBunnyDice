using System;

/// <summary>
/// 배속 버튼의 상태(SO_GameSpeedConfig.Steps 중 몇 번째인가). 버튼(UI_GameHud)과 속도(GameSpeed)를 서로 모르게 잇는다.
/// 새 판마다 첫 배율로 돌아간다 — 지난 판의 3배속으로 판 주사위 연출·초반 배치를 시작하지 않게.
/// </summary>
public sealed class GameSpeedToggle
{
    private readonly SO_GameSpeedConfig _config;
    private readonly GameSpeed _speed;
    private int _index;

    /// <summary>배율이 바뀌었다(버튼 글을 다시 쓴다).</summary>
    public event Action<float> Changed;

    public float Current => _config.Steps.Count > 0 ? _config.Steps[Math.Min(_index, _config.Steps.Count - 1)] : 1f;

    public GameSpeedToggle(SO_GameSpeedConfig config, GameSpeed speed)
    {
        _config = config;
        _speed = speed;
    }

    /// <summary>다음 배율로(끝이면 처음으로).</summary>
    public void Next()
    {
        _index = _config.Steps.Count > 0 ? (_index + 1) % _config.Steps.Count : 0;
        Apply();
    }

    /// <summary>첫 배율로(새 판). 설정을 고쳤을 때도 불러 지금 값을 다시 넣는다.</summary>
    public void Reset()
    {
        _index = 0;
        Apply();
    }

    private void Apply()
    {
        _speed.PlayerSpeed = Current;
        Changed?.Invoke(Current);
    }
}
