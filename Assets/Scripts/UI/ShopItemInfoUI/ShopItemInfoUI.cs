using CrystalMagic.Core;
using UnityEngine.UI;

public class ShopItemInfoUI : UIBase<ShopItemInfoUIData, CrystalMagic.UI.ShopItemInfoUIModel>
{
    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        base.OnOpen();
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    protected override void RefreshView()
    {
        UI.Name.TextMeshProUGUI.text = Model != null ? Model.Name : string.Empty;
        UI.HaveCount.TextMeshProUGUI.text = Model != null ? Model.HaveCount.ToString() : "0";
        UI.Description.TextMeshProUGUI.text = Model != null ? Model.Description : string.Empty;
        UI.Price.TextMeshProUGUI.text = Model != null ? Model.Price.ToString() : string.Empty;
        UI.Icon.Image.sprite = LoadIcon(Model != null ? Model.IconPath : string.Empty);
    }

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
