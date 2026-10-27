using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 게임이 시작될 때 코어를 자동으로 띄우고, 정적 상태를 초기화한다.
/// </summary>
public static class AutoCore
{
    // 플레이 시작의 가장 이른 단계. "도메인 리로드 없이 플레이" 옵션을 켜면 정적 필드가
    // 이전 플레이의 값을 그대로 들고 있으므로, 여기서 먼저 깨끗이 되돌린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Core.ResetStatics();

    // 첫 씬이 로드되기 전에 코어를 띄운다.
    // 단, 부팅은 비동기라 첫 씬의 Awake/Start가 매니저 초기화보다 먼저 돌 수 있다(Addressables 초기화 등을 기다리는 동안).
    // 그래서 씬에는 로직을 두지 않는 것이 전제다 — 씬 로직은 준비가 끝난 뒤 SceneFlowManager가 실행하는
    // SceneBase에서 시작하거나, 씬 오브젝트라면 await Core.WaitUntilReady() 이후에 매니저를 쓴다.
    // (에디터에서는 초기화 전 접근을 Core.Resolve가 경고한다)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot() => _ = Core.Instance;
}

/// <summary>
/// 프로젝트에서 유일한 MonoBehaviour 싱글턴이자 컴포지션 루트.
/// 모든 매니저를 소유하고, Unity 콜백(Update 등)을 받아 매니저에 전달한다.
/// </summary>
public class Core : MonoBehaviour
{
    private static Core _instance;
    private static bool _booting;

    // 게이트에서 막혔다면 어느 단계였는지. 재시도는 이 지점부터 이어간다.
    private static BootGatePhase? _blockedPhase;

    private readonly ManagerRegistry _registry = new();
    private readonly BootGateRunner _gates = new();

    /// <summary>
    /// 코어 스테이지 매니저(외부 SDK에 의존하지 않는 것들)의 초기화가 끝났는지 여부.
    /// 이 시점부터 Update 루프가 돈다 — 동의·버전 게이트 화면도 애니메이션과 타이머를 쓸 수 있어야 하기 때문이다.
    /// </summary>
    public static bool IsCoreReady { get; private set; }

    /// <summary>서비스 매니저와 모든 부팅 게이트까지 끝나, 게임을 시작해도 되는 상태인지 여부.</summary>
    public static bool IsReady { get; private set; }

    public static Core Instance
    {
        get
        {
            // 에디터 비실행 상태(에디터 스크립트·OnValidate 등)에서 건드려도 부팅하지 않는다.
            // DontDestroyOnLoad는 플레이 중에만 동작하고, 편집 중인 씬에 @Core가 남게 된다.
            if (_instance == null && Application.isPlaying) StartBoot();
            return _instance;
        }
    }

    #region Manager Accessors

    public static LoopManager Loop         => Resolve<LoopManager>();
    public static AppStateManager AppState => Resolve<AppStateManager>();
    public static ResourceManager Resource => Resolve<ResourceManager>();
    public static PoolManager Pool         => Resolve<PoolManager>();
    public static SceneFlowManager Scene   => Resolve<SceneFlowManager>();
    public static InputManager Input       => Resolve<InputManager>();
    public static DataManager Data         => Resolve<DataManager>();
    public static CameraManager Camera     => Resolve<CameraManager>();
    public static AudioManager Audio       => Resolve<AudioManager>();
    public static UIManager UI             => Resolve<UIManager>();
    public static SafeAreaManager Safe     => Resolve<SafeAreaManager>();
    public static HapticManager Haptic     => Resolve<HapticManager>();
    public static DisplayManager Display   => Resolve<DisplayManager>();
    public static LocalizationManager Localization => Resolve<LocalizationManager>();
    public static SettingsManager Settings => Resolve<SettingsManager>();
    public static DeepLinkManager DeepLink => Resolve<DeepLinkManager>();

    // 서비스 레이어(외부 SDK: 광고·결제·분석 등)는 코어에 포함하지 않는다.
    // 붙일 때는 서비스 쪽이 자기 매니저를 ServiceProviders.Add<Manager>로 등록하고,
    // 접근자도 서비스 쪽에 둔다. (예: public static AdManager Ad => Core.Get<AdManager>();)

    /// <summary>
    /// 등록된 매니저를 타입으로 가져온다. 없으면 null.
    ///
    /// 코어 매니저는 위의 전용 프로퍼티를 쓰고, 게임에서 덧붙인 매니저(<see cref="RegisterManagers"/> 참조)는
    /// 이것으로 꺼낸다. 코어는 게임 매니저의 타입을 모르므로 전용 프로퍼티를 만들어 줄 수 없다 —
    /// 대신 게임 쪽 정적 클래스에 접근자를 두면 호출부가 코어 매니저처럼 짧아진다.
    /// (코어는 별도 어셈블리라 <c>App</c>/<c>Core</c>를 partial로 확장할 수는 없다)
    ///
    /// <example>
    /// <code>
    /// // _Project/G.cs
    /// public static class G
    /// {
    ///     public static BattleManager Battle => Core.Get&lt;BattleManager&gt;();
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public static T Get<T>() where T : Manager
        => Resolve<T>();

    private static T Resolve<T>() where T : Manager
    {
        if (_instance == null) return null;

        T manager = _instance._registry.Get<T>();
#if UNITY_EDITOR
        WarnIfNotInitialized(manager);
#endif
        return manager;
    }

#if UNITY_EDITOR
    // 같은 매니저로 매 프레임 경고가 쌓이지 않도록 타입당 한 번만 알린다.
    private static readonly HashSet<Type> _warnedUninitialized = new();

    /// <summary>
    /// 초기화되지 않은 매니저에 접근하면 알린다. (에디터 전용 — 빌드에는 비용이 없다)
    ///
    /// 잡히는 실수는 셋이다.
    /// 1) 씬 오브젝트가 부팅이 끝나기 전(Awake/Start)에 매니저를 쓴 경우
    /// 2) 매니저가 초기화 중에 Dependencies에 적지 않은 매니저를 쓴 경우
    ///    — 지금은 우연히 순서가 맞아 동작해도, 순서가 바뀌는 순간 깨진다
    /// 3) 매니저 초기화가 실패한 경우 (앞선 에러 로그에 원인이 있다)
    /// </summary>
    private static void WarnIfNotInitialized(Manager manager)
    {
        if (manager == null || manager.IsInitialized) return;
        if (!_warnedUninitialized.Add(manager.GetType())) return;

        string name = manager.GetType().Name;
        Debug.LogWarning(
            $"[Core] 초기화되지 않은 {name}에 접근했습니다. 아래 호출 스택에서 원인을 확인하세요.\n" +
            "- 씬 오브젝트라면: SceneBase에서 시작하거나 await Core.WaitUntilReady() 이후에 쓰세요.\n" +
            $"- 매니저 초기화 중이라면: 그 매니저의 Dependencies에 {name}을(를) 선언하세요.\n" +
            "- 그 외라면: 이 매니저의 초기화가 실패했을 수 있습니다. 앞선 에러 로그를 확인하세요.");
    }
#endif

    #endregion

    #region Boot

    private static void StartBoot()
    {
        if (_instance != null || _booting) return;
        _booting = true;
        BootAsync().Forget();
    }

    /// <summary>
    /// 부팅 순서.
    ///
    /// 외부 SDK는 초기화되는 순간부터 기기 식별자를 수집한다. 동의를 받기 전에 깨우면
    /// 그 자체가 규제 위반이고 스토어 심사에서도 걸린다. 그래서 매니저 초기화를 두 단계로 나누고
    /// 그 사이에 동의 관문을 둔다. 원격 설정이 있어야 판단할 수 있는 것들(버전·점검·패치)은
    /// 서비스 초기화가 끝난 뒤로 미룬다.
    /// </summary>
    private static async UniTaskVoid BootAsync()
    {
        try
        {
            CreateInstance();
            _instance.RegisterManagers();
            _instance.RegisterBootGates();

            // 1단계: 외부 의존이 없는 코어 매니저. 오프라인에서도 반드시 성공한다.
            await _instance._registry.InitializeStageAsync(ManagerStage.Core);
            IsCoreReady = true;   // 여기부터 Update 루프가 돈다 (게이트 화면의 연출·타이머용)
            SortDontDestroyRoots();

            await ContinueBootAsync(BootGatePhase.BeforeService);
        }
        catch (Exception e)
        {
            // 예상 못 한 실패는 게이트의 의도된 중단과 다르게 다룬다.
            // 여기서 멈춰 버리면 IsReady가 영원히 false로 남아 WaitUntilReady()가 끝나지 않고,
            // 유저는 아무 안내도 없는 화면을 보게 된다. 남은 준비를 포기하더라도 게임에는 들여보낸다.
            Debug.LogError($"[Core] 부팅 중 예외가 발생해 남은 준비를 건너뜁니다.\n{e}");

            IsCoreReady = true;
            IsReady = true;

            if (Scene != null) await Scene.EnterInitialSceneAsync();
        }
    }

    /// <summary>
    /// 게이트 단계부터 시작 씬 진입까지를 진행한다.
    /// 재시도할 때 막힌 지점부터 이어갈 수 있도록 부팅 본체와 분리해 두었다.
    /// </summary>
    private static async UniTask ContinueBootAsync(BootGatePhase from)
    {
        if (from == BootGatePhase.BeforeService)
        {
            // 동의(GDPR·ATT). SDK가 깨어나기 전에 결론이 나야 한다.
            if (!await _instance._gates.RunPhaseAsync(BootGatePhase.BeforeService))
            {
                _blockedPhase = BootGatePhase.BeforeService;
                return;
            }

            // 동의 결과를 반영해 SDK 매니저를 초기화한다.
            // (이미 끝난 매니저는 스스로 건너뛰므로 재시도해도 중복 초기화되지 않는다)
            await _instance._registry.InitializeStageAsync(ManagerStage.Service);
        }

        // 버전 체크·점검 공지·에셋 패치. 막히면 시작 씬으로 넘어가지 않는다.
        if (!await _instance._gates.RunPhaseAsync(BootGatePhase.AfterService))
        {
            _blockedPhase = BootGatePhase.AfterService;
            return;
        }

        _blockedPhase = null;
        IsReady = true;
        SortDontDestroyRoots();   // 서비스 매니저가 만든 루트까지 다시 정렬

        // 모든 준비가 끝난 뒤 시작 씬으로 진입한다.
        if (Scene != null) await Scene.EnterInitialSceneAsync();
    }

    /// <summary>
    /// 게이트에서 막힌 부팅을 다시 시도한다. 통과해 게임에 진입했으면 true.
    ///
    /// 점검 안내 화면의 "재시도" 버튼이 부를 자리다. 막힌 Phase의 게이트를 처음부터 다시 실행한다.
    /// 게이트가 판단 근거(원격 설정 등)를 새로 받아야 한다면, 그 갱신은 게이트의 RunAsync 안에서 한다.
    /// 강제 업데이트처럼 재시도가 의미 없는 경우에는 이 버튼을 두지 않으면 된다.
    /// </summary>
    public static async UniTask<bool> RetryBootAsync()
    {
        if (IsReady) return true;
        if (_instance == null || _blockedPhase == null) return false;

        BootGatePhase from = _blockedPhase.Value;
        _blockedPhase = null;

        try
        {
            await ContinueBootAsync(from);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Core] 부팅 재시도 중 예외가 발생했습니다.\n{e}");
            _blockedPhase = from;
            return false;
        }

        return IsReady;
    }

    /// <summary>
    /// DontDestroyOnLoad 루트를 보기 좋게 정렬한다. 에디터 전용, 순수하게 하이어라키 가독성을 위한 것이다.
    /// 매니저가 서로 모른 채 각자 루트를 만들기 때문에 생성 순서는 초기화 순서를 따르고, 그 사이에
    /// Addressables·URP가 만든 내부 오브젝트가 끼어든다. 매니저끼리 순서를 맞추게 하는 대신 여기서 한 번에 정리한다.
    /// 규칙: @Core → 나머지 '@' 루트(이름순) → 그 밖의 것(원래 순서 유지). 이후 늦게 생기는 오브젝트는 맨 아래에 붙는다.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void SortDontDestroyRoots()
    {
        GameObject[] roots = _instance.gameObject.scene.GetRootGameObjects();

        // 안정 삽입 정렬 — 같은 순위끼리는 원래 순서를 지킨다.
        for (int i = 1; i < roots.Length; i++)
        {
            GameObject cur = roots[i];
            int j = i - 1;
            while (j >= 0 && CompareRoot(roots[j], cur) > 0)
            {
                roots[j + 1] = roots[j];
                j--;
            }
            roots[j + 1] = cur;
        }

        for (int i = 0; i < roots.Length; i++)
            roots[i].transform.SetSiblingIndex(i);
    }

    private static int CompareRoot(GameObject a, GameObject b)
    {
        int rankA = RootRank(a), rankB = RootRank(b);
        if (rankA != rankB) return rankA - rankB;
        return rankA == 1 ? string.CompareOrdinal(a.name, b.name) : 0;
    }

    private static int RootRank(GameObject go)
    {
        if (go == _instance.gameObject) return 0;
        string name = go.name;
        return name.Length > 0 && name[0] == '@' ? 1 : 2;
    }

    private static void CreateInstance()
    {
        // 혹시 남아있는 인스턴스가 있으면 재사용하고, 없으면 새로 만든다.
        GameObject obj = GameObject.Find("@Core");
        if (obj == null) obj = new GameObject("@Core");

        DontDestroyOnLoad(obj);

        _instance = obj.GetComponent<Core>();
        if (_instance == null) _instance = obj.AddComponent<Core>();
    }

    /// <summary>
    /// 코어가 항상 갖는 매니저들을 등록한다.
    ///
    /// <b>게임 고유 매니저는 이 목록에 넣지 않는다.</b> <see cref="ServiceProviders.Add{T}"/>로
    /// <see cref="Manager"/>를 추가하면 이 파일을 고치지 않고 덧붙일 수 있다
    /// — 부팅 게이트를 붙이는 방식(<see cref="RegisterBootGates"/>)과 같다.
    /// 코어를 여러 프로젝트가 공유하는 이상, 게임마다 다른 목록이 코어 파일에 쌓이면 안 된다.
    /// 추가는 누적되므로 게임과 서비스 레이어가 각자 등록해도 서로 덮어쓰지 않는다.
    ///
    /// 덧붙인 매니저는 <see cref="Get{T}"/>로 꺼내 쓰고, 초기화 순서는 각 매니저의
    /// <see cref="Manager.Dependencies"/>에 코어 매니저 타입을 적어 표현한다.
    ///
    /// <example>
    /// <code>
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    /// private static void SetupManagers()
    /// {
    ///     ServiceProviders.Add&lt;Manager&gt;(() => new BattleManager());
    ///     ServiceProviders.Add&lt;Manager&gt;(() => new QuestManager());
    /// }
    /// </code>
    /// </example>
    /// </summary>
    private void RegisterManagers()
    {
        _registry
            .Register(new LoopManager())
            .Register(new AppStateManager())
            .Register(new ResourceManager())
            .Register(new PoolManager())
            .Register(new SceneFlowManager())
            .Register(new InputManager())
            .Register(new DataManager())
            .Register(new CameraManager())
            .Register(new AudioManager())
            .Register(new UIManager())
            .Register(new SafeAreaManager())
            .Register(new HapticManager())
            .Register(new DisplayManager())
            .Register(new LocalizationManager())
            .Register(new SettingsManager())
            // 딥링크는 앱이 링크로 시작될 때 부팅 이른 시점에 들어와 코어 스테이지다.
            .Register(new DeepLinkManager());

        // 게임 고유 매니저·서비스 매니저. 코어는 무엇이 붙는지 모른 채 등록만 대행한다.
        // (서비스 매니저는 Stage를 Service로 두면 BeforeService 게이트 뒤에 초기화된다)
        // 등록이 없으면(대부분의 초기 상태) 그냥 코어 매니저만으로 부팅한다.
        foreach (Manager manager in ServiceProviders.TakeAll<Manager>())
            _registry.Register(manager);
    }

    /// <summary>
    /// 시작 씬에 들어가기 전 통과해야 할 관문들을 등록한다.
    ///
    /// 게이트는 <see cref="ServiceProviders.Add{T}"/>로 <see cref="IBootGate"/>를 추가해 붙인다
    /// — 이 파일을 고칠 필요가 없다.
    ///
    /// <example>
    /// <code>
    /// ServiceProviders.Add&lt;IBootGate&gt;(() => new MyExtraGate());
    /// </code>
    /// </example>
    /// </summary>
    private void RegisterBootGates()
    {
        // 코어는 기본 게이트를 갖지 않는다. 동의·버전 체크·점검 같은 게이트는 서비스 레이어나 게임이 덧붙인다.
        // 등록된 게이트가 없으면 각 Phase는 그대로 통과한다.
        foreach (IBootGate gate in ServiceProviders.TakeAll<IBootGate>())
            _gates.Register(gate);
    }

    #endregion

    #region Unity Loop

    // 부팅 게이트 화면도 연출과 타이머를 쓰므로, 전체 준비(IsReady)가 아니라
    // 코어 준비(IsCoreReady) 시점부터 루프를 돌린다.
    private void Update()
    {
        if (!IsCoreReady) return;

        var loop = Loop;
        float dt = FrameDelta;
        loop.Tick(dt);
        loop.GameTick(dt);
    }

    private void LateUpdate()
    {
        if (!IsCoreReady) return;
        Loop.LateTick(FrameDelta);
    }

    /// <summary>
    /// 이번 프레임의 실제 시간 간격. Unity가 배율 시간(<see cref="Time.deltaTime"/>)에 거는 상한
    /// (<see cref="Time.maximumDeltaTime"/>)을 실제 시간에도 똑같이 건다.
    ///
    /// 실제 시간(<see cref="Time.unscaledDeltaTime"/>)에는 그 상한이 없다. 창이 포커스를 잃어 앱이 멈췄다 돌아오면
    /// 첫 프레임의 간격이 수 초가 되고, 그 시간을 한 번에 적분한 몸·무리가 그만큼 순간이동했다(빌드에서 창을 오가면
    /// 무리의 자리가 확 바뀌어 있었다). 끊긴 시간은 따라잡지 않는다 — 멈춰 있던 동안은 멈춰 있던 것이다.
    /// </summary>
    private static float FrameDelta => Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);

    // 플랫폼마다 다른 콜백으로 들어오는 포커스 전환을 AppStateManager 한곳으로 모은다.
    private void OnApplicationPause(bool pause) => AppState?.HandleStateChange(!pause);
    private void OnApplicationFocus(bool focus) => AppState?.HandleStateChange(focus);

    // PC에서 창을 닫는 경우(에디터에서는 플레이를 끌 때). 저장과 세션 종료가 여기서 마지막으로 돌고, 그다음 현재 씬을 내린다 —
    // 씬이 쥔 네이티브 메모리는 씬과 함께 풀리지 않는다. (모바일은 백그라운드 전환이 사실상 종료라 호출이 보장되지 않는다)
    private void OnApplicationQuit()
    {
        AppState?.HandleQuit();
        Scene?.ExitOnQuit();
    }

    #endregion

    #region Lifecycle

    /// <summary>씬 전환 시 모든 매니저의 씬 종속 상태를 리셋한다.</summary>
    public static void Clear() => _instance?._registry.ClearAll();

    /// <summary>
    /// 모든 매니저 초기화와 부팅 게이트가 끝날 때까지 기다린다.
    /// 게이트에서 막히면(점검 등) 끝나지 않으므로, 기다림을 포기할 수 있게 토큰을 받는다.
    /// </summary>
    public static async UniTask WaitUntilReady(System.Threading.CancellationToken ct = default)
        => await UniTask.WaitUntil(() => IsReady, cancellationToken: ct);

    internal static void ResetStatics()
    {
        _instance = null;
        _booting = false;
        _blockedPhase = null;
        IsCoreReady = false;
        IsReady = false;
#if UNITY_EDITOR
        _warnedUninitialized.Clear();
#endif

        SO_CoreConfig.ResetStatics();
    }

    #endregion
}
