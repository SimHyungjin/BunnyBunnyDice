using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 네 꼭짓점을 따로 정하는 사각형 UI 도형. 기울어진 판·모서리가 비뚤어진 띠처럼 도형을 겹쳐 만드는 UI에 쓴다
/// — 그림 파일 없이 해상도와 무관하게 선명하고, 색은 Graphic.color로 바꾼다.
/// 꼭짓점은 RectTransform 안의 비율(0~1, 왼쪽 아래 기준)이다. CSS clip-path의 polygon(x% y%)는 (x, 1 − y)로 옮기면 같다.
/// 위·아래 투명도를 따로 주면 세로 그러데이션이 된다(그림 위에 깐 판이 위로 갈수록 흐려져 그림에 묻히게 할 때).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UI_Utility_Quad : MaskableGraphic
{
    [SerializeField] private Vector2 _topLeft = new Vector2(0f, 1f);
    [SerializeField] private Vector2 _topRight = new Vector2(1f, 1f);
    [SerializeField] private Vector2 _bottomRight = new Vector2(1f, 0f);
    [SerializeField] private Vector2 _bottomLeft = new Vector2(0f, 0f);
    [Tooltip("위 두 꼭짓점의 투명도 배수(색 알파에 곱한다). 아래와 다르면 세로 그러데이션이 된다.")]
    [Range(0f, 1f)] [SerializeField] private float _topAlpha = 1f;
    [Tooltip("아래 두 꼭짓점의 투명도 배수.")]
    [Range(0f, 1f)] [SerializeField] private float _bottomAlpha = 1f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color top = color;
        top.a *= _topAlpha;
        Color bottom = color;
        bottom.a *= _bottomAlpha;
        vh.AddVert(ToLocal(rect, _bottomLeft), bottom, Vector2.zero);
        vh.AddVert(ToLocal(rect, _topLeft), top, Vector2.zero);
        vh.AddVert(ToLocal(rect, _topRight), top, Vector2.zero);
        vh.AddVert(ToLocal(rect, _bottomRight), bottom, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    private static Vector3 ToLocal(Rect rect, Vector2 normalized) =>
        new Vector3(rect.x + rect.width * normalized.x, rect.y + rect.height * normalized.y);
}
