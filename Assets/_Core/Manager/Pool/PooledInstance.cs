using System;
using UnityEngine;

/// <summary>스폰/반납 시점에 호출받고 싶은 컴포넌트가 구현한다. 인스턴스의 자식에 있어도 불린다.</summary>
public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

/// <summary>스폰이 끝난 직후(위치·부모 설정과 모든 OnSpawn 이후) 호출받고 싶을 때 구현한다. 자식에 있어도 불린다.</summary>
public interface IAfterSpawn
{
    void AfterSpawn();
}

/// <summary>
/// 풀이 만든 인스턴스에 자동으로 붙는 경량 핸들.
/// 자기가 속한 풀과, 생성 시 한 번만 모아 둔 콜백 대상을 들고 있다.
/// 덕분에 반납·콜백에 매니저 딕셔너리 조회나 매번의 GetComponent가 필요 없다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledInstance : MonoBehaviour
{
    internal GameObjectPool Pool;
    internal IPoolable[] Poolables;
    internal IAfterSpawn[] AfterSpawns;

    // 스폰될 때마다 증가한다. 예약 디스폰이 "바로 그 스폰"에 대한 것인지 판정하는 데 쓴다.
    internal int Generation;

    // 현재 스폰 상태인지. 이중 반납을 막는다.
    internal bool IsActive;

    // Spawn<T>가 매번 GetComponent하지 않도록 마지막으로 꺼낸 타입 하나를 기억한다. (키 하나는 보통 한 타입으로만 꺼낸다)
    private Type _cachedType;
    private Component _cachedComponent;

    internal T GetTarget<T>() where T : Component
    {
        if (_cachedType == typeof(T)) return (T)_cachedComponent;

        T component = GetComponent<T>();
        _cachedType = typeof(T);
        _cachedComponent = component;
        return component;
    }
}
