// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class TipFormData : UIData
{
    public UINode Info;

    public override void Bind(Transform root)
    {
        Info = UINode.From(Find(root, "Info"));
    }
}
