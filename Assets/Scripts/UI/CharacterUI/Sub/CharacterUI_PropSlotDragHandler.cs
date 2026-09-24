using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CharacterUI_PropSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int SlotIndex { get; private set; }
    public event Action<int, PointerEventData> DragStarted;
    public event Action<int, PointerEventData> Dragging;
    public event Action<int, PointerEventData> DragEnded;

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        DragStarted?.Invoke(SlotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Dragging?.Invoke(SlotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragEnded?.Invoke(SlotIndex, eventData);
    }
}
