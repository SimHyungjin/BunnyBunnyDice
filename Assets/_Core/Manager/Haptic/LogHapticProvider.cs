/// <summary>
/// 콘솔 로그만 남기는 진동 구현. 에디터·QA에서 "여기서 떨려야 한다"를 눈으로 확인할 때 쓴다.
/// 에디터는 실제로 진동할 수 없으므로 기본값이 이것이다.
/// </summary>
public sealed class LogHapticProvider : IHapticProvider
{
    // 에디터에서 설정 화면의 진동 항목을 확인할 수 있도록 지원한다고 답한다.
    public bool IsSupported => true;

    // 고른 사실 자체는 알리지 않는다. 에디터에서는 매번 이것이라 부팅마다 같은 줄이 쌓일 뿐이고,
    // 이 구현이 보여 줄 것은 실제로 떨린 순간(Vibrate)이다.
    public void Initialize() { }

    public void Vibrate(HapticType type) => Debug.Log($"[Haptic] {type}");
}
