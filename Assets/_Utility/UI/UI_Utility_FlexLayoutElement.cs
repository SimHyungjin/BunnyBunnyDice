using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI_Utility_FlexLayout 자식의 주축 크기 규칙.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UI_Utility_FlexLayoutElement : MonoBehaviour
{
    public enum SizeType
    {
        [Tooltip("교차축 길이 × value (1이면 정사각형)")]
        Aspect,
        [Tooltip("주축 가용 길이 × value (0.3이면 30%)")]
        Percent,
        [Tooltip("Aspect/Percent를 배치하고 남은 공간을 value 가중치로 나눠 가짐")]
        Fill,
    }

    public SizeType type = SizeType.Aspect;

    [Tooltip("Aspect: 가로/세로 비율, Percent: 가용 길이 대비 비율, Fill: 남은 공간 가중치")]
    [Min(0f)] public float value = 1f;

    private void OnEnable() => SetDirty();
    private void OnDisable() => SetDirty();
    private void OnTransformParentChanged() => SetDirty();

#if UNITY_EDITOR
    private void OnValidate() => SetDirty();
#endif

    // 부모를 캐시하지 않고 LayoutRebuilder에 맡긴다. (재부모화되어도 현재 부모 레이아웃이 갱신됨)
    public void SetDirty() => LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
}
