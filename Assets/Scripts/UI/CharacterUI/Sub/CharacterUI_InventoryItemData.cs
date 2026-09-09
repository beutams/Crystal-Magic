// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class CharacterUI_InventoryItemData : UIData
{
    public UINode Mask;
    public UINode Mask_Icon;
    public UINode Count;
    public UINode Name;

    public override void Bind(Transform root)
    {
        Mask = UINode.From(Find(root, "Mask"));
        Mask_Icon = UINode.From(Find(root, "Mask/Icon"));
        Count = UINode.From(Find(root, "Count"));
        Name = UINode.From(Find(root, "Name"));
    }
}
