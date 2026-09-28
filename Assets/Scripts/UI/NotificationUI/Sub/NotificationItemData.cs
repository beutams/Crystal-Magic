using CrystalMagic.Core;
using UnityEngine;

public sealed class NotificationItemData : UIData
{
    public UINode Root, Label, Fill;
    public override void Bind(Transform root)
    {
        Root = UINode.From(root.gameObject);
        Label = UINode.From(Find(root, "Label"));
        // Text-only prefabs deliberately omit the progress bar.
        Fill = UINode.From(Find(root, "Bar/Fill"));
    }
}
