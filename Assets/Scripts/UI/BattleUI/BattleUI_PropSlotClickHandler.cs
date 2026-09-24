using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleUI_PropSlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    public int SlotIndex { get; private set; }
    public event Action<int> Clicked;

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(SlotIndex);
    }
}
