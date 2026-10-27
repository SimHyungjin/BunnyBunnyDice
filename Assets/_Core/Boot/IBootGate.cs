using Cysharp.Threading.Tasks;

/// <summary>부팅 게이트가 실행되는 시점.</summary>
public enum BootGatePhase
{
    /// <summary>
    /// 서비스 매니저 초기화 <b>전</b>.
    /// 개인정보 동의(GDPR·ATT)처럼 SDK가 깨어나기 전에 결론이 나야 하는 것들이 여기 온다.
    /// </summary>
    BeforeService,

    /// <summary>
    /// 서비스 매니저 초기화 <b>후</b>.
    /// 원격 설정을 읽어야 판단할 수 있는 것들 — 버전 체크·점검 공지·에셋 패치가 여기 온다.
    /// </summary>
    AfterService,
}

/// <summary>게이트 실행 결과.</summary>
public enum BootGateResult
{
    /// <summary>통과. 다음 게이트로 넘어간다.</summary>
    Continue,

    /// <summary>
    /// 부팅 중단. 시작 씬으로 진입하지 않는다.
    /// 강제 업데이트·점검처럼 유저가 게임에 들어가면 안 되는 상황에서 쓴다.
    /// 이 결과를 돌려주기 전에 게이트가 스스로 안내 화면을 띄워 둬야 한다
    /// — 부팅이 여기서 멈추므로, 이후 아무도 대신 띄워주지 않는다.
    /// </summary>
    Blocked,
}

/// <summary>
/// 시작 씬에 들어가기 전에 통과해야 하는 관문.
/// 동의 · 버전 체크 · 점검 공지 · 에셋 패치처럼
/// "게임을 시작해도 되는가"를 판정하는 단계들이 이 인터페이스로 표현된다.
///
/// 재시도는 게이트 안에서 처리한다. 네트워크 실패 시 다시 시도할지, 그냥 통과시킬지,
/// 막을지는 게이트마다 정책이 다르므로 바깥에서 일괄로 정하지 않는다.
/// </summary>
public interface IBootGate
{
    /// <summary>이 게이트가 실행될 시점.</summary>
    BootGatePhase Phase { get; }

    /// <summary>같은 Phase 안에서의 실행 순서. 작을수록 먼저 실행된다.</summary>
    int Order { get; }

    /// <summary>
    /// 실행 중 예외가 났을 때의 결과.
    ///
    /// 게이트마다 답이 다르다. <b>막는 것이 목적인 게이트</b>(강제 업데이트·점검)는
    /// 판단하지 못했다면 막아야 한다 — 안내 화면의 프리팹이 없다는 이유로 통과시키면
    /// 점검 중인 서버에 클라이언트가 붙고 구버전이 그대로 들어온다.
    /// 반대로 동의처럼 "받아 두면 좋지만 없어도 게임은 할 수 있어야 하는" 것은 통과가 맞다.
    /// </summary>
    BootGateResult FallbackOnError { get; }

    /// <summary>관문을 실행한다.</summary>
    UniTask<BootGateResult> RunAsync();
}
