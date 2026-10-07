// AUTO-GENERATED — DO NOT EDIT MANUALLY
using CrystalMagic.Core;
using UnityEngine;

public sealed class DungeonSettlementUI_ItemData : UIData
{
    public UINode Socket;
    public UINode Socket_Icon;
    public UINode Socket_AmountBG;
    public UINode Socket_Amount;
    public UINode Name;
    public UINode Selected;

    public override void Bind(Transform root)
    {
        Socket = UINode.From(Find(root, "Socket"));
        Socket_Icon = UINode.From(Find(root, "Socket/Icon"));
        Socket_AmountBG = UINode.From(Find(root, "Socket/AmountBG"));
        Socket_Amount = UINode.From(Find(root, "Socket/Amount"));
        Name = UINode.From(Find(root, "Name"));
        Selected = UINode.From(Find(root, "Selected"));
    }
}
