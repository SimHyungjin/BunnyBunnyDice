using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창의 언어 버튼 하나. 지금 언어는 테두리·글자를 노랑으로 칠한다(선택 창의 지금 캐릭터 강조와 같은 색).
/// 색은 프리팹 데이터라 여기 필드로 둔다.
/// </summary>
public sealed class UI_SettingsLanguageButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Graphic _frame;
    [SerializeField] private TMP_Text _label;

    [Header("색")]
    [SerializeField] private Color _frameColor = new Color(0.961f, 0.02f, 0.424f);
    [SerializeField] private Color _labelColor = new Color(0.984f, 0.933f, 0.988f);
    [Tooltip("지금 언어 강조색(테두리·글자).")]
    [SerializeField] private Color _selectedColor = new Color(1f, 0.88f, 0.29f);

    /// <summary>이 버튼을 눌렀다.</summary>
    public event Action Clicked;

    private void Awake() => _button.onClick.AddListener(() => Clicked?.Invoke());

    public void SetLabel(string text) => _label.text = text;

    public void SetSelected(bool selected)
    {
        _frame.color = selected ? _selectedColor : _frameColor;
        _label.color = selected ? _selectedColor : _labelColor;
    }
}
