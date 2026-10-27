using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// 캐릭터 선택 창을 띄우고 닫는다. 이미 떠 있으면 다시 띄우지 않는다. 불러오는 동안 닫히면(게임 시작 등) 다 불러온 창을 띄우지 않고 닫는다
/// (LobbyScreen과 같은 세대 세기). 창은 바깥·뒤로가기로도 닫히므로, 닫힌 것은 창의 닫힘 이벤트로 안다.
/// </summary>
public sealed class CharacterSelectScreen : IDisposable
{
    private readonly IReadOnlyList<SO_HeroCharacter> _characters;
    private readonly HeroStats _stats;
    private readonly MetaProfile _meta;
    private readonly HeroPortraits _portraits;
    private readonly SO_CharacterSelectConfig _config;
    private readonly SO_MetaDiceConfig _diceConfig;
    private UI_CharacterSelectPopup _popup;
    private bool _loading;
    private int _generation;

    public CharacterSelectScreen(IReadOnlyList<SO_HeroCharacter> characters, HeroStats stats, MetaProfile meta, HeroPortraits portraits,
        SO_CharacterSelectConfig config, SO_MetaDiceConfig diceConfig)
    {
        _diceConfig = diceConfig;
        _config = config;
        _characters = characters;
        _stats = stats;
        _meta = meta;
        _portraits = portraits;
    }

    public void Show()
    {
        // 닫히는 연출 중인 창은 닫힌 것으로 본다(곧 반납된다).
        if (_loading || (_popup != null && _popup.IsOpened)) return;
        _popup = null;
        ShowAsync().Forget();
    }

    public void Close()
    {
        _generation++;
        _loading = false;
        if (_popup != null) App.ClosePopup(_popup);
        _popup = null;
    }

    public void Dispose()
    {
        Close();
        _portraits.Dispose();
    }

    private async UniTaskVoid ShowAsync()
    {
        _loading = true;
        int generation = _generation;
        UI_CharacterSelectPopup popup = await App.ShowPopup<UI_CharacterSelectPopup>(clickGuard: true, clickGuardAlpha: _config.GuardAlpha,
            clickToClose: true);
        if (generation != _generation)
        {
            if (popup != null) App.ClosePopup(popup);
            return;
        }
        _loading = false;
        if (popup == null)
        {
            Debug.LogWarning("[CharacterSelectScreen] UI_CharacterSelectPopup을 띄우지 못했다 — Addressables 주소를 확인");
            return;
        }
        _popup = popup;
        // 표시 단위 구독이라 반납 때 지워진다(UI_Panel).
        popup.OnCloseEvent.AddListener(() =>
        {
            if (_popup == popup) _popup = null;
        });
        popup.Show(_characters, _stats, _meta, _portraits, _diceConfig);
    }
}
