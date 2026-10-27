using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 설정 창을 띄우고 닫는다. 이미 떠 있으면 다시 띄우지 않는다. 불러오는 동안 닫히면(게임 시작 등) 다 불러온 창을 띄우지 않고 닫는다
/// (CharacterSelectScreen과 같은 세대 세기). 창은 바깥·뒤로가기로도 닫히므로, 닫힌 것은 창의 닫힘 이벤트로 안다.
/// 판 중에 열면 판을 멈추고(GameSpeed 0) 닫히면 되돌린다 — 불러오는 동안에도 멈춰 있게 누른 순간 멈춘다.
/// </summary>
public sealed class SettingsScreen
{
    private readonly GameSpeed _speed;
    private UI_SettingsPopup _popup;
    private bool _loading;
    private int _generation;
    private Action _giveUp;

    public SettingsScreen(GameSpeed speed) => _speed = speed;

    /// <summary>로비에서 연다(포기하기 없음, 멈추지 않음).</summary>
    public void Show() => Open(null);

    /// <summary>판 중에 연다: 판을 멈추고 포기하기를 붙인다. 포기하면 창을 닫고 giveUp을 부른다.</summary>
    public void ShowInGame(Action giveUp) => Open(giveUp);

    private void Open(Action giveUp)
    {
        // 닫히는 연출 중인 창은 닫힌 것으로 본다(곧 반납된다).
        if (_loading || (_popup != null && _popup.IsOpened)) return;
        _popup = null;
        _giveUp = giveUp;
        if (giveUp != null) _speed.Request(this, 0f);
        ShowAsync().Forget();
    }

    public void Close()
    {
        _generation++;
        _loading = false;
        if (_popup != null) App.ClosePopup(_popup);
        _popup = null;
        _speed.Release(this);
    }

    private async UniTaskVoid ShowAsync()
    {
        _loading = true;
        int generation = _generation;
        UI_SettingsPopup popup = await App.ShowPopup<UI_SettingsPopup>(clickGuard: true, clickToClose: true);
        if (generation != _generation)
        {
            if (popup != null) App.ClosePopup(popup);
            return;
        }
        _loading = false;
        if (popup == null)
        {
            Debug.LogWarning("[SettingsScreen] UI_SettingsPopup을 띄우지 못했다 — Addressables 주소를 확인");
            _speed.Release(this);
            return;
        }
        _popup = popup;
        // 표시 단위 구독이라 반납 때 지워진다(UI_Panel). 바깥·뒤로가기로 닫혀도 판이 다시 움직인다.
        popup.OnCloseEvent.AddListener(() =>
        {
            if (_popup != popup) return;
            _popup = null;
            _speed.Release(this);
        });
        Action giveUp = _giveUp;
        popup.Show(giveUp == null ? null : (Action)(() =>
        {
            Close();
            giveUp();
        }));
    }
}
