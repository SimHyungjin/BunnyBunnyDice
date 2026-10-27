using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP 글자를 띄어쓰기로 나뉜 낱말 순서대로 조금씩 내린다(계단진 로고·제목). 글자의 꼭짓점을 옮기므로 같은 메시로 그려지는
/// 외곽선·그림자(머티리얼의 Outline·Underlay)도 함께 내려간다.
/// 글 안에 서식 태그(voffset)를 넣지 않는 이유: 원문이 번역 키라 태그가 번역표까지 따라가고, 언어마다 낱말 수가 달라 따로 맞춰야 한다.
/// 이 방식은 어떤 언어든 낱말 수에 맞춰 계단이 생긴다.
/// 내리는 양은 글자 크기 비율이라 자동 크기(Auto Size)로 글자가 줄어도 같은 모양이다. 글자 계단은 편집 중에도 보인다.
///
/// 낱말 판(선택): 판 견본(_plateTemplate)을 주면 실행 중 낱말마다 판을 깔아 판도 낱말과 함께 계단진다.
/// - 견본의 자식 하나하나가 층이다(예: 그림자 · 테두리 · 본체). 층마다 모든 낱말의 판을 한꺼번에 그린다 — 낱말마다 판을 통째로 그리면
///   겹친 곳에 옆 판의 테두리가 드러나지만, 층별로 그리면 겹친 곳은 본체가 테두리를 덮어 계단진 한 덩어리 띠가 된다.
///   그래서 이웃 판이 겹치도록 여백(_platePadding)을 낱말 사이 공백보다 넉넉히 준다.
/// - 견본은 이 글자와 같은 부모 아래, 글자보다 앞(뒤에 그려지게) 둔다. 편집 중에는 견본이 띠 전체로 보여 모양을 미리 본다.
/// - 판 크기는 글자 길이로 정해지는 값이라 코드가 계산한다(낱말 범위 + 여백 — 여백은 글자 크기 비율). 층 자식의 앵커는 판 안에서 그대로 쓴다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class UI_Utility_TextWordStep : MonoBehaviour
{
    [Tooltip("낱말 하나마다 내리는 양(글자 크기 비율). 음수면 올라간다.")]
    [SerializeField] private float _stepRatio = 0.1f;
    [Tooltip("낱말 판 견본(선택). 자식 하나가 층 하나(그림자·테두리·본체 순). 이 글자와 같은 부모 아래, 글자보다 앞 순서에 둔다.")]
    [SerializeField] private RectTransform _plateTemplate;
    [Tooltip("낱말 판의 여백(글자 크기 비율, x = 좌우 한쪽, y = 위아래 한쪽). 이웃 판이 겹쳐야 한 덩어리로 보인다.")]
    [SerializeField] private Vector2 _platePadding = new Vector2(0.25f, 0.12f);
    [Tooltip("이웃 판과 맞닿는 안쪽 끝이 이웃 낱말 안으로 뻗는 양(글자 크기 비율). 판 끝이 비스듬하거나 테두리가 두꺼워도 본체끼리 넉넉히 겹쳐 이음매가 보이지 않게.")]
    [SerializeField] private float _plateJoin = 0.6f;

    // 층마다: 층을 담는 상자(부모 전체에 펼침)와 낱말별 판(판 크기 상자 + 그 안의 층 복사본).
    private readonly List<RectTransform> _layers = new List<RectTransform>();
    private readonly List<List<RectTransform>> _plates = new List<List<RectTransform>>();
    // 낱말 범위(글자 로컬, 계단 반영). 판은 LateUpdate에서 놓는다 — 범위를 재는 때는 캔버스가 다시 그리는 도중이라
    // 그때 오브젝트를 만들거나 순서를 바꾸면 재구성과 부딪힌다.
    private readonly List<Vector2> _wordMin = new List<Vector2>();
    private readonly List<Vector2> _wordMax = new List<Vector2>();
    private bool _platesDirty;
    private TMP_Text _text;

    private void OnEnable()
    {
        _text = GetComponent<TMP_Text>();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        _text.ForceMeshUpdate();
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        // 원래 자리로 되돌린다(메시를 다시 만든다).
        if (_text != null) _text.ForceMeshUpdate();
        for (int i = 0; i < _layers.Count; i++)
            if (_layers[i] != null) _layers[i].gameObject.SetActive(false);
        if (_plateTemplate != null) _plateTemplate.gameObject.SetActive(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_text != null && isActiveAndEnabled) _text.ForceMeshUpdate();
    }
#endif

    // 화면 크기가 바뀌면 글자는 다시 배치되지만(TEXT_CHANGED) 판 위치 계산에 쓰는 부모 크기도 함께 바뀌므로 다시 맞춘다.
    private void OnRectTransformDimensionsChange()
    {
        if (_text != null && isActiveAndEnabled) _text.ForceMeshUpdate();
    }

    // 글이 바뀌거나 다시 배치되면(크기·언어) TMP가 메시를 새로 만든 뒤 부른다. 그 위에 낱말별로 옮긴다.
    private void OnTextChanged(Object changed)
    {
        if (changed != _text) return;
        Apply();
    }

    private void Apply()
    {
        TMP_TextInfo info = _text.textInfo;
        float step = _text.fontSize * _stepRatio;
        _wordMin.Clear();
        _wordMax.Clear();

        int word = 0;
        bool inWord = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (char.IsWhiteSpace(ch.character))
            {
                // 낱말이 끝난 뒤의 첫 공백에서만 센다(공백이 여러 개여도 한 칸).
                if (inWord)
                {
                    _wordMin.Add(min);
                    _wordMax.Add(max);
                    word++;
                }
                inWord = false;
                continue;
            }

            Vector3 offset = Vector3.down * (step * word);
            // 낱말 범위는 글자 칸(줄 높이 기준)으로 잡는다 — 획 모양 경계로 잡으면 낱말마다 판 높이가 들쭉날쭉하다.
            Vector2 low = new Vector2(ch.origin, ch.descender) + (Vector2)offset;
            Vector2 high = new Vector2(ch.xAdvance, ch.ascender) + (Vector2)offset;
            if (!inWord)
            {
                min = low;
                max = high;
            }
            else
            {
                min = Vector2.Min(min, low);
                max = Vector2.Max(max, high);
            }
            inWord = true;

            if (!ch.isVisible || word == 0) continue;
            Vector3[] vertices = info.meshInfo[ch.materialReferenceIndex].vertices;
            for (int k = 0; k < 4; k++) vertices[ch.vertexIndex + k] += offset;
        }
        if (inWord)
        {
            _wordMin.Add(min);
            _wordMax.Add(max);
        }
        // 판을 만드는 건 실행 중에만 — 편집 중에 만들면 생성물이 프리팹에 저장된다.
        if (_plateTemplate != null && Application.isPlaying) _platesDirty = true;

        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private void LateUpdate()
    {
        if (!_platesDirty || _plateTemplate == null) return;
        _platesDirty = false;
        EnsureLayers();
        _plateTemplate.gameObject.SetActive(false);

        RectTransform parent = (RectTransform)_plateTemplate.parent;
        Rect area = parent.rect;
        if (area.width <= 0f || area.height <= 0f) return;
        Vector2 padding = _platePadding * _text.fontSize;
        for (int layer = 0; layer < _layers.Count; layer++)
        {
            // 꺼졌다 다시 켜진 경우(풀에 들어갔다 나온 화면) 층 상자가 꺼져 있다(OnDisable).
            _layers[layer].gameObject.SetActive(true);
            List<RectTransform> plates = _plates[layer];
            for (int i = 0; i < _wordMin.Count; i++)
            {
                if (plates.Count <= i) plates.Add(CreatePlate(layer, i));
                RectTransform plate = plates[i];
                plate.gameObject.SetActive(true);
                // 계산은 글자 길이로 하되 결과는 부모 대비 앵커 비율로 넣는다(offset 0, UI 배치 규칙) — 인스펙터에서 "띠의 몇 %"로 읽히고,
                // 부모 크기만 바뀔 때는 다시 계산하지 않아도 따라간다.
                // 바깥 끝은 여백만큼, 이웃과 맞닿는 안쪽 끝은 이웃 낱말 안으로 _plateJoin만큼 뻗는다.
                float join = _plateJoin * _text.fontSize;
                float left = i == 0 ? _wordMin[i].x - padding.x : _wordMax[i - 1].x - join;
                float right = i == _wordMin.Count - 1 ? _wordMax[i].x + padding.x : _wordMin[i + 1].x + join;
                Vector2 low = ToParent(parent, new Vector2(left, _wordMin[i].y - padding.y));
                Vector2 high = ToParent(parent, new Vector2(right, _wordMax[i].y + padding.y));
                plate.anchorMin = new Vector2((low.x - area.xMin) / area.width, (low.y - area.yMin) / area.height);
                plate.anchorMax = new Vector2((high.x - area.xMin) / area.width, (high.y - area.yMin) / area.height);
                plate.offsetMin = Vector2.zero;
                plate.offsetMax = Vector2.zero;
            }
            for (int i = _wordMin.Count; i < plates.Count; i++) plates[i].gameObject.SetActive(false);
        }
    }

    // 글자 로컬 점 → 판 부모 로컬 점(글자와 판 부모는 같은 회전이라 사각형이 그대로 옮겨진다).
    private Vector2 ToParent(RectTransform parent, Vector2 textLocal) =>
        parent.InverseTransformPoint(_text.rectTransform.TransformPoint(textLocal));

    // 견본의 자식마다 층 상자를 하나씩, 견본 바로 뒤 순서에 만든다(그 순서대로 그려진다).
    private void EnsureLayers()
    {
        if (_layers.Count > 0) return;
        RectTransform parent = (RectTransform)_plateTemplate.parent;
        int sibling = _plateTemplate.GetSiblingIndex() + 1;
        for (int i = 0; i < _plateTemplate.childCount; i++)
        {
            RectTransform layer = new GameObject(_plateTemplate.name + "_" + _plateTemplate.GetChild(i).name, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            layer.SetSiblingIndex(sibling + i);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            _layers.Add(layer);
            _plates.Add(new List<RectTransform>());
        }
    }

    // 판 = 낱말 자리 상자(앵커는 LateUpdate가 정한다) + 그 안에 견본 층 자식의 복사본(견본에서의 앵커 그대로).
    private RectTransform CreatePlate(int layer, int index)
    {
        RectTransform plate = new GameObject("Plate_" + index, typeof(RectTransform)).GetComponent<RectTransform>();
        plate.SetParent(_layers[layer], false);
        Instantiate(_plateTemplate.GetChild(layer), plate, false);
        return plate;
    }
}
