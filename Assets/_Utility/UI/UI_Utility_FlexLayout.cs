using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 축으로 자식을 배치하는 레이아웃. 자식별 크기 규칙은 UI_Utility_FlexLayoutElement로 지정한다.
/// (Element가 없는 자식은 Aspect 1 = 정사각형)
/// 1) Aspect/Percent 크기를 먼저 계산하고, 공간이 부족하면 shrinkToFit에 따라 비율 유지 축소
/// 2) 남은 공간을 Fill 자식들이 가중치대로 나눠 가짐
/// 3) 남는 공간이 있으면 Child Alignment에 따라 정렬
/// 부모 영역을 채우는 용도라 preferred size는 계산하지 않는다. (Content Size Fitter와 함께 쓰지 않음)
/// </summary>
[DisallowMultipleComponent]
public class UI_Utility_FlexLayout : LayoutGroup
{
    public enum Direction { Horizontal, Vertical }

    [SerializeField] private Direction direction = Direction.Horizontal;

    [Tooltip("자식 사이 간격 (주축 가용 길이 대비 비율)")]
    [SerializeField, Range(0f, 1f)] private float spacingRatio = 0f;

    [Tooltip("체크 시 마지막 자식부터 배치")]
    [SerializeField] private bool reverse = false;

    [Tooltip("Aspect/Percent 자식 합이 가용 공간을 넘으면 비율을 유지한 채 축소")]
    [SerializeField] private bool shrinkToFit = true;

    public override void CalculateLayoutInputVertical() { }

    public override void SetLayoutHorizontal() => ApplyLayout(0);
    public override void SetLayoutVertical() => ApplyLayout(1);

    // 레이아웃 시스템이 축별로 호출하므로, 같은 계산을 하고 해당 축만 적용한다.
    private void ApplyLayout(int axis)
    {
        int count = rectChildren.Count;
        if (count == 0) return;

        int mainAxis = direction == Direction.Horizontal ? 0 : 1;
        int crossAxis = 1 - mainAxis;

        float usableMain = GetUsableSize(mainAxis);
        float usableCross = GetUsableSize(crossAxis);
        float spacing = usableMain * spacingRatio;

        var types = new UI_Utility_FlexLayoutElement.SizeType[count];
        var sizes = new float[count];
        float fixedTotal = 0f;
        float fillWeight = 0f;

        for (int i = 0; i < count; i++)
        {
            var element = rectChildren[i].GetComponent<UI_Utility_FlexLayoutElement>();
            bool useElement = element != null && element.isActiveAndEnabled;
            types[i] = useElement ? element.type : UI_Utility_FlexLayoutElement.SizeType.Aspect;
            float value = useElement ? element.value : 1f;

            switch (types[i])
            {
                case UI_Utility_FlexLayoutElement.SizeType.Aspect:  sizes[i] = usableCross * value; break;
                case UI_Utility_FlexLayoutElement.SizeType.Percent: sizes[i] = usableMain * value;  break;
                case UI_Utility_FlexLayoutElement.SizeType.Fill:    sizes[i] = value; fillWeight += value; continue;
            }
            fixedTotal += sizes[i];
        }

        float required = fixedTotal + spacing * (count - 1);
        float scale = shrinkToFit && required > usableMain && required > 0f ? usableMain / required : 1f;
        float remaining = Mathf.Max(0f, usableMain - required * scale);

        float total = spacing * scale * (count - 1);
        for (int i = 0; i < count; i++)
        {
            if (types[i] == UI_Utility_FlexLayoutElement.SizeType.Fill)
                sizes[i] = fillWeight > 0f ? remaining * sizes[i] / fillWeight : 0f;
            else
                sizes[i] *= scale;
            total += sizes[i];
        }

        float pos = GetStartOffset(mainAxis, total);

        for (int k = 0; k < count; k++)
        {
            int i = reverse ? count - 1 - k : k;
            RectTransform child = rectChildren[i];

            if (axis == mainAxis)
            {
                SetChildAlongAxis(child, mainAxis, pos, sizes[i]);
            }
            else
            {
                // Aspect는 축소 시 교차축도 함께 줄여 비율을 유지한다.
                float crossSize = types[i] == UI_Utility_FlexLayoutElement.SizeType.Aspect ? usableCross * scale : usableCross;
                SetChildAlongAxis(child, crossAxis, GetStartOffset(crossAxis, crossSize), crossSize);
            }

            pos += sizes[i] + spacing * scale;
        }
    }

    private float GetUsableSize(int axis)
        => Mathf.Max(0f, rectTransform.rect.size[axis] - (axis == 0 ? padding.horizontal : padding.vertical));
}
