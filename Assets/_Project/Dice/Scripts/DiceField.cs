using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>놓인 주사위 하나. 칸과 주사위 상태만 둔다(공격 수치는 색·눈에서 계산하고, 붙은 강화는 그 칸의 칩이다 — ChipField).</summary>
public struct PlacedDice
{
    public Vector2Int Cell;
    public DiceState Dice;
}

/// <summary>
/// 맵 위에 놓인 주사위들. 빈 칸에 놓으면 맵의 점유 정보를 바꾸고, 놓인 주사위 위에 얹으면 합성한다(기획서 6.3).
/// 굴리기는 여기서 한다 — 놓는 순간 굴려 눈이 정해진다(6.2).
/// </summary>
public sealed class DiceField : IDisposable
{
    private readonly SO_DiceConfig _config;
    private readonly SO_DiceStatsConfig _statsConfig;
    private readonly SO_MapViewConfig _viewConfig;
    private readonly Transform _root;
    private readonly Func<Camera> _getCamera;
    private readonly Material _outline;
    private readonly TraitHooks _traits;
    private readonly List<PlacedDice> _placed = new List<PlacedDice>();
    // 슬롯 순서가 _placed와 같다.
    private readonly List<DiceView> _views = new List<DiceView>();
    private MapData _map;
    private float _lastYaw = float.NaN;

    /// <summary>빈 칸에 새로 놓였다. 칸이 막혀 길이 바뀌었을 수 있으므로 경로 표시 등이 구독해 갱신한다.</summary>
    public event Action<Vector2Int> DicePlaced;

    /// <summary>
    /// 거둬졌다(캐시 아웃). index는 지우기 전의 번호 — 같은 번호로 상태를 붙여 둔 쪽(DiceCombat)이 같은 자리를 지운다.
    /// 칸이 열려 길이 바뀌므로 경로 표시 등도 구독한다.
    /// </summary>
    public event Action<int, Vector2Int> DiceRemoved;

    /// <summary>주사위가 각성했다(놓기·얹기·스킬로 눈이 바뀌어 각성에 닿았다). 측정용(한 판 각성 수, 기획서 12.4).</summary>
    public event Action<Vector2Int> DiceAwakened;

    /// <summary>
    /// 앞에서부터 순서대로 놓인 주사위. 거두기(캐시 아웃)는 순서를 지킨 채 그 자리만 빼고 DiceRemoved로 알린다
    /// — 공격(DiceCombat)이 같은 번호로 자기 상태(재사용 대기)를 붙여 두므로 번호가 섞이면 안 된다.
    /// </summary>
    public IReadOnlyList<PlacedDice> Placed => _placed;

    /// <param name="outline">툰 아웃라인 머티리얼(SceneLighting.OutlineMaterial). 없으면 null.</param>
    /// <param name="traits">특성 갈고리(눈 굴림, IDicePipRoller). 없으면 null.</param>
    public DiceField(Transform parent, SO_DiceConfig config, SO_DiceStatsConfig statsConfig, SO_MapViewConfig viewConfig, Func<Camera> getCamera,
        Material outline, TraitHooks traits)
    {
        _traits = traits;
        _config = config;
        _statsConfig = statsConfig;
        _viewConfig = viewConfig;
        _getCamera = getCamera;
        _outline = outline;
        _root = new GameObject("Dice").transform;
        _root.SetParent(parent, false);
        Core.Loop.OnUpdate += Tick;
        Core.Loop.OnLateUpdate += LateTick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        Core.Loop.OnLateUpdate -= LateTick;
    }

    public bool TryGetPlaced(Vector2Int cell, out PlacedDice placed)
    {
        int index = IndexOf(cell);
        placed = index >= 0 ? _placed[index] : default;
        return index >= 0;
    }

    /// <summary>cell에 놓인 주사위 몸체의 윗면 높이(맵 로컬). 영웅이 주사위를 밟고 설 때 쓴다. 주사위가 없으면 false.</summary>
    public bool TryGetSurface(Vector2Int cell, out float height)
    {
        int index = IndexOf(cell);
        // 판 치우기(DetachViews) 뒤 Clear 전까지는 표시가 없다.
        bool found = index >= 0 && index < _views.Count;
        height = found ? _views[index].SurfaceHeight : 0f;
        return found;
    }

    /// <summary>판정(DicePlacement.Check)이 Ok인 빈 칸에만 부른다.</summary>
    public void Place(MapData map, Vector2Int cell, DiceCard card)
    {
        _map = map;
        map.SetDice(cell.x, cell.y, true);
        DiceState dice = DiceRules.Create(card, Roll(card.Faces, true));
        DiceRules.ResolveBlack(ref dice, _statsConfig.AwakenPips);
        _placed.Add(new PlacedDice
        {
            Cell = cell,
            Dice = dice,
        });
        _views.Add(new DiceView(_root, _config, $"Dice_{cell.x}_{cell.y}", _outline));
        Refresh(_placed.Count - 1);
        // 영웅이 판 위를 뛰어다니므로 위에서 떨어지면 겹친다 — 바닥에서 솟아난다.
        _views[_views.Count - 1].Pop(0f);
        DicePlaced?.Invoke(cell);
        NotifyAwakened(false, dice, cell);
    }

    /// <summary>판정(DicePlacement.CheckStack)이 Stack인 칸에만 부른다. 칸은 이미 막혀 있으므로 길은 바뀌지 않는다.</summary>
    public void Stack(Vector2Int cell, DiceCard card)
    {
        int index = IndexOf(cell);
        if (index < 0) return;
        PlacedDice placed = _placed[index];
        bool wasAwakened = _statsConfig.IsAwakened(placed.Dice);
        DiceRules.Stack(ref placed.Dice, card, Roll(card.Faces, true));
        DiceRules.ResolveBlack(ref placed.Dice, _statsConfig.AwakenPips);
        _placed[index] = placed;
        Refresh(index);
        // 얹으면 새로 굴리므로 제자리에서 톡 튀며 커진다(영웅이 위에 올라서 있어 위로 튀면 겹친다).
        _views[index].Pop(_config.StackPopStart);
        NotifyAwakened(wasAwakened, placed.Dice, cell);
    }

    /// <summary>주사위 하나의 상태를 바꾸는 일(주사위 대상 스킬). DiceField.Change에 넘긴다.</summary>
    public delegate void DiceChange(ref DiceState dice);

    /// <summary>
    /// cell의 주사위 상태를 change로 바꾸고, 검정 각성·표시·각성 알림을 맞춘다. 주사위 대상 스킬(SkillCaster)이 부른다 —
    /// 주사위가 스킬 종류를 모르게, 무엇을 바꿀지는 부르는 쪽이 정한다. rolled면 굴리는 연출을 보여 준다(바뀐 눈이 굴려서 나온 수로 보이게).
    /// </summary>
    public void Change(Vector2Int cell, DiceChange change, bool rolled)
    {
        int index = IndexOf(cell);
        if (index < 0 || change == null) return;
        PlacedDice placed = _placed[index];
        bool wasAwakened = _statsConfig.IsAwakened(placed.Dice);
        change(ref placed.Dice);
        // 리롤·잭팟으로 검정이 눈 20에 닿으면 무작위 색으로 바뀐다(6.5).
        DiceRules.ResolveBlack(ref placed.Dice, _statsConfig.AwakenPips);
        _placed[index] = placed;
        Refresh(index);
        if (rolled) _views[index].Roll(_config.StackDropHeight);
        NotifyAwakened(wasAwakened, placed.Dice, cell);
    }

    /// <summary>판의 모든 주사위를 지금 면체로 다시 굴린다(올인, 기획서 8). 다시 굴리기를 모두에게 한 번씩 쓰는 것과 같다.</summary>
    public void RerollAll()
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            PlacedDice placed = _placed[i];
            bool wasAwakened = _statsConfig.IsAwakened(placed.Dice);
            placed.Dice.Pips = Roll(placed.Dice.Faces, false);
            DiceRules.ResolveBlack(ref placed.Dice, _statsConfig.AwakenPips);
            _placed[i] = placed;
            Refresh(i);
            _views[i].Roll(_config.StackDropHeight);
            NotifyAwakened(wasAwakened, placed.Dice, placed.Cell);
        }
    }

    /// <summary>판의 눈이 1인 주사위만 지금 면체로 다시 굴린다(리스핀 — 더블 오어 낫싱 전용 카드). 다시 굴린 수를 돌려준다.</summary>
    public int RerollLowest()
    {
        int count = 0;
        for (int i = 0; i < _placed.Count; i++)
        {
            PlacedDice placed = _placed[i];
            if (placed.Dice.Pips != 1) continue;
            bool wasAwakened = _statsConfig.IsAwakened(placed.Dice);
            placed.Dice.Pips = Roll(placed.Dice.Faces, false);
            DiceRules.ResolveBlack(ref placed.Dice, _statsConfig.AwakenPips);
            _placed[i] = placed;
            Refresh(i);
            _views[i].Roll(_config.StackDropHeight);
            NotifyAwakened(wasAwakened, placed.Dice, placed.Cell);
            count++;
        }
        return count;
    }

    /// <summary>
    /// cell의 주사위 눈을 1 깎는다(영웅 오버드라이브, 기획서 10.5). 0 아래로는 내려가지 않는다 — 0이면 공격을 멈추고 벽으로만 남는다.
    /// 깎였으면 true. 주사위가 없거나 이미 0이면 false.
    /// </summary>
    public bool DrainPip(Vector2Int cell)
    {
        int index = IndexOf(cell);
        if (index < 0 || _placed[index].Dice.IsSpent) return false;
        PlacedDice placed = _placed[index];
        placed.Dice.Pips--;
        _placed[index] = placed;
        Refresh(index);
        return true;
    }

    /// <summary>cell의 주사위가 각성했는지.</summary>
    public bool IsAwakened(Vector2Int cell)
    {
        int index = IndexOf(cell);
        return index >= 0 && _statsConfig.IsAwakened(_placed[index].Dice);
    }

    /// <summary>cell의 주사위를 없앤다(캐시 아웃). 주사위가 없으면 false.</summary>
    public bool Remove(Vector2Int cell)
    {
        int index = IndexOf(cell);
        if (index < 0) return false;
        _map?.SetDice(cell.x, cell.y, false);
        _views[index].Destroy();
        _views.RemoveAt(index);
        _placed.RemoveAt(index);
        DiceRemoved?.Invoke(index, cell);
        return true;
    }

    // 각성하지 않았던 주사위가 각성에 닿았을 때만 알린다(이미 각성한 주사위를 다시 굴려 또 닿은 것은 새 각성이 아니다).
    private void NotifyAwakened(bool wasAwakened, in DiceState dice, Vector2Int cell)
    {
        if (!wasAwakened && _statsConfig.IsAwakened(dice)) DiceAwakened?.Invoke(cell);
    }

    // 리롤의 새 면체: 모델이 있는 면체(SO_DiceConfig.Models) 중 같은 확률로. 목록이 비면 그대로.
    /// <summary>모델이 있는 면체 중 하나를 같은 확률로 뽑는다(리롤). 판 난수(GameRandom.Dice)를 쓴다.</summary>
    public int RollFaces(int current)
    {
        List<SO_DiceConfig.FaceModel> models = _config.Models;
        return models.Count > 0 ? models[GameRandom.Dice.Next(models.Count)].Faces : current;
    }

    // 타워 주사위 굴림. 특성이 굴림을 바꾸면(더블 오어 낫싱·777) 그것을 쓴다. placing = 손패에서 놓거나 얹는 굴림.
    private int Roll(int faces, bool placing)
    {
        IDicePipRoller roller = _traits?.Find<IDicePipRoller>();
        return roller != null ? Mathf.Clamp(roller.RollPips(faces, placing), 1, faces) : DiceRules.Roll(faces);
    }

    /// <summary>놓인 주사위를 다시 굴리는 눈(스킬). 특성의 굴림이 있으면 그것을 쓴다.</summary>
    public int RollPips(int faces) => Roll(faces, false);

    /// <summary>cell의 주사위 표시를 다시 맞춘다(밑에 칩이 들어와 올라앉을 때). 주사위가 없으면 아무 일도 없다.</summary>
    public void RefreshAt(Vector2Int cell)
    {
        int index = IndexOf(cell);
        if (index >= 0) Refresh(index);
    }

    /// <summary>놓인 주사위는 그대로 두고 표시만 다시 맞춘다(칸 높이·색·글자 설정이 바뀌었을 때).</summary>
    public void RebuildViews(MapData map)
    {
        _map = map;
        for (int i = 0; i < _views.Count; i++) Refresh(i);
    }

    /// <summary>
    /// 얹기 고스트가 cell에 합친 결과를 보여 주는 동안 원래 주사위의 몸체를 숨기고 눈 숫자는 고스트 위(pipHeight)로 올린다.
    /// 주사위가 없으면 아무 일도 없다.
    /// </summary>
    public void ShowOverGhost(Vector2Int cell, float pipHeight)
    {
        int index = IndexOf(cell);
        if (index >= 0) _views[index].ShowOverGhost(pipHeight);
    }

    /// <summary>ShowOverGhost를 되돌린다.</summary>
    public void ClearGhost(Vector2Int cell)
    {
        int index = IndexOf(cell);
        if (index >= 0) _views[index].ClearGhost();
    }

    /// <summary>주사위를 모두 치운다(새 맵). 맵 점유 정보는 새 MapData와 함께 버려지므로 건드리지 않는다.</summary>
    /// <summary>표시만 판 치우기에 넘긴다(판 상태는 그대로 — 곧 Clear·새 판이 온다).</summary>
    public void DetachViews(BoardSweep sweep)
    {
        for (int i = 0; i < _views.Count; i++) _views[i].DetachTo(sweep);
        _views.Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < _views.Count; i++) _views[i].Destroy();
        _views.Clear();
        _placed.Clear();
    }

    /// <summary>cell에 놓인 주사위의 번호(Placed 순서). 없으면 -1.</summary>
    public int IndexOf(Vector2Int cell)
    {
        // 칸 수(최대 120)만큼만 있어 그냥 훑는다.
        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Cell == cell) return i;
        }
        return -1;
    }

    private void Refresh(int index)
    {
        if (_map == null) return;
        PlacedDice placed = _placed[index];
        float top = _viewConfig.GetCellTop(_map, placed.Cell.x, placed.Cell.y);
        _views[index].Apply(placed.Cell, top, placed.Dice, _statsConfig.IsAwakened(placed.Dice), _config);
        // 새로 만든 글자는 아직 돌려지지 않았으므로 다음 LateTick에서 전부 다시 돌린다.
        _lastYaw = float.NaN;
    }

    // 굴리는 연출을 진행하고, 카메라가 돌았을 때만(화면 회전) 눈 글자를 다시 돌린다.
    // 놓는 연출(솟아나기·굴러 떨어지기)은 실제 시간이다 — 멈춘 동안 놓아도 주사위가 나타나고 눈 숫자까지 보이게.
    // 판정은 연출을 기다리지 않으므로(DiceView) 게임 결과는 바뀌지 않는다. 카메라를 마주 보는 것은 카메라가 움직인 뒤(LateUpdate)에 한다.
    private void Tick(float deltaTime)
    {
        for (int i = 0; i < _views.Count; i++) _views[i].Tick(deltaTime, _config);
    }

    private void LateTick(float _)
    {
        Camera camera = _getCamera();
        if (camera == null) return;
        float yaw = camera.transform.eulerAngles.y;
        // NaN끼리는 같지 않으므로 다시 돌려야 할 때(_lastYaw = NaN)도 여기를 지난다.
        if (yaw == _lastYaw) return;
        _lastYaw = yaw;
        for (int i = 0; i < _views.Count; i++) _views[i].FaceCamera(yaw);
    }
}
