// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class GameSettingUIData : UIData
{
    public UINode Save;
    public UINode Save_TextTMP;

    public override void Bind(Transform root)
    {
        Save = UINode.From(Find(root, "Save"));
        Save_TextTMP = UINode.From(Find(root, "Save/Text (TMP)"));
    }
}
