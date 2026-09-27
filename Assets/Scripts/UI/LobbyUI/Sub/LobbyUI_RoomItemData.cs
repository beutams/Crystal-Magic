using CrystalMagic.Core;
using UnityEngine;

public class LobbyUI_RoomItemData : UIData
{
    public UINode Open;
    public UINode Open_RoomName;
    public UINode Open_Player;
    public UINode Open_Index;
    public UINode Open_Delete;

    public override void Bind(Transform root)
    {
        Open = UINode.From(Find(root, "Open"));
        Open_RoomName = UINode.From(Find(root, "Open/RoomName"));
        Open_Player = UINode.From(Find(root, "Open/Player"));
        Open_Index = UINode.From(Find(root, "Open/Index"));
        Open_Delete = UINode.From(Find(root, "Open/Delete"));
    }
}
