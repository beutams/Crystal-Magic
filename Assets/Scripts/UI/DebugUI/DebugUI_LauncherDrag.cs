using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DebugUI_LauncherDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private bool _wasDragged;

    public event Action<PointerEventData> DragStarted;
    public event Action<PointerEventData> Dragging;
    public event Action<PointerEventData> DragEnded;

    public void OnPointerDown(PointerEventData eventData)
    {
        _wasDragged = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _wasDragged = true;
        DragStarted?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_wasDragged)
            Dragging?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_wasDragged)
            DragEnded?.Invoke(eventData);
    }

    public bool ConsumeDrag()
    {
        bool wasDragged = _wasDragged;
        _wasDragged = false;
        return wasDragged;
    }
}
