// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopUI_CommodityItemData : UIData
{
    public UINode Icon;
    public UINode Text;

    public override void Bind(Transform root)
    {
        Icon = UINode.From(Find(root, "Icon"));
        Text = UINode.From(Find(root, "Text"));
    }
}
