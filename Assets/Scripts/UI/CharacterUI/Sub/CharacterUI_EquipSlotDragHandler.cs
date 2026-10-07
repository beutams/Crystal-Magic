using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Authored on the existing five equipment slots; no runtime component creation.
public sealed class CharacterUI_EquipSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int SlotIndex { get; private set; }
    public event Action<int, PointerEventData> DragStarted;
    public event Action<int, PointerEventData> Dragging;
    public event Action<int, PointerEventData> DragEnded;
    public event Action<int, bool> HoverChanged;

    public void Initialize(int slotIndex) => SlotIndex = slotIndex;
    public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(SlotIndex, true);
    public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(SlotIndex, false);
    private void OnDisable() => HoverChanged?.Invoke(SlotIndex, false);
    public void OnBeginDrag(PointerEventData eventData) => DragStarted?.Invoke(SlotIndex, eventData);
    public void OnDrag(PointerEventData eventData) => Dragging?.Invoke(SlotIndex, eventData);
    public void OnEndDrag(PointerEventData eventData) => DragEnded?.Invoke(SlotIndex, eventData);
}
