// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class LobbyRoomUIData : UIData
{
    public UINode Background;
    public UINode RoomItem;
    public UINode RoomItem_RoomName;
    public UINode RoomItem_Player;
    public UINode RoomItem_Player_PlayerName;
    public UINode RoomItem_Player_Ready;
    public UINode RoomItem_Player_KickOut;
    public UINode Back;
    public UINode Back_Default;
    public UINode Back_Click;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        RoomItem = UINode.From(Find(root, "RoomItem"));
        RoomItem_RoomName = UINode.From(Find(root, "RoomItem/RoomName"));
        RoomItem_Player = UINode.From(Find(root, "RoomItem/Player"));
        RoomItem_Player_PlayerName = UINode.From(Find(root, "RoomItem/Player/PlayerName"));
        RoomItem_Player_Ready = UINode.From(Find(root, "RoomItem/Player/Ready"));
        RoomItem_Player_KickOut = UINode.From(Find(root, "RoomItem/Player/KickOut"));
        Back = UINode.From(Find(root, "Back"));
        Back_Default = UINode.From(Find(root, "Back/Default"));
        Back_Click = UINode.From(Find(root, "Back/Click"));
    }
}
