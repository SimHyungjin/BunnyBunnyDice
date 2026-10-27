using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 판 치우기 연출(다시 하기·로비로 — 판 주사위 연출의 반대). 판 위 주사위·칩의 표시를 넘겨받아 하나씩 하늘로 날려 보내고 지운다.
/// 판 상태는 넘겨준 즉시 새 판으로 바뀐다 — 날아가는 것은 주인 없는 표시뿐이라 판 규칙과 섞이지 않는다.
/// 맵 크기·목표물 자리는 판마다 같고 시작 칩·포켓만 바뀌므로, 물건이 다 날아간 판은 새 판과 거의 같아 보인다(바뀌는 순간을 가릴 필요가 없다).
/// 무작위 차례로 날아간다(한쪽부터 쓸려 나가지 않게). 실제 시간으로 진행한다.
/// </summary>
public sealed class BoardSweep : IDisposable
{
    private sealed class Item
    {
        public Transform Body;
        public Action Dispose;
        public Vector3 Start;
        public Quaternion StartRotation;
        public Vector3 SpinAxis;
        public float At;
    }

    private readonly SO_BoardRollConfig _config;
    private readonly List<Item> _items = new List<Item>();
    private readonly System.Random _random = new System.Random();
    private float _time = -1f;
    private Action _then;

    public BoardSweep(SO_BoardRollConfig config)
    {
        _config = config;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        for (int i = 0; i < _items.Count; i++) _items[i].Dispose?.Invoke();
        _items.Clear();
        _then = null;
    }

    /// <summary>날려 보내는 중이다.</summary>
    public bool IsPlaying => _time >= 0f;

    /// <summary>날려 보낼 표시를 넘겨받는다. dispose는 다 날아간 뒤(또는 연출이 끊기면) 부른다.</summary>
    public void Add(Transform body, Action dispose)
    {
        if (body == null)
        {
            dispose?.Invoke();
            return;
        }
        // 자리에서 날아오를 때 기준. 이미 날던 것(연달아 다시 하기)도 지금 자리에서 다시 출발한다.
        Vector3 axis = new Vector3((float)_random.NextDouble() - 0.5f, 0f, (float)_random.NextDouble() - 0.5f);
        _items.Add(new Item
        {
            Body = body,
            Dispose = dispose,
            Start = body.localPosition,
            StartRotation = body.localRotation,
            SpinAxis = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.right,
            At = (float)_random.NextDouble() * _config.SweepSpread + (_time >= 0f ? _time : 0f),
        });
        if (_time < 0f) _time = 0f;
    }

    /// <summary>
    /// 다 날아가면 부를 일(다시 하기의 판 주사위 연출). 날릴 것이 없으면 바로 부른다. 다시 부르면 앞의 것을 바꾼다(연달아 다시 하기).
    /// null이면 기다리던 일을 지운다.
    /// </summary>
    public void Then(Action then)
    {
        if (then != null && !IsPlaying)
        {
            _then = null;
            then();
            return;
        }
        _then = then;
    }

    private void Tick(float deltaTime)
    {
        if (_time < 0f) return;
        _time += deltaTime;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            Item item = _items[i];
            float u = _config.SweepDuration > 0f ? Mathf.Clamp01((_time - item.At) / _config.SweepDuration) : 1f;
            if (u >= 1f || item.Body == null)
            {
                item.Dispose?.Invoke();
                _items.RemoveAt(i);
                continue;
            }
            if (u <= 0f) continue;
            // 처음엔 천천히 떠올라 점점 빨라진다(빨려 올라가는 느낌). 돌면서 작아지지 않고 화면 위로 빠져나간다.
            item.Body.localPosition = item.Start + Vector3.up * (_config.SweepHeight * u * u);
            item.Body.localRotation = Quaternion.AngleAxis(_config.SweepSpin * u, item.SpinAxis) * item.StartRotation;
        }
        if (_items.Count > 0) return;
        _time = -1f;
        Action then = _then;
        _then = null;
        then?.Invoke();
    }
}
