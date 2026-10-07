// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class InteractionSelectUI_OptionData : UIData
{
    public UINode Default;
    public UINode Default_TextTMP;
    public UINode Enter;
    public UINode Enter_TextTMP;
    public UINode Enter_Arrow;
    public UINode Click;
    public UINode Click_TextTMP;
    public UINode Click_Arrow;

    public override void Bind(Transform root)
    {
        Default = UINode.From(Find(root, "Default"));
        Default_TextTMP = UINode.From(Find(root, "Default/Text (TMP)"));
        Enter = UINode.From(Find(root, "Enter"));
        Enter_TextTMP = UINode.From(Find(root, "Enter/Text (TMP)"));
        Enter_Arrow = UINode.From(Find(root, "Enter/Arrow"));
        Click = UINode.From(Find(root, "Click"));
        Click_TextTMP = UINode.From(Find(root, "Click/Text (TMP)"));
        Click_Arrow = UINode.From(Find(root, "Click/Arrow"));
    }
}
