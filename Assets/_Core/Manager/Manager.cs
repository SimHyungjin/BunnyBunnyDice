using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 모든 매니저의 베이스. 순수 C# 객체이며 Unity 생명주기(Update/Awake 등)를 직접 갖지 않는다.
/// Unity 콜백은 컴포지션 루트 <see cref="Core"/>가 받아서 각 매니저에 위임한다.
///
/// 초기화 순서는 <see cref="Dependencies"/> 선언으로 표현한다.
/// 레지스트리가 이 선언을 읽어, 의존하는 매니저가 먼저 초기화되도록 순서를 맞춘다.
/// </summary>
public abstract class Manager
{
    /// <summary>
    /// 이 매니저보다 먼저 초기화돼야 하는 매니저 타입들.
    /// 여기에 적은 매니저들은 <see cref="OnInitializeAsync"/> 시점에 초기화가 끝나 있음이 보장된다.
    /// </summary>
    /// <example>public override Type[] Dependencies => new[] { typeof(DataManager) };</example>
    public virtual Type[] Dependencies => Type.EmptyTypes;

    /// <summary>
    /// 부팅의 어느 단계에서 초기화될지. 기본은 <see cref="ManagerStage.Core"/>다.
    /// 외부 SDK를 쓰는 매니저(서비스 레이어)는 Stage를 Service로 둬 동의 게이트 뒤에 초기화되게 한다.
    /// </summary>
    public virtual ManagerStage Stage => ManagerStage.Core;

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 레지스트리 전용 진입점. 한 번만 초기화되도록 막고, 완료 후 플래그를 세운다.
    /// </summary>
    internal async UniTask InitializeInternalAsync()
    {
        if (IsInitialized) return;

        try
        {
            await OnInitializeAsync();
            IsInitialized = true;
        }
        catch (Exception e)
        {
            // 매니저 하나가 실패해도 부팅은 계속한다.
            // 외부 SDK 초기화는 네트워크·권한·설정 문제로 얼마든지 던질 수 있는데,
            // 그것 때문에 게임에 아예 들어가지 못하면 훨씬 나쁘다.
            // IsInitialized는 false로 남으므로, 이 매니저에 의존하는 쪽이 상태를 확인할 수 있다.
            Debug.LogError($"[{GetType().Name}] 초기화에 실패했습니다. 이 매니저 없이 계속합니다.\n{e}");
        }
    }

    /// <summary>매니저 최초 1회 초기화. 실제 준비 작업은 여기에 작성한다.</summary>
    protected virtual UniTask OnInitializeAsync() => UniTask.CompletedTask;

    /// <summary>
    /// 씬 전환 시 호출. 씬에 종속된 상태만 리셋한다.
    /// 씬이 바뀌어도 살아있어야 하는 구독/리소스는 여기서 건드리지 않는다.
    /// </summary>
    public virtual void Clear() { }
}
