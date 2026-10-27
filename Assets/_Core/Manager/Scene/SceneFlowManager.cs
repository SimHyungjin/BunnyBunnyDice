using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 하나의 진입/퇴장 로직. 씬 이름과 같은 이름의 클래스를 만들면 전환 시 자동으로 연결된다.
/// 네임스페이스 없이 두어야 이름으로 찾을 수 있다. 어셈블리는 어디에 있어도 된다.
/// (그래서 씬 이름은 C# 클래스 이름으로 쓸 수 있어야 한다 — 공백·하이픈 불가)
/// </summary>
public abstract class SceneBase
{
    public abstract UniTask EnterScene(CancellationToken token);
    public virtual void ExitScene() { }
}

/// <summary>
/// 씬 전환 흐름을 지휘한다: 전환 연출 → 이전 씬 퇴장 → 모든 매니저 정리 → additive 로드 → 새 씬 진입.
///
/// 씬마다 <see cref="CurrentToken"/>을 발급한다. 이전 씬의 토큰은 <b>정리 시점</b>(Core.Clear 직전)에 취소되므로,
/// 씬 토큰에 묶인 작업과 씬 에셋 스코프에 묶인 작업(로드·스폰)이 같은 순간에 함께 끊긴다.
/// 전환 연출(onBeforeLoad)이 도는 동안에는 이전 씬이 아직 살아 있으므로 CurrentToken도 이전 씬의 것이다.
/// </summary>
public sealed class SceneFlowManager : Manager
{
    private SceneBase _current;
    private CancellationTokenSource _cts = new();

    // 전환 중에 미리 만들어 둔 다음 씬의 토큰. 연출·로드가 쓰고, 정리 시점에 현재 토큰이 된다.
    private CancellationTokenSource _pendingCts;

    // 씬 이름 → SceneBase 타입. 못 찾은 것(null)도 기억한다.
    private static readonly Dictionary<string, Type> _sceneTypes = new();

    /// <summary>현재 씬에 묶인 취소 토큰. 씬 전환의 정리 시점에 취소된다.</summary>
    public CancellationToken CurrentToken => _cts.Token;

    /// <summary>전환이 진행 중인지. 진행 중 들어온 전환 요청은 무시된다.</summary>
    public bool IsTransitioning => _pendingCts != null;

    /// <summary>모든 매니저 초기화가 끝난 뒤 Core가 한 번 호출한다. 시작 씬의 SceneBase를 찾아 진입한다.</summary>
    internal UniTask EnterInitialSceneAsync() => CreateAndEnter(SceneManager.GetActiveScene().name, _cts.Token);

    /// <summary>다른 씬으로 전환한다. 훅은 전환 연출(페이드 등) 자리다.</summary>
    public UniTask ChangeSceneAsync(string sceneName, Func<UniTask> onBeforeLoad = null, Func<UniTask> onAfterLoad = null)
        => TransitionAsync(sceneName, onBeforeLoad, onAfterLoad);

    /// <summary>현재 씬을 깨끗이 다시 로드한다. (한 판 재시작 등)</summary>
    public UniTask ReloadCurrentSceneAsync(Func<UniTask> onBeforeLoad = null, Func<UniTask> onAfterLoad = null)
        => TransitionAsync(SceneManager.GetActiveScene().name, onBeforeLoad, onAfterLoad);

    /// <summary>
    /// 전환과 리로드가 함께 쓰는 흐름. 리로드는 같은 이름의 씬으로 전환하는 것일 뿐이다
    /// — 새 씬을 additive로 올린 뒤 이전 씬을 내리므로, 이름이 같아도 핸들로 구분된다.
    /// </summary>
    private async UniTask TransitionAsync(string sceneName, Func<UniTask> onBeforeLoad, Func<UniTask> onAfterLoad)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneFlow] 씬 이름이 비어 있습니다.");
            return;
        }

        if (IsTransitioning)
        {
            Debug.LogWarning($"[SceneFlow] 다른 전환이 진행 중이라 '{sceneName}' 요청을 무시합니다.");
            return;
        }

        // 정리를 시작하기 전에 확인한다. 정리한 뒤에 로드가 실패하면 매니저는 비었는데
        // 이전 씬은 그대로 남고 진입 로직도 없는, 되돌릴 수 없는 상태가 된다.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneFlow] '{sceneName}' 씬을 로드할 수 없습니다. Build Settings(Build Profile)의 씬 목록을 확인하세요.");
            return;
        }

        _pendingCts = new CancellationTokenSource();
        CancellationToken token = _pendingCts.Token;

        try
        {
            if (onBeforeLoad != null) await onBeforeLoad().AttachExternalCancellation(token);

            Scene oldScene = SceneManager.GetActiveScene();

            ExitCurrent();

            // 이전 씬의 작업은 여기서 한꺼번에 끊긴다 — 씬 토큰은 지금, 씬 에셋 스코프는 바로 아래 Core.Clear 안에서.
            // 취소한 소스는 Dispose하지 않는다. 다른 곳이 받아 둔 토큰에 나중에 연결해도 예외가 나지 않게 하기 위해서다.
            _cts.Cancel();
            _cts = _pendingCts;

            Core.Clear();   // 모든 매니저의 씬 종속 상태 정리 (저장 · 풀·씬 에셋 해제 · 시간 배율 복구 …)

            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive).ToUniTask(cancellationToken: token);

            Scene loaded = FindLoadedScene(sceneName, oldScene);
            if (loaded.IsValid()) SceneManager.SetActiveScene(loaded);

            await UnloadAllExcept(loaded, token);

            if (onAfterLoad != null) await onAfterLoad().AttachExternalCancellation(token);
            await CreateAndEnter(sceneName, token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[SceneFlow] '{sceneName}' 전환이 취소되었습니다.");
        }
        finally
        {
            _pendingCts = null;
        }
    }

    /// <summary>
    /// 앱이 끝날 때 현재 씬을 내린다. Core가 종료 콜백에서 한 번 부른다.
    ///
    /// 씬이 내려가는 길은 셋(전환·리로드·종료)인데 종료만 <see cref="SceneBase.ExitScene"/>을 거치지 않았다. 씬이 쥔 것 중
    /// Unity 객체가 아닌 것 — 네이티브 메모리(길찾기 메시·크라우드), 엔티티 — 은 씬과 함께 저절로 풀리지 않으므로,
    /// 에디터에서 플레이를 끌 때마다 그대로 샜다(Leak Detected).
    /// </summary>
    internal void ExitOnQuit()
    {
        _pendingCts?.Cancel();
        _cts.Cancel();
        ExitCurrent();
    }

    // 퇴장 로직이 던져도 정리는 계속돼야 한다. 여기서 멈추면 Core.Clear가 돌지 않아 이전 씬의 상태가 남는다.
    private void ExitCurrent()
    {
        SceneBase current = _current;
        _current = null;
        if (current == null) return;

        try
        {
            current.ExitScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneFlow] {current.GetType().Name}.ExitScene 중 예외가 발생했습니다. 전환은 계속합니다.\n{e}");
        }
    }

    private async UniTask CreateAndEnter(string sceneName, CancellationToken token)
    {
        Type type = FindSceneType(sceneName);
        if (type == null)
        {
            // 대응 SceneBase가 없으면 씬 진입 로직 없이 넘어간다. (개발 초기엔 정상)
            // 씬 이름을 바꾸면 연결이 조용히 끊기므로 에디터에서는 경고로 알린다.
#if UNITY_EDITOR
            Debug.LogWarning($"[SceneFlow] '{sceneName}'에 대응하는 SceneBase 구현이 없어 씬 진입 로직을 건너뜁니다. " +
                             "씬 이름과 같은 이름의 클래스(네임스페이스 없이, SceneBase 상속)가 있는지 확인하세요.");
#endif
            return;
        }

        _current = (SceneBase)Activator.CreateInstance(type);
        await _current.EnterScene(token);
    }

    /// <summary>
    /// 씬 이름과 같은 이름의 SceneBase 하위 클래스를 찾는다.
    /// 모든 어셈블리에서 이름 하나만 조회하므로(타입 전체를 훑지 않는다) 게임 코드를 asmdef로 나눠도 찾을 수 있고 비용도 작다.
    /// </summary>
    private static Type FindSceneType(string sceneName)
    {
        if (_sceneTypes.TryGetValue(sceneName, out Type cached)) return cached;

        Type found = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(sceneName, false);
            if (type != null && !type.IsAbstract && typeof(SceneBase).IsAssignableFrom(type))
            {
                found = type;
                break;
            }
        }

        _sceneTypes[sceneName] = found;
        return found;
    }

    // 이름이 같은 씬이 둘일 수 있다(리로드). 이전 씬은 제외하고 찾는다.
    private static Scene FindLoadedScene(string name, Scene exclude)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name == name && s != exclude) return s;
        }
        return default;
    }

    private static async UniTask UnloadAllExcept(Scene keep, CancellationToken token)
    {
        var toUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s != keep && s.isLoaded) toUnload.Add(s);
        }

        foreach (Scene s in toUnload)
            await SceneManager.UnloadSceneAsync(s).ToUniTask(cancellationToken: token);
    }
}
