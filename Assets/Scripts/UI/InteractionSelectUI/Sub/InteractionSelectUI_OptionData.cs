// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class InteractionSelectUI_OptionData : UIData
{
    public UINode Default_TextTMP;
    public UINode Click_TextTMP;

    public override void Bind(Transform root)
    {
        Default_TextTMP = UINode.From(Find(root, "Default/Text (TMP)"));
        Click_TextTMP = UINode.From(Find(root, "Click/Text (TMP)"));
    }
}
