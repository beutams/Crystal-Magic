// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class ConfirmUIData : UIData
{
    public UINode Background;
    public UINode Title;
    public UINode Content;
    public UINode Confirm;
    public UINode Confirm_Text;
    public UINode Cancel;
    public UINode Cancel_Text;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        Title = UINode.From(Find(root, "Title"));
        Content = UINode.From(Find(root, "Content"));
        Confirm = UINode.From(Find(root, "Confirm"));
        Confirm_Text = UINode.From(Find(root, "Confirm/Text"));
        Cancel = UINode.From(Find(root, "Cancel"));
        Cancel_Text = UINode.From(Find(root, "Cancel/Text"));
    }
}
