using UnityEngine.InputSystem;

/// <summary>
/// 뒤로가기 입력. ESC(PC)와 Android 뒤로가기 키를 <see cref="App.HandleBackPressed"/>로 보낸다.
/// Input System은 Android 뒤로가기 키를 Escape 키로 보고하므로 키 하나로 둘 다 받는다.
///
/// 앱 전체에서 늘 살아 있어야 하므로 유지(Persistent) 모듈이고, 코어가 부팅 때 켠다.
/// 팝업은 아무것도 등록하지 않는다 — 팝업 레이어가 스택 맨 위를 닫는다.
/// 게임이 직접 처리하려면 <see cref="SO_CoreConfig.EnableBackInput"/>을 끄거나 RemoveInput으로 뺀다.
/// </summary>
public sealed class CoreBackInput : InputActions
{
    public override bool Persistent => true;

    public override void Connect() { }
    public override void Disconnect() { }

    public override void OnUpdate(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) App.HandleBackPressed();
    }
}
