// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopUI_CommodityItemData : UIData
{
    public UINode Icon;
    public UINode Name;
    public UINode Description;
    public UINode Coin;
    public UINode Price;

    public override void Bind(Transform root)
    {
        Icon = UINode.From(Find(root, "Icon"));
        Name = UINode.From(Find(root, "Name"));
        Description = UINode.From(Find(root, "Description"));
        Coin = UINode.From(Find(root, "Coin"));
        Price = UINode.From(Find(root, "Price"));
    }
}
