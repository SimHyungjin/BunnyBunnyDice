using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 규칙 화면 "합성" 장: 위는 얹기 예시(놓인 주사위 + 얹는 주사위 → 결과), 아래는 색 조합표(빨강 + 초록 = 노랑 …).
/// 결과는 얹기 규칙(DiceRules.Stack)으로, 조합표는 주사위 종류 목록의 색 비트로 만든다 — 규칙·종류가 바뀌어도 그림이 틀리지 않게.
/// 조합 줄은 견본을 복제해 세로 FlexLayout 안에 쌓는다(줄 수 = 섞인 색 종류 수).
/// </summary>
public sealed class UI_RulesMergePage : MonoBehaviour
{
    [Header("얹기 예시")]
    [SerializeField] private UI_RulesDie _base;
    [SerializeField] private UI_RulesDie _top;
    [SerializeField] private UI_RulesDie _result;

    [Header("색 조합표")]
    [Tooltip("조합 줄을 쌓는 곳(세로 FlexLayout).")]
    [SerializeField] private RectTransform _recipes;
    [Tooltip("조합 줄 견본(가로 FlexLayout, 꺼 둔다).")]
    [SerializeField] private RectTransform _rowTemplate;
    [Tooltip("조합 줄의 주사위 견본(꺼 둔다).")]
    [SerializeField] private UI_RulesDie _dieTemplate;
    [Tooltip("조합 줄의 기호(+ =) 견본(꺼 둔다).")]
    [SerializeField] private TMP_Text _opTemplate;

    private readonly List<DiceColor> _mixed = new List<DiceColor>();

    public void Fill(SO_RulesTableConfig config, SO_DiceConfig dice, SO_DiceStatsConfig stats)
    {
        FillExample(config, dice, stats);
        FillRecipes(config, dice);
    }

    // 예시 하나에 면체(큰 쪽)·눈(더하고 넘친 만큼 버림)·색(섞임)·각성(금색 눈)이 함께 보이게 값을 고른다(설정 SO).
    // 각성은 게임과 같은 판정·색을 쓴다.
    private void FillExample(SO_RulesTableConfig config, SO_DiceConfig dice, SO_DiceStatsConfig stats)
    {
        SO_RulesTableConfig.ExampleDie b = config.MergeBase;
        SO_RulesTableConfig.ExampleDie t = config.MergeTop;
        DiceState state = new DiceState { Faces = b.Faces, Colors = DiceColors.Normalize(b.Colors), Pips = b.Pips };
        SetDie(_base, config, dice, state.Faces, state.Colors, state.Pips);
        SetDie(_top, config, dice, t.Faces, t.Colors, t.Pips);
        DiceRules.Stack(ref state, new DiceCard(t.Faces, t.Colors), t.Pips);
        Color color = config.GetDiceColor(dice, state.Colors);
        Color pipColor = stats.IsAwakened(state) ? dice.AwakenedPipColor : color;
        _result.Set(config.GetFaceShape(state.Faces), color, state.Pips, pipColor);
    }

    private static void SetDie(UI_RulesDie die, SO_RulesTableConfig config, SO_DiceConfig dice, int faces, DiceColor colors, int pips) =>
        die.Set(config.GetFaceShape(faces), config.GetDiceColor(dice, colors), pips);

    // 섞인 색(비트 둘 이상)마다 한 줄: 들어간 기본색 + … = 섞인 색. 비트 수 → 값 순서(노랑·자홍·하늘 → 하양).
    private void FillRecipes(SO_RulesTableConfig config, SO_DiceConfig dice)
    {
        // 먼저 끈다 — Destroy는 프레임 끝에 지우므로, 그때까지 레이아웃이 낡은 칸까지 세지 않게.
        for (int i = _recipes.childCount - 1; i >= 0; i--)
        {
            GameObject old = _recipes.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        _mixed.Clear();
        for (int i = 0; i < dice.Types.Count; i++)
        {
            if (dice.Types[i] == null) continue;
            DiceColor colors = DiceColors.Normalize(dice.Types[i].Colors);
            if (BitCount(colors) < 2 || _mixed.Contains(colors)) continue;
            int at = _mixed.Count;
            while (at > 0 && Compare(_mixed[at - 1], colors) > 0) at--;
            _mixed.Insert(at, colors);
        }

        Sprite shape = config.GetFaceShape(6);
        for (int i = 0; i < _mixed.Count; i++)
        {
            RectTransform row = Instantiate(_rowTemplate, _recipes, false);
            row.gameObject.SetActive(true);
            DiceColor mixed = _mixed[i];
            bool first = true;
            for (int bit = 1; bit <= (int)DiceColors.All; bit <<= 1)
            {
                if (((int)mixed & bit) == 0) continue;
                if (!first) AddOp(row, "+");
                first = false;
                AddDie(row, shape, config.GetDiceColor(dice, (DiceColor)bit));
            }
            AddOp(row, "=");
            AddDie(row, shape, config.GetDiceColor(dice, mixed));
        }
    }

    private void AddDie(RectTransform row, Sprite shape, Color color)
    {
        UI_RulesDie die = Instantiate(_dieTemplate, row, false);
        die.gameObject.SetActive(true);
        die.Set(shape, color, 0);
    }

    private void AddOp(RectTransform row, string op)
    {
        TMP_Text text = Instantiate(_opTemplate, row, false);
        text.gameObject.SetActive(true);
        text.text = op;
    }

    private static int Compare(DiceColor a, DiceColor b)
    {
        int bits = BitCount(a) - BitCount(b);
        return bits != 0 ? bits : (int)a - (int)b;
    }

    private static int BitCount(DiceColor colors)
    {
        int count = 0;
        for (int v = (int)colors; v != 0; v &= v - 1) count++;
        return count;
    }
}
