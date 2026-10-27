using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 남은 불길. 자리에 잠시 남아, 안에 있는 몬스터에게 일정 간격으로 피해를 준다.
/// 피해는 MonsterField를 거쳐 방어력이 적용된다(효과라 치명타는 없다). 세기·시간·간격은 놓는 쪽이 넘긴다.
/// 지금은 쓰는 곳이 없다 — 2026-10-26 주사위 개편으로 폭발 각성에서 뺐고, 나중에 스킬로 옮길 때 다시 잇는다(기획서 9.4).
/// </summary>
public sealed class FireZones : IDisposable
{
    private struct Zone
    {
        public Vector2 Center;
        public float Radius;
        public float Dps;
        public float Interval;
        public float Remaining;
        public float Tick;
    }

    private readonly MonsterField _monsters;
    private readonly AttackEffects _effects;
    private readonly List<Zone> _zones = new List<Zone>();

    public FireZones(MonsterField monsters, AttackEffects effects)
    {
        _monsters = monsters;
        _effects = effects;
        Core.Loop.OnUpdate += Tick;
    }

    public void Dispose() => Core.Loop.OnUpdate -= Tick;

    /// <param name="interval">피해를 주는 간격.</param>
    /// <param name="ground">불길을 그릴 바닥 윗면 높이.</param>
    public void Add(Vector2 center, float radius, float dps, float duration, float interval, Color color, float ground)
    {
        if (dps <= 0f || radius <= 0f || duration <= 0f || interval <= 0f) return;
        _zones.Add(new Zone { Center = center, Radius = radius, Dps = dps, Interval = interval, Remaining = duration });
        _effects.ShowRing(new Vector3(center.x, ground, center.y), radius, color, duration);
    }

    public void Clear() => _zones.Clear();

    // 게임 시간을 따른다. 몬스터가 멈추면(게임 오버) 불길도 멈춘다.
    private void Tick(float _)
    {
        if (_zones.Count == 0 || _monsters.Paused) return;
        float deltaTime = Time.deltaTime;
        MonsterState[] states = _monsters.States;

        for (int z = _zones.Count - 1; z >= 0; z--)
        {
            Zone zone = _zones[z];
            // 남은 시간보다 길게 쌓지 않아, 꺼지는 순간 남은 몫까지 준다(독과 같은 방식).
            zone.Tick += Mathf.Min(deltaTime, zone.Remaining);
            zone.Remaining -= deltaTime;
            float radiusSqr = zone.Radius * zone.Radius;
            while (zone.Tick >= zone.Interval)
            {
                zone.Tick -= zone.Interval;
                for (int i = 0; i < _monsters.Count; i++)
                {
                    if (!states[i].IsAlive || (states[i].Position - zone.Center).sqrMagnitude > radiusSqr) continue;
                    _monsters.HitEffect(i, zone.Dps * zone.Interval, 0f, DamageKind.Fire, DamageSource.Fire);
                }
            }

            if (zone.Remaining <= 0f)
            {
                _zones[z] = _zones[_zones.Count - 1];
                _zones.RemoveAt(_zones.Count - 1);
                continue;
            }
            _zones[z] = zone;
        }
    }
}
