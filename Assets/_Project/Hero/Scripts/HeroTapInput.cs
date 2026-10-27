using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 맵 누르기 → 영웅 목적지. 누른 채 끌면 목적지가 손가락을 따라간다.
/// </summary>
public sealed class HeroTapInput : IDisposable
{
    private readonly UI_TouchArea _area;
    private readonly Hero _hero;
    private readonly MapPicker _picker;
    private readonly Func<MapData> _getMap;
    private readonly Func<Camera> _getCamera;
    private readonly Func<bool> _blocked;

    private const int NoPointer = int.MinValue;
    private int _pointerId = NoPointer;

    /// <param name="blocked">참이면 맵 탭을 받지 않는다(카드를 들고 있을 때 — CardDragController.Busy).</param>
    public HeroTapInput(UI_TouchArea area, Hero hero, MapPicker picker, Func<MapData> getMap, Func<Camera> getCamera, Func<bool> blocked)
    {
        _area = area;
        _hero = hero;
        _picker = picker;
        _getMap = getMap;
        _getCamera = getCamera;
        _blocked = blocked;

        _area.PointerDown += OnPointerDown;
        _area.Dragged += OnDragged;
        _area.PointerUp += OnPointerUp;
    }

    public void Dispose()
    {
        _area.PointerDown -= OnPointerDown;
        _area.Dragged -= OnDragged;
        _area.PointerUp -= OnPointerUp;
    }

    // 손가락 하나만 따른다 — 두 번째 손가락이 맵에 닿아도 목적지가 튀지 않게.
    private void OnPointerDown(PointerEventData eventData)
    {
        if (_pointerId != NoPointer || _blocked()) return;
        _pointerId = eventData.pointerId;
        Aim(eventData.position);
    }

    private void OnDragged(PointerEventData eventData)
    {
        if (eventData.pointerId == _pointerId && !_blocked()) Aim(eventData.position);
    }

    private void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == _pointerId) _pointerId = NoPointer;
    }

    private void Aim(Vector2 screenPosition)
    {
        MapData map = _getMap();
        if (_picker.TryScreenToCell(_getCamera(), map, screenPosition, out Vector2Int cell)) _hero.MoveTo(cell);
    }
}
