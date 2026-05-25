// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class InteractionSelectUIData : UIData
{
    public UINode Content;
    public UINode Content_Button;
    public UINode Content_Button_TextTMP;

    public override void Bind(Transform root)
    {
        Content = UINode.From(Find(root, "Content"));
        Content_Button = UINode.From(Find(root, "Content/Button"));
        Content_Button_TextTMP = UINode.From(Find(root, "Content/Button/Text (TMP)"));
    }
}
