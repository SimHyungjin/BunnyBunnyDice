using UnityEngine;

/// <summary>
/// 규칙 화면 "공격" 장: 색마다 공격 그림 + 공격 이름 칸을 두 줄씩 놓는다. 그림 순서는 설정(SO_RulesTableConfig.Attacks),
/// 이름·색은 주사위 종류 SO에서 읽는다. 칸은 견본을 복제해 가로 줄(FlexLayout) 안에 넣고, 줄은 세로 FlexLayout에 쌓는다.
/// </summary>
public sealed class UI_RulesAttackPage : MonoBehaviour
{
    [Tooltip("줄을 쌓는 곳(세로 FlexLayout).")]
    [SerializeField] private RectTransform _grid;
    [Tooltip("한 줄 견본(가로 FlexLayout, 꺼 둔다).")]
    [SerializeField] private RectTransform _rowTemplate;
    [Tooltip("칸 견본(꺼 둔다).")]
    [SerializeField] private UI_RulesAttackTile _tileTemplate;
    [Tooltip("한 줄에 놓는 칸 수.")]
    [Min(1)] [SerializeField] private int _columns = 2;

    public void Fill(SO_RulesTableConfig config, SO_DiceConfig dice)
    {
        // 먼저 끈다 — Destroy는 프레임 끝에 지우므로, 그때까지 레이아웃이 낡은 칸까지 세지 않게.
        for (int i = _grid.childCount - 1; i >= 0; i--)
        {
            GameObject old = _grid.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        RectTransform row = null;
        for (int i = 0; i < config.Attacks.Count; i++)
        {
            SO_RulesTableConfig.AttackPicture picture = config.Attacks[i];
            SO_DiceType type = dice.GetDiceType(picture.Colors);
            if (type == null) continue;
            if (row == null || row.childCount >= _columns)
            {
                row = Instantiate(_rowTemplate, _grid, false);
                row.gameObject.SetActive(true);
            }
            UI_RulesAttackTile tile = Instantiate(_tileTemplate, row, false);
            tile.gameObject.SetActive(true);
            tile.Set(picture.Shape, picture.Mark, config.GetDiceColor(dice, picture.Colors), App.Localize(type.AttackName));
        }
    }
}
