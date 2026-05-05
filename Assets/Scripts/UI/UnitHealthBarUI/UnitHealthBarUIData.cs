// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class UnitHealthBarUIData : UIData
{
    public UINode HP;
    public UINode HP_BarBackground;
    public UINode HP_BarMask;
    public UINode HP_BarMask_Bar;

    public override void Bind(Transform root)
    {
        HP = UINode.From(Find(root, "HP"));
        HP_BarBackground = UINode.From(Find(root, "HP/BarBackground"));
        HP_BarMask = UINode.From(Find(root, "HP/BarMask"));
        HP_BarMask_Bar = UINode.From(Find(root, "HP/BarMask/Bar"));
    }
}
