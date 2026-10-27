using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 패널의 능력치 한 칸(기획서 12.2): 주사위(눈 = 단계 1~6) · 능력치 이름 · 지금 값.
/// 단계를 숫자가 아니라 주사위 눈으로 보이는 이유: 능력치가 주사위로 굴린 값이라는 게(리롤) 한눈에 읽히고, 같은 단계는 늘 같은 눈이다.
/// 6단계면 오른쪽 위 안쪽 구석에 작은 "최대" 태그(시안 SampleImages/제목 없음6.png) — 더 굴릴 수 없다는 걸(리롤 버튼이 꺼진 까닭) 칸에서 바로 읽게.
/// 누르면 고른 칸이 된다(노란 테두리) — 리롤할 능력치를 고르는 자리다. 체크 표시는 두지 않는다(칸 모양과 어울리지 않고 "최대" 태그와 같은 자리를 다툰다).
///
/// 굴림(리롤·캐릭터 열기)은 세이브가 먼저 바뀐 뒤에 시작하므로, 새 단계·값을 받아도 바로 그리지 않고 프레임 끝(LateUpdate)에 그린다.
/// 같은 프레임에 Roll이 오면 화면에는 아직 옛 값이 남고, 착지할 때 새 값으로 바뀐다.
/// </summary>
public sealed class UI_LobbyStatCell : MonoBehaviour
{
    [SerializeField] private Button _button;
    [Tooltip("고른 칸 표시(노란 테두리).")]
    [SerializeField] private GameObject _selected;
    [Tooltip("6단계 표시(\"최대\" 태그).")]
    [SerializeField] private GameObject _maxBadge;
    [SerializeField] private UI_MetaDie _die;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _valueText;

    private int _stage = HeroStats.MaxStage;
    private string _value = "";
    // 화면에 그려진 단계(굴림이 오르는지 견주는 기준).
    private int _shownStage = HeroStats.MaxStage;
    private int _rollStartStage;
    private bool _dirty;

    /// <summary>이 칸을 눌렀다.</summary>
    public event Action Clicked;

    private void Awake() => _button.onClick.AddListener(() => Clicked?.Invoke());

    public void Show(HeroStat stat, int stage, float value)
    {
        _nameText.text = HeroStatText.GetName(stat);
        _stage = stage;
        _value = HeroStatText.Format(stat, value);
        _dirty = true;
    }

    public void SetSelected(bool selected) => _selected.SetActive(selected);

    /// <summary>
    /// 주사위를 굴린다(Show로 새 단계를 받은 뒤 같은 프레임에 부른다). 구르는 중에 다시 부르면 이어서 구른다.
    /// 착지 때 굴리기 시작할 때보다 단계가 올랐으면 그 눈으로 반짝, 아니면 마지막에 굴린 눈을 흐리게 보였다가 원래 눈으로 돌아온다.
    /// </summary>
    /// <param name="rolled">이번에 굴린 눈.</param>
    /// <param name="reveal">캐릭터를 열 때: 언제나 새 단계로 반짝이고, 착지 전까지 값을 비운다(이전 캐릭터 값이 보이지 않게).</param>
    public void Roll(SO_MetaDiceConfig config, int rolled, float delay = 0f, bool reveal = false)
    {
        if (!_die.IsTumbling)
        {
            _die.Finish();
            _rollStartStage = _shownStage;
            if (reveal) _valueText.text = "";
        }
        bool success = reveal || _stage > _rollStartStage;
        _die.SetFace(_stage);
        _die.Roll(config, success ? _stage : rolled, success, delay, Apply);
    }

    /// <summary>굴림을 건너뛴다(새 값으로 바로 놓는다).</summary>
    public void FinishRoll() => _die.Finish();

    private void LateUpdate()
    {
        if (_dirty && !_die.IsTumbling) Apply();
    }

    private void Apply()
    {
        _dirty = false;
        _shownStage = _stage;
        _die.SetFace(_stage);
        _valueText.text = _value;
        _maxBadge.SetActive(_stage >= HeroStats.MaxStage);
    }
}
