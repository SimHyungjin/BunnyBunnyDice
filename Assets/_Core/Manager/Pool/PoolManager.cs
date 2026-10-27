using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 주소(키)별로 풀을 만들고, 스폰/반납/예약 디스폰을 담당한다.
///
/// <b>풀은 에셋 스코프에 속한다.</b> 프리팹을 로드한 <see cref="AssetScope"/>가 그 풀의 수명도 정한다.
/// 씬 스코프의 풀은 씬 전환 때 스코프와 함께 정리되고, 영구 스코프의 풀은 남는다.
/// 같은 키라도 스코프가 다르면 서로 다른 풀이다.
/// 비동기 스폰·예열의 대기도 그 스코프의 수명 토큰에 묶여 있어, 씬 스코프라면 토큰을 넘기지 않아도 씬 전환 때 취소된다.
///
/// 스폰 경로는 두 갈래다:
///  - Spawn (동기): 이미 예열됐거나 에셋이 로드돼 있으면 즉시. 뜨거운 스폰용.
///  - SpawnAsync (비동기): 아직 없으면 에셋을 로드해 풀을 만든 뒤 스폰. 콜드 스폰용.
///
/// 위치 규칙: 위치·회전을 넘기는 오버로드는 <b>월드 좌표</b>(Instantiate와 같다),
/// 부모만 넘기는 오버로드는 <b>부모 기준 로컬 원점</b>에 놓는다.
/// 부모 없이 스폰한 인스턴스는 활성 씬에 놓이므로, 영구 풀의 인스턴스를 씬 너머로 유지하려면 영구 부모 아래 두어야 한다.
/// </summary>
public sealed class PoolManager : Manager
{
    // 스코프 하나에 딸린 풀 묶음. 스코프가 닫히면 통째로 정리된다.
    private sealed class ScopePools
    {
        public readonly Dictionary<string, GameObjectPool> Pools = new();
        public Transform Root;
    }

    private readonly Dictionary<AssetScope, ScopePools> _byScope = new();
    private GameObject _root;

    // 예약 디스폰(AutoDespawn 등). 매 프레임 만료된 것만 반납한다.
    private struct Scheduled
    {
        public PooledInstance Handle;
        public int Generation;
        public float DueTime;
        public bool Unscaled;
    }
    private readonly List<Scheduled> _scheduled = new();

    public override Type[] Dependencies => new[] { typeof(ResourceManager), typeof(LoopManager) };

    protected override UniTask OnInitializeAsync()
    {
        _root = new GameObject("@Pool");
        Object.DontDestroyOnLoad(_root);

        Core.Loop.OnUpdate += TickScheduler;
        return UniTask.CompletedTask;
    }

    #region Spawn (동기)

    /// <summary>월드 좌표로 스폰한다. 풀이 없으면 이미 로드된 프리팹으로 만들고, 그것도 없으면 null.</summary>
    public T Spawn<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null,
        AssetLifetime life = AssetLifetime.Scene) where T : Component
        => SpawnSync<T>(Core.Resource.GetScope(life), key, position, rotation, parent, local: false);

    /// <summary>부모 아래 로컬 원점에 스폰한다.</summary>
    public T Spawn<T>(string key, Transform parent = null, AssetLifetime life = AssetLifetime.Scene) where T : Component
        => SpawnSync<T>(Core.Resource.GetScope(life), key, Vector3.zero, Quaternion.identity, parent, local: true);

    private T SpawnSync<T>(AssetScope scope, string key, Vector3 position, Quaternion rotation, Transform parent, bool local)
        where T : Component
    {
        if (string.IsNullOrEmpty(key)) key = typeof(T).Name;

        GameObjectPool pool = GetPool(scope, key);
        if (pool == null)
        {
            // 예열은 안 됐지만 프리팹이 이미 로드돼 있으면(프리로드) 동기로 풀을 만든다.
            GameObject prefab = scope.GetNow<GameObject>(key);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[PoolManager] '{key}' 풀도 없고 프리팹도 아직 로드되지 않았습니다({scope.Name}). " +
                    "PrewarmAsync 또는 프리로드를 먼저 하거나 SpawnAsync를 쓰세요.");
                return null;
            }
            pool = CreatePool(scope, key, prefab);
        }

        return Resolve<T>(pool.Spawn(position, rotation, parent, local), key);
    }

    #endregion

    #region Spawn (비동기)

    /// <summary>부모 아래 로컬 원점에 스폰한다. (UI처럼 부모가 위치를 정하는 경우)</summary>
    public async UniTask<T> SpawnAsync<T>(string key = null, Transform parent = null,
        AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default) where T : Component
    {
        GameObjectPool pool = await EnsurePoolAsync(Core.Resource.GetScope(life), key ?? typeof(T).Name, ct);
        return pool != null ? Resolve<T>(pool.Spawn(Vector3.zero, Quaternion.identity, parent, true), pool.Key) : null;
    }

    /// <summary>월드 좌표로 스폰한다.</summary>
    public async UniTask<T> SpawnAsync<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null,
        AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default) where T : Component
    {
        GameObjectPool pool = await EnsurePoolAsync(Core.Resource.GetScope(life), key ?? typeof(T).Name, ct);
        return pool != null ? Resolve<T>(pool.Spawn(position, rotation, parent, false), pool.Key) : null;
    }

    /// <summary>풀을 미리 채운다. 8개마다 한 프레임 양보하고, 그 사이 스코프가 닫히면 멈춘다.</summary>
    public async UniTask PrewarmAsync(string key, int count, AssetLifetime life = AssetLifetime.Scene, CancellationToken ct = default)
    {
        AssetScope scope = Core.Resource.GetScope(life);
        GameObjectPool pool = await EnsurePoolAsync(scope, key, ct);
        if (pool == null) return;

        for (int i = 0; i < count; i++)
        {
            pool.PrewarmOne();
            if (i > 0 && (i & 7) == 0)
            {
                await UniTask.Yield(ct);
                if (scope.IsDisposed) return;
            }
        }
    }

    private async UniTask<GameObjectPool> EnsurePoolAsync(AssetScope scope, string key, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[PoolManager] 키가 비어 있습니다.");
            return null;
        }

        GameObjectPool pool = GetPool(scope, key);
        if (pool != null) return pool;

        // 같은 키의 동시 요청은 스코프가 같은 로드를 함께 기다리게 한다.
        GameObject prefab = await scope.LoadAsync<GameObject>(key, ct);
        if (prefab == null || scope.IsDisposed) return null;

        // 먼저 끝난 요청이 이미 만들었으면 그것을 쓴다.
        return GetPool(scope, key) ?? CreatePool(scope, key, prefab);
    }

    // 꺼낸 인스턴스에서 요청한 컴포넌트를 찾는다. 없으면 인스턴스를 되돌리고 null.
    private static T Resolve<T>(PooledInstance handle, string key) where T : Component
    {
        if (handle == null) return null;

        T target = handle.GetTarget<T>();
        if (target != null) return target;

        Debug.LogError($"[PoolManager] '{key}' 프리팹에 {typeof(T).Name} 컴포넌트가 없습니다.");
        handle.Pool.Return(handle);
        return null;
    }

    #endregion

    #region Despawn

    public void Despawn(GameObject go)
    {
        if (go == null) return;

        if (go.TryGetComponent(out PooledInstance handle))
            handle.Pool.Return(handle);
        else
            Object.Destroy(go);   // 풀 소속이 아니면 그냥 파괴
    }

    public void Despawn(Component comp)
    {
        if (comp != null) Despawn(comp.gameObject);
    }

    /// <summary>delay초 뒤 자동 반납을 예약한다. 중앙에서 관리하므로 인스턴스마다 타이머를 만들지 않는다.</summary>
    public void ScheduleDespawn(GameObject go, float delay, bool unscaled)
    {
        if (go == null) return;

        if (!go.TryGetComponent(out PooledInstance handle))
        {
            Debug.LogWarning($"[PoolManager] ScheduleDespawn 대상 '{go.name}'은 풀 인스턴스가 아닙니다.");
            return;
        }

        float now = unscaled ? Time.unscaledTime : Time.time;
        _scheduled.Add(new Scheduled
        {
            Handle = handle,
            Generation = handle.Generation,
            DueTime = now + delay,
            Unscaled = unscaled,
        });
    }

    /// <summary>
    /// 이 오브젝트에 걸린 반납 예약을 모두 취소한다.
    /// 스스로 수명을 관리하는 오브젝트(투사체·장판)가 프리팹의 기본 수명(AutoDespawn.duration)을
    /// 버리고 런타임 값을 쓰려면 필요하다 — 예약은 쌓이기만 하므로, 취소하지 않으면 둘 중
    /// **먼저 오는 쪽이 이겨** 저작한 수명이 무시된다.
    /// </summary>
    public void CancelScheduledDespawn(GameObject go)
    {
        if (go == null || !go.TryGetComponent(out PooledInstance handle)) return;

        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (_scheduled[i].Handle == handle) RemoveScheduledAt(i);
        }
    }

    private void TickScheduler(float _)
    {
        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            Scheduled s = _scheduled[i];

            // 인스턴스가 파괴됐거나(풀 정리 포함) 이미 다른 스폰으로 재사용됐으면 이 예약은 무효.
            if (s.Handle == null || s.Handle.Generation != s.Generation)
            {
                RemoveScheduledAt(i);
                continue;
            }

            float now = s.Unscaled ? Time.unscaledTime : Time.time;
            if (now >= s.DueTime)
            {
                RemoveScheduledAt(i);
                s.Handle.Pool.Return(s.Handle);
            }
        }
    }

    private void RemoveScheduledAt(int i)
    {
        int last = _scheduled.Count - 1;
        _scheduled[i] = _scheduled[last];
        _scheduled.RemoveAt(last);
    }

    #endregion

    #region Pool Lifecycle

    /// <summary>해당 수명 스코프의 풀 하나를 정리한다. 그 풀의 활성 인스턴스도 파괴된다.</summary>
    public void ClearPool(string key, AssetLifetime life = AssetLifetime.Scene)
    {
        AssetScope scope = Core.Resource.GetScope(life);
        if (!_byScope.TryGetValue(scope, out ScopePools group)) return;

        if (group.Pools.Remove(key, out GameObjectPool pool)) pool.Dispose();
    }

    // 씬 전환 정리는 스코프가 닫힐 때(Disposing) 스스로 일어나므로 Clear에서 할 일이 없다.

    private GameObjectPool GetPool(AssetScope scope, string key)
    {
        if (scope == null || scope.IsDisposed) return null;
        return _byScope.TryGetValue(scope, out ScopePools group) && group.Pools.TryGetValue(key, out GameObjectPool pool)
            ? pool
            : null;
    }

    private GameObjectPool CreatePool(AssetScope scope, string key, GameObject prefab)
    {
        ScopePools group = GetOrCreateGroup(scope);
        var pool = new GameObjectPool(key, prefab, group.Root, SO_CoreConfig.Instance.PoolDefaultMaxSize);
        group.Pools[key] = pool;
        return pool;
    }

    private ScopePools GetOrCreateGroup(AssetScope scope)
    {
        if (_byScope.TryGetValue(scope, out ScopePools group)) return group;

        var root = new GameObject(scope.Name);
        root.transform.SetParent(_root.transform, false);

        group = new ScopePools { Root = root.transform };
        _byScope[scope] = group;

        // 스코프가 닫히면 그 에셋으로 만든 풀도 함께 닫는다. (에셋 해제보다 먼저 불린다)
        scope.Disposing += () => DisposeGroup(scope);
        return group;
    }

    private void DisposeGroup(AssetScope scope)
    {
        if (!_byScope.Remove(scope, out ScopePools group)) return;

        foreach (GameObjectPool pool in group.Pools.Values) pool.Dispose();
        group.Pools.Clear();

        if (group.Root != null) Object.Destroy(group.Root.gameObject);
    }

    #endregion
}
