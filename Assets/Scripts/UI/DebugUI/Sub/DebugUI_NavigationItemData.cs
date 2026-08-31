// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Right-click Prefab -> Assets/Tools/Generate UIData to regenerate

using CrystalMagic.Core;
using UnityEngine;

public class DebugUI_NavigationItemData : UIData
{
    public UINode Root;
    public UINode Label;

    public override void Bind(Transform root)
    {
        Root = UINode.From(root.gameObject);
        Label = UINode.From(Find(root, "Label"));
    }
}
