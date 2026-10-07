using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Authored on the book's sliders; input stays in Unity's UI event system.
public sealed class CharacterUI_SettingSlider : Slider, IEndDragHandler
{
    public event Action EditCompleted;

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        if (eventData.button == PointerEventData.InputButton.Left)
            EditCompleted?.Invoke();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            EditCompleted?.Invoke();
    }

    public override void OnMove(AxisEventData eventData)
    {
        float before = value;
        base.OnMove(eventData);
        if (value != before)
            EditCompleted?.Invoke();
    }
}
