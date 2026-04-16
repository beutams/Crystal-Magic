// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class StashUI_StashItemData : UIData
{
    public UINode Icon;
    public UINode Count;
    public UINode Name;

    public override void Bind(Transform root)
    {
        Icon = UINode.From(Find(root, "Icon"));
        Count = UINode.From(Find(root, "Count"));
        Name = UINode.From(Find(root, "Name"));
    }
}
