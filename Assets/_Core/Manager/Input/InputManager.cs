using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 활성화된 입력 모듈(<see cref="InputActions"/>)들을 관리한다.
/// SetInput/AddInput/RemoveInput으로 상황(게임플레이·메뉴·컷신 등)에 맞는 모듈을 켜고 끈다.
/// 활성 모듈은 켜진 순서대로 매 프레임 OnUpdate를 받아 기기 입력을 게임의 입력 상태에 반영한다.
///
/// 씬 전환 시 일반 모듈은 모두 꺼지고 버려진다(다음 씬이 새로 켠다). <see cref="InputActions.Persistent"/> 모듈은 남는다.
/// 코어는 뒤로가기 모듈(<see cref="CoreBackInput"/>)을 부팅 때 켜 둔다(<see cref="SO_CoreConfig.EnableBackInput"/>).
///
/// 기기별 구체 모듈과 Unity Input Actions 에셋 바인딩은 게임 코드에서 만든다.
/// </summary>
public sealed class InputManager : Manager
{
    // 켜진 순서를 지킨다. 갱신 순서가 실행마다 달라지지 않게 하기 위해서다.
    private readonly List<Type> _active = new();
    private readonly Dictionary<Type, InputActions> _typeToAction = new();

    // 갱신 중 모듈이 입력을 바꿔도(일시정지 키로 메뉴 입력 전환 등) 안전하도록 목록을 복사해서 돈다.
    private readonly List<Type> _updateSnapshot = new();

    public override Type[] Dependencies => new[] { typeof(LoopManager) };

    /// <summary>
    /// 씬 전환으로 입력이 정리될 때. 게임의 입력 상태 클래스가 구독해 자기 값을 초기화한다.
    /// (입력 필드는 게임이 소유하므로 코어가 직접 지울 수 없다 — <see cref="ButtonInput"/> 참고)
    /// </summary>
    public event Action OnInputsReset;

    protected override UniTask OnInitializeAsync()
    {
        Core.Loop.OnUpdate += UpdateActiveActions;

        if (SO_CoreConfig.Instance.EnableBackInput) AddInput<CoreBackInput>();

        return UniTask.CompletedTask;
    }

    private void UpdateActiveActions(float deltaTime)
    {
        _updateSnapshot.Clear();
        _updateSnapshot.AddRange(_active);

        for (int i = 0; i < _updateSnapshot.Count; i++)
        {
            Type type = _updateSnapshot[i];

            // 앞선 모듈이 이 프레임에 끈 모듈은 건너뛴다.
            if (!_active.Contains(type) || !_typeToAction.TryGetValue(type, out InputActions action)) continue;

            try
            {
                action.OnUpdate(deltaTime);
            }
            catch (Exception e)
            {
                // 모듈 하나가 던져도 나머지 입력은 계속 돈다.
                Debug.LogException(e);
            }
        }
    }

    #region Public API

    /// <summary>모듈 T를 활성화하며, 활성화 전에 초기 설정을 준다.</summary>
    public T SetInput<T>(Action<T> onInit) where T : InputActions
    {
        var action = GetOrCreateAction<T>();
        onInit?.Invoke(action);
        SetInput(typeof(T));
        return action;
    }

    // SetInput: 일반 모듈을 이 목록으로 갈아 끼운다. 유지(Persistent) 모듈은 그대로 둔다.
    public void SetInput<T>() where T : InputActions => SetInput(typeof(T));
    public void SetInput<T1, T2>() where T1 : InputActions where T2 : InputActions => SetInput(typeof(T1), typeof(T2));
    public void SetInput<T1, T2, T3>() where T1 : InputActions where T2 : InputActions where T3 : InputActions => SetInput(typeof(T1), typeof(T2), typeof(T3));

    public void AddInput<T>()    where T : InputActions => AddInput(typeof(T));
    public void RemoveInput<T>() where T : InputActions => RemoveInput(typeof(T));

    /// <summary>일반 모듈을 모두 끈다. 유지(Persistent) 모듈은 남는다 — 끄려면 RemoveInput으로 직접 뺀다.</summary>
    public void RemoveAllInputs() => SetInput();

    public bool IsActive<T>()       where T : InputActions => _active.Contains(typeof(T));
    public T GetAction<T>()         where T : InputActions => _typeToAction.TryGetValue(typeof(T), out var a) ? a as T : null;
    public T GetOrCreateAction<T>() where T : InputActions => GetOrCreateAction(typeof(T)) as T;

    #endregion

    #region Set / Add / Remove (by Type)

    private void SetInput(params Type[] targetTypes)
    {
        var next = new List<Type>();

        // 유지 모듈은 갈아 끼우는 대상이 아니다.
        foreach (Type cur in _active)
            if (IsPersistent(cur)) next.Add(cur);

        foreach (Type t in targetTypes)
            if (IsValidActionType(t) && !next.Contains(t)) next.Add(t);

        Apply(next);
    }

    private void AddInput(Type type)
    {
        if (!IsValidActionType(type) || _active.Contains(type)) return;

        var next = new List<Type>(_active) { type };
        Apply(next);
    }

    private void RemoveInput(Type type)
    {
        if (!_active.Contains(type)) return;

        var next = new List<Type>(_active);
        next.Remove(type);
        Apply(next);
    }

    // 빠지는 모듈은 끊고, 새로 들어오는 모듈은 잇는다.
    private void Apply(List<Type> next)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Type cur = _active[i];
            if (next.Contains(cur)) continue;

            _active.RemoveAt(i);
            DisconnectAction(cur);
        }

        foreach (Type n in next)
        {
            if (_active.Contains(n)) continue;

            _active.Add(n);
            ConnectAction(n);
        }
    }

    #endregion

    #region Internal

    private static bool IsValidActionType(Type type)
        => type != null && !type.IsAbstract && typeof(InputActions).IsAssignableFrom(type);

    private bool IsPersistent(Type type)
        => _typeToAction.TryGetValue(type, out InputActions action) && action.Persistent;

    private void ConnectAction(Type type)
    {
        InputActions action = GetOrCreateAction(type);
        if (action == null)
        {
            _active.Remove(type);
            return;
        }

        try
        {
            action.Connect();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void DisconnectAction(Type type)
    {
        if (!_typeToAction.TryGetValue(type, out InputActions action)) return;

        try
        {
            action.Disconnect();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private InputActions GetOrCreateAction(Type type)
    {
        if (_typeToAction.TryGetValue(type, out var action)) return action;

        try
        {
            action = (InputActions)Activator.CreateInstance(type);
            action.Init(this);
            _typeToAction[type] = action;
            return action;
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputManager] 액션 생성 실패: {type.Name}\n{e}");
            return null;
        }
    }

    #endregion

    #region Utility

    private PointerEventData _pointerData;
    private readonly List<RaycastResult> _raycastResults = new(16);

    /// <summary>화면 좌표가 UI 위에 있는지. (터치/클릭이 UI에 먹혔는지 판정)</summary>
    public bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        _pointerData ??= new PointerEventData(EventSystem.current);
        _pointerData.position = screenPos;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerData, _raycastResults);
        return _raycastResults.Count > 0;
    }

    /// <summary>
    /// 화면 좌표를 카메라로부터 depth만큼 떨어진 월드 좌표로 바꾼다.
    /// 원근 카메라에서는 깊이가 있어야 의미가 있다(0이면 카메라 위치가 나온다). 바닥을 찍으려면 <see cref="ScreenToPlane"/>을 쓴다.
    /// </summary>
    public Vector3 ScreenToWorld(Vector2 screenPos, float depth)
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;

        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
    }

    /// <summary>2D(정사영, z=0 평면을 보는 카메라)용. 화면 좌표가 가리키는 z=0 평면 위의 점.</summary>
    public Vector3 ScreenToWorld(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;

        float depth = cam.orthographic ? -cam.transform.position.z : 0f;
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
    }

    /// <summary>
    /// 화면 좌표에서 쏜 광선이 평면과 만나는 점. 원근·정사영 모두 맞다(탑다운에서 바닥 클릭 등).
    /// 평면과 만나지 않으면(평면이 카메라 뒤·평행) false.
    /// </summary>
    public bool ScreenToPlane(Vector2 screenPos, Plane plane, out Vector3 point)
    {
        point = Vector3.zero;
        var cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!plane.Raycast(ray, out float distance)) return false;

        point = ray.GetPoint(distance);
        return true;
    }

    #endregion

    /// <summary>씬 전환 정리. 일반 모듈은 끄고 버린다. 유지 모듈은 남긴다(인스턴스와 상태 그대로).</summary>
    public override void Clear()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Type type = _active[i];
            if (IsPersistent(type)) continue;

            _active.RemoveAt(i);
            DisconnectAction(type);
        }

        var drop = new List<Type>();
        foreach (KeyValuePair<Type, InputActions> pair in _typeToAction)
            if (!pair.Value.Persistent) drop.Add(pair.Key);
        foreach (Type type in drop) _typeToAction.Remove(type);

        try
        {
            OnInputsReset?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        // Loop 구독은 유지한다 — 씬을 넘어 살아있는 매니저라 여기서 끊으면 다음 씬에서 틱이 멈춘다.
    }
}
