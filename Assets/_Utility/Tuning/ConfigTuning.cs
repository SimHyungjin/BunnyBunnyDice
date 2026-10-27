using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 설정 SO의 실행 중 복사본을 만들고 관리한다. 게임 코드는 복사본만 참조한다.
/// - 복사본을 인스펙터(GameTuning)에서 고치면 이번 플레이에서만 바뀐다(원본 에셋은 그대로).
/// - 에디터에서 원본 SO를 고치면 에셋에 저장되고, 복사본도 원본 값으로 통째로 다시 덮어쓴다.
/// 어느 쪽이든 복사본의 Changed가 불리므로, 구독한 쪽이 바뀐 설정을 다시 적용한다.
/// </summary>
public sealed class ConfigTuning : IDisposable
{
    private readonly List<SO_Config> _copies = new List<SO_Config>();
    private readonly List<Action> _unlinks = new List<Action>();
    // 복사본 → 원본. GameTuning이 원본 에셋으로 가는 창구를 줄 때 쓴다(값을 원본에 쓰지는 않는다).
    private readonly Dictionary<SO_Config, SO_Config> _originals = new Dictionary<SO_Config, SO_Config>();

    public IReadOnlyList<SO_Config> Copies => _copies;

    /// <summary>복사본의 원본 에셋. 모르는 것이면 null.</summary>
    public SO_Config GetOriginal(SO_Config copy) =>
        copy != null && _originals.TryGetValue(copy, out SO_Config original) ? original : null;

    public T Copy<T>(T original) where T : SO_Config
    {
        T copy = Object.Instantiate(original);
        copy.name = original.name;
        _copies.Add(copy);
        _originals[copy] = original;

        // 하위 설정(SwapNested)도 복사본으로 바꿔 끼운다. 하위 복사본이 바뀌면 부모도 바뀐 것으로 알린다
        // — 부모를 구독한 쪽(주사위 표시 등)이 하위 값 변경도 받게.
        Dictionary<SO_Config, SO_Config> nested = new Dictionary<SO_Config, SO_Config>();
        Func<SO_Config, SO_Config> swap = child =>
        {
            if (child == null) return null;
            if (nested.TryGetValue(child, out SO_Config childCopy)) return childCopy;
            childCopy = Copy(child);
            childCopy.Changed += copy.NotifyChanged;
            nested[child] = childCopy;
            return childCopy;
        };
        copy.SwapNested(swap);

#if UNITY_EDITOR
        Action sync = () =>
        {
            if (copy == null) return;
            UnityEditor.EditorUtility.CopySerialized(original, copy);
            // 통째로 덮어쓰면 하위 참조가 원본으로 돌아가므로 다시 복사본으로 바꿔 끼운다(원본에 새로 넣은 하위도 이때 복사된다).
            copy.SwapNested(swap);
            copy.NotifyChanged();
        };
        original.Changed += sync;
        // 원본은 에셋이라 플레이가 끝나도 살아 있다. 구독을 풀지 않으면 다음 플레이에 죽은 복사본으로의 연결이 남는다.
        _unlinks.Add(() => original.Changed -= sync);
#endif
        return copy;
    }

    public void Dispose()
    {
        for (int i = 0; i < _unlinks.Count; i++) _unlinks[i]();
        _unlinks.Clear();
        for (int i = 0; i < _copies.Count; i++)
            if (_copies[i] != null) Object.Destroy(_copies[i]);
        _copies.Clear();
        _originals.Clear();
    }
}
