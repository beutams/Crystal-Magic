using CrystalMagic.Core;
using UnityEngine;

public sealed class GuideUIData : UIData
{
    public UINode Mask;
    public UINode Hint;
    public UINode Hint_Label;
    public override void Bind(Transform root)
    {
        Mask = UINode.From(Find(root, "Mask"));
        Hint = UINode.From(Find(root, "Hint"));
        Hint_Label = UINode.From(Find(root, "Hint/Label"));
    }
}
