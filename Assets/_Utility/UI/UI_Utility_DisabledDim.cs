using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼(Selectable)이 꺼지면 버튼 안의 그림·글자를 모두 어둡게 한다(색의 밝기만 줄이고 알파는 그대로).
/// Selectable의 색 전환은 대상 그래픽 하나만 칠하므로, 테두리·몸통·글자를 겹쳐 만든 버튼은 꺼져도 겉모습이 거의 그대로다.
/// 버튼 전체를 반투명하게 하면(CanvasGroup 알파) 겹친 몸통이 비쳐 아래의 테두리 색 판이 드러난다 — 그래서 투명하게 하지 않고 어둡게 한다.
/// 버튼의 Disabled Color는 흰색으로 두어 두 번 어두워지지 않게 한다.
///
/// 꺼지는 순간의 색을 기억했다가 켜질 때 되돌린다 — 꺼져 있는 동안 코드가 자식 색을 바꾸면 켜질 때 그 색은 덮인다.
/// interactable이 바뀌어도 알림이 없어 매 프레임 확인한다(값이 같으면 아무것도 하지 않는다).
/// </summary>
public sealed class UI_Utility_DisabledDim : MonoBehaviour
{
    [Tooltip("지켜볼 버튼. 비우면 같은 오브젝트의 Selectable.")]
    [SerializeField] private Selectable _target;
    [Tooltip("꺼졌을 때 밝기 배수(0 = 검정, 1 = 그대로).")]
    [SerializeField, Range(0f, 1f)] private float _brightness = 0.45f;

    private Graphic[] _graphics;
    private Color[] _colors;
    private bool _dimmed;

    private void Awake()
    {
        if (_target == null) _target = GetComponent<Selectable>();
        _graphics = GetComponentsInChildren<Graphic>(true);
        _colors = new Color[_graphics.Length];
    }

    private void OnEnable() => Apply();

    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (_target == null || _graphics == null) return;
        // IsInteractable은 부모 CanvasGroup까지 본다.
        bool dim = !_target.IsInteractable();
        if (dim == _dimmed) return;
        _dimmed = dim;
        for (int i = 0; i < _graphics.Length; i++)
        {
            Graphic graphic = _graphics[i];
            if (graphic == null) continue;
            if (dim)
            {
                _colors[i] = graphic.color;
                Color c = _colors[i];
                graphic.color = new Color(c.r * _brightness, c.g * _brightness, c.b * _brightness, c.a);
            }
            else
            {
                graphic.color = _colors[i];
            }
        }
    }
}
