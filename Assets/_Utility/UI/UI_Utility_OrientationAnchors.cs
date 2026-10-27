using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 가로·세로 화면에서 앵커를 바꿔 끼우는 배치. 요소마다 가로용·세로용 자리(빈 RectTransform)를 두고,
/// 이 컴포넌트가 붙은 영역이 세로로 길면 세로 자리의, 아니면 가로 자리의 앵커를 요소에 그대로 옮긴다(offset은 0).
///
/// 자리를 데이터 값이 아니라 오브젝트로 두는 이유: 프리팹을 열면 두 배치가 씬 뷰에 사각형으로 보여 앵커만으로 다듬을 수 있다.
/// 요소를 자리 밑으로 옮기지(SetParent) 않고 앵커만 복사하는 이유: 크기 변경 콜백 안에서 부모를 바꾸면 레이아웃 재계산과 부딪힌다.
/// 그래서 요소와 두 자리는 같은 부모 아래 있어야 한다. 실행 중에만 동작한다 — 편집 중에는 요소에 저작한 앵커가 그대로 보인다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UI_Utility_OrientationAnchors : UIBehaviour
{
    [Serializable]
    public struct Entry
    {
        public RectTransform Item;
        [Tooltip("가로 화면 자리. 요소와 같은 부모 아래 둔다.")]
        public RectTransform Landscape;
        [Tooltip("세로 화면 자리. 요소와 같은 부모 아래 둔다.")]
        public RectTransform Portrait;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();

    private int _applied = -1;

    protected override void OnEnable()
    {
        base.OnEnable();
        _applied = -1;
        Apply();
    }

    protected override void OnRectTransformDimensionsChange() => Apply();

    private void Apply()
    {
        if (!Application.isPlaying) return;
        Rect rect = ((RectTransform)transform).rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        int portrait = rect.height > rect.width ? 1 : 0;
        if (portrait == _applied) return;
        _applied = portrait;

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            RectTransform slot = portrait == 1 ? entry.Portrait : entry.Landscape;
            if (entry.Item == null || slot == null) continue;
            entry.Item.anchorMin = slot.anchorMin;
            entry.Item.anchorMax = slot.anchorMax;
            entry.Item.offsetMin = Vector2.zero;
            entry.Item.offsetMax = Vector2.zero;
        }
    }
}
