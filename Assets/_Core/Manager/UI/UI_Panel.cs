using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 레이어에 뜨는 UI의 베이스. 인스턴스는 풀로 재사용된다.
///
/// <see cref="OnOpenEvent"/>·<see cref="OnCloseEvent"/>는 <b>이번 표시에 대한</b> 구독이다. 띄운 뒤 구독하고,
/// 반납될 때 모두 지워진다 — 지우지 않으면 재사용될 때마다 이전 표시의 콜백이 쌓여 함께 불린다.
/// 매번 해야 하는 처리는 <see cref="Open"/>/<see cref="Close"/>를 override하거나 <see cref="OnDespawned"/>에 둔다.
/// </summary>
public abstract class UI_Panel : UI, IPoolable
{
    [HideInInspector] public UnityEvent OnOpenEvent  = new();
    [HideInInspector] public UnityEvent OnCloseEvent = new();

    protected bool _isOpened;

    /// <summary>지금 열려 있는지. 닫히는 연출 중이면 false다.</summary>
    public bool IsOpened => _isOpened;

    void IPoolable.OnSpawn() { }

    // 즉시 닫기(씬 전환·instant)는 Close를 거치지 않고 바로 반납되므로, 열림 상태와 연출도 여기서 정리한다.
    // 그러지 않으면 다음에 꺼냈을 때 _isOpened가 true로 남아 Open()이 아무것도 하지 않는다.
    // 순서가 중요하다: 리스너·요청을 먼저 비우고 연출을 멈춘다.
    // 연출을 멈추면 그것을 기다리던 쪽이 그 자리에서 이어 실행되는데, 그때 아직 리스너가 남아 있으면
    // 이번 표시의 닫기 콜백이 반납 도중에 불린다.
    void IPoolable.OnDespawn()
    {
        OnOpenEvent.RemoveAllListeners();
        OnCloseEvent.RemoveAllListeners();
        OnDespawned();
        _isOpened = false;
        this.StopAnimation();
    }

    /// <summary>풀로 반납될 때. 표시마다 바뀌는 상태를 여기서 비운다.</summary>
    protected virtual void OnDespawned() { }

    public virtual void Open()
    {
        if (_isOpened) return;
        _isOpened = true;
        OnOpenEvent?.Invoke();
    }

    public virtual void Close()
    {
        if (!_isOpened) return;
        _isOpened = false;
        OnCloseEvent?.Invoke();
    }

    protected virtual void OnDestroy()
    {
        OnOpenEvent.RemoveAllListeners();
        OnCloseEvent.RemoveAllListeners();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

        var fields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<SerializeField>() == null) continue;

            var currentValue = field.GetValue(this);
            if (currentValue != null && !currentValue.Equals(null)) continue;

            if (typeof(Component).IsAssignableFrom(field.FieldType))
            {
                var target = FindComponent(field.FieldType, field.Name);
                if (target != null)
                {
                    field.SetValue(this, target);
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
            else if (field.FieldType == typeof(GameObject))
            {
                var target = FindComponent(typeof(Transform), field.Name);
                if (target != null)
                {
                    field.SetValue(this, (target as Transform).gameObject);
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
        }
    }

    private Component FindComponent(System.Type type, string name)
    {
        var components = GetComponentsInChildren(type, true);
        foreach (var c in components)
            if (c.name == name) return c;
        return null;
    }
#endif
}
