using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 로비 화면을 띄우고 닫는다. 불러오는 동안 닫히면(곧바로 게임 시작 등) 다 불러온 화면을 띄우지 않고 닫는다(GameResult와 같은 세대 세기).
/// </summary>
public sealed class LobbyScreen
{
    private readonly HeroStats _stats;
    private readonly MetaProfile _meta;
    private readonly SO_MetaDiceConfig _diceConfig;
    private readonly Action _start;
    private readonly Action _selectCharacter;
    private readonly Action _openSettings;
    private UI_LobbyPopup _popup;
    private int _generation;

    /// <param name="start">게임 시작을 눌렀다. 화면은 이쪽이 닫는다.</param>
    /// <param name="selectCharacter">이름 버튼을 눌렀다(캐릭터 선택 창).</param>
    /// <param name="openSettings">설정 버튼을 눌렀다(설정 창).</param>
    public LobbyScreen(HeroStats stats, MetaProfile meta, SO_MetaDiceConfig diceConfig, Action start, Action selectCharacter, Action openSettings)
    {
        _openSettings = openSettings;
        _selectCharacter = selectCharacter;
        _stats = stats;
        _meta = meta;
        _diceConfig = diceConfig;
        _start = start;
    }

    public void Show() => ShowAsync().Forget();

    public void Close()
    {
        _generation++;
        if (_popup != null) App.ClosePopup(_popup);
        _popup = null;
    }

    private async UniTaskVoid ShowAsync()
    {
        Close();
        int generation = _generation;
        UI_LobbyPopup popup = await App.ShowPopup<UI_LobbyPopup>(clickGuard: false, clickToClose: false);
        if (generation != _generation)
        {
            if (popup != null) App.ClosePopup(popup);
            return;
        }
        if (popup == null)
        {
            Debug.LogWarning("[LobbyScreen] UI_LobbyPopup을 띄우지 못했다 — Addressables 주소를 확인");
            return;
        }
        _popup = popup;
        popup.Show(_stats, _meta, _diceConfig, OnStart, _selectCharacter, _openSettings);
    }

    /// <summary>
    /// "게임 시작"을 누른 것과 같다(봇이 로비에서 판을 시작할 때 — 사람과 같은 길로 들어가야 로비 화면이 닫히고 뛰어들기 연출이 돈다).
    /// 로비 화면이 떠 있지 않으면(이미 시작했거나 판 중) 아무 일도 없다.
    /// </summary>
    public void Start()
    {
        if (_popup == null) return;
        OnStart();
    }

    private void OnStart()
    {
        Close();
        _start();
    }
}
