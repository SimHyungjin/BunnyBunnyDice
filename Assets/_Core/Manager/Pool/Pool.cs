using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 프리팹 하나에 대한 풀. 단위는 GameObject다 — 컴포넌트 타입은 꺼낼 때 보는 관점일 뿐이라,
/// 같은 키를 다른 컴포넌트로 꺼내도 풀이 갈리거나 충돌하지 않는다.
///
/// 루트를 비활성 오브젝트로 둔다. 비활성 부모 아래에서 만들어진 인스턴스는 Awake·OnEnable이 돌지 않으므로,
/// 예열이 게임 코드(PlayOnAwake 사운드·파티클, 등록 등)를 실행하지 않는다. Awake는 첫 스폰 때 돈다.
/// </summary>
internal sealed class GameObjectPool : IDisposable
{
    private readonly GameObject _prefab;
    private readonly Transform _root;
    private readonly int _maxSize;

    private readonly Stack<PooledInstance> _inactive = new();
    private readonly HashSet<PooledInstance> _all = new();   // 활성 포함 전체 (정리용)
    private bool _disposed;

    public string Key { get; }

    public GameObjectPool(string key, GameObject prefab, Transform parent, int maxSize)
    {
        Key = key;
        _prefab = prefab;
        _maxSize = maxSize;

        var root = new GameObject($"Pool_{key}");
        root.SetActive(false);
        _root = root.transform;
        _root.SetParent(parent, false);
    }

    /// <summary>
    /// 인스턴스를 꺼내 배치하고 활성화한다.
    /// local이면 부모 기준 로컬 좌표로, 아니면 월드 좌표로 놓는다.
    /// </summary>
    public PooledInstance Spawn(Vector3 position, Quaternion rotation, Transform parent, bool local)
    {
        if (_disposed) return null;

        PooledInstance handle = Take();

        // 비활성 상태에서 배치를 끝내고 마지막에 켠다. Awake·OnEnable이 최종 위치를 보게 하기 위해서다.
        Transform t = handle.transform;
        t.SetParent(parent, false);
        if (local) t.SetLocalPositionAndRotation(position, rotation);
        else       t.SetPositionAndRotation(position, rotation);

        handle.Generation++;
        handle.IsActive = true;
        handle.gameObject.SetActive(true);

        for (int i = 0; i < handle.Poolables.Length; i++)   handle.Poolables[i].OnSpawn();
        for (int i = 0; i < handle.AfterSpawns.Length; i++) handle.AfterSpawns[i].AfterSpawn();

        return handle;
    }

    public void Return(PooledInstance handle)
    {
        if (handle == null || !handle.IsActive) return;   // 이중 반납 방지

        handle.IsActive = false;
        for (int i = 0; i < handle.Poolables.Length; i++) handle.Poolables[i].OnDespawn();

        // 풀이 이미 정리됐다면(씬 전환 직후 늦게 들어온 반납 등) 돌려놓을 곳이 없으니 파괴한다.
        if (_disposed)
        {
            Object.Destroy(handle.gameObject);
            return;
        }

        handle.gameObject.SetActive(false);
        handle.transform.SetParent(_root, false);

        // 풀이 넘치면 되돌리지 않고 파괴한다.
        if (_inactive.Count >= _maxSize)
        {
            DestroyHandle(handle);
            return;
        }
        _inactive.Push(handle);
    }

    /// <summary>비활성 인스턴스 하나를 미리 만들어 쌓아둔다. (Awake·콜백은 돌지 않는다)</summary>
    public void PrewarmOne()
    {
        if (_disposed || _inactive.Count >= _maxSize) return;
        _inactive.Push(CreateOne());
    }

    public void ClearInactive()
    {
        while (_inactive.Count > 0) DestroyHandle(_inactive.Pop());
    }

    /// <summary>풀을 닫는다. 활성 인스턴스(다른 부모 아래 놓인 것 포함)까지 모두 파괴한다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (PooledInstance handle in _all)
            if (handle != null) Object.Destroy(handle.gameObject);

        _all.Clear();
        _inactive.Clear();
        if (_root != null) Object.Destroy(_root.gameObject);
    }

    // 대기 중이던 인스턴스가 밖에서 파괴됐을 수 있다. 그런 것은 건너뛰고 버린다.
    private PooledInstance Take()
    {
        while (_inactive.Count > 0)
        {
            PooledInstance handle = _inactive.Pop();
            if (handle != null) return handle;
            _all.Remove(handle);
        }

        return CreateOne();
    }

    private PooledInstance CreateOne()
    {
        GameObject go = Object.Instantiate(_prefab, _root, false);
        go.SetActive(false);

        var handle = go.AddComponent<PooledInstance>();
        handle.Pool = this;

        // 생성 시 한 번만 모은다. 자식(트레일·하위 이펙트 등)에 붙은 것도 콜백을 받는다.
        handle.Poolables = go.GetComponentsInChildren<IPoolable>(true);
        handle.AfterSpawns = go.GetComponentsInChildren<IAfterSpawn>(true);

        _all.Add(handle);
        return handle;
    }

    private void DestroyHandle(PooledInstance handle)
    {
        _all.Remove(handle);
        if (handle != null) Object.Destroy(handle.gameObject);
    }
}
