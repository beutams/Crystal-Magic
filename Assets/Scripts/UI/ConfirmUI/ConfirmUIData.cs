// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ConfirmUIData : UIData
{
    public UINode Background;
    public UINode TitleBG;
    public UINode TitleBG_Title;
    public UINode Content;
    public UINode Confirm;
    public UINode Confirm_Default;
    public UINode Confirm_Default_Text;
    public UINode Confirm_Click;
    public UINode Confirm_Click_Text;
    public UINode Cancel;
    public UINode Cancel_Default;
    public UINode Cancel_Default_Text;
    public UINode Cancel_Click;
    public UINode Cancel_Click_Text;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        TitleBG = UINode.From(Find(root, "TitleBG"));
        TitleBG_Title = UINode.From(Find(root, "TitleBG/Title"));
        Content = UINode.From(Find(root, "Content"));
        Confirm = UINode.From(Find(root, "Confirm"));
        Confirm_Default = UINode.From(Find(root, "Confirm/Default"));
        Confirm_Default_Text = UINode.From(Find(root, "Confirm/Default/Text"));
        Confirm_Click = UINode.From(Find(root, "Confirm/Click"));
        Confirm_Click_Text = UINode.From(Find(root, "Confirm/Click/Text"));
        Cancel = UINode.From(Find(root, "Cancel"));
        Cancel_Default = UINode.From(Find(root, "Cancel/Default"));
        Cancel_Default_Text = UINode.From(Find(root, "Cancel/Default/Text"));
        Cancel_Click = UINode.From(Find(root, "Cancel/Click"));
        Cancel_Click_Text = UINode.From(Find(root, "Cancel/Click/Text"));
    }
}
