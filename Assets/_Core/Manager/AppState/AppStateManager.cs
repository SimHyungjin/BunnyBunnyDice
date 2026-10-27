using System;
using UnityEngine;

/// <summary>
/// 앱이 백그라운드로 갔다가 다시 돌아오는 전환을 감지해 이벤트로 알린다.
/// 게임 일시정지·데이터 저장·오디오 음소거 같은 반응을 여기에 연결한다.
///
/// 포커스 전환은 플랫폼마다 다른 콜백으로 들어오지만(모바일 pause, 데스크톱 focus 등),
/// Core가 그것들을 모두 <see cref="HandleStateChange"/>로 모아 넣고, 여기서 중복을 걸러 한 번만 알린다.
/// (모바일은 홈으로 나갈 때 pause와 focus가 둘 다 들어온다)
/// </summary>
public class AppStateManager : Manager
{
    private bool _isForeground = true;

    // OnBackgroundRaw용 상태. 억제·포커스 정책과 무관하게 실제 콜백의 전환만 따라간다.
    private bool _rawForeground = true;

    // 억제를 건 쪽의 수. 0이 되어야 풀린다.
    private int _suppressCount;

    // 억제 중에 들어온 마지막 전환. 억제가 풀릴 때 실제 상태와 대조해 반영한다.
    private bool? _suppressedState;

    /// <summary>
    /// 광고·결제창·시스템 오버레이처럼 잠깐 포커스를 뺏는 상황을 "백그라운드 전환"으로 취급하지 않도록 막는다.
    /// 그런 화면을 띄우는 동안을 using으로 감싼다.
    ///
    /// <example>
    /// <code>
    /// using (Core.AppState.SuppressStateEvents())
    /// {
    ///     await ShowRewardedAdAsync();
    /// }
    /// </code>
    /// </example>
    ///
    /// <b>억제는 "무시"가 아니라 "연기"다.</b> 광고를 보다가 유저가 정말로 홈 버튼을 누를 수 있는데,
    /// 그것까지 버리면 상태 머신이 포그라운드인 채로 굳어 저장·세션 종료·알림 예약이 전부 사라진다.
    /// 억제 중 들어온 전환은 기억해 두었다가 해제 시 반영한다.
    ///
    /// bool 하나가 아니라 수를 세는 이유: 억제가 필요한 상황이 겹칠 수 있다(광고 위에 결제창 등).
    /// bool이면 먼저 끝난 쪽이 꺼 버려, 다른 쪽이 아직 떠 있는데 억제가 풀린다.
    /// 돌려받은 스코프를 두 번 Dispose해도 한 번만 풀린다.
    /// </summary>
    public IDisposable SuppressStateEvents()
    {
        _suppressCount++;
        return new SuppressScope(this);
    }

    /// <summary>지금 억제 중인지.</summary>
    public bool IsSuppressingStateEvents => _suppressCount > 0;

    /// <summary>
    /// 포커스를 잃은 것을 백그라운드 전환으로 볼지.
    ///
    /// 모바일에서는 포커스 상실이 곧 앱을 떠난 것이라 true여야 한다.
    /// 데스크톱은 사정이 다르다 — alt-tab은 수시로 일어나는 조작이고,
    /// <see cref="Application.runInBackground"/>가 켜져 있으면 게임은 계속 돈다.
    /// 그런데도 매번 백그라운드로 처리하면 세션이 잘게 끊기고 오디오가 껐다 켜진다.
    /// 그래서 기본값은 "모바일이거나, 백그라운드에서 멈추는 빌드"일 때만 true다. 필요하면 직접 바꾼다.
    ///
    /// 끄더라도 <see cref="OnBackgroundRaw"/>는 그대로 발생한다 — 저장은 놓치는 쪽이 항상 더 나쁘다.
    /// </summary>
    public bool TreatFocusLossAsBackground { get; set; } =
        Application.isMobilePlatform || !Application.runInBackground;

    /// <summary>
    /// 뒤로가기 요청(Android 뒤로가기 키 · PC ESC)이 팝업으로 소비되지 않고 남았을 때.
    /// 일시정지 메뉴를 열거나 종료 확인을 띄우는 자리다.
    ///
    /// 키는 코어 입력 모듈 <see cref="CoreBackInput"/>(ESC·Android 뒤로가기)이 읽어 <c>App.HandleBackPressed()</c>로 보낸다.
    /// 게임이 직접 처리하려면 그 모듈을 끄고(<see cref="SO_CoreConfig.EnableBackInput"/>) 자기 입력 모듈에서 부른다.
    /// </summary>
    public event Action OnBackPressed;

    /// <summary>앱이 다시 포그라운드로 돌아왔을 때.</summary>
    public event Action OnEnterForeground;

    /// <summary>앱이 백그라운드로 넘어갔을 때.</summary>
    public event Action OnEnterBackground;

    /// <summary>
    /// 백그라운드 전환 시 <b>억제·포커스 정책과 무관하게</b> 발생한다. 전환당 한 번이다.
    ///
    /// 광고나 결제창이 떠 있는 동안 OS가 앱을 종료할 수 있는데, 억제를 존중하다가
    /// 저장까지 놓치면 그 세션의 진행도가 통째로 사라진다.
    /// 데이터 저장처럼 "떠나는 것이 확실하지 않아도 해 두는 편이 나은" 일만 여기에 건다.
    /// </summary>
    public event Action OnBackgroundRaw;

    /// <summary>
    /// 앱이 종료될 때 호출한다.
    ///
    /// PC에서 창을 닫는 경우가 여기 해당한다. 모바일은 백그라운드 전환이 사실상 종료라
    /// 이 콜백이 오지 않을 수도 있지만, 오는 경우엔 저장과 세션 종료가 반드시 돌아야 한다.
    /// 억제 중이더라도 미룰 수 없으므로 보류 상태를 버리고 곧바로 반영한다.
    /// 이미 백그라운드였더라도 Raw를 한 번 더 낸다 — 그 사이 바뀐 것이 있을 수 있고, 종료는 마지막 기회다.
    /// </summary>
    public void HandleQuit()
    {
        _rawForeground = false;
        Invoke(OnBackgroundRaw);

        _suppressedState = null;
        ApplyState(false);
    }

    /// <summary>뒤로가기 요청이 코어에서 소비되지 않았을 때 게임에 넘긴다. <c>App.HandleBackPressed()</c>가 부른다.</summary>
    internal void RaiseBackPressed() => Invoke(OnBackPressed);

    public void HandleStateChange(bool isForeground)
    {
        // pause와 focus가 함께 들어와도 Raw는 전환당 한 번만 낸다.
        if (_rawForeground != isForeground)
        {
            _rawForeground = isForeground;
            if (!isForeground) Invoke(OnBackgroundRaw);
        }

        // 데스크톱에서 alt-tab을 앱 이탈로 보지 않도록 하는 정책. 저장(OnBackgroundRaw)은 위에서 이미 끝났다.
        if (!isForeground && !TreatFocusLossAsBackground) return;

        if (IsSuppressingStateEvents)
        {
            // 버리지 않고 기억한다.
            _suppressedState = isForeground;
            return;
        }

        ApplyState(isForeground);
    }

    private void ReleaseSuppress()
    {
        if (_suppressCount == 0) return;
        if (--_suppressCount == 0) FlushSuppressed();
    }

    // 억제가 풀렸다. 그동안 들어온 마지막 상태를 지금 반영한다.
    // 광고를 보는 사이 홈으로 나갔다 돌아왔다면 두 전환이 상쇄되어 아무 일도 일어나지 않고,
    // 나간 채로 억제가 풀렸다면 그때 백그라운드 처리가 돈다.
    private void FlushSuppressed()
    {
        if (_suppressedState == null) return;

        bool state = _suppressedState.Value;
        _suppressedState = null;

        ApplyState(state);
    }

    private void ApplyState(bool isForeground)
    {
        if (_isForeground == isForeground) return;   // 이미 같은 상태면 중복 알림 방지

        _isForeground = isForeground;

        Invoke(_isForeground ? OnEnterForeground : OnEnterBackground);
    }

    /// <summary>
    /// 구독자를 하나씩 부르되, 하나가 예외를 던져도 나머지는 이어서 실행한다.
    ///
    /// 이 이벤트에는 세이브·알림 예약·세션 종료가 함께 걸려 있다.
    /// 그냥 Invoke하면 앞의 구독자 하나가 던지는 순간 뒤쪽이 통째로 실행되지 않아,
    /// 예컨대 알림 예약이 실패했다는 이유로 데이터 저장까지 건너뛰게 된다.
    /// </summary>
    private static void Invoke(Action handlers)
    {
        if (handlers == null) return;

        Delegate[] list = handlers.GetInvocationList();

        for (int i = 0; i < list.Length; i++)
        {
            try
            {
                ((Action)list[i])();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppState] 상태 전환 처리 중 예외가 발생했습니다.\n{e}");
            }
        }
    }

    private sealed class SuppressScope : IDisposable
    {
        private AppStateManager _owner;

        public SuppressScope(AppStateManager owner) => _owner = owner;

        public void Dispose()
        {
            AppStateManager owner = _owner;
            _owner = null;
            owner?.ReleaseSuppress();
        }
    }
}
