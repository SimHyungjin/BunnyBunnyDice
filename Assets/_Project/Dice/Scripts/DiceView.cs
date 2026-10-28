using TMPro;
using UnityEngine;

/// <summary>
/// 놓인 주사위 하나의 표시: 색을 입힌 몸체(면체별 모델) + 윗면의 눈 글자(기획서 9.9 — 눈은 윗면 숫자로만 보인다).
/// 몸체는 DiceBody가 면체별 모델로 만든다(중심 피벗·크기 맞춤 — 배치 고스트와 같은 모양).
/// 놓거나 얹을 때는 굴러 떨어지는 연출을 코드로 돌린다 — 모든 주사위가 같은 동작이라 애니메이션 클립을 따로 두지 않고,
/// 높이·시간·회전 수를 SO로 조정한다. 눈은 판정 때 이미 정해졌지만 멈춘 뒤에 보여 준다(굴려서 나온 수로 보이게).
/// </summary>
public sealed class DiceView
{
    private readonly Transform _parent;
    private readonly string _name;
    private readonly TextMeshPro _pips;
    private readonly Material _outline;
    private GameObject _body;
    // 몸체 중심에서 윗면까지. 몸체를 칸 위에 세우고 눈 글자를 윗면에 놓는 데 쓴다.
    private float _halfHeight;
    private Vector2Int _cell;
    private float _top;
    // 굴리는 중이면 0 이상. 연출은 표시만 바꾸고 판정(공격 등)은 기다리지 않는다.
    private float _rollTime = -1f;
    // 솟아나는 중이면 0 이상(영웅이 놓을 때 — Pop). 시작 크기 비율.
    private float _popTime = -1f;
    private float _popFrom;
    private float _dropHeight;
    private Vector3 _rollAxis;
    // 눈 글자의 원래 자리. 얹기 고스트 중(ShowOverGhost)에는 고스트 위로 옮겼다가 되돌린다.
    private Vector3 _pipPosition;

    public bool IsRolling => _rollTime >= 0f || _popTime >= 0f;

    /// <summary>쉬는 자리의 몸체 윗면 높이(맵 로컬, 굴리는 연출과 무관). 영웅이 밟고 서는 높이다.</summary>
    public float SurfaceHeight => _top + _halfHeight * 2f;

    /// <param name="outline">툰 아웃라인 머티리얼. 없으면 null.</param>
    public DiceView(Transform parent, SO_DiceConfig config, string name, Material outline)
    {
        _parent = parent;
        _name = name;
        _outline = outline;

        // 몸체의 자식으로 두지 않는다 — 몸체 색 입히기(RendererTint)가 자식 렌더러까지 덮어 글자 렌더러에도 값을 넣게 되고,
        // 굴리는 동안 글자까지 같이 돌게 된다.
        GameObject text = new GameObject(name + "_Pips");
        text.transform.SetParent(parent, false);
        _pips = text.AddComponent<TextMeshPro>();
        _pips.font = config.PipFont;
        if (config.PipMaterial != null) _pips.fontSharedMaterial = config.PipMaterial;
        _pips.alignment = TextAlignmentOptions.Center;
        _pips.textWrappingMode = TextWrappingModes.NoWrap;
        _pips.rectTransform.sizeDelta = new Vector2(1f, 1f);
    }

    /// <summary>
    /// top은 주사위가 선 칸의 윗면 높이(맵 로컬 — 칩 위면 칩 윗면). 몸체는 매번 새로 만든다 — 얹어서 면체가 바뀌거나 모델 설정을 고쳐도 따로 볼 것이 없게
    /// (놓기·얹기·설정 변경 때만 불리므로 비싸지 않다). 굴리는 중이면 자세는 연출이 맡는다.
    /// </summary>
    public void Apply(Vector2Int cell, float top, in DiceState dice, bool awakened, SO_DiceConfig config)
    {
        _cell = cell;
        _top = top;
        SO_DiceConfig.FaceModel model = config.GetModel(dice.Faces);
        if (_body != null) Object.Destroy(_body);
        _body = DiceBody.Build(_parent, model, _name, out _halfHeight, _outline);
        RendererTint.Apply(_body, config.GetColor(dice.Colors));

        _pips.fontSize = config.PipFontSize * model.PipScale;
        _pips.color = awakened ? config.AwakenedPipColor : config.PipColor;
        // 면체는 모델 모양으로 보이므로 숫자만 둔다(기획서 9.9).
        // 눈을 다 태운 주사위는 숫자를 비운다 — 꺼진 것을 몸 색이 아니라 눈으로 보인다(색은 공격 방식이라 그대로 둔다).
        _pips.text = dice.IsSpent ? string.Empty : dice.Pips.ToString();
        _pipPosition = new Vector3(cell.x, top + _halfHeight * 2f + config.PipLift, cell.y);
        _pips.transform.localPosition = _pipPosition;

        if (IsRolling) return;
        _pips.gameObject.SetActive(true);
        _body.transform.localPosition = new Vector3(cell.x, top + _halfHeight, cell.y);
        _body.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 얹기 고스트가 합친 결과 모양을 같은 자리에 보여 주는 동안 몸체만 숨긴다. 지금 눈 숫자는 그대로 보이게 두고
    /// 고스트 윗면(pipHeight, 맵 로컬 높이)으로 올린다 — 몸체가 큰 고스트에 묻히지 않고, 지금 눈에 얼마가 더해질지 보며 고르게.
    /// </summary>
    public void ShowOverGhost(float pipHeight)
    {
        if (_body != null) _body.SetActive(false);
        _pips.transform.localPosition = new Vector3(_cell.x, pipHeight, _cell.y);
    }

    /// <summary>ShowOverGhost를 되돌린다.</summary>
    public void ClearGhost()
    {
        if (_body != null) _body.SetActive(true);
        _pips.transform.localPosition = _pipPosition;
    }

    /// <summary>제자리 위 dropHeight에서 굴러 떨어진다(스킬로 다시 굴릴 때 — 놓기·얹기는 Pop).</summary>
    public void Roll(float dropHeight)
    {
        _rollTime = 0f;
        _dropHeight = dropHeight;
        // 옆으로 넘어지듯 구르게 수평에 가까운 축을 무작위로 고른다.
        Vector2 axis = Random.insideUnitCircle.normalized;
        _rollAxis = new Vector3(axis.x, Random.Range(-0.3f, 0.3f), axis.y).normalized;
        _pips.gameObject.SetActive(false);
    }

    /// <summary>
    /// 제자리 바닥에서 톡 솟아난다(영웅이 발밑에 놓을 때 — 위에서 떨어지면 뛰어오르는 영웅과 겹친다).
    /// fromScale에서 제 크기를 살짝 넘었다 돌아오며 돌고, 멈춘 뒤 눈이 보인다. 새로 놓으면 0, 얹으면 StackPopStart.
    /// </summary>
    public void Pop(float fromScale)
    {
        _rollTime = -1f;
        _popTime = 0f;
        _popFrom = fromScale;
        Vector2 axis = Random.insideUnitCircle.normalized;
        _rollAxis = new Vector3(axis.x, Random.Range(-0.3f, 0.3f), axis.y).normalized;
        _pips.gameObject.SetActive(false);
        // 첫 Tick 전 한 프레임 동안 제 크기로 번쩍 보이지 않게 시작 자세를 바로 놓는다.
        Transform body = _body.transform;
        body.localScale = Vector3.one * fromScale;
        body.localPosition = new Vector3(_cell.x, _top + _halfHeight * fromScale, _cell.y);
    }

    /// <summary>굴리는 연출을 진행한다. 실제 시간을 따른다(멈춤·배속·감속과 상관없이 같은 빠르기 — DiceField).</summary>
    public void Tick(float deltaTime, SO_DiceConfig config)
    {
        if (_popTime >= 0f)
        {
            TickPop(deltaTime, config);
            return;
        }
        if (!IsRolling) return;
        _rollTime += deltaTime;
        float t = Mathf.Clamp01(_rollTime / config.RollDuration);
        float land = config.LandPortion;

        // 떨어질 때는 점점 빨라지고(제곱), 바닥에 닿은 뒤 남은 시간에 작게 한 번 튄다.
        float height;
        if (t < land)
        {
            float fall = t / land;
            height = _dropHeight * (1f - fall * fall);
        }
        else
        {
            float bounce = land < 1f ? (t - land) / (1f - land) : 1f;
            height = config.BounceHeight * 4f * bounce * (1f - bounce);
        }

        // 도는 각도는 끝으로 갈수록 줄어 반듯하게(윗면이 위로) 멈춘다.
        float ease = 1f - (1f - t) * (1f - t);
        float angle = config.RollTurns * 360f * (1f - ease);
        _body.transform.localPosition = new Vector3(_cell.x, _top + _halfHeight + height, _cell.y);
        _body.transform.localRotation = Quaternion.AngleAxis(angle, _rollAxis);

        if (t < 1f) return;
        _rollTime = -1f;
        _body.transform.localRotation = Quaternion.identity;
        _pips.gameObject.SetActive(true);
    }

    private void TickPop(float deltaTime, SO_DiceConfig config)
    {
        _popTime += deltaTime;
        float t = Mathf.Clamp01(_popTime / config.PopDuration);
        // ease-out-back: 빠르게 커져 제 크기를 살짝 넘었다가 돌아온다.
        float c = config.PopOvershoot;
        float u = t - 1f;
        float back = 1f + (c + 1f) * u * u * u + c * u * u;
        float scale = Mathf.LerpUnclamped(_popFrom, 1f, back);

        float ease = 1f - (1f - t) * (1f - t);
        Transform body = _body.transform;
        body.localScale = Vector3.one * Mathf.Max(scale, 0f);
        // 바닥에 붙은 채 커지게 중심 높이도 크기만큼.
        body.localPosition = new Vector3(_cell.x, _top + _halfHeight * Mathf.Max(scale, 0f), _cell.y);
        body.localRotation = Quaternion.AngleAxis(config.PopTurns * 360f * (1f - ease), _rollAxis);

        if (t < 1f) return;
        _popTime = -1f;
        body.localScale = Vector3.one;
        body.localPosition = new Vector3(_cell.x, _top + _halfHeight, _cell.y);
        body.localRotation = Quaternion.identity;
        _pips.gameObject.SetActive(true);
    }

    /// <summary>
    /// 눈 글자를 윗면에 눕히고, 글자 위쪽이 화면 위를 향하게 카메라 쪽으로 돌린다
    /// — 세로 화면에서 카메라가 90° 돌아도(MapCameraFitter) 숫자가 옆으로 눕지 않게.
    /// </summary>
    public void FaceCamera(float yaw)
    {
        _pips.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }

    public void Destroy()
    {
        Object.Destroy(_body);
        Object.Destroy(_pips.gameObject);
    }

    /// <summary>
    /// 표시를 판 치우기(BoardSweep)에 넘긴다 — 몸체만 날아가고 눈 글자는 바로 숨긴다(돌아가는 몸체와 따로 놀지 않게).
    /// 넘긴 뒤에는 이 표시를 쓰지 않는다.
    /// </summary>
    public void DetachTo(BoardSweep sweep)
    {
        GameObject body = _body;
        GameObject pips = _pips.gameObject;
        pips.SetActive(false);
        _body = null;
        sweep.Add(body != null ? body.transform : null, () =>
        {
            if (body != null) Object.Destroy(body);
            Object.Destroy(pips);
        });
    }
}
