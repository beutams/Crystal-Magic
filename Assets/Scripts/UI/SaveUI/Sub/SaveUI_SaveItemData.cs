// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class SaveUI_SaveItemData : UIData
{
    public UINode CreateTime;
    public UINode Money;
    public UINode Index;

    public override void Bind(Transform root)
    {
        CreateTime = UINode.From(Find(root, "CreateTime"));
        Money = UINode.From(Find(root, "Money"));
        Index = UINode.From(Find(root, "Index"));
    }
}
