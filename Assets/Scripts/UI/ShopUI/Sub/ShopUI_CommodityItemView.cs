using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using UnityEngine.EventSystems;

public class ShopUI_CommodityItemView : UISubView<ShopUI_CommodityItemData>, IPointerEnterHandler, IPointerExitHandler
{
    private ShopCommodityDisplayData _data;

    public event Action<ShopCommodityDisplayData> HoverEntered;
    public event Action<ShopCommodityDisplayData> HoverExited;

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

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
