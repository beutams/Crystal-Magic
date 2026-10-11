// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class LobbyUI_RoomItemData : UIData
{
    public UINode RoomName;
    public UINode HostName;
    public UINode PlayerCount;
    public UINode State;
    public UINode Join;
    public UINode Join_Default;
    public UINode Join_Click;
    public UINode Join_Label;

    public override void Bind(Transform root)
    {
        RoomName = UINode.From(Find(root, "RoomName"));
        HostName = UINode.From(Find(root, "HostName"));
        PlayerCount = UINode.From(Find(root, "PlayerCount"));
        State = UINode.From(Find(root, "State"));
        Join = UINode.From(Find(root, "Join"));
        Join_Default = UINode.From(Find(root, "Join/Default"));
        Join_Click = UINode.From(Find(root, "Join/Click"));
        Join_Label = UINode.From(Find(root, "Join/Label"));
    }
}
