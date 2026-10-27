/// <summary>
/// 씬 전환 커튼·로딩 화면. 가장 위 층(<see cref="SO_CoreConfig.TransitionOrder"/>)에 하나만 뜬다.
/// 전환 중 씬이 바뀌는 동안 화면을 덮고 있어야 하므로 기본 수명이 Persistent다.
/// </summary>
public class UI_Transition : UI_Panel { }
