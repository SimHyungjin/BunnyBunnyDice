using TMPro;
using UnityEngine;

/// <summary>
/// 프리팹에 적어 둔 글(TMP)을 지금 언어로 바꿔 보인다. 프리팹의 글이 곧 원문(키)이라 키를 따로 적지 않는다.
/// 번역표 갱신 툴은 이 컴포넌트가 붙은 글만 모은다 — 코드가 채우는 글(숫자·카드 내용)에는 붙이지 않는다.
/// (붙이면 코드가 채운 글을 언어가 바뀔 때 원문으로 되돌린다)
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class UI_LocalizedText : MonoBehaviour
{
    private TMP_Text _text;
    private string _source;

    /// <summary>원문(프리팹에 적힌 글).</summary>
    public string Source
    {
        get
        {
            Cache();
            return _source;
        }
    }

    private void Cache()
    {
        if (_text != null) return;
        _text = GetComponent<TMP_Text>();
        _source = _text.text;
    }

    private void OnEnable()
    {
        Cache();
        Apply();
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged += Apply;
    }

    private void OnDisable()
    {
        LocalizationManager localization = Core.Localization;
        if (localization != null) localization.OnLanguageChanged -= Apply;
    }

    private void Apply() => _text.text = App.Localize(_source);
}
