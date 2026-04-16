using CrystalMagic.Core;

public class StashUI_InventoryItemView : UISubView<StashUI_InventoryItemData>
{
    public void Render(CrystalMagic.UI.StashInventoryDisplayData data)
    {
        Rebind();

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

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
