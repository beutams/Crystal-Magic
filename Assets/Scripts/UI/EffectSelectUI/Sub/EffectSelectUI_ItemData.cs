// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class EffectSelectUI_ItemData : UIData
{
    public UINode Background;
    public UINode Icon;
    public UINode Name;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        Icon = UINode.From(Find(root, "Icon"));
        Name = UINode.From(Find(root, "Name"));
    }
}
