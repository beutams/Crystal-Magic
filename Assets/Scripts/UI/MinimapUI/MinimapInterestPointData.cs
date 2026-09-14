using CrystalMagic.Core;
using UnityEngine;

public sealed class MinimapInterestPointData : UIData
{
    public UINode Icon;

    public override void Bind(Transform root)
    {
        Icon = UINode.From(Find(root, "Icon"));
    }
}
