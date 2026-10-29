using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택 창의 캐릭터 한 칸(기획서 12.3, 시안 SampleImages/제목 없음7.png). 모든 칸이 사진 → 이름 → 띠의 같은 모양이고 띠 내용만 다르다.
/// 열린 캐릭터: 얼굴 사진 + 이름 + 띠에 특성 이름(캐릭터의 정체는 특성이라 고르는 기준이 된다, 12.2).
/// 잠긴 캐릭터: 같은 사진을 검게 칠한 실루엣 + 이름 + 띠에 해금 조건과 진행도(눌러야 보이게 하지 않고 늘 보인다).
/// 조건을 채운 캐릭터: 실루엣 그대로 띠에 "눌러서 열기"(노랑) — 누르면 실루엣이 걷히고(Reveal) 창이 그 캐릭터를 연다.
/// 지금 캐릭터는 바깥 테두리·이름·띠 테두리·띠 글을 모두 노랑으로 칠해 강조한다(체크 표시 같은 덧붙인 기호는 칸 모양과 어울리지 않아 두지 않는다).
/// 나머지는 이름 청록, 띠 테두리 자홍, 띠 글 흰색이다.
/// 색은 프리팹 데이터라 여기 필드로 둔다.
/// </summary>
public sealed class UI_CharacterSelectCell : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Graphic _frame;
    [SerializeField] private RawImage _portrait;
    [SerializeField] private TMP_Text _nameText;
    [Tooltip("아래 띠의 글(열린 캐릭터는 특성 이름, 잠긴 캐릭터는 해금 조건).")]
    [SerializeField] private TMP_Text _bandText;
    [Tooltip("아래 띠의 테두리.")]
    [SerializeField] private Graphic _bandEdge;

    [Header("색")]
    [Tooltip("바깥 테두리·띠 테두리.")]
    [SerializeField] private Color _frameColor = new Color(0.961f, 0.02f, 0.424f);
    [SerializeField] private Color _nameColor = new Color(0.247f, 0.878f, 1f);
    [SerializeField] private Color _bandTextColor = new Color(0.984f, 0.933f, 0.988f);
    [Tooltip("지금 캐릭터 강조색(바깥 테두리·이름·띠 테두리·띠 글).")]
    [SerializeField] private Color _selectedColor = new Color(1f, 0.88f, 0.29f);
    [SerializeField] private Color _silhouetteColor = Color.black;
    [Tooltip("조건을 채워 열 수 있는 캐릭터의 띠 글.")]
    [SerializeField] private Color _readyColor = new Color(1f, 0.88f, 0.29f);

    /// <summary>이 칸을 눌렀다.</summary>
    public event Action Clicked;

    /// <summary>이름 글. 창이 칸끼리 글 크기를 맞출 때 쓴다.</summary>
    public TMP_Text NameText => _nameText;

    /// <summary>띠 글. 창이 칸끼리 글 크기를 맞출 때 쓴다.</summary>
    public TMP_Text BandText => _bandText;

    private float _focusY = 0.5f;
    private float _focusX = 0.5f;
    private float _zoom = 1f;
    // 실루엣 걷기(Reveal). 시작 < 0 = 쉬는 중.
    private float _revealStart = -1f;
    private SO_MetaDiceConfig _revealConfig;
    private Action _revealed;

    private void Awake() => _button.onClick.AddListener(() => Clicked?.Invoke());

    /// <summary>실루엣이 걷히며 색이 드러난다. 끝나면(또는 칸이 꺼지면) done을 부른다.</summary>
    public void Reveal(SO_MetaDiceConfig config, Action done)
    {
        _revealConfig = config;
        _revealed = done;
        _revealStart = Time.unscaledTime;
    }

    private void Update()
    {
        if (_revealStart < 0f) return;
        float t = Mathf.Clamp01((Time.unscaledTime - _revealStart) / _revealConfig.RevealDuration);
        _portrait.color = Color.Lerp(_silhouetteColor, Color.white, t);
        transform.localScale = Vector3.one * (1f + (_revealConfig.RevealPopScale - 1f) * Mathf.Sin(t * Mathf.PI));
        if (t >= 1f) EndReveal();
    }

    // 창이 바깥 터치로 닫혀도 열기는 끝낸다(누른 순간 연 것으로 본다).
    private void OnDisable() => EndReveal();

    private void EndReveal()
    {
        if (_revealStart < 0f) return;
        _revealStart = -1f;
        transform.localScale = Vector3.one;
        Action done = _revealed;
        _revealed = null;
        done?.Invoke();
    }

    // 칸 크기가 바뀌면(화면 회전·열 수 변경) 자르는 범위도 다시 맞춘다.
    private void OnRectTransformDimensionsChange() => FitPortrait();

    // 그림을 늘리지 않고 칸 사진 자리를 꽉 채우게 자른 뒤(cover) zoom만큼 확대·축소한다. 남기는 자리는 focusX·focusY.
    // 그림은 컷인까지 쓰려고 세로로 길게(2:3) 그려서, 칸에서는 얼굴·손 둘레만 남긴다.
    private void FitPortrait()
    {
        if (_portrait == null || _portrait.texture == null) return;
        Rect rect = _portrait.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        float rectAspect = rect.width / rect.height;
        float textureAspect = _portrait.texture.width / (float)_portrait.texture.height;
        float w = textureAspect > rectAspect ? rectAspect / textureAspect : 1f;
        float h = textureAspect > rectAspect ? 1f : textureAspect / rectAspect;
        w /= _zoom;
        h /= _zoom;
        // 확대(범위가 그림 안)면 그림 밖으로 나가지 않게 막고, 축소(범위가 그림보다 큼)면 기준점이 칸의 같은 자리에 오게 둔다.
        float x = w <= 1f ? Mathf.Clamp(_focusX - w * 0.5f, 0f, 1f - w) : (1f - w) * _focusX;
        float y = h <= 1f ? Mathf.Clamp(_focusY - h * 0.5f, 0f, 1f - h) : (1f - h) * _focusY;
        _portrait.uvRect = new Rect(x, y, w, h);
    }

    /// <param name="unlockable">잠겼지만 조건을 채워 누르면 열린다.</param>
    /// <param name="bandText">열린 캐릭터는 특성 이름, 잠긴 캐릭터는 해금 조건 글(진행도 포함), 열 수 있는 캐릭터는 "눌러서 열기".</param>
    /// <param name="focusX">그림을 자를 때 남길 가로 자리(0 = 왼쪽 · 1 = 오른쪽).</param>
    /// <param name="focusY">그림을 칸 비율로 자를 때 남길 세로 자리(0 = 아래 · 1 = 위).</param>
    /// <param name="zoom">칸을 꽉 채운 크기에 곱하는 배율(1 = 꽉 채움, 1보다 작으면 축소).</param>
    public void Show(string displayName, Texture portrait, float focusX, float focusY, float zoom, bool unlocked, bool unlockable, bool selected,
        string bandText)
    {
        _nameText.text = displayName;
        _nameText.color = selected ? _selectedColor : _nameColor;
        _portrait.texture = portrait;
        _portrait.enabled = portrait != null;
        // 걷히는 중이면 색은 Update가 정한다(언어가 바뀌어 다시 그려도 끊기지 않게).
        if (_revealStart < 0f) _portrait.color = unlocked ? Color.white : _silhouetteColor;
        _focusX = focusX;
        _focusY = focusY;
        _zoom = Mathf.Max(zoom, 0.1f);
        FitPortrait();

        _frame.color = selected ? _selectedColor : _frameColor;
        _bandEdge.color = selected ? _selectedColor : _frameColor;
        _bandText.text = bandText;
        _bandText.color = selected ? _selectedColor : unlockable ? _readyColor : _bandTextColor;

        // 아직 열 수 없는 칸은 눌러도 아무 일이 없으므로 눌리는 반응도 없앤다.
        _button.interactable = unlocked || unlockable;
    }
}
