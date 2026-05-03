using CrystalMagic.Core;

using System;
using CrystalMagic.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class StashUI_InventoryItemView : UISubView<StashUI_InventoryItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private StashInventoryDisplayData _data;

    public event Action<StashInventoryDisplayData> DoubleClicked;
    public event Action<StashInventoryDisplayData, PointerEventData> DragStarted;
    public event Action<StashInventoryDisplayData, PointerEventData> Dragging;
    public event Action<StashInventoryDisplayData, PointerEventData> DragEnded;

    public void Render(CrystalMagic.UI.StashInventoryDisplayData data)
    {
        Rebind();
        _data = data;

        if (data == null)
        {
            UI.Icon.Image.sprite = null;
            UI.Count.TextMeshProUGUI.text = string.Empty;
            UI.Name.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.Icon.Image.sprite = LoadIcon(data.IconPath);
        UI.Count.TextMeshProUGUI.text = data.Count.ToString();
        UI.Name.TextMeshProUGUI.text = data.Name;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        DragStarted?.Invoke(_data, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        Dragging?.Invoke(_data, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_data == null || eventData == null)
            return;

        DragEnded?.Invoke(_data, eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null || eventData == null || eventData.clickCount < 2)
            return;

        DoubleClicked?.Invoke(_data);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedResource<Sprite>(iconPath);
    }
}
