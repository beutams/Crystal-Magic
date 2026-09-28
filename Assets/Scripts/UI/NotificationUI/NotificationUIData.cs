using CrystalMagic.Core;
using UnityEngine;

public sealed class NotificationUIData : UIData
{
    public UINode Root;
    public override void Bind(Transform root) => Root = UINode.From(root.gameObject);
}
