// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class EffectSelectUI_ItemData : UIData
{
    public UINode IconBG;
    public UINode IconBG_Mask;
    public UINode IconBG_Mask_Icon;
    public UINode Name;

    public override void Bind(Transform root)
    {
        IconBG = UINode.From(Find(root, "IconBG"));
        IconBG_Mask = UINode.From(Find(root, "IconBG/Mask"));
        IconBG_Mask_Icon = UINode.From(Find(root, "IconBG/Mask/Icon"));
        Name = UINode.From(Find(root, "Name"));
    }
}
