using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 카드 드래그 → 주사위 배치. (이전 프로젝트 DeckHandler의 흐름)
/// 카드를 눌러 끌어 올리면 데드존부터 카드가 사라지고 맵 위에 고스트가 따라오며,
/// 놓을 수 있는 칸에서 떼면 바로 놓이고(CardPlacement), 아니면 카드가 손패로 돌아간다.
/// 주사위는 빈 칸이면 새로 놓이고 주사위가 있는 칸이면 얹힌다(기획서 6.3).
/// 칩 카드는 칸에 놓인다 — 빈 칸이면 새 벽, 칩 없는 주사위 칸이면 그 밑으로 들어간다(ChipField).
/// 스킬 카드는 쓰는 곳에 따라 다르다(기획서 8): 주사위 대상은 주사위가 있는 칸에만 쓰고 고스트는 쓴 뒤의 주사위 모양(페인트면 바뀐 색),
/// 범위는 칸이 아니라 손가락 아래 점을 중심으로 한 원(SkillPreview), 판 전체는 판 위 아무 곳에나 놓으면 쓴다. 모두 한 번의 드래그로 발동한다.
/// 영웅 강화 카드는 레벨업 밖(코인 플립)에서 손패로 들어온 것만 끈다 — 판 전체 스킬처럼 판 위 아무 곳에 놓으면 적용된다.
/// </summary>
public sealed class CardDragController : IDisposable
{
    private readonly CardHand _hand;
    private readonly SO_CardDragConfig _config;
    private readonly MapPicker _picker;
    private readonly PlacementPreview _preview;
    private readonly DiceField _diceField;
    private readonly ChipField _chips;
    private readonly SkillCaster _skills;
    private readonly SkillPreview _skillPreview;
    private readonly CardPlacement _placement;
    private readonly Func<MapData> _getMap;
    private readonly Func<Camera> _getCamera;
    private readonly GameSpeed _speed;

    private int _pointerId;
    private Vector2 _pressPosition;
    private bool _placing;
    private bool _hasCell;
    // 맵 밖 칸으로 시작해야 첫 칸에서 반드시 판정한다((0,0)이면 이전 결과를 재사용할 수 있다).
    private Vector2Int _cell = InvalidCell;
    private PlacementResult _cellResult;
    // 놓으면 될 주사위(고스트 모양). 얹기면 합친 결과다.
    private DiceState _ghostDice;
    // 얹기 고스트를 보이는 동안 몸체를 숨겨 둔 원래 주사위의 칸.
    private Vector2Int _ghostCell = InvalidCell;
    // 범위·판 전체 스킬은 칸이 아니라 손가락 아래 바닥 점을 쓴다.
    private bool _hasPoint;
    private Vector2 _point;

    private static readonly Vector2Int InvalidCell = new Vector2Int(-1, -1);

    /// <summary>
    /// 카드를 들고 있다. 그동안 맵 탭(HeroTapInput)을 받지 않는다 — 카드를 끄는 손가락 말고 다른 손가락이 맵에 닿아도
    /// 영웅 목적지가 바뀌지 않게(카드를 놓을 칸을 고르는 중이다).
    /// </summary>
    public bool Busy => _hand.Held != null;

    /// <summary>켜져 있으면 카드를 집을 수 없다. 판 주사위 연출 동안 잠근다 — 아직 떨어지지 않은 칩 칸에 놓는 일이 없게.</summary>
    public bool Locked { get; set; }

    /// <summary>카드가 데드존을 넘어 맵 위에 놓을 자리를 고르기 시작(true)하거나 끝냈을(false) 때. 칸 선 표시 등이 듣는다.</summary>
    public event Action<bool> PlacingChanged;

    public CardDragController(CardHand hand, SO_CardDragConfig config, MapPicker picker, PlacementPreview preview,
        DiceField diceField, ChipField chips, SkillCaster skills, SkillPreview skillPreview, CardPlacement placement,
        GameSpeed speed, Func<MapData> getMap, Func<Camera> getCamera)
    {
        _hand = hand;
        _config = config;
        _picker = picker;
        _preview = preview;
        _diceField = diceField;
        _chips = chips;
        _skills = skills;
        _skillPreview = skillPreview;
        _placement = placement;
        _getMap = getMap;
        _getCamera = getCamera;
        _speed = speed;

        _hand.CardPressed += OnPressed;
        _hand.CardDragged += OnDragged;
        _hand.CardReleased += OnReleased;
    }

    public void Dispose()
    {
        _hand.CardPressed -= OnPressed;
        _hand.CardDragged -= OnDragged;
        _hand.CardReleased -= OnReleased;
        _speed.Release(this);
        SetGhostCell(InvalidCell, float.NaN);
    }

    /// <summary>
    /// 끄는 중이면 놓지 않고 손패로 되돌린다(새 판 등 판이 바뀔 때). 끄는 동안의 감속도 같이 푼다
    /// — 손패를 그냥 비우면 손을 떼도 이벤트가 오지 않아 감속이 남는다.
    /// </summary>
    public void Cancel()
    {
        if (_hand.Held == null) return;
        HidePreview();
        SetPlacing(false);
        _speed.Release(this);
        _hand.ReturnHeld();
    }

    private void SetPlacing(bool placing)
    {
        if (placing == _placing) return;
        _placing = placing;
        PlacingChanged?.Invoke(placing);
    }

    private void OnPressed(CardView card, PointerEventData eventData)
    {
        if (Locked || _getMap() == null || !_hand.TryHold(card)) return;

        _pointerId = eventData.pointerId;
        _pressPosition = eventData.position;
        _placing = false;
        _hasCell = false;
        _hasPoint = false;
        _cell = InvalidCell;

        _speed.Request(this, _config.HoldTimeScale);
        // 끌기 전까지는 카드가 커져 설명을 보여 준다(누르고 있는 동안).
        _hand.MoveHeld(eventData.position, true);
    }

    private void OnDragged(CardView card, PointerEventData eventData)
    {
        // 다른 손가락(맵 탭 등)의 입력은 무시한다.
        if (card != _hand.Held || eventData.pointerId != _pointerId) return;

        float distance = Vector2.Distance(eventData.position, _pressPosition) / _hand.ScaleFactor;
        bool placing = distance >= _config.DeadZone;
        if (placing != _placing)
        {
            SetPlacing(placing);
            card.SetVisible(!placing);
            if (!placing) HidePreview();
        }
        _hand.MoveHeld(eventData.position, !_placing);
        if (_placing) UpdateAim(eventData.position);
    }

    private void OnReleased(CardView card, PointerEventData eventData)
    {
        if (card != _hand.Held || eventData.pointerId != _pointerId) return;

        if (_placing) UpdateAim(eventData.position);
        HidePreview();
        _speed.Release(this);
        bool placing = _placing;
        SetPlacing(false);

        // 카드를 쓰는 길은 모두 손패에서 먼저 빼고 효과를 낸다 — 효과가 레벨업 창을 열면(더블 업) 창이 끄는 중인 카드를
        // 손패로 되돌려(Cancel) 뒤늦은 소모가 아무 일도 하지 않았다.
        HandCard held = card.Card;
        if (placing && held.IsHero)
        {
            if (!_hasPoint)
            {
                _hand.ReturnHeld();
                return;
            }
            _hand.ConsumeHeld();
            _skills.UseHeroCard(held.Hero);
            return;
        }

        if (placing && IsPointSkill(held))
        {
            if (!_hasPoint || !CanCastAtPoint(held.Skill))
            {
                _hand.ReturnHeld();
                return;
            }
            _hand.ConsumeHeld();
            CastAtPoint(held.Skill);
            return;
        }

        if (placing && _hasCell && DicePlacement.IsAccepted(_cellResult))
        {
            if (held.IsSkill)
            {
                _hand.ConsumeHeld();
                _skills.CastOnCell(_cell, held.Skill);
                return;
            }
            if (!_placement.TryPlace(_getMap(), _cell, held))
            {
                _hand.ReturnHeld();
                return;
            }
            _hand.ConsumeHeld();
            return;
        }

        if (placing && _hasCell) Debug.Log($"[CardDrag] 배치 불가 ({_cell.x},{_cell.y}): {_cellResult}");
        _hand.ReturnHeld();
    }

    // 범위·판 전체 스킬 — 칸이 아니라 바닥 점을 고른다.
    private static bool IsPointSkill(HandCard card) =>
        card.IsSkill && (card.Skill.Target == SkillTarget.Enemies || card.Skill.Target == SkillTarget.Board);

    // 범위·판 전체는 맵 위면 쓴다(대상이 없어도 — 카드를 버리는 방법이기도 하다).
    private bool CanCastAtPoint(SO_SkillCard card) =>
        card is SO_EnemySkillCard enemies ? _skills.CanCast(enemies, _point) : _skills.CanCastBoard(card);

    private void CastAtPoint(SO_SkillCard card)
    {
        if (card is SO_EnemySkillCard enemies) _skills.Cast(enemies, _point);
        else _skills.CastBoard(card);
    }

    private void UpdateAim(Vector2 screenPosition)
    {
        MapData map = _getMap();
        HandCard aimed = _hand.Held.Card;
        if (aimed.IsHero || IsPointSkill(aimed))
        {
            AimPoint(map, screenPosition, aimed.Asset);
            return;
        }

        _hasCell = _picker.TryScreenToCell(_getCamera(), map, screenPosition, out Vector2Int cell);
        if (!_hasCell)
        {
            HidePreview();
            return;
        }

        // 칸이 바뀔 때만 판정한다(판정마다 길 전체를 다시 계산하므로).
        if (cell != _cell)
        {
            _cell = cell;
            HandCard card = _hand.Held.Card;
            if (card.IsChip) _cellResult = _placement.Check(map, cell, card);
            else if (card.IsSkill) AimDiceSkill(cell, card.Skill);
            else AimDice(map, cell, card);
        }

        HandCard held = _hand.Held.Card;
        if (held.IsSkill && held.Skill.Target == SkillTarget.Cell)
        {
            _preview.ShowCell(cell, DicePlacement.IsAccepted(_cellResult));
            SetGhostCell(InvalidCell, float.NaN);
            return;
        }
        if (held.IsChip)
        {
            // 칩은 주사위 밑으로 들어가므로 주사위를 숨기지 않는다.
            DiceState? dice = _diceField.TryGetPlaced(cell, out PlacedDice under) ? under.Dice : (DiceState?)null;
            _preview.ShowChip(cell, DicePlacement.IsAccepted(_cellResult), held.Chip, dice);
            SetGhostCell(InvalidCell, float.NaN);
            return;
        }
        float pipHeight = _preview.Show(map, cell, DicePlacement.IsAccepted(_cellResult), _ghostDice);
        // 얹기·스킬이면 원래 주사위의 몸체를 숨기고(고스트와 겹치지 않게) 지금 눈 숫자는 고스트 위에 보인다.
        bool overDice = _cellResult == PlacementResult.Stack || _cellResult == PlacementResult.Upgrade;
        SetGhostCell(overDice ? cell : InvalidCell, pipHeight);
    }

    private void AimDice(MapData map, Vector2Int cell, HandCard card)
    {
        _cellResult = _placement.Check(map, cell, card);
        // 고스트는 놓였을 때의 모양이다. 눈은 굴려야 정해지므로 면체·색만 쓴다(굴림 값은 아무거나).
        _ghostDice = DiceRules.Create(card.Dice, 1);
        bool overDice = _cellResult == PlacementResult.Stack || _cellResult == PlacementResult.AtCap;
        if (overDice && _diceField.TryGetPlaced(cell, out PlacedDice placed))
        {
            _ghostDice = placed.Dice;
            DiceRules.Stack(ref _ghostDice, card.Dice, 0);
        }
    }

    // 다시 굴리기는 눈을 알 수 없으므로 지금 눈 그대로 보인다(고스트는 면체·색만 보인다).
    private void AimDiceSkill(Vector2Int cell, SO_SkillCard card)
    {
        if (card.Target == SkillTarget.Cell)
        {
            _cellResult = SkillRules.CheckCell(_diceField.IndexOf(cell) >= 0, _chips.Get(cell) != null);
            return;
        }
        if (!_diceField.TryGetPlaced(cell, out PlacedDice placed))
        {
            _cellResult = PlacementResult.NoDice;
            return;
        }
        _cellResult = SkillRules.CheckDice(placed.Dice, card);
        _ghostDice = placed.Dice;
        SkillRules.ApplyDice(ref _ghostDice, card, placed.Dice.Pips);
    }

    // 적 대상·판 전체·영웅 강화는 판 위 아무 곳(SkillPreview 규칙). 맵 밖이면 아무것도 보이지 않는다(놓으면 손패로 돌아간다).
    private void AimPoint(MapData map, Vector2 screenPosition, SO_Card card)
    {
        _hasPoint = _picker.TryScreenToPoint(_getCamera(), map, screenPosition, out _point);
        if (!_hasPoint)
        {
            HidePreview();
            return;
        }
        if (card is SO_EnemySkillCard enemies)
        {
            _skillPreview.ShowEnemies(enemies, _point);
            return;
        }
        _skillPreview.ShowBoard(card);
    }

    private void HidePreview()
    {
        _preview.Hide();
        _skillPreview.Hide();
        SetGhostCell(InvalidCell, float.NaN);
    }

    private void SetGhostCell(Vector2Int cell, float pipHeight)
    {
        if (cell != _ghostCell && _ghostCell != InvalidCell) _diceField.ClearGhost(_ghostCell);
        _ghostCell = cell;
        if (cell != InvalidCell) _diceField.ShowOverGhost(cell, pipHeight);
    }
}
