// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopItemInfoUIData : UIData
{
    public UINode Back;
    public UINode IconBack;
    public UINode IconBack_Mask;
    public UINode IconBack_Mask_Icon;
    public UINode Name;
    public UINode Have;
    public UINode HaveCount;
    public UINode Description;
    public UINode Price;
    public UINode Coin;

    public override void Bind(Transform root)
    {
        Back = UINode.From(Find(root, "Back"));
        IconBack = UINode.From(Find(root, "IconBack"));
        IconBack_Mask = UINode.From(Find(root, "IconBack/Mask"));
        IconBack_Mask_Icon = UINode.From(Find(root, "IconBack/Mask/Icon"));
        Name = UINode.From(Find(root, "Name"));
        Have = UINode.From(Find(root, "Have"));
        HaveCount = UINode.From(Find(root, "HaveCount"));
        Description = UINode.From(Find(root, "Description"));
        Price = UINode.From(Find(root, "Price"));
        Coin = UINode.From(Find(root, "Coin"));
    }
}
