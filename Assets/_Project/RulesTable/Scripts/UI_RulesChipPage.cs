using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 규칙 화면 "칩" 장: 버프 / 디버프 소제목 아래 칩마다 고리(칩 색) + 이름. 칩 기호 그림은 넣지 않는다 — 색과 이름으로 충분하고
/// 기호는 뜻을 알기 어려웠다(사용자 2026-11-13). 줄은 견본을 복제해 세로 FlexLayout에 쌓는다.
/// </summary>
public sealed class UI_RulesChipPage : MonoBehaviour
{
    [Tooltip("소제목·칩 줄을 쌓는 곳(세로 FlexLayout).")]
    [SerializeField] private RectTransform _list;
    [Tooltip("소제목 견본(꺼 둔다).")]
    [SerializeField] private TMP_Text _headerTemplate;
    [Tooltip("칩 줄 견본(꺼 둔다).")]
    [SerializeField] private UI_RulesChipRow _rowTemplate;

    public void Fill(SO_RulesTableConfig config)
    {
        // 먼저 끈다 — Destroy는 프레임 끝에 지우므로, 그때까지 레이아웃이 낡은 칸까지 세지 않게.
        for (int i = _list.childCount - 1; i >= 0; i--)
        {
            GameObject old = _list.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        // 소제목 글은 코드가 고르므로(버프·디버프 견본 하나) 호출 안에 원문을 적는다 — 번역표 툴이 모을 수 있게.
        AddGroup(App.Localize("버프"), config.BuffColor, config.BuffChips, config.ChipRing);
        AddGroup(App.Localize("디버프"), config.DebuffColor, config.DebuffChips, config.ChipRing);
    }

    private void AddGroup(string title, Color color, List<SO_ChipCard> chips, Sprite ring)
    {
        if (chips.Count == 0) return;
        TMP_Text header = Instantiate(_headerTemplate, _list, false);
        header.gameObject.SetActive(true);
        header.text = title;
        header.color = color;
        for (int i = 0; i < chips.Count; i++)
        {
            if (chips[i] == null) continue;
            UI_RulesChipRow row = Instantiate(_rowTemplate, _list, false);
            row.gameObject.SetActive(true);
            row.Set(ring, chips[i].IconColor, App.Localize(chips[i].DisplayName));
        }
    }
}
