using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 발동 컷인 표시(HUD 안 — 팝업처럼 쌓이거나 입력을 막지 않는다). 화면 왼쪽의 비스듬한 판(마스크) 안에 캐릭터 그림,
/// 판 오른쪽 사선을 따라 카드 종류 색 줄 두 개, 판 뒤로 큰 글자(SKILL), 아래에 카드 이름(기획서 12.3).
/// 열림: 무대(판·줄)가 가운데 사선에서 좌우로 펼쳐진다(가로 크기 0 → 1). 그림은 반대로 줄여 제 비율 그대로 드러난다.
/// 빠짐: 다시 가운데 사선으로 접힌다. 연달아 쓰면 앞 것을 끊고 처음부터 — 마지막에 쓴 카드가 보이게.
/// 쉬는 자리는 프리팹 앵커이고, 움직임만 크기로 준다(UI 배치 규칙의 실행 중 움직임 예외).
/// </summary>
public sealed class UI_SkillCutIn : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [Tooltip("펼쳐지고 접히는 무대(판·줄·뒤 글자). 피벗이 판 가운데 사선이다.")]
    [SerializeField] private RectTransform _stage;
    [SerializeField] private RawImage _portrait;
    [SerializeField] private AspectRatioFitter _portraitFitter;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _backText;
    [Tooltip("카드 종류 색을 입히는 줄들.")]
    [SerializeField] private Graphic[] _edges;

    // 새로 띄우면 올라간다 — 앞 연출이 기다리다 깨어나도 자기 차례가 아니면 손대지 않게.
    private int _version;

    private void Awake()
    {
        _group.alpha = 0f;
    }

    /// <param name="portrait">캐릭터 그림. 없으면 그림 칸을 숨긴다.</param>
    /// <param name="kindColor">카드 종류 색(전용·스킬) — 줄과 뒤 글자에 입힌다.</param>
    public void Show(Texture portrait, string title, Color kindColor, SO_SkillCutInConfig config)
    {
        _portrait.texture = portrait;
        _portrait.enabled = portrait != null;
        if (portrait != null && portrait.height > 0) _portraitFitter.aspectRatio = portrait.width / (float)portrait.height;
        // 마스크 안 그림의 재질(스텐실)을 다시 만든다 — 처음 띄울 때 마스크보다 먼저 만들어진 재질이 남아 그림 대신 단색으로 칠해졌다.
        _portrait.SetMaterialDirty();
        _title.text = title;
        for (int i = 0; i < _edges.Length; i++) _edges[i].color = kindColor;
        Color back = kindColor;
        back.a *= config.BackTextAlpha;
        _backText.color = back;
        Play(++_version, config).Forget();
    }

    private async UniTaskVoid Play(int version, SO_SkillCutInConfig config)
    {
        _group.alpha = 1f;
        _title.alpha = 0f;

        // 열림: 가로 크기를 0에서 1로(감속). 그림은 1/크기로 되돌려 찌그러지지 않고 판 안에서 드러나게.
        for (float t = 0f; t < config.InDuration; t += Time.unscaledDeltaTime)
        {
            SetOpen(CoreEase.OutCubic.Evaluate(t / config.InDuration));
            await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken).SuppressCancellationThrow();
            if (this == null || version != _version) return;
        }
        SetOpen(1f);

        for (float t = 0f; t < config.HoldDuration; t += Time.unscaledDeltaTime)
        {
            _title.alpha = Mathf.Clamp01(t / config.TitleFadeDuration);
            await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken).SuppressCancellationThrow();
            if (this == null || version != _version) return;
        }
        _title.alpha = 1f;

        // 빠짐: 열릴 때의 반대로 가운데 사선으로 접힌다(가속). 카드 이름은 되돌려 줄이지 않아 찌그러지므로 접히는 앞 절반에 흐려진다.
        for (float t = 0f; t < config.OutDuration; t += Time.unscaledDeltaTime)
        {
            float u = t / config.OutDuration;
            SetOpen(1f - CoreEase.InCubic.Evaluate(u));
            _title.alpha = Mathf.Clamp01(1f - u * 2f);
            await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken).SuppressCancellationThrow();
            if (this == null || version != _version) return;
        }
        _group.alpha = 0f;
        SetOpen(1f);
    }

    // open = 0(닫힘) ~ 1(다 펼침). 0이면 그림 되돌림이 무한대가 되므로 아주 작은 값에서 멈춘다.
    private void SetOpen(float open)
    {
        float scale = Mathf.Max(open, 0.001f);
        _stage.localScale = new Vector3(scale, 1f, 1f);
        _portrait.rectTransform.localScale = new Vector3(1f / scale, 1f, 1f);
    }
}
