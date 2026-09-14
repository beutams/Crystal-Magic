// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using CrystalMagic.Core;
using UnityEngine;

public sealed class DungeonSettlementUIData : UIData
{
    public UINode Title;
    public UINode Summary;
    public UINode Confirm;
    public UINode Confirm_Label;

    public override void Bind(Transform root)
    {
        Title = UINode.From(Find(root, "Panel/Title"));
        Summary = UINode.From(Find(root, "Panel/Summary"));
        Confirm = UINode.From(Find(root, "Panel/Confirm"));
        Confirm_Label = UINode.From(Find(root, "Panel/Confirm/Label"));
    }
}
