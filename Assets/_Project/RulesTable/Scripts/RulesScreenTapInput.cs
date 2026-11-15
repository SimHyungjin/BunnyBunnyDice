using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 규칙 화면 탭 누르기. 화면 전체를 덮은 HUD 터치 영역(UI_TouchArea)이 누름을 먼저 받으므로 같은 누름을 받아
/// 책상 화면의 탭 위인지 묻는다 — 화면 캔버스에 레이캐스터를 따로 달면 HUD 터치 영역과 순서를 다툰다.
/// 누른 탭과 뗀 탭이 같고 크게 끌지 않았을 때만 누른 것으로 본다(판을 끌다 놓은 손가락이 탭을 누르지 않게).
/// 탭은 판 밖(책상 위)이라 영웅 이동(HeroTapInput)과 같은 누름을 받아도 겹치지 않는다.
/// </summary>
public sealed class RulesScreenTapInput : IDisposable
{
    private readonly UI_TouchArea _area;
    private readonly RulesScreenView _screen;
    private readonly SO_RulesTableConfig _config;
    private readonly Func<Camera> _getCamera;

    private const int NoPointer = int.MinValue;
    private int _pointerId = NoPointer;
    private int _tab = -1;
    private Vector2 _downPosition;

    public RulesScreenTapInput(UI_TouchArea area, RulesScreenView screen, SO_RulesTableConfig config, Func<Camera> getCamera)
    {
        _area = area;
        _screen = screen;
        _config = config;
        _getCamera = getCamera;
        _area.PointerDown += OnPointerDown;
        _area.PointerUp += OnPointerUp;
    }

    public void Dispose()
    {
        _area.PointerDown -= OnPointerDown;
        _area.PointerUp -= OnPointerUp;
    }

    private void OnPointerDown(PointerEventData eventData)
    {
        if (_pointerId != NoPointer) return;
        if (!_screen.TryPickTab(eventData.position, _getCamera(), out _tab)) return;
        _pointerId = eventData.pointerId;
        _downPosition = eventData.position;
    }

    private void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId) return;
        _pointerId = NoPointer;
        float slop = _config.TapSlop * Mathf.Min(Screen.width, Screen.height);
        if ((eventData.position - _downPosition).sqrMagnitude > slop * slop) return;
        if (_screen.TryPickTab(eventData.position, _getCamera(), out int tab) && tab == _tab) _screen.ShowPage(tab);
    }
}
