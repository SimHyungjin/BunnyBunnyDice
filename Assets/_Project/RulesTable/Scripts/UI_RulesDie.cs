using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 규칙 화면의 주사위 하나: 면체 윤곽(주사위 색으로 물들인 흰 그림) + 가운데 눈 숫자. 눈이 없으면 숫자를 비운다(색 조합표).
/// </summary>
public sealed class UI_RulesDie : MonoBehaviour
{
    [SerializeField] private Image _outline;
    [SerializeField] private TMP_Text _pips;

    public void Set(Sprite outline, Color color, int pips) => Set(outline, color, pips, color);

    /// <param name="pipColor">눈 숫자 색 — 각성한 주사위는 게임 안처럼 금색으로 보인다.</param>
    public void Set(Sprite outline, Color color, int pips, Color pipColor)
    {
        _outline.sprite = outline;
        _outline.color = color;
        _pips.color = pipColor;
        _pips.text = pips > 0 ? pips.ToString() : string.Empty;
    }
}
