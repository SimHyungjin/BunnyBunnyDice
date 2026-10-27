/// <summary>
/// 아무것도 하지 않는 진동 구현. 진동 하드웨어가 없는 플랫폼(PC 등)의 기본값이다.
/// 호출부가 플랫폼을 검사하지 않아도 되도록, "없음"도 구현으로 존재한다.
/// </summary>
public sealed class NullHapticProvider : IHapticProvider
{
    public bool IsSupported => false;
    public void Initialize() { }
    public void Vibrate(HapticType type) { }
}
