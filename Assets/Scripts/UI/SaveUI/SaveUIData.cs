// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class SaveUIData : UIData
{
    public UINode Content;
    public UINode SaveItem;
    public UINode Back;

    public override void Bind(Transform root)
    {
        Content = UINode.From(Find(root, "Scroll View/Viewport/Content"));
        SaveItem = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem"));
        Back = UINode.From(Find(root, "Back"));
    }
}
