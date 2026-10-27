using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 결과 화면(게임 오버). 이번 판의 기록·받은 재화(클리어한 웨이브 × 보상 주사위 눈의 합, 기획서 12.5)와 다시 하기·로비로 버튼만 보이고,
/// 누른 뒤 무엇을 할지는 띄운 쪽(GameResult)이 정한다.
/// 판이 끝난 화면이라 클릭 가드로 아래 조작을 막고, 뒤로가기로도 닫히지 않는다(닫으면 멈춘 판에 갇힌다).
/// 카드·상단 바와 같은 모양(자홍 테두리·기울어진 띠)이고, 묶음은 화면비가 바뀌어도 통째로 줄어든다(AspectRatioFitter).
///
/// 보상: 보상 주사위들(10웨이브마다 하나)이 한 줄로 굴러 착지 → 합 숫자 자리로 모이며 사라짐 → "40 × 15 = +600"까지 숫자가 오른다
/// (재화는 이미 세이브에 들어가 있다 — 보여 주기만 한다). 주사위를 "40 ×" 옆에 늘어놓은 채로 두면 눈끼리 곱하는 것으로 읽혀서 합으로 모은다.
/// 주사위가 없으면(10웨이브 전) 굴리지 않고 "+0"만 보인다.
/// 위 "웨이브" 줄도 클리어한 웨이브다(RunSummary.Wave) — 보상 줄과 숫자가 같다.
/// 연출 중에 화면 어디든 누르면 결과로 건너뛴다. 버튼은 결과가 보인 뒤에 눌린다 — 받은 재화를 못 보고 넘어가지 않게.
/// </summary>
public sealed class UI_ResultPopup : UI_Popup
{
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _killText;
    [Tooltip("보상 줄의 \"웨이브 ×\".")]
    [SerializeField] private TMP_Text _rewardWaveText;
    [Tooltip("보상 주사위 원본(숨겨 두고 주사위마다 복제한다).")]
    [SerializeField] private UI_MetaDie _rewardDie;
    [Tooltip("보상 주사위가 굴러 놓이는 줄(FlexLayout). 주사위마다 칸을 만들어 넣는다 — 많으면 줄 안에서 작아진다.")]
    [SerializeField] private RectTransform _rewardDiceRow;
    [Tooltip("주사위가 모인 뒤 보이는 눈의 합.")]
    [SerializeField] private TMP_Text _rewardSumText;
    [Tooltip("보상 줄의 \"= +재화\".")]
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _lobbyButton;

    private Action _retry;
    private Action _lobby;
    private SO_MetaDiceConfig _diceConfig;
    private int _amount;
    // 굴린 주사위 칸(줄의 자식)과 그 안의 주사위. 다음 Show·반납 때 지운다.
    private readonly List<RectTransform> _slots = new List<RectTransform>();
    private readonly List<UI_MetaDie> _dice = new List<UI_MetaDie>();
    private readonly List<Vector3> _slotStarts = new List<Vector3>();
    private int _landed;
    // 모이기 시작 시각·숫자 오르기 시작 시각. < 0 = 아직.
    private float _gatherStart = -1f;
    private float _countStart = -1f;
    private bool _revealing;

    public void Show(RunSummary run, RunReward reward, SO_MetaDiceConfig diceConfig, Action retry, Action lobby)
    {
        _waveText.text = run.Wave.ToString();
        _levelText.text = run.Level.ToString();
        _killText.text = run.Kills.ToString();
        _retry = retry;
        _lobby = lobby;
        _retryButton.onClick.RemoveListener(OnRetry);
        _retryButton.onClick.AddListener(OnRetry);
        _lobbyButton.onClick.RemoveListener(OnLobby);
        _lobbyButton.onClick.AddListener(OnLobby);

        // 숫자와 기호뿐이라 번역하지 않는다.
        _diceConfig = diceConfig;
        _amount = reward.Amount;
        _rewardWaveText.text = $"{reward.Wave} ×";
        _rewardSumText.text = reward.Sum.ToString();
        _rewardText.text = "";
        _gatherStart = -1f;
        _countStart = -1f;
        _landed = 0;
        ClearDice();
        _rewardDie.gameObject.SetActive(false);

        int count = reward.Rolls != null ? reward.Rolls.Length : 0;
        bool roll = count > 0;
        // 굴리는 동안은 주사위 줄만, 모인 뒤에 "40 × 15 = +600".
        ShowFormula(false);
        _rewardDiceRow.gameObject.SetActive(roll);
        // 굴리지 않으면 "+0"만 남으므로 위 줄의 숫자들처럼 오른쪽에 붙인다.
        _rewardText.gameObject.SetActive(true);
        _rewardText.horizontalAlignment = roll ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
        if (!roll)
        {
            _revealing = false;
            _rewardText.text = "+0";
            SetButtons(true);
            return;
        }
        _revealing = true;
        SetButtons(false);

        for (int i = 0; i < count; i++)
        {
            RectTransform slot = new GameObject("DieSlot", typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(_rewardDiceRow, false);
            UI_MetaDie die = Instantiate(_rewardDie, slot, false);
            die.gameObject.SetActive(true);
            _slots.Add(slot);
            _dice.Add(die);
        }
        // 칸 크기가 정해진 뒤에 굴린다(주사위 크기는 칸을 따른다).
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardDiceRow);
        for (int i = 0; i < count; i++)
        {
            _dice[i].Roll(diceConfig, reward.Rolls[i], true, diceConfig.RewardDelay + i * diceConfig.Stagger, OnDieLanded);
            // 굴리기 전 기다리는 동안 빈 주사위로 둔다(결과 눈이 미리 보이지 않게).
            _dice[i].HidePips();
        }
    }

    private void OnDieLanded()
    {
        _landed++;
        if (_landed < _dice.Count) return;
        _gatherStart = Time.unscaledTime + _diceConfig.GatherDelay;
        // 칸은 FlexLayout이 놓은 자리에서 합 숫자 쪽으로 움직인다 — 레이아웃이 다 끝난 지금 자리를 출발점으로 기억한다.
        _slotStarts.Clear();
        for (int i = 0; i < _slots.Count; i++) _slotStarts.Add(_slots[i].localPosition);
    }

    private void ShowFormula(bool visible)
    {
        _rewardWaveText.gameObject.SetActive(visible);
        _rewardSumText.gameObject.SetActive(visible);
    }

    private void ClearDice()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null) Destroy(_slots[i].gameObject);
        _slots.Clear();
        _dice.Clear();
        _slotStarts.Clear();
    }

    private void Update()
    {
        if (!_revealing) return;
        // 화면 어디든 누르면 건너뛴다(꺼 둔 버튼 위를 눌러도).
        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            for (int i = 0; i < _dice.Count; i++) _dice[i].Finish();
            EndReveal();
            return;
        }

        if (_gatherStart >= 0f && _countStart < 0f)
        {
            float g = Mathf.Clamp01((Time.unscaledTime - _gatherStart) / _diceConfig.GatherDuration);
            if (g <= 0f) return;
            // 실행 중 움직임이라 자리·크기를 직접 옮긴다(쉬는 자리는 FlexLayout 칸 그대로).
            Vector3 target = _rewardDiceRow.InverseTransformPoint(_rewardSumText.rectTransform.position);
            float eased = g * g;
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].localPosition = Vector3.Lerp(_slotStarts[i], target, eased);
                _slots[i].localScale = Vector3.one * (1f - eased);
            }
            if (g < 1f) return;
            _rewardDiceRow.gameObject.SetActive(false);
            ShowFormula(true);
            _countStart = Time.unscaledTime;
        }

        if (_countStart < 0f) return;
        float t = Mathf.Clamp01((Time.unscaledTime - _countStart) / _diceConfig.CountUpDuration);
        _rewardText.text = $"= +{Mathf.RoundToInt(_amount * t)}";
        if (t >= 1f) EndReveal();
    }

    private void EndReveal()
    {
        _revealing = false;
        _rewardDiceRow.gameObject.SetActive(false);
        ShowFormula(true);
        _rewardText.text = $"= +{_amount}";
        SetButtons(true);
    }

    private void SetButtons(bool interactable)
    {
        _retryButton.interactable = interactable;
        _lobbyButton.interactable = interactable;
    }

    private void OnRetry() => Choose(_retry);

    private void OnLobby() => Choose(_lobby);

    // 닫히는 연출 동안 다시 눌러도, 두 버튼을 연달아 눌러도 한 번만.
    private void Choose(Action action)
    {
        _retry = null;
        _lobby = null;
        action?.Invoke();
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        _retry = null;
        _lobby = null;
        _diceConfig = null;
        _revealing = false;
        ClearDice();
        _retryButton.onClick.RemoveListener(OnRetry);
        _lobbyButton.onClick.RemoveListener(OnLobby);
    }
}
