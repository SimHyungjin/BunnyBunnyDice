using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 매니저들의 등록·조회·생명주기를 담당한다.
/// Core에서 분리해 둔 덕분에, 초기화 순서 계산 같은 로직을 MonoBehaviour 없이 단독으로 다룰 수 있다.
/// </summary>
public sealed class ManagerRegistry
{
    private readonly List<Manager> _managers = new();
    private readonly Dictionary<Type, Manager> _byType = new();

    // 실제 초기화 순서(스테이지·계층 순). 정리는 이 역순으로 한다.
    private readonly List<Manager> _initOrder = new();
    private readonly HashSet<Manager> _ordered = new();

    /// <summary>매니저를 등록한다. 반환값으로 체이닝할 수 있다.</summary>
    public ManagerRegistry Register(Manager manager)
    {
        if (manager == null) return this;

        Type type = manager.GetType();
        if (_byType.ContainsKey(type))
        {
            Debug.LogWarning($"[ManagerRegistry] 이미 등록된 매니저입니다: {type.Name}");
            return this;
        }

        _byType[type] = manager;
        _managers.Add(manager);
        return this;
    }

    /// <summary>등록된 매니저를 타입으로 조회한다. 없으면 null.</summary>
    public T Get<T>() where T : Manager
        => _byType.TryGetValue(typeof(T), out var m) ? (T)m : null;

    /// <summary>
    /// 해당 스테이지의 매니저를 의존성 순서대로 초기화한다.
    /// 서로 의존이 없는 매니저들은 같은 계층으로 묶여 병렬로 초기화된다.
    ///
    /// 스테이지는 부팅 순서상 <see cref="ManagerStage.Core"/> → <see cref="ManagerStage.Service"/>로 진행한다.
    /// 다른 스테이지의 매니저에 대한 의존은 "이미 끝나 있다"고 보고 순서 계산에서 제외한다.
    /// </summary>
    public async UniTask InitializeStageAsync(ManagerStage stage)
    {
        foreach (var layer in BuildLayers(stage))
        {
            var tasks = new UniTask[layer.Count];
            for (int i = 0; i < layer.Count; i++)
                tasks[i] = layer[i].InitializeInternalAsync();

            await UniTask.WhenAll(tasks);

            // 실패한 매니저도 자리는 기록한다 — 정리 순서는 성공 여부와 무관하게 의존 관계를 따라야 한다.
            // (부팅 재시도로 같은 스테이지를 다시 돌 수 있어 중복은 거른다)
            foreach (var m in layer)
                if (_ordered.Add(m)) _initOrder.Add(m);
        }
    }

    /// <summary>
    /// 씬 전환 시 모든 매니저의 씬 종속 상태를 리셋한다.
    ///
    /// 초기화의 <b>역순</b>으로 정리한다. 의존하는 쪽을 먼저 정리해야 의존받는 쪽이 아직 살아 있다.
    /// 예: Pool(Resource에 의존)이 인스턴스를 파괴하기 전에 Resource가 프리팹 핸들을 먼저 해제하면,
    /// 번들 빌드에서는 파괴 대기 중인 인스턴스가 메시·머티리얼을 잃은 채 한 프레임 그려질 수 있다.
    ///
    /// 한 매니저의 정리가 예외를 던져도 나머지는 계속 정리한다. (초기화와 같은 정책)
    /// </summary>
    public void ClearAll()
    {
        for (int i = _initOrder.Count - 1; i >= 0; i--)
            ClearSafe(_initOrder[i]);

        // 아직 초기화 순서에 오르지 않은 매니저(Service 스테이지 전에 씬이 바뀐 경우 등)
        foreach (var m in _managers)
            if (!_ordered.Contains(m)) ClearSafe(m);
    }

    private static void ClearSafe(Manager manager)
    {
        try
        {
            manager.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError($"[{manager.GetType().Name}] 정리 중 예외가 발생했습니다. 나머지 매니저는 계속 정리합니다.\n{e}");
        }
    }

    /// <summary>
    /// 해당 스테이지의 매니저를 의존성 기준으로 초기화 계층으로 나눈다.
    /// 계층 0은 아무것도 의존하지 않는 매니저, 계층 1은 계층 0에만 의존하는 매니저… 순으로 채워진다.
    /// 같은 계층 안의 매니저들은 서로 독립이라 병렬로 초기화해도 안전하다.
    /// </summary>
    private List<List<Manager>> BuildLayers(ManagerStage stage)
    {
        // 이번 스테이지에서 초기화할 매니저만 추린다.
        var targets = new List<Manager>();
        foreach (var m in _managers)
            if (m.Stage == stage)
                targets.Add(m);

        // remainingDeps: 아직 초기화되지 않은 선행 매니저 수. 0이 되면 초기화할 차례.
        // dependents: 특정 매니저가 끝났을 때 카운트를 내려줘야 할 후행 매니저 목록.
        var remainingDeps = new Dictionary<Manager, int>();
        var dependents = new Dictionary<Manager, List<Manager>>();

        foreach (var m in targets)
        {
            remainingDeps[m] = 0;
            dependents[m] = new List<Manager>();
        }

        foreach (var m in targets)
        {
            foreach (var depType in m.Dependencies)
            {
                if (!_byType.TryGetValue(depType, out var dep))
                {
                    Debug.LogError(
                        $"[ManagerRegistry] {m.GetType().Name}이(가) 의존하는 '{depType.Name}'이(가) 등록되지 않았습니다.");
                    continue;
                }

                // 다른 스테이지의 매니저는 앞선 단계에서 이미 끝나 있어야 한다.
                // 끝나 있지 않다면 스테이지 순서를 거스르는 의존이다
                // — 코어 매니저가 서비스 매니저에 의존하는 경우가 여기 걸린다.
                if (dep.Stage != stage)
                {
                    if (!dep.IsInitialized)
                        Debug.LogError(
                            $"[ManagerRegistry] {m.GetType().Name}({stage})이(가) 아직 초기화되지 않은 " +
                            $"{dep.GetType().Name}({dep.Stage})에 의존합니다. 스테이지 선언을 확인하세요.");
                    continue;
                }

                remainingDeps[m]++;
                dependents[dep].Add(m);
            }
        }

        var layers = new List<List<Manager>>();

        var ready = new List<Manager>();
        foreach (var m in targets)
            if (remainingDeps[m] == 0)
                ready.Add(m);

        int resolved = 0;

        while (ready.Count > 0)
        {
            layers.Add(ready);
            resolved += ready.Count;

            var next = new List<Manager>();
            foreach (var m in ready)
                foreach (var d in dependents[m])
                    if (--remainingDeps[d] == 0)
                        next.Add(d);

            ready = next;
        }

        // 모든 매니저가 계층에 담기지 못했다면, 서로 물고 도는 의존성(순환)이 있다는 뜻이다.
        if (resolved < targets.Count)
            Debug.LogError(
                $"[ManagerRegistry] {stage} 스테이지의 매니저 의존성에 순환이 있어 초기화하지 못한 매니저가 있습니다. " +
                "Dependencies 선언을 확인하세요.");

        return layers;
    }
}
