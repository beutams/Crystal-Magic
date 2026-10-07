using CrystalMagic.Core;
using UnityEngine;

public sealed class DialogueUIData : UIData
{
    public UINode Bubble;
    public UINode Bubble_Label;

    public override void Bind(Transform root)
    {
        Bubble = UINode.From(Find(root, "Bubble"));
        Bubble_Label = UINode.From(Find(root, "Bubble/Label"));
    }
}
