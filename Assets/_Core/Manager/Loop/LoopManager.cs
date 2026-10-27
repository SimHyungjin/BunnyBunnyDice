using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 게임 전역의 매 프레임 갱신을 한곳에서 나눠준다.
/// 개별 오브젝트가 각자 Update를 갖는 대신 이 매니저의 이벤트를 구독하면,
/// 갱신 순서와 시간 배율을 중앙에서 통제할 수 있다.
///
/// 시간 배율을 둘로 나눈 이유: 월드(적·물리)는 멈추되 플레이어는 계속 움직이는
/// 연출(히트스톱 등)을 하려면, 둘의 시간 흐름을 따로 제어할 수 있어야 한다.
///
/// 슬로모션 중 물리를 부드럽게 하려면 fixedDeltaTime도 배율에 맞춰 줄여야 하지만,
/// 그 비용(물리 연산 증가)을 감수할지는 게임이 정할 일이라 여기서 건드리지 않는다.
/// </summary>
public class LoopManager : Manager
{
    #region Events

    private readonly Channel _update = new();
    private readonly Channel _gameUpdate = new();
    private readonly Channel _lateUpdate = new();

    /// <summary>실제 흐른 시간 기준 갱신(dt = unscaled). UI·입력 등 게임 정지와 무관한 것들.</summary>
    public event Action<float> OnUpdate
    {
        add => _update.Add(value);
        remove => _update.Remove(value);
    }

    /// <summary>플레이어 시간 배율이 적용된 갱신. 플레이어 로직에 쓴다.</summary>
    public event Action<float> OnGameUpdate
    {
        add => _gameUpdate.Add(value);
        remove => _gameUpdate.Remove(value);
    }

    /// <summary>후처리 갱신(dt = unscaled). 카메라 추적 등.</summary>
    public event Action<float> OnLateUpdate
    {
        add => _lateUpdate.Add(value);
        remove => _lateUpdate.Remove(value);
    }

    #endregion

    #region Time Scale

    /// <summary>
    /// 월드(적·물리 등)의 시간 배율. <see cref="Time.timeScale"/> 그 자체다.
    /// 따로 들고 있으면 게임 코드나 외부 에셋이 Time.timeScale을 직접 바꿨을 때 값이 어긋난다.
    /// </summary>
    public float WorldTimeScale => Time.timeScale;

    /// <summary>플레이어의 시간 배율. Time.timeScale과 무관하게 독립적으로 적용된다.</summary>
    public float PlayerTimeScale { get; private set; } = 1f;

    // Unity는 Time.timeScale을 0~100 밖으로 두면 에러를 낸다.
    private const float MaxTimeScale = 100f;

    /// <summary>월드와 플레이어를 같은 배율로 맞춘다. (완전 일시정지·전체 슬로모션 등)</summary>
    public void SetGameSpeed(float speed) => SetTimeScales(speed, speed);

    /// <summary>월드와 플레이어 배율을 따로 설정한다. 음수는 0으로, 월드 배율은 100까지로 자른다.</summary>
    public void SetTimeScales(float worldScale, float playerScale)
    {
        PlayerTimeScale = Mathf.Max(0f, playerScale);
        Time.timeScale = Mathf.Clamp(worldScale, 0f, MaxTimeScale);
    }

    /// <summary>월드만 멈추고 플레이어는 정상 동작시킨다. (히트스톱 등)</summary>
    public void FreezeWorldOnly() => SetTimeScales(0f, 1f);

    /// <summary>배율을 기본값(1배)으로 되돌린다.</summary>
    public void ResumeAll() => SetTimeScales(1f, 1f);

    #endregion

    #region Tick (Core가 매 프레임 호출)

    public void Tick(float unscaledDt)     => _update.Invoke(unscaledDt);
    public void GameTick(float unscaledDt) => _gameUpdate.Invoke(unscaledDt * PlayerTimeScale);
    public void LateTick(float unscaledDt) => _lateUpdate.Invoke(unscaledDt);

    #endregion

    public override void Clear()
    {
        // 씬이 바뀌면 시간 배율은 기본으로 되돌린다.
        // 이벤트 구독은 지우지 않는다 — 씬을 넘어 살아있는 매니저(카메라 등)의 구독까지 끊기면
        // 다음 씬에서 그들의 갱신이 멈춘다. 씬에 속한 구독자는 각자 사라질 때 스스로 해제한다.
        ResumeAll();
    }

    /// <summary>
    /// 매 프레임 부르는 구독자 목록. 멀티캐스트 델리게이트(event) 대신 쓰는 이유는 둘이다.
    ///
    /// 1) 예외 격리 — 델리게이트는 앞의 구독자가 던지면 뒤가 통째로 실행되지 않는다.
    ///    버그 있는 오브젝트 하나가 카메라·입력·풀 스케줄러의 갱신까지 매 프레임 멈추게 된다.
    ///    Unity의 Update는 MonoBehaviour마다 예외를 격리하므로, 그 보호를 잃지 않으려면 하나씩 감싸야 한다.
    /// 2) 할당 — 델리게이트의 +=, -=는 매번 구독자 수만큼의 배열을 새로 만든다.
    ///    풀링 오브젝트가 스폰·반납마다 구독을 오가면 그 할당이 계속 쌓인다.
    ///
    /// 순회 중 구독·해제도 안전하다. 해제된 자리는 비워 두었다가 순회가 끝나면 정리하고,
    /// 순회 중 새로 구독한 것은 다음 프레임부터 불린다.
    /// </summary>
    private sealed class Channel
    {
        private readonly List<Action<float>> _handlers = new();
        private int _iterating;
        private bool _hasHoles;

        public void Add(Action<float> handler)
        {
            if (handler != null) _handlers.Add(handler);
        }

        // 델리게이트의 -=와 같게, 가장 나중에 구독한 같은 핸들러 하나를 뺀다.
        public void Remove(Action<float> handler)
        {
            if (handler == null) return;

            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i] != handler) continue;

                if (_iterating > 0)
                {
                    _handlers[i] = null;
                    _hasHoles = true;
                }
                else
                {
                    _handlers.RemoveAt(i);
                }
                return;
            }
        }

        public void Invoke(float dt)
        {
            int count = _handlers.Count;
            _iterating++;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Action<float> handler = _handlers[i];
                    if (handler == null) continue;

                    try
                    {
                        handler(dt);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                _iterating--;
                if (_iterating == 0 && _hasHoles) Compact();
            }
        }

        private void Compact()
        {
            int write = 0;
            for (int read = 0; read < _handlers.Count; read++)
            {
                if (_handlers[read] != null) _handlers[write++] = _handlers[read];
            }

            _handlers.RemoveRange(write, _handlers.Count - write);
            _hasHoles = false;
        }
    }
}

/// <summary>
/// 이 클래스를 상속하면 OnEnable/OnDisable에서 루프 이벤트를 자동으로 구독·해제한다.
/// 필요한 갱신 메서드만 override 하면 된다 — <b>override한 채널만 구독한다.</b>
/// (OnUpdate만 쓰는 오브젝트가 빈 OnGameUpdate·OnLateUpdate까지 매 프레임 불리지 않도록)
///
/// OnEnable/OnDisable을 override한다면 반드시 base를 불러야 한다.
/// </summary>
public abstract class LoopMonoBehaviour : MonoBehaviour
{
    [Flags]
    private enum Channels
    {
        None       = 0,
        Update     = 1 << 0,
        GameUpdate = 1 << 1,
        LateUpdate = 1 << 2,
    }

    // 타입마다 어떤 갱신 메서드를 override했는지. 리플렉션은 타입당 한 번만 한다.
    private static readonly Dictionary<Type, Channels> _channelsByType = new();

    // 구독할 때마다 메서드 그룹에서 델리게이트가 새로 만들어지지 않도록 인스턴스당 한 번만 만든다.
    private Action<float> _onUpdate;
    private Action<float> _onGameUpdate;
    private Action<float> _onLateUpdate;

    protected virtual void OnEnable()
    {
        var loop = Core.Loop;
        if (loop == null) return;

        Channels channels = GetChannels(GetType());

        if ((channels & Channels.Update) != 0)     loop.OnUpdate     += _onUpdate     ??= OnUpdate;
        if ((channels & Channels.GameUpdate) != 0) loop.OnGameUpdate += _onGameUpdate ??= OnGameUpdate;
        if ((channels & Channels.LateUpdate) != 0) loop.OnLateUpdate += _onLateUpdate ??= OnLateUpdate;
    }

    protected virtual void OnDisable()
    {
        var loop = Core.Loop;
        if (loop == null) return;

        // 구독하지 않은 채널은 델리게이트가 null이라 해제도 건너뛴다.
        loop.OnUpdate     -= _onUpdate;
        loop.OnGameUpdate -= _onGameUpdate;
        loop.OnLateUpdate -= _onLateUpdate;
    }

    protected virtual void OnUpdate(float dt) { }
    protected virtual void OnGameUpdate(float dt) { }
    protected virtual void OnLateUpdate(float dt) { }

    private static Channels GetChannels(Type type)
    {
        if (_channelsByType.TryGetValue(type, out Channels channels)) return channels;

        channels = Channels.None;
        if (IsOverridden(type, nameof(OnUpdate)))     channels |= Channels.Update;
        if (IsOverridden(type, nameof(OnGameUpdate))) channels |= Channels.GameUpdate;
        if (IsOverridden(type, nameof(OnLateUpdate))) channels |= Channels.LateUpdate;

        _channelsByType[type] = channels;
        return channels;
    }

    private static bool IsOverridden(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(float) }, null);

        return method != null && method.DeclaringType != typeof(LoopMonoBehaviour);
    }
}
