using System;
using UnityEngine;

/// <summary>
/// 팁이 비출 대상(TutorialTarget)의 화면 위 영역(화면 픽셀, 왼쪽 아래 원점)을 잰다.
/// 손패 부채꼴·영웅·주사위가 움직이므로 표시가 매 프레임 다시 묻는다. 대상이 없으면(카드를 다 썼다 등) false.
/// 판 위 물체는 칸·몸의 꼭짓점 몇 개를 화면으로 옮겨 감싸는 사각형으로 잰다 — 카메라가 기울어 있어 한 점 + 반지름으로는 모양이 어긋난다.
/// </summary>
public sealed class TutorialTargets
{
    private readonly CardHand _hand;
    private readonly UI_GameHud _hud;
    private readonly Hero _hero;
    private readonly DiceField _diceField;
    private readonly Transform _mapRoot;
    private readonly Func<MapData> _getMap;
    private readonly Func<Camera> _getCamera;
    private readonly Func<UI_LevelUpPopup> _getOffer;
    private readonly SO_TutorialConfig _config;
    private readonly Vector3[] _corners = new Vector3[4];

    /// <summary>마지막으로 놓거나 얹은 주사위 칸. 그 주사위가 없어지면 판 위 아무 주사위를 비춘다.</summary>
    public Vector2Int? LastDice { get; set; }

    public TutorialTargets(SO_TutorialConfig config, CardHand hand, UI_GameHud hud, Hero hero, DiceField diceField, Transform mapRoot,
        Func<MapData> getMap, Func<Camera> getCamera, Func<UI_LevelUpPopup> getOffer)
    {
        _config = config;
        _hand = hand;
        _hud = hud;
        _hero = hero;
        _diceField = diceField;
        _mapRoot = mapRoot;
        _getMap = getMap;
        _getCamera = getCamera;
        _getOffer = getOffer;
    }

    /// <summary>대상이 지금 화면에 있는지(팁을 띄울 수 있는지).</summary>
    public bool Exists(TutorialTarget target) => target == TutorialTarget.None || TryGetRect(target, out _);

    public bool TryGetRect(TutorialTarget target, out Rect rect)
    {
        rect = default;
        switch (target)
        {
            case TutorialTarget.DiceCard: return TryGetCard(CardKind.Dice, out rect);
            case TutorialTarget.OfferChip: return TryGetOffer(CardKind.Chip, out rect);
            case TutorialTarget.OfferSkill: return TryGetOffer(CardKind.Skill, out rect);
            case TutorialTarget.OfferExclusive: return TryGetOffer(CardKind.Exclusive, out rect);
            case TutorialTarget.OfferHero: return TryGetOffer(CardKind.Hero, out rect);
            case TutorialTarget.Hand: return TryGetHand(out rect);
            case TutorialTarget.PlacedDice: return TryGetDice(out rect);
            case TutorialTarget.Hero: return TryGetHero(out rect);
            case TutorialTarget.PlayButton: return TryGetUi(_hud != null ? _hud.PauseButtonRect : null, out rect);
            case TutorialTarget.Board: return TryGetBoard(out rect);
            default: return false;
        }
    }

    private bool TryGetCard(CardKind kind, out Rect rect)
    {
        rect = default;
        if (_hand == null) return false;
        for (int i = 0; i < _hand.Count; i++)
        {
            if (_hand.GetCard(i).Kind != kind) continue;
            return TryGetUi(_hand.GetView(i).Rect, out rect);
        }
        return false;
    }

    // 레벨업 창의 후보 카드(고르기 전에 알려 주려고 — 고른 뒤 손패에서 알려 주면 이미 모르고 고른 뒤다).
    private bool TryGetOffer(CardKind kind, out Rect rect)
    {
        rect = default;
        UI_LevelUpPopup popup = _getOffer();
        if (popup == null) return false;
        for (int i = 0; i < popup.CardCount; i++)
        {
            CardView view = popup.GetCard(i);
            if (view == null || view.Card.Kind != kind) continue;
            return TryGetUi(view.Rect, out rect);
        }
        return false;
    }

    // 손패의 카드 전부를 감싼다(부채꼴이라 기준선 영역이 아니라 카드에서 잰다).
    private bool TryGetHand(out Rect rect)
    {
        rect = default;
        if (_hand == null || _hand.Count == 0) return false;
        bool any = false;
        for (int i = 0; i < _hand.Count; i++)
        {
            if (!TryGetUi(_hand.GetView(i).Rect, out Rect card)) continue;
            rect = any ? Union(rect, card) : card;
            any = true;
        }
        return any;
    }

    private bool TryGetUi(RectTransform target, out Rect rect)
    {
        rect = default;
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera cam = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        target.GetWorldCorners(_corners);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, _corners[i]);
            rect = i == 0 ? new Rect(screen, Vector2.zero) : Grow(rect, screen);
        }
        return true;
    }

    /// <summary>PlacedDice가 비추는 주사위 칸.</summary>
    public bool TryGetDiceCell(out Vector2Int cell)
    {
        cell = default;
        if (_diceField == null) return false;
        if (LastDice.HasValue && _diceField.TryGetPlaced(LastDice.Value, out _)) cell = LastDice.Value;
        else if (_diceField.Placed.Count > 0) cell = _diceField.Placed[0].Cell;
        else return false;
        return true;
    }

    private bool TryGetDice(out Rect rect)
    {
        rect = default;
        if (!TryGetDiceCell(out Vector2Int cell)) return false;

        // 칸 바닥 네 모서리 + 주사위 윗면 가운데를 감싼다.
        float top = _diceField.TryGetSurface(cell, out float surface) ? surface : 0f;
        bool any = false;
        for (int i = 0; i < 4; i++)
        {
            float x = cell.x + ((i & 1) == 0 ? -0.5f : 0.5f);
            float z = cell.y + ((i & 2) == 0 ? -0.5f : 0.5f);
            if (!TryProject(_mapRoot.TransformPoint(new Vector3(x, 0f, z)), ref rect, ref any)) return false;
        }
        return TryProject(_mapRoot.TransformPoint(new Vector3(cell.x, top, cell.y)), ref rect, ref any);
    }

    // 발과 머리를 잇는 세로 띠. 너비는 키에 비례(SO_TutorialConfig.HeroHoleAspect).
    private bool TryGetHero(out Rect rect)
    {
        rect = default;
        if (_hero == null) return false;
        Camera cam = _getCamera();
        if (cam == null) return false;
        Vector3 foot = _hero.ViewFoot;
        Vector3 head = foot + Vector3.up * _hero.ViewHeight;
        Vector3 footScreen = cam.WorldToScreenPoint(foot);
        Vector3 headScreen = cam.WorldToScreenPoint(head);
        if (footScreen.z <= 0f || headScreen.z <= 0f) return false;
        float height = Mathf.Abs(headScreen.y - footScreen.y);
        float width = height * _config.HeroHoleAspect;
        Vector2 center = (footScreen + headScreen) * 0.5f;
        rect = new Rect(center.x - width * 0.5f, Mathf.Min(footScreen.y, headScreen.y), width, height);
        return true;
    }

    // 칸이 깔린 영역(바깥 칸의 바깥 모서리까지).
    private bool TryGetBoard(out Rect rect)
    {
        rect = default;
        MapData map = _getMap();
        if (map == null) return false;
        bool any = false;
        for (int i = 0; i < 4; i++)
        {
            float x = (i & 1) == 0 ? -0.5f : map.Width - 0.5f;
            float z = (i & 2) == 0 ? -0.5f : map.Height - 0.5f;
            if (!TryProject(_mapRoot.TransformPoint(new Vector3(x, 0f, z)), ref rect, ref any)) return false;
        }
        return true;
    }

    private bool TryProject(Vector3 world, ref Rect rect, ref bool any)
    {
        Camera cam = _getCamera();
        if (cam == null) return false;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) return false;
        rect = any ? Grow(rect, screen) : new Rect(screen, Vector2.zero);
        any = true;
        return true;
    }

    private static Rect Grow(Rect rect, Vector2 point) => Rect.MinMaxRect(Mathf.Min(rect.xMin, point.x), Mathf.Min(rect.yMin, point.y),
        Mathf.Max(rect.xMax, point.x), Mathf.Max(rect.yMax, point.y));

    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
}
