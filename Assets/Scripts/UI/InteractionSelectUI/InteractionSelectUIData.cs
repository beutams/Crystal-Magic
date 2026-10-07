// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class InteractionSelectUIData : UIData
{
    public UINode BG;
    public UINode BG_Fill;
    public UINode Dialog;
    public UINode Content;
    public UINode Content_Button;
    public UINode Content_Button_Default;
    public UINode Content_Button_Default_TextTMP;
    public UINode Content_Button_Enter;
    public UINode Content_Button_Enter_TextTMP;
    public UINode Content_Button_Enter_Arrow;
    public UINode Content_Button_Click;
    public UINode Content_Button_Click_TextTMP;
    public UINode Content_Button_Click_Arrow;

    public override void Bind(Transform root)
    {
        BG = UINode.From(Find(root, "BG"));
        BG_Fill = UINode.From(Find(root, "BG/Fill"));
        Dialog = UINode.From(Find(root, "Dialog"));
        Content = UINode.From(Find(root, "Content"));
        Content_Button = UINode.From(Find(root, "Content/Button"));
        Content_Button_Default = UINode.From(Find(root, "Content/Button/Default"));
        Content_Button_Default_TextTMP = UINode.From(Find(root, "Content/Button/Default/Text (TMP)"));
        Content_Button_Enter = UINode.From(Find(root, "Content/Button/Enter"));
        Content_Button_Enter_TextTMP = UINode.From(Find(root, "Content/Button/Enter/Text (TMP)"));
        Content_Button_Enter_Arrow = UINode.From(Find(root, "Content/Button/Enter/Arrow"));
        Content_Button_Click = UINode.From(Find(root, "Content/Button/Click"));
        Content_Button_Click_TextTMP = UINode.From(Find(root, "Content/Button/Click/Text (TMP)"));
        Content_Button_Click_Arrow = UINode.From(Find(root, "Content/Button/Click/Arrow"));
    }
}
