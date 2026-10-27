using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 속도를 한곳에서 정한다. 여러 곳이 각자 느리게 해 달라고 요청하면 그중 가장 느린 값을 쓴다.
/// 코어(LoopManager)는 배율을 정하는 함수만 있고 요청을 모으는 기능은 없다 — 요청을 어떻게 합칠지는 게임 규칙이라 여기 둔다.
/// 각자 배율을 저장했다 되돌리면, 카드를 끄는 중에 레벨업 선택이 끝나는 것처럼 순서가 엇갈릴 때 엉뚱한 속도로 돌아간다.
/// 곱하지 않고 가장 느린 값을 쓰는 이유: 둘 다 "50%"인 감속이 겹쳤다고 25%가 되면 의도보다 훨씬 느려진다.
/// 빨리 감기는 둘이다 — 봇(FastForward)과 플레이어의 배속 버튼(PlayerSpeed). 큰 쪽을 쓴다(봇을 켠 채 버튼을 눌러도 곱해지지 않게).
/// </summary>
public sealed class GameSpeed : IDisposable
{
    private readonly Dictionary<object, float> _requests = new Dictionary<object, float>();
    private float _fastForward = 1f;
    private float _playerSpeed = 1f;

    /// <summary>모은 감속에 곱하는 빨리 감기 배율(봇이 판을 빠르게 돌릴 때). 기본 1.</summary>
    public float FastForward
    {
        get => _fastForward;
        set
        {
            _fastForward = Mathf.Max(value, 0f);
            Apply();
        }
    }

    /// <summary>
    /// 플레이어가 고른 배속(배속 버튼). 감속이 걸려 있는 동안(레벨업 고르기·카드 끌기)은 빠진다 — 고르고 겨누는 시간은 배속과 상관없이 같아야 한다.
    /// </summary>
    public float PlayerSpeed
    {
        get => _playerSpeed;
        set
        {
            _playerSpeed = Mathf.Max(value, 0f);
            Apply();
        }
    }

    /// <summary>owner의 감속 요청을 넣거나 바꾼다(0~1).</summary>
    public void Request(object owner, float scale)
    {
        _requests[owner] = scale;
        Apply();
    }

    public void Release(object owner)
    {
        if (_requests.Remove(owner)) Apply();
    }

    /// <summary>씬을 벗어날 때 느려진 채로 남지 않게 되돌린다.</summary>
    public void Dispose()
    {
        _requests.Clear();
        _fastForward = 1f;
        _playerSpeed = 1f;
        Apply();
    }

    private void Apply()
    {
        float scale = 1f;
        foreach (float request in _requests.Values) scale = Mathf.Min(scale, request);
        // 코어의 배율 설정을 거친다(월드·플레이어 배율을 같이 맞추고 범위를 자른다). 영웅도 몬스터와 같이 느려지는 것이 기획이다.
        float player = scale < 1f ? 1f : _playerSpeed;
        Core.Loop?.SetGameSpeed(scale * Mathf.Max(_fastForward, player));
    }
}
