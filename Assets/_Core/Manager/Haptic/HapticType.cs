/// <summary>
/// 진동 세기. 실제로 몇 ms를 어떤 방식으로 떨지는 플랫폼 구현(<see cref="IHapticProvider"/>)이 정한다.
/// 게임 코드는 세기의 "의미"만 고르고 기기 차이는 신경 쓰지 않는다.
/// </summary>
public enum HapticType { Weak, Soft, Medium, Hard }
