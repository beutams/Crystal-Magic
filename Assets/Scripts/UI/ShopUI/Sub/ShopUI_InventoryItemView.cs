using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShopUI_InventoryItemView : UISubView<ShopUI_InventoryItemData>, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ShopInventoryDisplayData _data;

    public event Action<ShopInventoryDisplayData, PointerEventData> DragStarted;
    public event Action<ShopInventoryDisplayData, PointerEventData> Dragging;
    public event Action<ShopInventoryDisplayData, PointerEventData> DragEnded;

    public void Render(ShopInventoryDisplayData data)
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

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<Sprite>(iconPath);
    }
}
