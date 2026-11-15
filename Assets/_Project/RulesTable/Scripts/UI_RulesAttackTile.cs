using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공격 장의 칸 하나: 공격 그림(주사위 색으로 물들이는 층 + 색 그대로인 층) + 공격 이름(주사위 색).
/// </summary>
public sealed class UI_RulesAttackTile : MonoBehaviour
{
    [SerializeField] private Image _shape;
    [SerializeField] private Image _mark;
    [SerializeField] private TMP_Text _name;

    public void Set(Sprite shape, Sprite mark, Color color, string attackName)
    {
        _shape.sprite = shape;
        _shape.color = color;
        _shape.enabled = shape != null;
        _mark.sprite = mark;
        _mark.enabled = mark != null;
        _name.text = attackName;
        _name.color = color;
    }
}
