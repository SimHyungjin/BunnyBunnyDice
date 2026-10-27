using System;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// UnityEngine.Debug를 감싸는 로그 창구.
/// 전역 네임스페이스에 Debug라는 이름으로 두면, 코드의 Debug.Log(...) 호출이 자동으로 이쪽으로 잡힌다
/// (전역 타입이 using으로 들여온 UnityEngine.Debug보다 우선하기 때문).
///
/// 개발용 로그(Log/Assert/Draw/Break)는 DEBUG_ENABLE 심볼이 없으면 [Conditional]로 호출 자체가 제거된다
/// — 인자(문자열 조합) 평가 비용까지 통째로 사라진다.
/// 반면 경고·에러·예외는 출시 빌드에서도 실제 문제를 봐야 하므로 제거하지 않는다.
///
/// 개발용 로그를 보려면 Player Settings의 Scripting Define Symbols에 DEBUG_ENABLE을 추가해야 한다.
///
/// <b>UnityEngine.Debug의 자주 쓰는 API를 빠짐없이 갖춰야 한다.</b> 이 클래스가 프로젝트 전체(Assembly-CSharp)의
/// Debug를 가로채므로, 여기 없는 멤버(LogFormat 등)를 쓰는 외부 에셋 스크립트는 컴파일 에러가 난다.
/// 원본의 시그니처·기본값(DrawLine의 기본 색은 흰색)을 그대로 따른다.
/// </summary>
public static class Debug
{
    private const string SYMBOL = "DEBUG_ENABLE";

#if !DEBUG_ENABLE && !UNITY_EDITOR
    /// <summary>
    /// 이 래퍼가 닿지 않는 코드(asmdef가 있는 에셋·패키지, DLL 플러그인, UnityEngine.Debug를 직접 쓴 코드)의
    /// 일반 로그 <b>출력</b>을 막는다. 경고·에러·Assert·예외는 남는다.
    ///
    /// 출력만 막을 뿐 호출과 인자 계산은 그대로 일어난다 — 그것까지 없앨 수 있는 건 [Conditional]뿐이고,
    /// 그래서 이 프로젝트 소스는 위 래퍼로 호출째 제거한다. 둘을 합친 것이 할 수 있는 최대치다.
    /// 에디터에서는 외부 에셋의 로그도 개발에 필요하므로 걸지 않는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void FilterReleaseLogs() => UnityEngine.Debug.unityLogger.filterLogType = LogType.Warning;
#endif

    // ── 개발용: 심볼이 없으면 호출 지점째 제거된다 ──

    [Conditional(SYMBOL), HideInCallstack]
    public static void Log(object message, Object context = null)
        => UnityEngine.Debug.Log(message, context);

    [Conditional(SYMBOL), HideInCallstack]
    public static void LogFormat(string format, params object[] args)
        => UnityEngine.Debug.LogFormat(format, args);

    [Conditional(SYMBOL), HideInCallstack]
    public static void LogFormat(Object context, string format, params object[] args)
        => UnityEngine.Debug.LogFormat(context, format, args);

    [Conditional(SYMBOL), HideInCallstack]
    public static void Assert(bool condition)
        => UnityEngine.Debug.Assert(condition);

    [Conditional(SYMBOL), HideInCallstack]
    public static void Assert(bool condition, Object context)
        => UnityEngine.Debug.Assert(condition, context);

    [Conditional(SYMBOL), HideInCallstack]
    public static void Assert(bool condition, object message, Object context = null)
        => UnityEngine.Debug.Assert(condition, message, context);

    [Conditional(SYMBOL), HideInCallstack]
    public static void AssertFormat(bool condition, string format, params object[] args)
        => UnityEngine.Debug.AssertFormat(condition, format, args);

    [Conditional(SYMBOL), HideInCallstack]
    public static void LogAssertion(object message, Object context = null)
        => UnityEngine.Debug.LogAssertion(message, context);

    [Conditional(SYMBOL), HideInCallstack]
    public static void LogAssertionFormat(string format, params object[] args)
        => UnityEngine.Debug.LogAssertionFormat(format, args);

    // 기본 색은 원본과 같은 흰색이다. (Color의 default는 투명이라 기본값으로 두면 선이 보이지 않는다)
    [Conditional(SYMBOL), HideInCallstack]
    public static void DrawLine(Vector3 start, Vector3 end)
        => UnityEngine.Debug.DrawLine(start, end, Color.white);

    [Conditional(SYMBOL), HideInCallstack]
    public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0f, bool depthTest = true)
        => UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);

    [Conditional(SYMBOL), HideInCallstack]
    public static void DrawRay(Vector3 start, Vector3 direction)
        => UnityEngine.Debug.DrawRay(start, direction, Color.white);

    [Conditional(SYMBOL), HideInCallstack]
    public static void DrawRay(Vector3 start, Vector3 direction, Color color, float duration = 0f, bool depthTest = true)
        => UnityEngine.Debug.DrawRay(start, direction, color, duration, depthTest);

    /// <summary>에디터를 일시정지한다. 개발용이라 심볼이 없으면 제거된다.</summary>
    [Conditional(SYMBOL)]
    public static void Break() => UnityEngine.Debug.Break();

    // ── 실제 문제: 출시 빌드에서도 남긴다 ──

    [HideInCallstack]
    public static void LogWarning(object message, Object context = null)
        => UnityEngine.Debug.LogWarning(message, context);

    [HideInCallstack]
    public static void LogWarningFormat(string format, params object[] args)
        => UnityEngine.Debug.LogWarningFormat(format, args);

    [HideInCallstack]
    public static void LogWarningFormat(Object context, string format, params object[] args)
        => UnityEngine.Debug.LogWarningFormat(context, format, args);

    [HideInCallstack]
    public static void LogError(object message, Object context = null)
        => UnityEngine.Debug.LogError(message, context);

    [HideInCallstack]
    public static void LogErrorFormat(string format, params object[] args)
        => UnityEngine.Debug.LogErrorFormat(format, args);

    [HideInCallstack]
    public static void LogErrorFormat(Object context, string format, params object[] args)
        => UnityEngine.Debug.LogErrorFormat(context, format, args);

    [HideInCallstack]
    public static void LogException(Exception e, Object context = null)
        => UnityEngine.Debug.LogException(e, context);

    // ── 그대로 넘기는 상태·설정 ──

    public static bool isDebugBuild => UnityEngine.Debug.isDebugBuild;
    public static ILogger unityLogger => UnityEngine.Debug.unityLogger;

    public static bool developerConsoleVisible
    {
        get => UnityEngine.Debug.developerConsoleVisible;
        set => UnityEngine.Debug.developerConsoleVisible = value;
    }

    public static void ClearDeveloperConsole() => UnityEngine.Debug.ClearDeveloperConsole();
}
