// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopUI_CommodityItemData : UIData
{
    public UINode Select;
    public UINode IconBG;
    public UINode IconBG_Mask;
    public UINode IconBG_Mask_Icon;
    public UINode Name;
    public UINode Description;
    public UINode Coin;
    public UINode Price;

    public override void Bind(Transform root)
    {
        Select = UINode.From(Find(root, "Select"));
        IconBG = UINode.From(Find(root, "IconBG"));
        IconBG_Mask = UINode.From(Find(root, "IconBG/Mask"));
        IconBG_Mask_Icon = UINode.From(Find(root, "IconBG/Mask/Icon"));
        Name = UINode.From(Find(root, "Name"));
        Description = UINode.From(Find(root, "Description"));
        Coin = UINode.From(Find(root, "Coin"));
        Price = UINode.From(Find(root, "Price"));
    }
}
