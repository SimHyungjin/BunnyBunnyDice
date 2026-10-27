using UnityEngine;

/// <summary>
/// 카드 크기 계산. 손패 카드 높이를 화면(캔버스) 짧은 변의 비율로 정한다 — 가로·세로 어느 화면이든 손가락 대비 같은 크기로 보이게.
/// 손패의 거리 값(부채꼴 반지름·높이 등)은 이 높이의 배수라, 화면이 바뀌어도 손패 모양이 그대로 커지고 줄어든다.
/// </summary>
public static class CardSizing
{
    /// <summary>손패 카드 한 장의 높이(canvas 단위).</summary>
    /// <param name="canvas">루트 캔버스의 RectTransform.</param>
    public static float GetHandCardHeight(RectTransform canvas, SO_CardHandConfig config)
    {
        Rect rect = canvas.rect;
        return Mathf.Min(rect.width, rect.height) * config.CardHeightRatio;
    }

    /// <summary>프리팹(설명창 크기)을 손패 카드 크기로 줄이는 배율.</summary>
    public static float GetBaseScale(float handCardHeight, SO_CardHandConfig config)
    {
        float prefabHeight = ((RectTransform)config.CardPrefab.transform).sizeDelta.y;
        return prefabHeight > 0f ? handCardHeight / prefabHeight : 1f;
    }
}
