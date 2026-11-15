using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 규칙 책상 윗면에 화면 UI(UI_RulesScreen, 월드 공간 캔버스)를 깔고 장을 넘긴다.
/// 상판이 세로로 길어 화면도 세로로 길게 깔면 글자를 크게 쓸 수 있다(사용자 2026-11-12). 화면은 펠트 판을 꽉 채운다 —
/// 책상 네온 테두리가 곧 화면 테두리가 되게. UI 안은 모두 비율 앵커라 판 비율이 맵마다 달라도 그대로 맞는다.
///
/// 장은 PageInterval마다 다음 장으로 밀리고(마지막 → 처음도 같은 방향), 탭을 누르면 그 장으로 밀린 뒤 ManualHoldTime 동안 머문다.
/// 넘김은 실제 시간이다(판 멈춤과 상관없이 넘어간다).
/// </summary>
public sealed class RulesScreenView : IDisposable
{
    private readonly SO_RulesTableConfig _config;
    private readonly SO_DiceConfig _dice;
    private readonly SO_DiceStatsConfig _stats;
    private UI_RulesScreen _screen;
    private float _pageTime;
    private float _hold;

    public RulesScreenView(SO_RulesTableConfig config, SO_DiceConfig dice, SO_DiceStatsConfig stats)
    {
        _config = config;
        _dice = dice;
        _stats = stats;
        Core.Loop.OnUpdate += Tick;
        _dice.Changed += Refill;
        _stats.Changed += Refill;
    }

    public void Dispose()
    {
        Core.Loop.OnUpdate -= Tick;
        _dice.Changed -= Refill;
        _stats.Changed -= Refill;
        Clear();
    }

    public void Clear()
    {
        if (_screen != null) Object.Destroy(_screen.gameObject);
        _screen = null;
    }

    /// <param name="surface">상판 윗면 가운데(부모 기준).</param>
    /// <param name="area">화면이 채울 영역(가로 = 당구대에서 멀어지는 쪽, 세로 = 당구대 긴 변을 따라) — 만든 책상이면 펠트 판.</param>
    public void Build(Transform parent, Vector3 surface, Vector2 area)
    {
        int page = _screen != null ? _screen.Current : 0;
        Clear();
        if (_config.ScreenPrefab == null || area.x <= 0f || area.y <= 0f) return;

        _screen = Object.Instantiate(_config.ScreenPrefab, parent, false);
        float tilt = _config.ScreenTilt * Mathf.Deg2Rad;
        // 기울이면 상판 위에서 차지하는 세로가 cos만큼 주므로 화면 세로를 그만큼 늘린다.
        float width = area.x;
        float height = area.y / Mathf.Max(Mathf.Cos(tilt), 0.01f);
        float reference = Mathf.Max(_screen.ReferenceWidth, 1f);
        RectTransform rect = (RectTransform)_screen.transform;
        rect.sizeDelta = new Vector2(reference, reference * height / width);
        rect.localScale = Vector3.one * (width / reference);
        // 캔버스는 -Z를 본다. X축으로 (90° − 기울기) 돌리면 위를 보고, 화면 위쪽이 먼 쪽(+Z)으로 가며 먼 쪽이 들린다.
        rect.localRotation = Quaternion.Euler(90f - _config.ScreenTilt, 0f, 0f);
        rect.localPosition = surface + Vector3.up * (_config.ScreenLift + height * 0.5f * Mathf.Sin(tilt));

        _screen.Init(_config, _dice, _stats, Core.Camera.MainCamera);
        _screen.Select(page, false, 0f);
    }

    /// <summary>화면 좌표가 누른 탭(없으면 -1).</summary>
    public bool TryPickTab(Vector2 screenPosition, Camera camera, out int index)
    {
        index = -1;
        return _screen != null && _screen.TryPickTab(screenPosition, camera, out index);
    }

    /// <summary>탭을 눌렀다: 그 장으로 밀고 한동안 저절로 넘기지 않는다.</summary>
    public void ShowPage(int index)
    {
        if (_screen == null) return;
        _screen.Select(index, true, 0f);
        _pageTime = 0f;
        _hold = _config.ManualHoldTime;
    }

    private void Refill()
    {
        if (_screen != null) _screen.Fill();
    }

    private void Tick(float deltaTime)
    {
        if (_screen == null) return;
        _screen.Tick(deltaTime);
        if (_screen.PageCount < 2) return;
        if (_hold > 0f)
        {
            _hold -= deltaTime;
            return;
        }
        _pageTime += deltaTime;
        if (_pageTime < _config.PageInterval) return;
        _pageTime = 0f;
        // 마지막 장 다음은 처음 장이지만 같은 방향(오른쪽에서 들어옴)으로 민다 — 도는 느낌이 나게.
        _screen.Select((_screen.Current + 1) % _screen.PageCount, true, 1f);
    }
}
