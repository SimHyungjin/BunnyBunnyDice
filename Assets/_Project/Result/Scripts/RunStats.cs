using System;

/// <summary>한 판의 측정값(기획서 11.2의 4번, 12.4). 판이 끝나면 RunStats.Ended로 나간다.</summary>
public struct RunSummary
{
    /// <summary>클리어한 웨이브(진 웨이브 − 1, 첫 웨이브에서 지면 0).</summary>
    public int Wave;
    public int Level;
    public int Kills;
    public int JackpotKills;
    /// <summary>빈 칸에 새로 놓은 주사위 수(얹기는 세지 않는다 — 미로를 짠 양을 재려고).</summary>
    public int DicePlaced;
    /// <summary>주사위가 각성한 횟수. 같은 주사위가 풀렸다가(다시 굴리기) 다시 각성하면 또 센다.</summary>
    public int Awakenings;
}

/// <summary>
/// 판 동안 측정값을 모은다. 판 시스템이 무엇을 셀지 연결은 GameScene이 하고, 듣는 쪽(메타 해금·나중의 시뮬레이션 기록기)은
/// 요약만 받는다 — 해금 코드가 몬스터·주사위 시스템을 모르게(12.4).
/// </summary>
public sealed class RunStats
{
    private RunSummary _current;
    private bool _finished;

    /// <summary>판이 끝났다(게임 오버). 판마다 한 번.</summary>
    public event Action<RunSummary> Ended;

    public RunSummary Current => _current;

    /// <summary>새 판.</summary>
    public void Reset()
    {
        _current = default;
        _finished = false;
    }

    public void CountKill(bool jackpot)
    {
        _current.Kills++;
        if (jackpot) _current.JackpotKills++;
    }

    public void CountDicePlaced() => _current.DicePlaced++;

    public void CountAwakening() => _current.Awakenings++;

    public RunSummary Finish(int wave, int level)
    {
        _current.Wave = wave;
        _current.Level = level;
        if (!_finished)
        {
            _finished = true;
            Ended?.Invoke(_current);
        }
        return _current;
    }
}
