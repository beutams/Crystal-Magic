// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class EffectItemInfoUIData : UIData
{
    public UINode Back;
    public UINode IconBack;
    public UINode IconBack_Mask;
    public UINode IconBack_Mask_Icon;
    public UINode Name;
    public UINode Divide;
    public UINode Description;

    public override void Bind(Transform root)
    {
        Back = UINode.From(Find(root, "Back"));
        IconBack = UINode.From(Find(root, "IconBack"));
        IconBack_Mask = UINode.From(Find(root, "IconBack/Mask"));
        IconBack_Mask_Icon = UINode.From(Find(root, "IconBack/Mask/Icon"));
        Name = UINode.From(Find(root, "Name"));
        Divide = UINode.From(Find(root, "Divide"));
        Description = UINode.From(Find(root, "Description"));
    }
}
