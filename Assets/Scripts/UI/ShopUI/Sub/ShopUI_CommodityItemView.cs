using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShopUI_CommodityItemView : UISubView<ShopUI_CommodityItemData>, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ShopCommodityDisplayData _data;

    public event Action<ShopCommodityDisplayData> HoverEntered;
    public event Action<ShopCommodityDisplayData> HoverExited;
    public event Action<ShopCommodityDisplayData> DoubleClicked;
    public event Action<ShopCommodityDisplayData, PointerEventData> DragStarted;
    public event Action<ShopCommodityDisplayData, PointerEventData> Dragging;
    public event Action<ShopCommodityDisplayData, PointerEventData> DragEnded;

    public void Render(ShopCommodityDisplayData data)
    {
        Rebind();
        _data = data;

        if (data == null)
        {
            UI.Icon.Image.sprite = null;
            UI.Name.TextMeshProUGUI.text = string.Empty;
            UI.Description.TextMeshProUGUI.text = string.Empty;
            UI.Price.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.Icon.Image.sprite = LoadIcon(data.IconPath);
        UI.Name.TextMeshProUGUI.text = data.Name;
        UI.Description.TextMeshProUGUI.text = data.Description;
        UI.Price.TextMeshProUGUI.text = data.Price.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_data == null)
            return;

        HoverEntered?.Invoke(_data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_data == null)
            return;

        HoverExited?.Invoke(_data);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null || eventData == null || eventData.clickCount < 2)
            return;

        DoubleClicked?.Invoke(_data);
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
