// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Right-click Prefab -> Assets/Tools/Generate UIData to regenerate

using CrystalMagic.Core;
using UnityEngine;

public class TrainingDebugUI_UnitItemData : UIData
{
    public UINode Button;
    public UINode Label;

    public override void Bind(Transform root)
    {
        Button = UINode.From(Find(root, "Button"));
        Label = UINode.From(Find(root, "Button/Label"));
    }
}
