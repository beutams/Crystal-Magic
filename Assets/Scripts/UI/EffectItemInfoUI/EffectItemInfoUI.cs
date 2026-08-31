using CrystalMagic.Core;
using UnityEngine;
using UnityEngine.UI;

public class EffectItemInfoUI : UIBase<EffectItemInfoUIData, CrystalMagic.UI.EffectItemInfoUIModel>
{
    protected override void OnInit()
    {
        base.OnInit();
        Graphic[] graphics = gameObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
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
        UI.Description.TextMeshProUGUI.text = Model != null ? Model.Description : string.Empty;
        UI.IconBack_Mask_Icon.Image.sprite = LoadIcon(Model != null ? Model.IconPath : string.Empty);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedSprite(iconPath);
    }
}
