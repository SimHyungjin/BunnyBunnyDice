using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코어가 쓰는 최소 트윈. 값 하나를 시간에 따라 보간하고, 핸들로 중단·완료 대기를 한다.
///
/// 외부 트윈 라이브러리(PrimeTween 등)를 코어에서 걷어내려고 직접 갖는다.
/// 이유는 편의가 아니라 이식성이다 — UniTask·Addressables는 레지스트리에서 자동으로 받아지지만
/// 유료 에셋은 그럴 수 없어서, 코어를 다른 프로젝트로 옮길 때 유일하게 수작업으로 남는 의존이었다.
/// 게임 코드에서 외부 트윈을 쓰는 건 자유다. 코어만 의존하지 않으면 된다.
///
/// 대상 종류를 델리게이트가 아니라 열거형으로 분기하는 이유: 트윈마다 클로저를 만들지 않으려는 것과,
/// 대상 UnityEngine.Object를 그대로 들고 있어야 파괴된 오브젝트를 매 프레임 걸러낼 수 있기 때문이다
/// (씬 전환으로 사라진 UI의 트윈이 이 검사에서 자동 정리된다).
///
/// 매 프레임 갱신은 <see cref="LoopManager.OnUpdate"/>(unscaled)에 얹는다.
/// 시간 배율을 따라야 하는 트윈은 <c>useUnscaledTime: false</c>로 월드 배율을 곱한다.
///
/// 같은 대상의 같은 속성(예: 같은 Transform의 스케일)에 새 트윈을 걸면 기존 것은 중단된다.
/// 둘이 함께 돌면 매 프레임 번갈아 값을 써서 튀기 때문이다.
/// </summary>
public readonly struct CoreTween : System.IEquatable<CoreTween>
{
    private readonly int _slot;
    private readonly int _version;

    // 핸들을 서로 비교할 일이 있다(같은 대상에 새 연출이 걸렸는지 판정 등).
    // 직접 구현하지 않으면 ValueType.Equals가 리플렉션으로 비교하며 박싱까지 일어난다.
    public bool Equals(CoreTween other) => _slot == other._slot && _version == other._version;
    public override bool Equals(object obj) => obj is CoreTween other && Equals(other);
    public override int GetHashCode() => (_slot * 397) ^ _version;

    public static bool operator ==(CoreTween a, CoreTween b) => a.Equals(b);
    public static bool operator !=(CoreTween a, CoreTween b) => !a.Equals(b);

    private CoreTween(int slot, int version)
    {
        _slot = slot;
        _version = version;
    }

    #region 핸들

    /// <summary>아직 진행 중인가. 기본값 핸들(default)은 항상 false다.</summary>
    public bool isAlive => Runner.IsAlive(_slot, _version);

    /// <summary>진행 중이면 즉시 중단한다. 완료로 치지 않으므로 OnComplete는 호출되지 않는다.</summary>
    public void Stop() => Runner.Stop(_slot, _version);

    /// <summary>
    /// 정상 완료 시 한 번 호출될 콜백을 건다. <see cref="Stop"/>으로 끊긴 경우엔 호출되지 않는다.
    /// 중단됐는데 완료 후처리(패널 비활성화 등)가 돌면 안 되는 곳이 있어 이 구분을 유지한다.
    /// </summary>
    public CoreTween OnComplete(Action action)
    {
        Runner.SetOnComplete(_slot, _version, action);
        return this;
    }

    /// <summary>
    /// <c>await tween</c> 지원. 완료든 중단이든 끝나면 깨어나며 예외를 던지지 않는다.
    /// 중단됐을 때 await가 영원히 멈춰 있으면 호출부의 정리 코드(finally)가 실행되지 않기 때문이다.
    ///
    /// 결과는 정상 완료면 true, 중단(Stop·대상 파괴·같은 대상에 새 트윈)이면 false다.
    /// "연출이 끝나면 닫는다" 같은 후속 처리는 false일 때 건너뛰어야 한다 — 중단은 대개 다른 흐름이 끼어들었다는 뜻이다.
    /// 이미 끝난 핸들(기본값 포함)을 기다리면 곧바로 true다.
    /// </summary>
    public UniTask<bool>.Awaiter GetAwaiter() => Runner.WaitAsync(_slot, _version).GetAwaiter();

    #endregion

    #region 생성

    public static CoreTween Scale(Transform target, Vector3 endValue, float duration,
                                  CoreEase ease = CoreEase.OutQuad, bool useUnscaledTime = false)
        => Runner.BeginVector3(Runner.Kind.TransformScale, target,
                               target != null ? target.localScale : default,
                               endValue, duration, ease, useUnscaledTime);

    public static CoreTween LocalPosition(Transform target, Vector3 endValue, float duration,
                                          CoreEase ease = CoreEase.OutQuad, bool useUnscaledTime = false)
        => Runner.BeginVector3(Runner.Kind.TransformLocalPosition, target,
                               target != null ? target.localPosition : default,
                               endValue, duration, ease, useUnscaledTime);

    /// <summary>
    /// RectTransform을 anchoredPosition으로 움직인다. <b>UI는 이쪽을 써야 한다.</b>
    /// stretch 앵커를 가진 RectTransform에 <see cref="LocalPosition"/>을 쓰면,
    /// 레이아웃이 다시 계산될 때 anchoredPosition 기준으로 위치가 되돌아가 애니메이션이 무시된다.
    /// </summary>
    public static CoreTween AnchoredPosition(RectTransform target, Vector2 endValue, float duration,
                                             CoreEase ease = CoreEase.OutQuad, bool useUnscaledTime = false)
        => Runner.BeginAnchoredPosition(target, endValue, duration, ease, useUnscaledTime);

    public static CoreTween Alpha(CanvasGroup target, float endValue, float duration,
                                  CoreEase ease = CoreEase.OutQuad, bool useUnscaledTime = false)
        => Runner.BeginCanvasGroup(target, endValue, duration, ease, useUnscaledTime);

    public static CoreTween Alpha(Graphic target, float endValue, float duration,
                                  CoreEase ease = CoreEase.OutQuad, bool useUnscaledTime = false)
        => Runner.BeginGraphic(target, endValue, duration, ease, useUnscaledTime);

    public static CoreTween AudioVolume(AudioSource target, float endValue, float duration,
                                        CoreEase ease = CoreEase.OutQuad, bool useUnscaledTime = false)
        => Runner.BeginAudio(target, endValue, duration, ease, useUnscaledTime);

    #endregion

    /// <summary>
    /// 실행 중인 트윈을 담아 두고 매 프레임 굴리는 곳.
    /// 슬롯은 재사용하고, 죽을 때 버전을 올려 예전 핸들을 무효화한다.
    /// </summary>
    private static class Runner
    {
        internal enum Kind
        {
            TransformScale,
            TransformLocalPosition,
            RectAnchoredPosition,
            CanvasGroupAlpha,
            GraphicAlpha,
            AudioVolume,
        }

        private sealed class Slot
        {
            public int  Version;   // 살아있는 핸들과 대조할 일련번호. 죽을 때마다 올라간다.
            public bool Active;

            public Kind          Kind;
            public Transform     Tr;
            public RectTransform Rt;
            public CanvasGroup Cg;
            public Graphic     Gr;
            public AudioSource Au;

            public Vector3 FromV3, ToV3;
            public float   FromF,  ToF;

            public float    Duration, Elapsed;
            public CoreEase Ease;
            public bool     Unscaled;

            public Action                  OnComplete;
            public UniTaskCompletionSource<bool> Tcs;   // await하는 쪽이 생겼을 때만 만든다. 결과: 완료 true / 중단 false
        }

        private static readonly List<Slot> _slots = new();

        // 후킹할 때 잡아 둔다. Core.Loop은 매번 레지스트리를 뒤지므로 매 프레임 부르지 않는다.
        private static LoopManager _loop;

        /// <summary>
        /// 도메인 리로드를 끈 에디터에서도 플레이 진입마다 초기 상태로 돌아가게 한다.
        /// 지난 재생의 죽은 LoopManager에 후킹된 채로 남으면 트윈이 영원히 멈춘다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _slots.Clear();
            _loop = null;
        }

        #region 생성

        internal static CoreTween BeginVector3(Kind kind, Transform target, Vector3 from, Vector3 to,
                                               float duration, CoreEase ease, bool unscaled)
        {
            if (target == null) return default;

            KillConflicting(kind, target);
            Slot s = Take(out int index);
            s.Kind   = kind;
            s.Tr     = target;
            s.FromV3 = from;
            s.ToV3   = to;
            return Start(s, index, duration, ease, unscaled);
        }

        internal static CoreTween BeginAnchoredPosition(RectTransform target, Vector2 to, float duration,
                                                        CoreEase ease, bool unscaled)
        {
            if (target == null) return default;

            KillConflicting(Kind.RectAnchoredPosition, target);
            Slot s = Take(out int index);
            s.Kind   = Kind.RectAnchoredPosition;
            s.Rt     = target;
            s.FromV3 = target.anchoredPosition;
            s.ToV3   = to;
            return Start(s, index, duration, ease, unscaled);
        }

        internal static CoreTween BeginCanvasGroup(CanvasGroup target, float to, float duration,
                                                   CoreEase ease, bool unscaled)
        {
            if (target == null) return default;

            KillConflicting(Kind.CanvasGroupAlpha, target);
            Slot s = Take(out int index);
            s.Kind  = Kind.CanvasGroupAlpha;
            s.Cg    = target;
            s.FromF = target.alpha;
            s.ToF   = to;
            return Start(s, index, duration, ease, unscaled);
        }

        internal static CoreTween BeginGraphic(Graphic target, float to, float duration,
                                               CoreEase ease, bool unscaled)
        {
            if (target == null) return default;

            KillConflicting(Kind.GraphicAlpha, target);
            Slot s = Take(out int index);
            s.Kind  = Kind.GraphicAlpha;
            s.Gr    = target;
            s.FromF = target.color.a;
            s.ToF   = to;
            return Start(s, index, duration, ease, unscaled);
        }

        internal static CoreTween BeginAudio(AudioSource target, float to, float duration,
                                             CoreEase ease, bool unscaled)
        {
            if (target == null) return default;

            KillConflicting(Kind.AudioVolume, target);
            Slot s = Take(out int index);
            s.Kind  = Kind.AudioVolume;
            s.Au    = target;
            s.FromF = target.volume;
            s.ToF   = to;
            return Start(s, index, duration, ease, unscaled);
        }

        /// <summary>같은 대상·같은 속성에 걸린 살아 있는 트윈을 중단한다.</summary>
        private static void KillConflicting(Kind kind, UnityEngine.Object target)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (s.Active && s.Kind == kind && ReferenceEquals(TargetOf(s), target)) Kill(s, false);
            }
        }

        private static UnityEngine.Object TargetOf(Slot s)
        {
            switch (s.Kind)
            {
                case Kind.TransformScale:
                case Kind.TransformLocalPosition: return s.Tr;
                case Kind.RectAnchoredPosition:   return s.Rt;
                case Kind.CanvasGroupAlpha:       return s.Cg;
                case Kind.GraphicAlpha:           return s.Gr;
                case Kind.AudioVolume:            return s.Au;
                default:                          return null;
            }
        }

        /// <summary>
        /// 죽어 있는 슬롯을 재활용한다. 없으면 새로 만든다.
        /// 버전을 1부터 시작하는 이유: 기본값 핸들(slot 0, version 0)이 0번 슬롯과
        /// 우연히 맞아떨어져 살아있는 것으로 보이면 안 된다.
        /// </summary>
        private static Slot Take(out int index)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Active) continue;

                index = i;
                return _slots[i];
            }

            var slot = new Slot { Version = 1 };
            index = _slots.Count;
            _slots.Add(slot);
            return slot;
        }

        private static CoreTween Start(Slot s, int index, float duration, CoreEase ease, bool unscaled)
        {
            s.Duration   = duration;
            s.Elapsed    = 0f;
            s.Ease       = ease;
            s.Unscaled   = unscaled;
            s.OnComplete = null;
            s.Tcs        = null;
            s.Active     = true;

            EnsureHooked();
            return new CoreTween(index, s.Version);
        }

        /// <summary>
        /// 첫 트윈이 생길 때 루프에 붙는다. 코어 부팅 전이라 LoopManager가 아직 없으면
        /// 다음 생성 때 다시 시도한다(그때 밀려 있던 트윈들도 함께 돌기 시작한다).
        /// </summary>
        private static void EnsureHooked()
        {
            if (_loop != null) return;

            LoopManager loop = Core.Loop;
            if (loop == null) return;

            loop.OnUpdate += Tick;
            _loop = loop;
        }

        #endregion

        #region 갱신

        private static void Tick(float unscaledDt)
        {
            // 완료 콜백이 새 트윈을 만들 수 있다. 이번 프레임에 추가된 것까지 굴리지 않도록 개수를 먼저 고정한다.
            int count = _slots.Count;
            float worldScale = _loop != null ? _loop.WorldTimeScale : 1f;

            for (int i = 0; i < count; i++)
            {
                Slot s = _slots[i];
                if (!s.Active) continue;

                // 대상이 파괴됐으면 조용히 접는다. 씬 전환으로 사라진 UI의 트윈이 여기서 정리된다.
                if (!IsTargetAlive(s))
                {
                    Kill(s, false);
                    continue;
                }

                s.Elapsed += s.Unscaled ? unscaledDt : unscaledDt * worldScale;

                float t = s.Duration > 0f ? Mathf.Clamp01(s.Elapsed / s.Duration) : 1f;
                Apply(s, s.Ease.Evaluate(t));

                if (t >= 1f) Kill(s, true);
            }
        }

        private static bool IsTargetAlive(Slot s)
        {
            switch (s.Kind)
            {
                case Kind.TransformScale:
                case Kind.TransformLocalPosition: return s.Tr != null;
                case Kind.RectAnchoredPosition:   return s.Rt != null;
                case Kind.CanvasGroupAlpha:       return s.Cg != null;
                case Kind.GraphicAlpha:           return s.Gr != null;
                case Kind.AudioVolume:            return s.Au != null;
                default:                          return false;
            }
        }

        /// <summary>
        /// 보간값을 대상에 쓴다. Back 계열은 0~1을 넘기므로(의도된 오버슈트) 스케일·위치는 언클램프로 살리고,
        /// 넘치면 곤란한 알파·볼륨만 잘라 낸다.
        /// </summary>
        private static void Apply(Slot s, float e)
        {
            switch (s.Kind)
            {
                case Kind.TransformScale:
                    s.Tr.localScale = Vector3.LerpUnclamped(s.FromV3, s.ToV3, e);
                    break;

                case Kind.TransformLocalPosition:
                    s.Tr.localPosition = Vector3.LerpUnclamped(s.FromV3, s.ToV3, e);
                    break;

                case Kind.RectAnchoredPosition:
                    s.Rt.anchoredPosition = Vector2.LerpUnclamped(s.FromV3, s.ToV3, e);
                    break;

                case Kind.CanvasGroupAlpha:
                    s.Cg.alpha = Mathf.Clamp01(Mathf.LerpUnclamped(s.FromF, s.ToF, e));
                    break;

                case Kind.GraphicAlpha:
                {
                    Color c = s.Gr.color;
                    c.a = Mathf.Clamp01(Mathf.LerpUnclamped(s.FromF, s.ToF, e));
                    s.Gr.color = c;
                    break;
                }

                case Kind.AudioVolume:
                    s.Au.volume = Mathf.Clamp01(Mathf.LerpUnclamped(s.FromF, s.ToF, e));
                    break;
            }
        }

        /// <summary>
        /// 슬롯을 죽이고 핸들을 무효화한다.
        /// 완료 콜백은 정상 완료일 때만 부르고, await는 어느 쪽이든 깨운다(결과로 완료 여부를 넘긴다).
        /// 콜백·대기 쪽이 던져도 다른 트윈의 갱신은 계속돼야 하므로 각각 감싼다.
        /// </summary>
        private static void Kill(Slot s, bool completed)
        {
            s.Active = false;
            s.Version++;

            Action onComplete = s.OnComplete;
            UniTaskCompletionSource<bool> tcs = s.Tcs;

            // 대상 참조를 끊어 둔다. 죽은 슬롯이 파괴 대기 중인 오브젝트를 붙잡고 있을 이유가 없다.
            s.OnComplete = null;
            s.Tcs = null;
            s.Tr  = null;
            s.Rt  = null;
            s.Cg  = null;
            s.Gr  = null;
            s.Au  = null;

            if (completed && onComplete != null)
            {
                try
                {
                    onComplete();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (tcs != null)
            {
                try
                {
                    tcs.TrySetResult(completed);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        #endregion

        #region 핸들 조회

        internal static bool IsAlive(int slot, int version)
        {
            if (slot < 0 || slot >= _slots.Count) return false;

            Slot s = _slots[slot];
            return s.Active && s.Version == version;
        }

        internal static void Stop(int slot, int version)
        {
            if (!IsAlive(slot, version)) return;
            Kill(_slots[slot], false);
        }

        internal static void SetOnComplete(int slot, int version, Action action)
        {
            if (!IsAlive(slot, version)) return;
            _slots[slot].OnComplete = action;
        }

        internal static UniTask<bool> WaitAsync(int slot, int version)
        {
            if (!IsAlive(slot, version)) return UniTask.FromResult(true);

            Slot s = _slots[slot];
            s.Tcs ??= new UniTaskCompletionSource<bool>();
            return s.Tcs.Task;
        }

        #endregion
    }
}
