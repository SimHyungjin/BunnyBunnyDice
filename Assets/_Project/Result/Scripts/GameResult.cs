using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 게임 오버 → 결과 화면 흐름. 기록을 받아 결과 팝업을 띄우고, 다시 하기·로비로를 누르면 팝업을 닫고 띄운 쪽에 알린다.
/// 팝업을 불러오는 동안 새 판이 시작되면(R 키 등) 다 불러온 팝업을 띄우지 않고 닫는다(LevelUpChoice와 같은 세대 세기).
/// </summary>
public sealed class GameResult
{
    private readonly Action _restart;
    private readonly Action _toLobby;
    private readonly SO_MetaDiceConfig _diceConfig;
    private UI_ResultPopup _popup;
    private int _generation;

    /// <param name="restart">다시 하기 — 새 판을 시작한다.</param>
    /// <param name="toLobby">로비로 — 판을 접고 로비로 돌아간다.</param>
    /// <param name="diceConfig">보상 주사위 연출.</param>
    public GameResult(Action restart, Action toLobby, SO_MetaDiceConfig diceConfig)
    {
        _diceConfig = diceConfig;
        _restart = restart;
        _toLobby = toLobby;
    }

    public void Show(RunSummary run, RunReward reward) => ShowAsync(run, reward).Forget();

    /// <summary>띄운(또는 불러오는 중인) 결과 화면을 닫는다(새 판).</summary>
    public void Close()
    {
        _generation++;
        if (_popup != null) App.ClosePopup(_popup);
        _popup = null;
    }

    private async UniTaskVoid ShowAsync(RunSummary run, RunReward reward)
    {
        Close();
        int generation = _generation;
        UI_ResultPopup popup = await App.ShowPopup<UI_ResultPopup>(clickGuard: true, clickToClose: false);
        if (generation != _generation)
        {
            if (popup != null) App.ClosePopup(popup);
            return;
        }
        if (popup == null)
        {
            Debug.LogWarning("[GameResult] UI_ResultPopup을 띄우지 못했다 — Addressables 주소를 확인");
            return;
        }
        _popup = popup;
        popup.Show(run, reward, _diceConfig, OnRetry, OnLobby);
    }

    private void OnRetry()
    {
        Close();
        _restart();
    }

    private void OnLobby()
    {
        Close();
        _toLobby();
    }
}
