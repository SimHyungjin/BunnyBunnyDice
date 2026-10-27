using System;
using UnityEngine;

/// <summary>
/// 설정 SO의 공통 부모. 값이 바뀌면 Changed로 알린다 — 실행 중 조정(GameTuning)이 바뀐 설정만 다시 적용하기 위해서다.
/// 에디터에서 인스펙터로 고치면 OnValidate가 불리므로 그때 알린다. 코드로 값을 통째로 덮어쓴 뒤에는 NotifyChanged를 직접 부른다.
/// </summary>
public abstract class SO_Config : ScriptableObject
{
    public event Action Changed;

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// 이 설정이 참조하는 하위 설정 SO(주사위 종류 목록 등)를 swap이 돌려준 것으로 바꿔 끼운다. 하위 설정을 가진 SO만 override한다.
    /// 실행 중 복사본(ConfigTuning)이 하위까지 복사본으로 바꿔 끼우는 데 쓴다 — 그러지 않으면 실행 중 조정이 원본 에셋을 고쳐 저장해 버린다.
    /// </summary>
    public virtual void SwapNested(Func<SO_Config, SO_Config> swap)
    {
    }

#if UNITY_EDITOR
    protected virtual void OnValidate() => Changed?.Invoke();
#endif
}
