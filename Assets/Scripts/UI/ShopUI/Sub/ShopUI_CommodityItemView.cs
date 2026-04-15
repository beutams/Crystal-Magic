using CrystalMagic.Core;
using CrystalMagic.UI;

public class ShopUI_CommodityItemView : UISubView<ShopUI_CommodityItemData>
{
    public void Render(ShopCommodityDisplayData data)
    {
        Rebind();

        if (data == null)
        {
            UI.Icon.Image.sprite = null;
            UI.Text.TextMeshProUGUI.text = string.Empty;
            return;
        }

        UI.Icon.Image.sprite = LoadIcon(data.IconPath);
        UI.Text.TextMeshProUGUI.text = $"{data.Name}  {data.Price}";
    }

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
