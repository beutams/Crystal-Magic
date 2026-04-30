// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class TransitionUIData : UIData
{
    public UINode Image;

    public override void Bind(Transform root)
    {
        Image = UINode.From(Find(root, "Image"));
    }
}
