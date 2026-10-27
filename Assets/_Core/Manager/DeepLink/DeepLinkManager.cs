using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 딥링크를 받아 등록된 처리기로 넘긴다.
///
/// 외부 SDK가 필요 없어(<see cref="Application.deepLinkActivated"/>) 코어 스테이지에 둔다.
/// 앱이 링크로 <b>시작되는</b> 경우 주소가 부팅 아주 이른 시점에 들어오는데,
/// 서비스 스테이지에 두면 동의 게이트 뒤라 그 사이에 놓칠 수 있다.
///
/// 처리기가 아직 등록되지 않은 채로 링크가 들어오면 보관해 두었다가,
/// 해당 호스트의 처리기가 등록되는 순간 넘긴다. 콜드 스타트에서는 이 상황이 오히려 보통이다
/// — 링크가 먼저 오고 씬은 나중에 준비된다.
/// </summary>
public sealed class DeepLinkManager : Manager
{
    // Application.deepLinkActivated는 정적 이벤트다. 인스턴스 메서드를 직접 걸면
    // "도메인 리로드 없이 플레이"에서 이전 플레이의 인스턴스가 구독된 채 남아 중복 처리된다.
    // 정적 메서드를 한 번만 걸어 두고, 지금 살아 있는 인스턴스로 넘긴다.
    private static DeepLinkManager _active;
    private static bool _hooked;

    private readonly Dictionary<string, Func<DeepLinkData, UniTask>> _handlers = new();

    // 처리기를 찾지 못해 대기 중인 링크.
    private DeepLinkData _pending;

    /// <summary>처리기가 없어 대기 중인 링크. 없으면 null.</summary>
    public DeepLinkData Pending => _pending;

    protected override UniTask OnInitializeAsync()
    {
        _active = this;

        if (!_hooked)
        {
            Application.deepLinkActivated += OnDeepLinkActivated;
            _hooked = true;
        }

        // 앱이 딥링크로 시작된 경우, 위 이벤트는 이미 지나간 뒤라 여기서 직접 확인한다.
        // WebGL에서는 absoluteURL이 페이지 주소라 딥링크가 아니다.
        if (Application.platform != RuntimePlatform.WebGLPlayer && !string.IsNullOrEmpty(Application.absoluteURL))
            HandleActivated(Application.absoluteURL);

        return UniTask.CompletedTask;
    }

    private static void OnDeepLinkActivated(string url) => _active?.HandleActivated(url);

    /// <summary>
    /// 호스트별 처리기를 등록한다. 같은 호스트를 다시 등록하면 덮어쓴다.
    /// 등록 전에 도착해 대기 중인 링크가 있으면 곧바로 넘긴다.
    /// </summary>
    public void Register(string host, Func<DeepLinkData, UniTask> handler)
    {
        if (string.IsNullOrEmpty(host) || handler == null) return;

        _handlers[host] = handler;
        TryDispatchPending();
    }

    public void Unregister(string host)
    {
        if (!string.IsNullOrEmpty(host)) _handlers.Remove(host);
    }

    /// <summary>대기 중인 링크를 버린다. 유저가 안내를 닫았을 때 등.</summary>
    public void DiscardPending() => _pending = null;

    private void HandleActivated(string url)
    {
        DeepLinkData data = DeepLinkData.Parse(url);
        if (data == null) return;

        Debug.Log($"[DeepLink] 수신: {url}");

        if (!Dispatch(data))
        {
            // 아직 받을 곳이 없다. 처리기가 등록되면 그때 넘긴다.
            _pending = data;
            Debug.Log($"[DeepLink] '{data.Host}' 처리기가 없어 보관합니다.");
        }
    }

    private void TryDispatchPending()
    {
        if (_pending == null) return;

        DeepLinkData data = _pending;
        if (Dispatch(data)) _pending = null;
    }

    private bool Dispatch(DeepLinkData data)
    {
        if (!_handlers.TryGetValue(data.Host, out Func<DeepLinkData, UniTask> handler)) return false;

        // 처리기 안에서 씬 전환처럼 시간이 걸리는 일을 할 수 있으므로 기다리지 않는다.
        // 처리기가 첫 await 전에 던지면 여기로 올라오므로 잡는다 — 외부에서 들어온 링크 하나로 앱 흐름이 깨지면 안 된다.
        try
        {
            handler(data).Forget();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DeepLink] '{data.Host}' 처리기에서 예외가 발생했습니다.\n{e}");
        }
        return true;
    }
}
