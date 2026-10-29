using System;
using UnityEngine;

/// <summary>
/// HUD 보스 체력 바를 채운다(기획서 9.4). 살아 있는 보스가 있으면 띄우고 없으면 숨긴다.
/// 보스는 무리 맨 뒤에 섞여 오므로, 몸 위의 작은 체력 바만으로는 얼마나 남았는지 읽기 어렵다.
/// 체력 숫자(정수)가 바뀔 때만 HUD를 고친다 — 매 프레임 글을 새로 쓰지 않게.
/// </summary>
public sealed class BossHealthBar : IDisposable
{
    private readonly MonsterField _monsters;
    private readonly UI_GameHud _hud;
    private bool _shown;
    private int _health = -1;
    private int _max = -1;
    private bool _jackpot;

    public BossHealthBar(MonsterField monsters, UI_GameHud hud)
    {
        _monsters = monsters;
        _hud = hud;
        Core.Loop.OnLateUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnLateUpdate -= Tick;

    private void Tick(float _)
    {
        MonsterState[] states = _monsters.States;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (!states[i].IsAlive) continue;
            MonsterKind kind = _monsters.KindOf(i);
            if (!kind.IsBoss || kind.IsPiece) continue;
            Show(Mathf.CeilToInt(states[i].Health), Mathf.CeilToInt(states[i].MaxHealth), kind.IsJackpot);
            return;
        }
        if (!_shown) return;
        _shown = false;
        _health = -1;
        _hud.HideBoss();
    }

    private void Show(int health, int max, bool jackpot)
    {
        if (_shown && health == _health && max == _max && jackpot == _jackpot) return;
        _shown = true;
        _health = health;
        _max = max;
        _jackpot = jackpot;
        _hud.ShowBoss(health, max, jackpot);
    }
}
