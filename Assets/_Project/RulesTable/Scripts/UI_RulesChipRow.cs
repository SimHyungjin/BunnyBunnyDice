using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>칩 장의 줄 하나: 칩 고리(칩 색으로 물들인 흰 그림) + 칩 이름.</summary>
public sealed class UI_RulesChipRow : MonoBehaviour
{
    [SerializeField] private Image _ring;
    [SerializeField] private TMP_Text _name;

    public void Set(Sprite ring, Color color, string chipName)
    {
        _ring.sprite = ring;
        _ring.color = color;
        _name.text = chipName;
    }
}
