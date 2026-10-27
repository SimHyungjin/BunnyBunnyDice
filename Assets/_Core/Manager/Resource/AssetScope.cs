using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

/// <summary>
/// 에셋의 소유 단위. 이 스코프로 로드한 에셋은 스코프가 <see cref="Dispose"/>될 때 함께 해제된다.
///
/// Addressables는 로드마다 참조 카운트를 올리고 해제마다 내린다. 스코프는 그 카운트를 "소유자 하나당 한 번"으로
/// 맞춰 줄 뿐이다 — 같은 에셋을 여러 스코프가 로드해도 각자 한 번씩 세므로, 한쪽이 해제돼도 다른 쪽은 안전하다.
/// 같은 키·같은 타입의 동시 로드도 Addressables가 하나의 작업으로 합치므로 여기서 따로 합치지 않는다.
///
/// 코어가 두 개를 갖는다: <see cref="ResourceManager.Scene"/>(씬 전환 시 교체)과 <see cref="ResourceManager.Persistent"/>(앱 수명).
/// 특정 시스템만의 수명이 필요하면 <see cref="ResourceManager.CreateScope"/>로 만들어 직접 Dispose한다.
///
/// 캐시 키는 (주소, 로드 타입)이다. 같은 주소라도 요청한 타입에 따라 다른 에셋이 나오기 때문이다
/// — 스프라이트 텍스처를 Object로 로드하면 Texture2D가, Sprite로 로드하면 스프라이트가 나온다.
/// 컴포넌트 타입을 요청하면 GameObject로 로드해 컴포넌트를 꺼내 준다.
/// </summary>
public sealed class AssetScope : IDisposable
{
    private const string Tag = "[AssetScope]";

    private readonly Dictionary<AssetKey, AsyncOperationHandle> _handles = new();
    private bool _disposed;

    // 스코프의 수명 토큰. Dispose 때 취소된다.
    // 외부 토큰(씬 토큰 등)을 받아 쓰지 않고 스스로 가진다 — 취소 시점은 소유자(스코프)의 수명이 정하고,
    // 리소스가 씬 흐름 같은 다른 매니저를 알 필요가 없게 하기 위해서다.
    // CTS는 Dispose하지 않는다. 해제된 뒤에도 Token을 읽는 코드가 ObjectDisposedException을 내지 않게 하기 위해서다.
    private readonly CancellationTokenSource _lifetime = new();

    public string Name { get; }
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 스코프가 닫힐 때 취소되는 토큰. 이 스코프의 에셋을 쓰는 비동기 작업을 스코프 수명에 묶을 때 쓴다.
    /// (씬 스코프라면 씬 전환 때 취소된다)
    /// </summary>
    public CancellationToken Token => _lifetime.Token;

    /// <summary>
    /// 해제 직전에 발생한다. 이 스코프의 에셋으로 만든 것(풀 등)을 정리할 기회다.
    /// 에셋은 이 이벤트 뒤에 해제되므로, 핸들러 안에서는 아직 유효하다.
    /// </summary>
    public event Action Disposing;

    public AssetScope(string name) => Name = name;

    #region Load

    /// <summary>
    /// 키로 에셋을 로드한다. 키를 비우면 타입 이름이 키가 된다.
    /// 이미 이 스코프에 있으면 그것을 돌려주고, 로드 중이면 그 로드를 함께 기다린다.
    ///
    /// 대기는 호출자 토큰과 스코프 수명 토큰 중 먼저 취소되는 쪽에서 끝난다(OperationCanceledException).
    /// 그래서 씬 스코프의 로드는 토큰을 넘기지 않아도 씬 전환 때 함께 취소된다.
    /// 호출자의 취소는 이 대기만 포기시킨다. 로드 자체는 계속되어 스코프에 남는다(스코프가 해제할 때 함께 풀린다).
    /// </summary>
    public async UniTask<T> LoadAsync<T>(string key = null, CancellationToken ct = default) where T : Object
    {
        if (string.IsNullOrEmpty(key)) key = typeof(T).Name;

        if (_disposed)
        {
            Debug.LogError($"{Tag} 해제된 스코프 '{Name}'에서 '{key}'를 로드하려 했습니다.");
            return null;
        }

        var id = AssetKey.Of<T>(key);

        // 시작하자마자 등록한다 — 뒤이은 같은 요청은 새로 로드하지 않고 이 핸들을 함께 기다린다.
        // (스코프가 핸들을 하나만 들어야 참조 카운트가 소유자 하나당 한 번으로 맞는다)
        if (!_handles.TryGetValue(id, out AsyncOperationHandle handle))
        {
            handle = id.Type == typeof(GameObject)
                ? Addressables.LoadAssetAsync<GameObject>(key)
                : Addressables.LoadAssetAsync<T>(key);
            _handles[id] = handle;
        }

        try
        {
            using var link = Link(ct, out CancellationToken token);
            await handle.ToUniTask().AttachExternalCancellation(token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} '{Name}' 로드 실패 key='{key}' type={id.Type.Name}: {e.Message}");
            Forget(id, handle);
            return null;
        }

        if (_disposed) return null;   // 기다리는 사이 스코프가 해제됐다

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Forget(id, handle);
            return null;
        }

        return Extract<T>(handle.Result, key);
    }

    /// <summary>
    /// 라벨에 속한 에셋을 한 번에 미리 로드한다. 로딩 화면 등에서 불러 두면 이후 <see cref="GetNow{T}"/>로 즉시 꺼낼 수 있다.
    ///
    /// 각 에셋은 그 에셋의 실제 타입으로 로드된다(프리팹은 GameObject). 그래서 스프라이트 텍스처는 Texture2D로 들어오고,
    /// GetNow&lt;Sprite&gt;로는 꺼낼 수 없다 — 스프라이트는 LoadAsync&lt;Sprite&gt;로 따로 불러야 한다.
    /// 일부가 실패해도 나머지는 계속 로드한다.
    /// </summary>
    public async UniTask PreloadLabelAsync(string label, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(label) || _disposed) return;

        using var link = Link(ct, out CancellationToken token);

        var locHandle = Addressables.LoadResourceLocationsAsync(label);
        IList<IResourceLocation> locations;
        try
        {
            locations = await locHandle.ToUniTask(cancellationToken: token);
        }
        finally
        {
            if (locHandle.IsValid()) Addressables.Release(locHandle);
        }

        if (locations == null || locations.Count == 0 || _disposed) return;

        var tasks = new List<UniTask>(locations.Count);
        foreach (IResourceLocation location in locations)
        {
            var id = new AssetKey(location.PrimaryKey, AssetKey.Normalize(location.ResourceType));
            if (_handles.ContainsKey(id)) continue;

            AsyncOperationHandle handle = Addressables.LoadAssetAsync<Object>(location);
            _handles[id] = handle;
            tasks.Add(AwaitPreload(id, handle, token));
        }

        await UniTask.WhenAll(tasks);
    }

    private async UniTask AwaitPreload(AssetKey id, AsyncOperationHandle handle, CancellationToken ct)
    {
        try
        {
            await handle.ToUniTask().AttachExternalCancellation(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} '{Name}' 프리로드 실패 key='{id.Key}': {e.Message}");
            Forget(id, handle);
        }
    }

    /// <summary>이미 로드가 끝난 에셋을 즉시 꺼낸다(동기). 없거나 아직 로드 중이면 null.</summary>
    public T GetNow<T>(string key = null) where T : Object
    {
        if (string.IsNullOrEmpty(key)) key = typeof(T).Name;
        if (_disposed) return null;

        if (!_handles.TryGetValue(AssetKey.Of<T>(key), out AsyncOperationHandle handle)) return null;
        if (!handle.IsValid() || !handle.IsDone || handle.Status != AsyncOperationStatus.Succeeded) return null;

        return Extract<T>(handle.Result, key);
    }

    #endregion

    #region Release

    /// <summary>
    /// 이 스코프가 그 키로 로드한 것(모든 타입)을 해제한다. 다른 스코프가 로드한 같은 에셋에는 영향이 없다.
    /// 이 스코프에서 그 에셋으로 만든 인스턴스가 남아 있다면 먼저 정리해야 한다.
    /// </summary>
    public void Release(string key)
    {
        if (string.IsNullOrEmpty(key) || _disposed) return;

        List<AssetKey> targets = null;
        foreach (AssetKey id in _handles.Keys)
        {
            if (id.Key != key) continue;
            (targets ??= new List<AssetKey>()).Add(id);
        }

        if (targets == null) return;

        foreach (AssetKey id in targets)
        {
            ReleaseHandle(_handles[id]);
            _handles.Remove(id);
        }
    }

    /// <summary>
    /// 스코프를 닫는다. <see cref="Disposing"/>을 알린 뒤 로드한 에셋을 모두 해제한다.
    ///
    /// 해제는 한 프레임 미룬다. Disposing에서 파괴한 인스턴스는 프레임 끝에야 실제로 사라지는데,
    /// 그 전에 번들이 내려가면 남은 한 프레임 동안 메시·머티리얼이 빠진 채 그려질 수 있다.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 먼저 기다리던 작업을 끊는다. 그다음 이 스코프로 만든 것(풀 등)을 정리하고, 마지막에 에셋을 내린다.
        try
        {
            _lifetime.Cancel();
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} '{Name}' 취소 처리 중 예외가 발생했습니다. 정리는 계속합니다.\n{e}");
        }

        try
        {
            Disposing?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} '{Name}' 해제 알림 처리 중 예외가 발생했습니다. 에셋 해제는 계속합니다.\n{e}");
        }
        Disposing = null;

        var handles = new List<AsyncOperationHandle>(_handles.Values);
        _handles.Clear();
        ReleaseNextFrame(handles).Forget();
    }

    private static async UniTaskVoid ReleaseNextFrame(List<AsyncOperationHandle> handles)
    {
        await UniTask.Yield();
        foreach (AsyncOperationHandle handle in handles) ReleaseHandle(handle);
    }

    #endregion

    #region Internal

    /// <summary>
    /// 호출자 토큰과 스코프 수명 토큰을 합친다. 호출자가 토큰을 넘기지 않았으면 수명 토큰을 그대로 써서 할당이 없다.
    /// 돌려받은 객체는 using으로 감싸 해제한다(합치지 않은 경우 null — using은 null을 허용한다).
    /// </summary>
    private CancellationTokenSource Link(CancellationToken ct, out CancellationToken token)
    {
        if (!ct.CanBeCanceled)
        {
            token = _lifetime.Token;
            return null;
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        token = linked.Token;
        return linked;
    }

    // 실패한 로드를 스코프에서 뺀다. 같은 핸들을 기다리던 여러 요청이 각자 부르므로, 아직 그 핸들일 때만 처리한다.
    private void Forget(AssetKey id, AsyncOperationHandle handle)
    {
        if (!_handles.TryGetValue(id, out AsyncOperationHandle current) || !current.Equals(handle)) return;

        _handles.Remove(id);
        ReleaseHandle(handle);
    }

    // 아직 로드 중인 핸들은 끝난 뒤에 해제한다.
    private static void ReleaseHandle(AsyncOperationHandle handle)
    {
        if (!handle.IsValid()) return;

        if (handle.IsDone) Addressables.Release(handle);
        else handle.Completed += op => Addressables.Release(op);
    }

    private static T Extract<T>(object result, string key) where T : Object
    {
        if (result is T typed) return typed;

        if (result is GameObject go && typeof(Component).IsAssignableFrom(typeof(T)))
        {
            if (go.TryGetComponent(out T component)) return component;
            Debug.LogError($"{Tag} '{key}' 프리팹에 {typeof(T).Name} 컴포넌트가 없습니다.");
            return null;
        }

        Debug.LogError($"{Tag} 타입 불일치 key='{key}' 요청={typeof(T).Name} 실제={result?.GetType().Name}");
        return null;
    }

    private readonly struct AssetKey : IEquatable<AssetKey>
    {
        public readonly string Key;
        public readonly Type Type;

        public AssetKey(string key, Type type)
        {
            Key = key;
            Type = type;
        }

        // 컴포넌트는 프리팹(GameObject)으로 로드한다.
        public static AssetKey Of<T>(string key) where T : Object => new(key, Normalize(typeof(T)));

        public static Type Normalize(Type type)
        {
            if (type == null) return typeof(Object);
            return typeof(Component).IsAssignableFrom(type) ? typeof(GameObject) : type;
        }

        public bool Equals(AssetKey other) => Key == other.Key && Type == other.Type;
        public override bool Equals(object obj) => obj is AssetKey other && Equals(other);
        public override int GetHashCode() => ((Key != null ? Key.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
    }

    #endregion
}
