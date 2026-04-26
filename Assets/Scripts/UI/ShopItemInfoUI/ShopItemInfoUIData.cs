// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ShopItemInfoUIData : UIData
{
    public UINode Back;
    public UINode Solt;
    public UINode Icon;
    public UINode Name;
    public UINode Have;
    public UINode HaveCount;
    public UINode Divide;
    public UINode Description;
    public UINode Price;
    public UINode Coin;

    public override void Bind(Transform root)
    {
        Back = UINode.From(Find(root, "Back"));
        Solt = UINode.From(Find(root, "Solt"));
        Icon = UINode.From(Find(root, "Icon"));
        Name = UINode.From(Find(root, "Name"));
        Have = UINode.From(Find(root, "Have"));
        HaveCount = UINode.From(Find(root, "HaveCount"));
        Divide = UINode.From(Find(root, "Divide"));
        Description = UINode.From(Find(root, "Description"));
        Price = UINode.From(Find(root, "Price"));
        Coin = UINode.From(Find(root, "Coin"));
    }
}
