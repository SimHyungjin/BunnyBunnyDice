using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 판 주사위 연출(기획서 3.3, 표시만). 맵 생성이 정한 굴림 결과(MapData.Roll)를 그대로 보여 준다
/// — 물리로 굴리면 결과가 매번 달라지고 무효한 맵이 나올 수 있어, 결과를 먼저 정하고 연출이 거기에 맞춰 움직인다.
///
/// 박자를 여러 번 두는 것이 핵심이다: 터짐(흩어짐) → 데구르르(흩어지던 방향으로 굴러 멈춤) → 확정(눈이 함께 톡) → 하나씩 날아가 쏙(포켓마다 빛) → 시작 칩 착착.
/// 날아갈 때 주사위를 돌리지 않는다 — 윗면 눈이 보여야 "4가 4번 포켓으로" 간 것이 읽힌다.
/// 실제 시간으로 진행한다(카드를 집어 느려져도 같은 빠르기). 건너뛸 수 없다.
///
/// 굴림은 연출 전에 정해지지만 시작 칩 자리는 맵 계산(미로 검사, 워커 스레드)이 연출 중에 정한다. 주사위는 RollTurns번 구른 뒤에도 계산이 안 끝났으면
/// 끝날 때까지 더 구른다(사용자 제안 — 멈춰 기다리는 것보다 자연스럽다). 그래서 확정 이후 시각은 모든 주사위가 멈춘 뒤에 정한다(Schedule).
/// 칩은 그 뒤에 떨어지므로 늘 계산이 끝난 칩이다.
/// </summary>
public sealed class BoardRollPresenter : IDisposable
{
    private readonly SO_BoardRollConfig _config;
    private readonly SO_DiceConfig _diceConfig;
    private readonly ChipField _chips;
    private readonly EnvironmentView _environment;
    private readonly Func<Camera> _getCamera;
    private readonly Func<bool> _isMapReady;
    private readonly Transform _root;
    private readonly List<Die> _dice = new List<Die>();
    private Action _onDone;
    private float _time = -1f;
    private Vector3 _center;
    // 구르는 주사위가 벗어나지 않는 범위(칸 중심 좌표). 끝에 닿으면 방향을 꺾는다.
    private Vector2 _boundsMin, _boundsMax;
    // 모든 주사위가 멈춘 뒤 정하는 시각(Schedule). 그 전에는 칩을 숨긴다.
    private bool _scheduled;
    private float _confirmStart;
    private float _chipsStart;
    // 연출을 끈 설정에서 맵 계산만 기다리는 중.
    private bool _waitingOnly;

    private sealed class Die
    {
        public GameObject Body;
        public TextMeshPro Pips;
        public float HalfHeight;
        // 터져 떨어지는 곳 → 거기서 굴러 멈춘 곳(Land, 구르며 정해진다).
        public Vector3 Touchdown;
        public Vector3 Land;
        public Vector3 Pocket;
        public Vector2Int PocketCell;
        public Vector3 SpinAxis;
        public float Apex;
        public float Launch, Landed, FlyStart, Arrive;
        public bool Arrived;
        // 지금 구르는 한 번: 시작 자리·방향·시각·길이·몇 번째인지. Stopped면 Land에 멈춰 있다.
        public Vector3 TurnFrom;
        public Vector3 TurnDir;
        public float TurnStart, TurnDuration;
        public int TurnIndex = -1;
        public bool Stopped;
        public float Settled;
        // 처음 구르는 방향의 각도 — 몸체는 90° 대칭이라 꺾여도 이 각도로 서 있다.
        public Quaternion Yaw = Quaternion.identity;
    }

    public BoardRollPresenter(Transform parent, SO_BoardRollConfig config, SO_DiceConfig diceConfig,
        ChipField chips, EnvironmentView environment, Func<Camera> getCamera, Func<bool> isMapReady)
    {
        _config = config;
        _diceConfig = diceConfig;
        _chips = chips;
        _environment = environment;
        _getCamera = getCamera;
        _isMapReady = isMapReady;
        _root = new GameObject("BoardRoll").transform;
        _root.SetParent(parent, false);
        Core.Loop.OnUpdate += Tick;
    }

    public bool IsPlaying => _time >= 0f;

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        ClearDice();
    }

    /// <summary>
    /// map의 굴림 결과를 보여 준 뒤 onDone을 부른다. 재생 중에 다시 부르면 앞의 것은 버린다(그 onDone은 부르지 않는다).
    /// map은 굴림만 쓴다(칩 자리는 연출 중에 바뀔 수 있다 — 칩은 ChipField의 지금 표시를 떨어뜨린다).
    /// </summary>
    public void Play(MapData map, Action onDone)
    {
        ClearDice();
        _onDone = onDone;
        _waitingOnly = false;
        _scheduled = false;
        _chipsStart = float.MaxValue;
        if (!_config.Enabled || map.Roll.Count == 0)
        {
            // 연출이 없어도 칩 자리가 정해질 때까지는 판을 시작하지 않는다.
            if (_isMapReady()) Finish();
            else
            {
                _waitingOnly = true;
                _time = 0f;
            }
            return;
        }

        SO_DiceConfig.FaceModel model = _diceConfig.GetModel(6);
        model.Size *= _config.DieScale;
        _center = new Vector3((map.Width - 1) * 0.5f, 0f, (map.Height - 1) * 0.5f);
        _boundsMin = Vector2.zero;
        _boundsMax = new Vector2(map.Width - 1, map.Height - 1);

        // 1. 터짐: 시차를 두고 판 가운데에서 솟아 흩어진다. 떨어진 곳에서 흩어지던 방향으로 구른다.
        for (int i = 0; i < map.Roll.Count; i++)
        {
            int face = map.Roll[i];
            Vector2Int cell = map.Pockets[face - 1];
            Die die = new Die
            {
                Body = DiceBody.Build(_root, model, $"BoardDie_{face}", out float halfHeight),
                HalfHeight = halfHeight,
                Touchdown = PickLanding(_center),
                Pocket = _environment.GetPocketCenter(map, cell),
                PocketCell = cell,
                Apex = UnityEngine.Random.Range(_config.FountainHeight.x, _config.FountainHeight.y),
                Launch = i * _config.FountainStagger,
            };
            die.Land = die.Touchdown;
            die.Landed = die.Launch + _config.FountainDuration;
            Vector3 outward = die.Touchdown - _center;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle.normalized;
                outward = new Vector3(random.x, 0f, random.y);
            }
            die.TurnDir = outward.normalized;
            die.Yaw = Quaternion.LookRotation(die.TurnDir, Vector3.up);
            Vector2 axis = UnityEngine.Random.insideUnitCircle.normalized;
            die.SpinAxis = new Vector3(axis.x, UnityEngine.Random.Range(-0.3f, 0.3f), axis.y).normalized;
            RendererTint.Apply(die.Body, _config.DieColor);
            die.Pips = CreatePips(face, model.PipScale * _config.DieScale);
            _dice.Add(die);
        }

        _time = 0f;
        _environment.SetPocketGlowVisible(false);
        Apply();
    }

    // 2. 확정 → 3. 하나씩 날아감 → 4. 시작 칩. 모든 주사위가 멈춘 시각부터 센다(얼마나 더 굴렀는지 미리 알 수 없다).
    private void Schedule()
    {
        float settled = 0f;
        for (int i = 0; i < _dice.Count; i++) settled = Mathf.Max(settled, _dice[i].Settled);
        float flyFrom = settled + _config.ConfirmDuration;
        float lastArrive = flyFrom;
        for (int i = 0; i < _dice.Count; i++)
        {
            _dice[i].FlyStart = flyFrom + i * _config.FlyStagger;
            _dice[i].Arrive = _dice[i].FlyStart + _config.FlyDuration;
            lastArrive = Mathf.Max(lastArrive, _dice[i].Arrive);
        }
        _confirmStart = settled;
        _chipsStart = lastArrive + _config.ChipDelay;
        _scheduled = true;
    }

    /// <summary>
    /// 굴리기 전 모습으로 둔다(로비 — 뛰어든 뒤에 굴린다): 시작 칩을 숨기고 포켓 빛을 끈다. 재생 중이면 멈춘다(그 onDone은 부르지 않는다).
    /// 칩을 다시 만들면(RebuildViews) 보이게 되므로 그 뒤에 다시 부른다.
    /// </summary>
    public void Hold()
    {
        _time = -1f;
        _onDone = null;
        _waitingOnly = false;
        ClearDice();
        _environment.SetPocketGlowVisible(false);
        IReadOnlyList<Transform> stacks = _chips.Views;
        for (int i = 0; i < stacks.Count; i++) stacks[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// 칩 표시를 새로 만든 뒤(맵 검사가 칩을 다시 놓음) 부른다. 새 표시는 보이는 채로 생기므로 연출 중이면 지금 시각에 맞추고,
    /// 연출 전(판 치우기 중·로비)이면 숨긴다. 연출을 끈 설정에서는 그대로 둔다(칩이 바로 보인다).
    /// </summary>
    public void RefreshChips()
    {
        if (_waitingOnly) return;
        if (IsPlaying)
        {
            ApplyChips();
            return;
        }
        IReadOnlyList<Transform> stacks = _chips.Views;
        for (int i = 0; i < stacks.Count; i++) stacks[i].gameObject.SetActive(false);
    }

    // 판 가운데 둘레에 흩뿌리되 서로 겹치지 않게 몇 번 다시 뽑는다(못 찾으면 마지막 자리를 쓴다 — 겹쳐도 연출일 뿐이다).
    private Vector3 PickLanding(Vector3 center)
    {
        Vector3 pick = center;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            pick = center + new Vector3(
                UnityEngine.Random.Range(-_config.ScatterExtent.x, _config.ScatterExtent.x), 0f,
                UnityEngine.Random.Range(-_config.ScatterExtent.y, _config.ScatterExtent.y));
            bool clear = true;
            for (int i = 0; i < _dice.Count && clear; i++)
                if (Vector3.Distance(_dice[i].Touchdown, pick) < _config.MinSpacing) clear = false;
            if (clear) break;
        }
        return pick;
    }

    private TextMeshPro CreatePips(int face, float scale)
    {
        // 몸체의 자식으로 두지 않는다 — 몸체 색 입히기(RendererTint)가 글자 렌더러까지 덮고, 도는 동안 글자까지 같이 돈다(DiceView와 같은 이유).
        GameObject text = new GameObject($"BoardDie_{face}_Pips");
        text.transform.SetParent(_root, false);
        TextMeshPro pips = text.AddComponent<TextMeshPro>();
        pips.font = _diceConfig.PipFont;
        if (_diceConfig.PipMaterial != null) pips.fontSharedMaterial = _diceConfig.PipMaterial;
        pips.alignment = TextAlignmentOptions.Center;
        pips.textWrappingMode = TextWrappingModes.NoWrap;
        pips.rectTransform.sizeDelta = new Vector2(1f, 1f);
        pips.fontSize = _diceConfig.PipFontSize * scale;
        pips.color = _config.PipColor;
        pips.text = face.ToString();
        return pips;
    }

    private void Tick(float deltaTime)
    {
        if (!IsPlaying) return;
        if (_waitingOnly)
        {
            if (_isMapReady()) Finish();
            return;
        }
        _time += deltaTime;
        Apply();
    }

    private void Apply()
    {
        Camera camera = _getCamera();
        float yaw = camera != null ? camera.transform.eulerAngles.y : 0f;
        bool allStopped = true;
        for (int i = 0; i < _dice.Count; i++)
        {
            ApplyDie(_dice[i], yaw);
            if (!_dice[i].Stopped) allStopped = false;
        }
        if (!_scheduled && allStopped) Schedule();
        // 계산이 끝나 칩 표시를 새로 만들어도 떨어질 차례 전까지 숨겨 둔다(정해지기 전 _chipsStart는 끝없이 뒤).
        float chipsEnd = ApplyChips();
        if (_scheduled && _time >= chipsEnd + _config.EndDelay) Finish();
    }

    // 구르기를 지금 시각까지 진행한다: 한 번 다 구르면 기본 횟수가 남았거나 맵 계산이 안 끝났으면 한 번 더(점점 느리게), 아니면 그 자리에서 멈춘다.
    private void AdvanceRoll(Die die)
    {
        if (die.TurnIndex < 0)
        {
            die.TurnIndex = 0;
            die.TurnFrom = die.Touchdown;
            die.TurnStart = die.Landed;
            if (!StartTurn(die)) return;
        }
        while (!die.Stopped && _time >= die.TurnStart + die.TurnDuration)
        {
            float end = die.TurnStart + die.TurnDuration;
            die.TurnFrom += die.TurnDir * (die.HalfHeight * 2f);
            die.TurnIndex++;
            die.TurnStart = end;
            if (!StartTurn(die)) return;
        }
    }

    // 다음 한 번을 시작한다. 더 구를 일이 없으면 지금 자리에 멈추고 false.
    private bool StartTurn(Die die)
    {
        if (die.TurnIndex >= _config.RollTurns && _isMapReady())
        {
            die.Stopped = true;
            die.Land = die.TurnFrom;
            die.Settled = die.TurnStart;
            return false;
        }
        die.TurnDuration = Mathf.Min(_config.RollTurnDuration * Mathf.Pow(_config.RollSlowdown, die.TurnIndex), _config.RollMaxTurnDuration);
        // 판 끝에 닿으면 옆으로 꺾고(둘 다 막히면 되돌아) 판 위에 남는다. 90° 단위로 꺾으므로 몸체 각도(Yaw)는 그대로 맞는다.
        float step = die.HalfHeight * 2f;
        if (!InBounds(die.TurnFrom + die.TurnDir * step))
        {
            Vector3 left = new Vector3(-die.TurnDir.z, 0f, die.TurnDir.x);
            if (InBounds(die.TurnFrom + left * step)) die.TurnDir = left;
            else if (InBounds(die.TurnFrom - left * step)) die.TurnDir = -left;
            else die.TurnDir = -die.TurnDir;
        }
        return true;
    }

    private bool InBounds(Vector3 position) =>
        position.x >= _boundsMin.x && position.x <= _boundsMax.x && position.z >= _boundsMin.y && position.z <= _boundsMax.y;

    private void ApplyDie(Die die, float yaw)
    {
        Transform body = die.Body.transform;
        float scale = 1f;
        bool visible = _time >= die.Launch && (!_scheduled || _time < die.Arrive);
        if (_time >= die.Landed) AdvanceRoll(die);
        // 눈은 멈춘 뒤에 보인다 — 구르는 동안 윗면 눈이 보이면 결과처럼 읽힌다(판 밖 주사위 연출과 같은 이유).
        bool pips = die.Stopped;

        if (_time < die.Landed)
        {
            // 터짐: 가운데에서 떨어질 곳까지 포물선, 도는 각도는 끝으로 갈수록 줄어 구를 방향으로 반듯하게 떨어진다.
            float u = Mathf.Clamp01((_time - die.Launch) / _config.FountainDuration);
            Vector3 flat = Vector3.Lerp(_center, die.Touchdown, u);
            float ease = 1f - (1f - u) * (1f - u);
            body.localPosition = new Vector3(flat.x, die.HalfHeight + die.Apex * 4f * u * (1f - u), flat.z);
            body.localRotation = die.Yaw * Quaternion.AngleAxis(_config.FountainTurns * 360f * (1f - ease), die.SpinAxis);
        }
        else if (!die.Stopped)
        {
            // 데구르르: 앞쪽 아래 모서리를 축으로 90° 넘어간다(무게중심이 모서리 위로 솟았다 내려온다).
            float u = Mathf.Clamp01((_time - die.TurnStart) / die.TurnDuration);
            Quaternion facing = Quaternion.LookRotation(die.TurnDir, Vector3.up);
            Quaternion tip = Quaternion.AngleAxis(90f * u, Vector3.right);
            float h = die.HalfHeight;
            Vector3 pivot = die.TurnFrom + die.TurnDir * h;
            body.localPosition = pivot + facing * (tip * new Vector3(0f, h, -h));
            body.localRotation = facing * tip;
        }
        else if (!_scheduled || _time < die.FlyStart)
        {
            // 멈춰 쉰다. 확정 동안 다 같이 톡 커졌다 돌아온다.
            body.localPosition = die.Land + Vector3.up * die.HalfHeight;
            body.localRotation = die.Yaw;
            if (_scheduled && _config.ConfirmDuration > 0f && _time >= _confirmStart)
            {
                float c = Mathf.Clamp01((_time - _confirmStart) / _config.ConfirmDuration);
                scale = 1f + (_config.ConfirmScale - 1f) * Mathf.Sin(c * Mathf.PI);
            }
        }
        else if (_time < die.Arrive)
        {
            // 날아감: 떨어진 자리에서 구멍 안(SinkDepth)까지 높은 포물선. 끝 무렵 작아지며 구멍으로 쏙 들어간다.
            float u = Mathf.Clamp01((_time - die.FlyStart) / _config.FlyDuration);
            Vector3 flat = Vector3.Lerp(die.Land, die.Pocket, u);
            float baseHeight = Mathf.Lerp(die.HalfHeight, _config.SinkDepth, u);
            body.localPosition = new Vector3(flat.x, baseHeight + _config.FlyHeight * 4f * u * (1f - u), flat.z);
            body.localRotation = die.Yaw;
            float sink = Mathf.Clamp01((u - (1f - _config.SinkPortion)) / _config.SinkPortion);
            scale = Mathf.Lerp(1f, _config.SinkScale, sink);
        }
        else if (!die.Arrived)
        {
            // 빠진 순간 그 포켓이 빛난다 — "이 주사위 때문에 이 포켓이 열렸다"가 이어지게.
            die.Arrived = true;
            _environment.SetPocketGlowVisible(die.PocketCell, true);
        }

        die.Body.SetActive(visible);
        body.localScale = Vector3.one * scale;
        die.Pips.gameObject.SetActive(visible && pips);
        // 눈 글자는 윗면에 눕히고 글자 위쪽이 화면 위를 향하게(DiceView.FaceCamera와 같은 방식).
        die.Pips.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        die.Pips.transform.localPosition = body.localPosition + Vector3.up * (die.HalfHeight * scale + _diceConfig.PipLift);
        die.Pips.transform.localScale = Vector3.one * scale;
    }

    // 시작 칩을 왼쪽 열부터 하나씩 떨어뜨린다. 마지막 칩이 자리 잡는 시각을 돌려준다.
    private float ApplyChips()
    {
        IReadOnlyList<Transform> stacks = _chips.Views;
        int count = stacks.Count;
        float interval = count > 0 ? Mathf.Min(_config.ChipInterval, _config.ChipMaxTotal / count) : 0f;
        for (int i = 0; i < count; i++)
        {
            Transform stack = stacks[i];
            float start = _chipsStart + i * interval;
            float landed = start + _config.ChipFallDuration;
            stack.gameObject.SetActive(_time >= start);
            Vector3 position = stack.localPosition;
            float y = 0f, sy = 1f;
            if (_time < landed)
            {
                // 점점 빨라지며 떨어진다(제곱).
                float f = Mathf.Clamp01((_time - start) / _config.ChipFallDuration);
                y = _config.ChipDropHeight * (1f - f * f);
            }
            else if (_config.ChipSquashDuration > 0f && _time < landed + _config.ChipSquashDuration)
            {
                // 닿으면 눌렸다 돌아온다 — 착 붙는 느낌.
                float q = (_time - landed) / _config.ChipSquashDuration;
                sy = 1f - _config.ChipSquash * Mathf.Sin(q * Mathf.PI);
            }
            stack.localPosition = new Vector3(position.x, y, position.z);
            float sxz = 1f + (1f - sy) * 0.5f;
            stack.localScale = new Vector3(sxz, sy, sxz);
        }
        return count > 0 ? _chipsStart + (count - 1) * interval + _config.ChipFallDuration + _config.ChipSquashDuration : _chipsStart;
    }

    // 결과 상태로 맞추고 onDone을 부른다.
    private void Finish()
    {
        _time = -1f;
        _waitingOnly = false;
        ClearDice();
        _environment.SetPocketGlowVisible(true);
        IReadOnlyList<Transform> stacks = _chips.Views;
        for (int i = 0; i < stacks.Count; i++)
        {
            Transform stack = stacks[i];
            stack.gameObject.SetActive(true);
            stack.localPosition = new Vector3(stack.localPosition.x, 0f, stack.localPosition.z);
            stack.localScale = Vector3.one;
        }
        Action done = _onDone;
        _onDone = null;
        done?.Invoke();
    }

    private void ClearDice()
    {
        for (int i = 0; i < _dice.Count; i++)
        {
            if (_dice[i].Body != null) Object.Destroy(_dice[i].Body);
            if (_dice[i].Pips != null) Object.Destroy(_dice[i].Pips.gameObject);
        }
        _dice.Clear();
    }
}
