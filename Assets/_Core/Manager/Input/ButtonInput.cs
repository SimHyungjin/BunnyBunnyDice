/// <summary>
/// 한 버튼의 입력 상태. 눌림(1프레임 이벤트)과 홀드를 구분한다.
/// 기기별 모듈(<see cref="InputActions"/>)이 Press/Release로 쓰고, 게임 로직은 Consume(one-shot)/IsHeld로 읽는다.
///
/// 입력 상태를 모아 두는 클래스는 게임이 소유한다(코어는 게임의 입력 필드를 모른다). 씬 전환 때는
/// <see cref="InputManager.OnInputsReset"/>을 구독해 직접 초기화한다.
/// <example>
/// <code>
/// public static class GameInput
/// {
///     public static Vector2 Move;
///     public static ButtonInput Attack;
///
///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
///     private static void Hook() => Core.Input.OnInputsReset += () => { Move = default; Attack.Reset(); };
/// }
/// </code>
/// </example>
/// </summary>
public struct ButtonInput
{
    private bool _pressed;   // 이번 프레임에 눌렸는가 (한 번 읽으면 소비)
    public bool IsHeld;      // 누르고 있는가

    public void Press()   { _pressed = true; IsHeld = true; }
    public void Release() { IsHeld = false; }

    /// <summary>눌림 이벤트를 읽고 소비한다. (같은 눌림을 두 번 처리하지 않도록)</summary>
    public bool Consume()
    {
        bool v = _pressed;
        _pressed = false;
        return v;
    }

    public void Reset()
    {
        _pressed = false;
        IsHeld = false;
    }
}
