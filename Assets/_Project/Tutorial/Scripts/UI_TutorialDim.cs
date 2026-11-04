using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼의 어두운 덮개: 화면 전체를 덮고 구멍(둘까지)만 뚫는다. 구멍 모양(둥근 사각형·흐린 가장자리)은 셰이더가 그린다 —
/// 메시로 구멍을 파면 두 구멍이 겹칠 때 모양 계산이 복잡해진다.
///
/// 누름: 구멍을 통과시키면(PassThrough) 구멍 안 누름은 아래 UI(손패 카드·▶ 버튼·맵 터치 영역)로 가고, 구멍 밖은 덮개가 받는다.
/// 덮개가 받은 누름은 Clicked로 알린다(장 넘기기).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_TutorialDim : MaskableGraphic, ICanvasRaycastFilter, IPointerClickHandler
{
    [Tooltip("구멍을 그리는 UI 셰이더(Def/UI/TutorialDim).")]
    [SerializeField] private Shader _shader;

    private static readonly int HoleAId = Shader.PropertyToID("_HoleA");
    private static readonly int HoleBId = Shader.PropertyToID("_HoleB");
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int CornerId = Shader.PropertyToID("_Corner");
    private static readonly int SoftId = Shader.PropertyToID("_Soft");

    private Material _material;
    // 구멍: 이 영역의 로컬 좌표(왼쪽 아래 원점, 캔버스 단위) — 가운데 xy + 절반 크기 zw. 크기 0이면 없음.
    private Vector4 _holeA;
    private Vector4 _holeB;
    // 그리지 않고 누름만 통과시키는 영역(같은 좌표). 크기 0이면 없음.
    private Vector4 _inputArea;

    /// <summary>참이면 구멍 안 누름이 아래로 지나간다.</summary>
    public bool PassThrough { get; set; }

    /// <summary>덮개(구멍 밖, 또는 통과시키지 않을 때 어디든)를 눌렀다.</summary>
    public event Action Clicked;

    public override Material material
    {
        get
        {
            if (_material == null && _shader != null) _material = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material != null ? _material : base.material;
        }
        set => base.material = value;
    }

    /// <summary>구멍 둘과 모양을 정한다. 구멍은 이 영역의 로컬 좌표(왼쪽 아래 원점) 사각형, 없으면 null.</summary>
    /// <summary>비추지 않고 누름만 통과시키는 영역(PassThrough일 때만). null이면 없음.</summary>
    public void SetInputArea(Rect? area) => _inputArea = ToHole(area);

    public void SetHoles(Rect? a, Rect? b, float corner, float softness)
    {
        _holeA = ToHole(a);
        _holeB = ToHole(b);
        Material mat = material;
        if (mat == null) return;
        Vector2 size = rectTransform.rect.size;
        mat.SetVector(HoleAId, _holeA);
        mat.SetVector(HoleBId, _holeB);
        mat.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
        mat.SetFloat(CornerId, corner);
        mat.SetFloat(SoftId, Mathf.Max(softness, 0.001f));
    }

    private static Vector4 ToHole(Rect? rect)
    {
        if (!rect.HasValue) return Vector4.zero;
        Rect r = rect.Value;
        return new Vector4(r.center.x, r.center.y, r.width * 0.5f, r.height * 0.5f);
    }

    // 사각형 하나에 0~1 UV를 깐다 — 셰이더가 UV × 영역 크기로 로컬 좌표를 되살려 구멍을 잰다.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Color32 c = color;
        vh.AddVert(new Vector3(r.xMin, r.yMin), c, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(r.xMin, r.yMax), c, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(r.xMax, r.yMax), c, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(r.xMax, r.yMin), c, new Vector2(1f, 0f));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!PassThrough) return true;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local)) return true;
        Rect r = rectTransform.rect;
        Vector2 p = local - r.min;
        return !Inside(p, _holeA) && !Inside(p, _holeB) && !Inside(p, _inputArea);
    }

    private static bool Inside(Vector2 p, Vector4 hole) =>
        hole.z > 0f && hole.w > 0f && Mathf.Abs(p.x - hole.x) <= hole.z && Mathf.Abs(p.y - hole.y) <= hole.w;

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_material == null) return;
        // 편집 중(프리팹 열기)에도 만들어지므로 그때는 바로 지운다.
        if (Application.isPlaying) Destroy(_material);
        else DestroyImmediate(_material);
    }
}
