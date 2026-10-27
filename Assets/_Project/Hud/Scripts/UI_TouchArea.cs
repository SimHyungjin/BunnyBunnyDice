using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 누르기·끌기·떼기를 이벤트로 내보내는 투명 영역. 맵을 직접 누르는 조작(탭 이동)을 받으려고 HUD 맨 아래에 화면 전체로 깐다
/// — 같은 이벤트 시스템을 거치므로 손패처럼 위에 있는 UI를 누르면 맵 입력으로 새지 않는다.
/// 투명 Image(Raycast Target)가 같은 오브젝트에 있어야 눌린다.
/// </summary>
public sealed class UI_TouchArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public event Action<PointerEventData> PointerDown;
    public event Action<PointerEventData> Dragged;
    public event Action<PointerEventData> PointerUp;

    public void OnPointerDown(PointerEventData eventData) => PointerDown?.Invoke(eventData);

    public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

    public void OnPointerUp(PointerEventData eventData) => PointerUp?.Invoke(eventData);
}
